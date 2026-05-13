using System;
using UnityEngine;
using AcesOverTheLines.Flight;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.AI
{
    // Basic AI opponent. Implements IFlightControlSource so AircraftController
    // discovers it the same way it discovers FlightInput; flight physics is
    // identical between player and AI — no cheating, same rate-targeting
    // controller, same stall behaviour, same gyro coupling.
    //
    // Six-state machine: Patrol → Search → Engage → Evade → Disengage (→ RTB
    // placeholder). Strategic decisions tick at 5 Hz; control outputs are
    // smoothed per-tick via the same rampTo helper FlightInput uses.
    //
    // The AI sees only what AircraftEntity exposes (its own pose, velocity,
    // component HP) plus the target's pose/velocity. It does NOT read the
    // target's HP, ammo, or controls.
    public class AIController : MonoBehaviour, IFlightControlSource
    {
        public enum State { Patrol, Search, Engage, Evade, Disengage, RTB }

        [SerializeField] Transform target;
        [SerializeField] float decisionRateHz = 5f;

        // Engagement geometry thresholds.
        [SerializeField] float visualRangeM = 1000f;
        [SerializeField] float firingRangeM = 200f;
        [SerializeField] float firingDeflectionDeg = 5f;
        [SerializeField] float closeRangeM = 300f;
        [SerializeField] float frontHemisphereDeg = 60f;

        // Burst-fire cadence.
        [SerializeField] float burstOnS = 0.3f;
        [SerializeField] float burstOffS = 0.5f;

        // Disengage / energy / health thresholds.
        [SerializeField] float ammoLowFraction = 0.30f;
        [SerializeField] float componentLowFraction = 0.50f;
        [SerializeField] float energyLowSpeedMs = 25f;
        [SerializeField] float energyLowAltitudeM = 200f;
        [SerializeField] float lostGeometrySeconds = 4f;
        [SerializeField] float disengageNoseDownNormalised = 0.7f;
        [SerializeField] float disengageDurationS = 5f;
        [SerializeField] float evadeDurationS = 3f;

        // Patrol energy management.
        [SerializeField] float stallSpeedV0Ms = 11.1f;          // matches AircraftEntity authority v0
        [SerializeField] float patrolThrottle = 0.7f;
        [SerializeField] float patrolMinAltitudeM = 200f;

        // Control smoothing — same rate as FlightInput's 250 ms ramp.
        const double RAMP_TIME_S = 0.25;
        const double RAMP_RATE = 1.0 / RAMP_TIME_S;

        AircraftController _ctrl;
        Rigidbody _rb;
        Rigidbody _targetRb;

        State _state = State.Patrol;
        float _stateEnteredTime;
        float _lastDecisionTime;
        float _noFiringSolutionTime;
        float _burstTimer;
        bool _burstOn;
        int _initialAmmoTotal;

        // Smoothed control outputs.
        double _smoothedElevator;
        double _smoothedAileron;
        double _smoothedRudder;

        public State CurrentState => _state;
        public Transform Target { get => target; set { target = value; _targetRb = target != null ? target.GetComponent<Rigidbody>() : null; } }

        void Awake()
        {
            _ctrl = GetComponent<AircraftController>();
            _rb = GetComponent<Rigidbody>();
            if (target == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null) target = playerGo.transform;
            }
            if (target != null) _targetRb = target.GetComponent<Rigidbody>();
        }

        void Start()
        {
            // Cache initial ammo total once WeaponSystem is initialized.
            var ws = GetComponent<WeaponSystem>();
            if (ws != null && ws.Guns != null)
            {
                _initialAmmoTotal = 0;
                foreach (var g in ws.Guns) _initialAmmoTotal += g.Spec.Rounds;
            }
        }

        public ControlInput ReadControls(double dt)
        {
            // Strategic-level decisions tick at 5 Hz.
            if (Time.time - _lastDecisionTime >= 1f / decisionRateHz)
            {
                _lastDecisionTime = Time.time;
                UpdateStateTransitions();
            }

            // Per-tick burst-fire timer + control output.
            UpdateBurst((float)dt);
            ControlInput desired = ComputeDesiredControls();

            // Smooth elevator / aileron / rudder; throttle bypasses smoothing.
            _smoothedElevator = RampTo(_smoothedElevator, desired.Elevator, dt);
            _smoothedAileron  = RampTo(_smoothedAileron,  desired.Aileron,  dt);
            _smoothedRudder   = RampTo(_smoothedRudder,   desired.Rudder,   dt);

            return new ControlInput
            {
                Elevator = _smoothedElevator,
                Aileron  = _smoothedAileron,
                Rudder   = _smoothedRudder,
                Throttle = desired.Throttle,
                Fire     = desired.Fire,
            };
        }

        // ============================================================
        // State transitions (called at 5 Hz)
        // ============================================================

        void UpdateStateTransitions()
        {
            if (target == null) { TransitionIfChanged(State.Patrol); return; }

            float range = Vector3.Distance(target.position, _rb.position);
            float bearing = BearingToTargetDeg();

            switch (_state)
            {
                case State.Patrol:
                case State.Search:
                    if (range < visualRangeM && bearing < frontHemisphereDeg)
                        TransitionIfChanged(State.Engage);
                    break;

                case State.Engage:
                    if (ShouldDisengage()) TransitionIfChanged(State.Disengage);
                    break;

                case State.Evade:
                    if (Time.time - _stateEnteredTime > evadeDurationS)
                        TransitionIfChanged(State.Engage);
                    break;

                case State.Disengage:
                    if (Time.time - _stateEnteredTime > disengageDurationS)
                        TransitionIfChanged(State.Search);
                    break;

                case State.RTB:
                    break;
            }
        }

        void TransitionIfChanged(State newState)
        {
            if (_state == newState) return;
            _state = newState;
            _stateEnteredTime = Time.time;
            _noFiringSolutionTime = 0f;
        }

        // ============================================================
        // Per-tick control output
        // ============================================================

        ControlInput ComputeDesiredControls()
        {
            switch (_state)
            {
                case State.Engage:    return DoEngage();
                case State.Evade:     return DoEvade();
                case State.Disengage: return DoDisengage();
                case State.Patrol:
                case State.Search:
                default:              return DoPatrol();
            }
        }

        ControlInput DoPatrol()
        {
            // Cruise level. Climb gently if below patrol altitude.
            double elevator = 0.0;
            if (_rb != null && _rb.position.y < patrolMinAltitudeM)
            {
                elevator = 0.15; // nose-up to climb
            }
            // Energy management: drop nose if speed is too low.
            if (_rb != null && _rb.linearVelocity.magnitude < 1.5f * stallSpeedV0Ms)
            {
                elevator = -0.20;
            }
            return new ControlInput { Elevator = elevator, Throttle = patrolThrottle };
        }

        ControlInput DoEngage()
        {
            if (target == null || _rb == null)
                return new ControlInput { Throttle = patrolThrottle };

            float range = Vector3.Distance(target.position, _rb.position);
            Vector3 leadPoint = ComputeLeadPoint(
                target.position,
                _targetRb != null ? _targetRb.linearVelocity : Vector3.zero,
                _rb.position,
                MuzzleVelocity());

            Vector3 toLeadWorld = (leadPoint - _rb.position).normalized;
            Vector3 toLeadBody = Quaternion.Inverse(_rb.rotation) * toLeadWorld;

            // Body frame: +x forward, +y up, +z right (JS sim convention).
            // Elevator + → pitch up (target above body x-y plane → toLeadBody.y > 0).
            // Aileron  + → roll right (target right of body → toLeadBody.z > 0).
            double elevator = Mathf.Clamp((float)toLeadBody.y * 3f, -1f, 1f);
            double aileron  = Mathf.Clamp((float)toLeadBody.z * 3f, -1f, 1f);
            double rudder   = 0.0;

            // Throttle: full when far, reduce to 0.5 when closing inside
            // closeRange so we don't overshoot.
            double throttle = range > closeRangeM ? 1.0 : 0.5;

            // Track no-firing-solution timer.
            float deflectionDeg = Vector3.Angle(_rb.rotation * new Vector3(1f, 0f, 0f), toLeadWorld);
            float dt = Time.fixedDeltaTime;
            if (deflectionDeg > 30f) _noFiringSolutionTime += dt;
            else _noFiringSolutionTime = Mathf.Max(0f, _noFiringSolutionTime - dt);

            // Energy management: drop nose if speed too low.
            if (_rb.linearVelocity.magnitude < 1.5f * stallSpeedV0Ms)
            {
                elevator = -0.30;
                throttle = 1.0;
            }

            bool fire = ShouldFire(deflectionDeg, range, firingDeflectionDeg, firingRangeM, _burstOn);
            return new ControlInput { Elevator = elevator, Aileron = aileron, Rudder = rudder, Throttle = throttle, Fire = fire };
        }

        ControlInput DoEvade()
        {
            // Hard turn (bias right), drop nose, full throttle.
            return new ControlInput { Elevator = -0.5, Aileron = 0.8, Throttle = 1.0 };
        }

        ControlInput DoDisengage()
        {
            // Nose-down dive, full throttle. After disengageDurationS the
            // transition back to Search fires from UpdateStateTransitions.
            return new ControlInput { Elevator = -disengageNoseDownNormalised, Throttle = 1.0 };
        }

        // ============================================================
        // Burst-fire timer
        // ============================================================

        void UpdateBurst(float dt)
        {
            _burstTimer += dt;
            float cycle = _burstOn ? burstOnS : burstOffS;
            if (_burstTimer >= cycle)
            {
                _burstTimer = 0f;
                _burstOn = !_burstOn;
            }
        }

        // ============================================================
        // Disengage trigger (any 2 of 4 conditions)
        // ============================================================

        public bool ShouldDisengage()
        {
            int conditions = 0;

            // 1. Ammo
            var ws = GetComponent<WeaponSystem>();
            int currentAmmo = 0;
            if (ws != null && ws.Guns != null)
                foreach (var g in ws.Guns) currentAmmo += g.Rounds;
            if (_initialAmmoTotal > 0 && (float)currentAmmo / _initialAmmoTotal < ammoLowFraction)
                conditions++;

            // 2. Health (any tracked component below 50 %).
            if (_ctrl != null && _ctrl.Entity != null && _ctrl.Entity.Components != null)
            {
                foreach (var key in new[] { "engine", "pilot", "left_wing_spar", "right_wing_spar", "fuel_tank" })
                {
                    if (_ctrl.Entity.Components.TryGetValue(key, out var c)
                        && c.hpMax > 0 && c.hp / c.hpMax < componentLowFraction)
                    {
                        conditions++;
                        break;
                    }
                }
            }

            // 3. Energy
            if (_rb != null && _rb.linearVelocity.magnitude < energyLowSpeedMs
                            && _rb.position.y < energyLowAltitudeM)
                conditions++;

            // 4. Geometry
            if (_noFiringSolutionTime > lostGeometrySeconds)
                conditions++;

            return conditions >= 2;
        }

        // ============================================================
        // Helpers
        // ============================================================

        float BearingToTargetDeg()
        {
            if (target == null || _rb == null) return 180f;
            Vector3 bodyForward = _rb.rotation * new Vector3(1f, 0f, 0f);
            Vector3 toTarget = (target.position - _rb.position).normalized;
            return Vector3.Angle(bodyForward, toTarget);
        }

        float MuzzleVelocity()
        {
            var ws = GetComponent<WeaponSystem>();
            if (ws != null && ws.Guns != null && ws.Guns.Count > 0)
                return (float)ws.Guns[0].Spec.MuzzleVelocityMS;
            return 820f; // sensible Vickers default
        }

        // ============================================================
        // Static testable helpers
        // ============================================================

        // Lead-target point: where to aim so a bullet travelling at
        // muzzleVelocity intersects the target's projected future position.
        // Iterative first-order: bullet time = current range / muzzleVelocity.
        public static Vector3 ComputeLeadPoint(Vector3 targetPos, Vector3 targetVel, Vector3 firerPos, float muzzleVelocity)
        {
            float range = Vector3.Distance(targetPos, firerPos);
            float bulletTime = range / Mathf.Max(1f, muzzleVelocity);
            return targetPos + targetVel * bulletTime;
        }

        public static bool ShouldFire(float deflectionDeg, float rangeM, float maxDeflectionDeg, float maxRangeM, bool burstOn)
        {
            return burstOn && deflectionDeg < maxDeflectionDeg && rangeM < maxRangeM;
        }

        public static bool IsLowEnergy(float speedMs, float v0StallMs)
        {
            return speedMs < 1.5f * v0StallMs;
        }

        public static int CountDisengageConditions(
            int currentAmmo, int initialAmmo, float ammoLowFraction,
            float lowestComponentHpFraction, float componentLowFraction,
            float speedMs, float altitudeM, float energyLowSpeedMs, float energyLowAltitudeM,
            float noSolutionTimeS, float lostGeometrySeconds)
        {
            int conditions = 0;
            if (initialAmmo > 0 && (float)currentAmmo / initialAmmo < ammoLowFraction) conditions++;
            if (lowestComponentHpFraction < componentLowFraction) conditions++;
            if (speedMs < energyLowSpeedMs && altitudeM < energyLowAltitudeM) conditions++;
            if (noSolutionTimeS > lostGeometrySeconds) conditions++;
            return conditions;
        }

        static double RampTo(double curr, double target, double dt)
        {
            double maxStep = RAMP_RATE * dt;
            double delta = target - curr;
            if (Math.Abs(delta) <= maxStep) return target;
            return curr + Math.Sign(delta) * maxStep;
        }
    }
}

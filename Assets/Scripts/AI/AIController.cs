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
        public enum State { Patrol, Search, Engage, Evade, Disengage, RTB, Climb }

        [SerializeField] Transform target;
        [SerializeField] float decisionRateHz = 5f;
        [SerializeField] bool showAIDebug = true;
        // Diagnostic: log every state machine transition with range, altitude,
        // descent rate, and dwell time. Public so PlayMode runs can toggle
        // it without recompile; default off to avoid log spam in production.
        public bool logStateTransitions = false;

        // Engagement geometry thresholds.
        [SerializeField] float visualRangeM = 1000f;
        [SerializeField] float closeRangeM = 300f;       // throttle modulation switch
        [SerializeField] float frontHemisphereDeg = 60f;
        [SerializeField] float engageRollCap = 1.0f;     // full bank authority above the low-alt clamp band
        [SerializeField] float engageCoordTurnAileronThreshold = 0.5f;
        [SerializeField] float engageCoordTurnBankScale = 1.0f;
        [SerializeField] float engageCoordTurnElevatorScale = 0.5f;

        // Three firing windows (any matching window fires when burst is on).
        // Far window: medium-range with moderate aim discipline.
        // Close window: short-range with tight aim (5° within 100m).
        // Snap window: very-close pass-by burst (wider deflection allowed).
        [SerializeField] float farFireRangeM = 300f;
        [SerializeField] float farFireDeflectionDeg = 10f;
        [SerializeField] float closeFireRangeM = 100f;
        [SerializeField] float closeFireDeflectionDeg = 5f;
        [SerializeField] float snapFireRangeM = 50f;
        [SerializeField] float snapFireDeflectionDeg = 30f;

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
        [SerializeField] float disengageAltitudeFloorM = 300f;  // below this, lateral escape only
        [SerializeField] float evadeDurationS = 3f;

        // Patrol energy management.
        [SerializeField] float stallSpeedV0Ms = 11.1f;          // matches AircraftEntity authority v0
        [SerializeField] float stallMarginX = 2.0f;             // recovery triggers below this × stall
        [SerializeField] float stallRecoveryDurationS = 2.0f;
        [SerializeField] float patrolThrottle = 0.7f;
        [SerializeField] float patrolTargetAltitudeM = 1200f;
        [SerializeField] float patrolAltitudeGain = 0.001f;
        [SerializeField] float patrolAltitudeDampingGain = 0.005f;
        [SerializeField] float patrolElevatorClamp = 0.15f;

        // Climb state thresholds. Climb can hard-interrupt any other state
        // when the AI is below the entry altitude OR descending too fast
        // for too long. Climb exits when high enough AND actually climbing.
        [SerializeField] float climbEntryAltitudeM = 600f;
        [SerializeField] float climbExitAltitudeM = 1000f;
        [SerializeField] float climbEntryDescentRateMs = 30f;
        [SerializeField] float climbEntryDescentSustainS = 2f;
        [SerializeField] float climbElevator = 0.6f;

        // Engage altitude-bleed limits. If the AI loses too much altitude
        // or starts descending too fast WHILE engaging, it abandons the
        // pursuit (transitions to Climb) instead of committing further.
        [SerializeField] float engageAbandonAltitudeDropM = 500f;
        [SerializeField] float engageAbandonDescentRateMs = 45f;

        // Hard altitude floor — never fly into the ground for ANY manoeuvre.
        [SerializeField] float altitudeFloorAGL = 250f;
        [SerializeField] float altitudeFloorElevatorMin = 0.80f;

        // Energy preservation band — between altitudeFloorAGL and this
        // threshold, clamp commanded inputs so the AI doesn't dive or
        // hard-bank itself into the ground. Floor override (above) wins
        // below altitudeFloorAGL.
        [SerializeField] float lowAltitudeThresholdM = 500f;
        [SerializeField] float lowAltElevatorMin = 0.0f;   // no commanded dive in low band
        [SerializeField] float lowAltElevatorMax = 0.5f;   // no extreme pull either
        [SerializeField] float lowAltAileronCap = 0.7f;    // firmer banks in the 250–500m band

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
        float _stallRecoveryTimer;
        float _excessDescentTime;     // accumulator for sustained-descent climb trigger
        float _engageEntryAltitude;   // captured on entering Engage; used for altitude-bleed check
        float _lastCmdLogTime = -10f; // diagnostic throttle for control-command logging

        // Diagnostic state cached per-tick for the overlay.
        float _diagRange;
        float _diagDeflectionDeg;
        float _diagSpeed;
        float _diagAltitude;
        double _diagThrottle;
        float _lastFireTime = -10f;
        bool _diagStallRecovery;
        bool _diagAltitudeFloor;
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
            // Per-tick descent-rate accumulator for the Climb trigger.
            if (_rb != null && _rb.linearVelocity.y < -climbEntryDescentRateMs)
                _excessDescentTime += (float)dt;
            else
                _excessDescentTime = 0f;

            // Strategic-level decisions tick at 5 Hz.
            if (Time.time - _lastDecisionTime >= 1f / decisionRateHz)
            {
                _lastDecisionTime = Time.time;
                UpdateStateTransitions();
            }

            // Per-tick burst-fire timer + control output.
            UpdateBurst((float)dt);
            ControlInput desired = ComputeDesiredControls();
            desired = ApplySafetyOverrides(desired, dt);

            // Smooth elevator / aileron / rudder; throttle bypasses smoothing.
            _smoothedElevator = RampTo(_smoothedElevator, desired.Elevator, dt);
            _smoothedAileron  = RampTo(_smoothedAileron,  desired.Aileron,  dt);
            _smoothedRudder   = RampTo(_smoothedRudder,   desired.Rudder,   dt);

            // Cache diagnostic state for the OnGUI overlay.
            if (_rb != null)
            {
                _diagSpeed = _rb.linearVelocity.magnitude;
                _diagAltitude = _rb.position.y;
            }
            _diagThrottle = desired.Throttle;
            if (desired.Fire) _lastFireTime = Time.time;

            // Throttled control-command log (0.5 Hz, gated on the same
            // logStateTransitions flag) so dwell-time bands of stable
            // commands stay greppable in the editor console.
            if (logStateTransitions && Time.time - _lastCmdLogTime >= 0.5f)
            {
                _lastCmdLogTime = Time.time;
                float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
                Debug.Log($"[AI-CMD] state={_state}  elev={_smoothedElevator:F2}  aile={_smoothedAileron:F2}  rud={_smoothedRudder:F2}  thr={desired.Throttle:F2}  alt={_diagAltitude:F0}  vy={_rb?.linearVelocity.y ?? 0f:F1}  speed={speed:F1}");
            }

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
        // Safety overrides — stall recovery + hard altitude floor
        // ============================================================

        ControlInput ApplySafetyOverrides(ControlInput desired, double dt)
        {
            if (_rb == null) return desired;
            float speed = _rb.linearVelocity.magnitude;
            float altitude = _rb.position.y;
            bool isStall = speed < stallMarginX * stallSpeedV0Ms;
            bool isBelowFloor = altitude < altitudeFloorAGL;

            // Both STALL and FLOOR firing simultaneously means a dive-for-
            // speed override would point the nose INTO terrain. Instead,
            // escape into the Climb state: wings-level full-throttle steady
            // pull-up is the only non-contradictory response.
            if (isStall && isBelowFloor)
            {
                if (_state != State.Climb) TransitionIfChanged(State.Climb);
                _diagStallRecovery = false;
                _diagAltitudeFloor = true;
                _stallRecoveryTimer = 0f; // reset so it doesn't fire spuriously after the climb
                return DoClimb();
            }

            // Stall recovery: forced dive + full throttle for a fixed window.
            // Triggered when speed drops below stallMarginX × stallSpeed and
            // we're not already mid-recovery. Runs to completion even if
            // speed climbs back early — gives a real margin before resuming.
            if (isStall && _stallRecoveryTimer <= 0f)
            {
                _stallRecoveryTimer = stallRecoveryDurationS;
            }
            if (_stallRecoveryTimer > 0f)
            {
                _stallRecoveryTimer -= (float)dt;
                _diagStallRecovery = true;
                desired = new ControlInput
                {
                    Elevator = -0.30,
                    Aileron = 0.0,
                    Rudder = 0.0,
                    Throttle = 1.0,
                    Fire = false,
                };
            }
            else
            {
                _diagStallRecovery = false;
            }

            // Hard altitude floor — overrides EVERY other consideration
            // except STALL+FLOOR-escape-to-Climb above. Better to clip a
            // stall than the terrain.
            desired = ApplyAltitudeFloor(desired, altitude, altitudeFloorAGL, altitudeFloorElevatorMin);
            _diagAltitudeFloor = isBelowFloor;
            return desired;
        }

        // ============================================================
        // State transitions (called at 5 Hz)
        // ============================================================

        void UpdateStateTransitions()
        {
            // Climb is a hard interrupt — it can fire from any state.
            if (_state != State.Climb && _rb != null
                && ShouldEnterClimb(
                    _rb.position.y, _rb.linearVelocity.y, _excessDescentTime,
                    climbEntryAltitudeM, climbEntryDescentRateMs, climbEntryDescentSustainS))
            {
                TransitionIfChanged(State.Climb);
                return;
            }

            if (target == null) { TransitionIfChanged(State.Patrol); return; }

            float range = Vector3.Distance(target.position, _rb.position);
            float bearing = BearingToTargetDeg();

            switch (_state)
            {
                case State.Climb:
                    // Exit Climb when high enough AND actually climbing.
                    if (_rb != null
                        && ShouldExitClimb(_rb.position.y, _rb.linearVelocity.y, climbExitAltitudeM))
                    {
                        TransitionIfChanged(State.Patrol);
                    }
                    break;

                case State.Patrol:
                case State.Search:
                    if (range < visualRangeM && bearing < frontHemisphereDeg)
                        TransitionIfChanged(State.Engage);
                    break;

                case State.Engage:
                    // Altitude-bleed-aware abandon: if we've lost too much
                    // altitude or are descending too fast, break off into
                    // Climb instead of committing further.
                    if (_rb != null
                        && (_engageEntryAltitude - _rb.position.y > engageAbandonAltitudeDropM
                            || _rb.linearVelocity.y < -engageAbandonDescentRateMs))
                    {
                        TransitionIfChanged(State.Climb);
                    }
                    else if (ShouldDisengage())
                    {
                        TransitionIfChanged(State.Disengage);
                    }
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
            var prevState = _state;
            float elapsedInPrev = Time.time - _stateEnteredTime;
            _state = newState;
            _stateEnteredTime = Time.time;
            _noFiringSolutionTime = 0f;
            _excessDescentTime = 0f;
            if (newState == State.Engage && _rb != null)
                _engageEntryAltitude = _rb.position.y;

            if (logStateTransitions)
            {
                float range = (target != null && _rb != null)
                    ? Vector3.Distance(target.position, _rb.position) : -1f;
                float altitude = _rb != null ? _rb.position.y : 0f;
                float vy = _rb != null ? _rb.linearVelocity.y : 0f;
                Debug.Log($"[AI] {prevState} → {newState}  range={range:F0}m  alt={altitude:F0}m  vy={vy:F1}m/s  dwelled={elapsedInPrev:F2}s");
            }
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
                case State.Climb:     return DoClimb();
                case State.Patrol:
                case State.Search:
                default:              return DoPatrol();
            }
        }

        // Climb: steady wings-level pull-up at full throttle. Used both as
        // a strategic-state response (low altitude / sustained descent /
        // altitude-bleed abandon from Engage) and as the safe fallback
        // when STALL + FLOOR would otherwise produce contradictory inputs.
        ControlInput DoClimb()
        {
            return new ControlInput
            {
                Elevator = climbElevator,
                Aileron = 0.0,
                Rudder = 0.0,
                Throttle = 1.0,
                Fire = false,
            };
        }

        ControlInput DoPatrol()
        {
            // PD altitude hold around patrolTargetAltitudeM. The D term
            // opposes vertical velocity directly and is what damps the
            // ±90m / 30s oscillation a pure-P controller produced once
            // it had any altitude error to act on.
            double elevator = 0.0;
            if (_rb != null)
            {
                float elevError = patrolTargetAltitudeM - _rb.position.y;
                float dampingTerm = _rb.linearVelocity.y * patrolAltitudeDampingGain;
                elevator = Mathf.Clamp(elevError * patrolAltitudeGain - dampingTerm,
                                       -patrolElevatorClamp, patrolElevatorClamp);
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
            // leadDir.y/z are sin(pitch error) / sin(yaw error) once the
            // direction is unit-length. Gain 3 saturated full deflection
            // at sin = 0.33 (~19°), causing the catastrophic dive when
            // the AI entered Engage with the target below its nose.
            // Gain 1.5 saturates at sin = 0.67 (~42° pitch error) and
            // gain 2 at sin = 0.5 (~30° yaw error) — aggressive enough
            // for pursuit, not for small corrections.
            Vector3 leadDir = toLeadBody.normalized;
            double elevator = Mathf.Clamp((float)leadDir.y * 1.5f, -1f, 1f);
            double aileron  = Mathf.Clamp((float)leadDir.z * 2f, -engageRollCap, engageRollCap);
            double rudder   = 0.0;

            // Coordinated-turn compensation: a banked wing's vertical lift
            // component falls to L·cos(bank), so a hard turn without extra
            // elevator sinks. Approximate bank from commanded |aileron| and
            // add 0.5× of the load-factor correction (n = 1/cos(φ)) above a
            // 0.5 aileron threshold. Round 4c logs showed −46 m/s vy peaks
            // in hard banks; this term reins them in.
            if (Mathf.Abs((float)aileron) > engageCoordTurnAileronThreshold)
            {
                float bankRad = Mathf.Abs((float)aileron) * engageCoordTurnBankScale;
                float extraElevator =
                    (1f / Mathf.Cos(bankRad) - 1f) * engageCoordTurnElevatorScale;
                elevator = Mathf.Clamp((float)(elevator + extraElevator), -1f, 1f);
            }

            // Throttle: full when far, reduce to 0.5 when closing inside
            // closeRange so we don't overshoot.
            double throttle = range > closeRangeM ? 1.0 : 0.5;

            // Track no-firing-solution timer.
            float deflectionDeg = Vector3.Angle(_rb.rotation * new Vector3(1f, 0f, 0f), toLeadWorld);
            float dt = Time.fixedDeltaTime;
            if (deflectionDeg > 30f) _noFiringSolutionTime += dt;
            else _noFiringSolutionTime = Mathf.Max(0f, _noFiringSolutionTime - dt);

            // Multi-window firing decision: fire if ANY window matches.
            //   Snap   — very close pass-by burst (≤ 50m, defl < 30°).
            //   Close  — short range, tight aim (≤ 100m, defl < 5°).
            //   Far    — medium range, moderate aim (≤ 300m, defl < 10°).
            bool fire = ShouldFireMultiWindow(
                deflectionDeg, range, _burstOn,
                farFireRangeM, farFireDeflectionDeg,
                closeFireRangeM, closeFireDeflectionDeg,
                snapFireRangeM, snapFireDeflectionDeg);

            _diagRange = range;
            _diagDeflectionDeg = deflectionDeg;

            var ctrl = new ControlInput { Elevator = elevator, Aileron = aileron, Rudder = rudder, Throttle = throttle, Fire = fire };

            // Energy preservation at low altitude: cap commanded pitch + roll
            // so the AI doesn't trade altitude it doesn't have during a
            // pursuit turn. ApplyAltitudeFloor (in ApplySafetyOverrides) is
            // the absolute floor below altitudeFloorAGL; this clamp is the
            // "be careful" band above the floor.
            if (_rb != null)
            {
                ctrl = ClampForLowAltitude(
                    ctrl, _rb.position.y, lowAltitudeThresholdM,
                    lowAltElevatorMin, lowAltElevatorMax, lowAltAileronCap);
            }
            return ctrl;
        }

        ControlInput DoEvade()
        {
            // Hard turn (bias right), drop nose, full throttle.
            return new ControlInput { Elevator = -0.5, Aileron = 0.8, Throttle = 1.0 };
        }

        ControlInput DoDisengage()
        {
            // Low-altitude disengage: lateral escape instead of diving.
            // Below the floor we don't have altitude to trade.
            if (_rb != null && _rb.position.y < disengageAltitudeFloorM)
            {
                return new ControlInput { Elevator = 0.0, Aileron = 0.6, Rudder = 0.0, Throttle = 1.0, Fire = false };
            }
            // Standard disengage: nose-down dive, full throttle. After
            // disengageDurationS the transition back to Search fires from
            // UpdateStateTransitions.
            return new ControlInput { Elevator = -disengageNoseDownNormalised, Throttle = 1.0 };
        }

        // ============================================================
        // Debug overlay
        // ============================================================

        void OnGUI()
        {
            if (!showAIDebug) return;
            const int W = 240;
            const int H = 168;
            const int LH = 18;
            int x = 12, y = 12;
            GUI.Box(new Rect(x, y, W, H), "AI Debug");
            y += 22;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"State:      {_state}"); y += LH;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Range:      {_diagRange,6:F0} m"); y += LH;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Deflection: {_diagDeflectionDeg,6:F1}°"); y += LH;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Speed:      {_diagSpeed,6:F1} m/s"); y += LH;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Altitude:   {_diagAltitude,6:F0} m"); y += LH;
            bool firedRecently = Time.time - _lastFireTime < 1.0f;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Throttle:   {_diagThrottle,6:F2}   Fire: {(firedRecently ? "●" : "○")}"); y += LH;
            string overrides = (_diagStallRecovery ? "STALL " : "") + (_diagAltitudeFloor ? "FLOOR" : "");
            if (overrides.Length > 0)
                GUI.Label(new Rect(x + 8, y, W - 16, LH), $"OVERRIDE:   {overrides}");
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

        // Climb-entry decision: below the entry altitude (hard trigger) OR
        // sustained excess descent rate (proactive trigger). Tested in
        // isolation by the AI tests.
        public static bool ShouldEnterClimb(
            float altitudeAGL, float verticalVelocityMs, float excessDescentSustainS,
            float entryAltitudeM, float entryDescentRateMs, float entryDescentSustainS)
        {
            if (altitudeAGL < entryAltitudeM) return true;
            if (verticalVelocityMs < -entryDescentRateMs && excessDescentSustainS > entryDescentSustainS) return true;
            return false;
        }

        // Climb-exit decision: above the exit altitude AND already climbing.
        // Both conditions matter — exit only when actually gaining alt.
        public static bool ShouldExitClimb(float altitudeAGL, float verticalVelocityMs, float exitAltitudeM)
        {
            return altitudeAGL > exitAltitudeM && verticalVelocityMs > 0f;
        }

        // Force minimum elevator (pull-up) when below the hard floor and
        // zero aileron + full throttle. Wings level for cleanest possible
        // pull-up; aileron causes energy loss + roll-coupled angle changes.
        public static ControlInput ApplyAltitudeFloor(
            ControlInput input, float altitudeAGL, float floorAGL, float forcedElevatorMin)
        {
            if (altitudeAGL >= floorAGL) return input;
            var output = input;
            if (output.Elevator < forcedElevatorMin) output.Elevator = forcedElevatorMin;
            output.Aileron = 0.0;
            output.Throttle = 1.0;
            return output;
        }

        // Clamp commanded inputs in the "careful" altitude band (between the
        // hard floor and a soft threshold above it). Prevents commanded
        // dives and hard banks that bleed altitude.
        public static ControlInput ClampForLowAltitude(
            ControlInput input, float altitudeAGL, float threshold,
            float elevatorMin, float elevatorMax, float aileronCap)
        {
            if (altitudeAGL >= threshold) return input;
            var output = input;
            if (output.Elevator < elevatorMin) output.Elevator = elevatorMin;
            if (output.Elevator > elevatorMax) output.Elevator = elevatorMax;
            if (output.Aileron < -aileronCap) output.Aileron = -aileronCap;
            if (output.Aileron >  aileronCap) output.Aileron =  aileronCap;
            return output;
        }

        // Three-window firing decision: fire if the (range, deflection)
        // pair satisfies ANY of the configured windows (snap, close, far).
        // Snap fires at very-close pass-by ranges even with wide deflection;
        // close requires tight aim; far is the general medium-range window.
        public static bool ShouldFireMultiWindow(
            float deflectionDeg, float rangeM, bool burstOn,
            float farRangeM, float farDeflectionDeg,
            float closeRangeM, float closeDeflectionDeg,
            float snapRangeM, float snapDeflectionDeg)
        {
            if (!burstOn) return false;
            if (rangeM < snapRangeM  && deflectionDeg < snapDeflectionDeg)  return true;
            if (rangeM < closeRangeM && deflectionDeg < closeDeflectionDeg) return true;
            if (rangeM < farRangeM   && deflectionDeg < farDeflectionDeg)   return true;
            return false;
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

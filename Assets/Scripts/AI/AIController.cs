using System;
using UnityEngine;
using AcesOverTheLines.Flight;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.AI
{
    // Basic AI opponent. Implements IFlightControlSource so AircraftController
    // discovers it the same way it discovers FlightInput; flight physics is
    // identical between player and AI — no cheating.
    //
    // Two-tier control architecture (Stage 6 Round 5):
    //   * Tier 1 (this class): FSM emits a FlightSetpoint per state.
    //   * Tier 2 (FlightStabilizer): cos(bank)-gated PIDs translate the
    //     setpoint into a ControlInput, with rate-limited actuator output.
    //
    // The "safety override" stack from rounds 4a–4h (forced stall dive,
    // altitude-floor pull-up, climb hard-interrupt, cos-loss elevator
    // compensation, wings-level helper) is gone from the runtime path —
    // they were all workarounds for an architectural defect the stabilizer
    // now solves mathematically via the cos(bank) pitch-authority gate.
    //
    // The static helper functions (ApplyAltitudeFloor, ClampForLowAltitude,
    // ShouldEnterClimb, ShouldExitClimb) are retained because their tests
    // continue to pass and they're harmless pure-math. ShouldExitClimb
    // is the only one still called from the live code.
    public class AIController : MonoBehaviour, IFlightControlSource
    {
        public enum State { Patrol, Search, Engage, Evade, Disengage, RTB, Climb, Recover }

        [SerializeField] Transform target;
        [SerializeField] float decisionRateHz = 5f;
        [SerializeField] bool showAIDebug = true;
        // Diagnostic: log every state machine transition with range, altitude,
        // descent rate, and dwell time. Public so PlayMode runs can toggle
        // it without recompile; default off to avoid log spam in production.
        public bool logStateTransitions = false;

        // Engagement geometry thresholds.
        [SerializeField] float visualRangeM = 1000f;
        [SerializeField] float frontHemisphereDeg = 60f;

        // Setpoint clamps (per state).
        [SerializeField] float engageBankClampRad  = 0.7f;  // ±40°
        [SerializeField] float engagePitchClampRad = 0.35f; // ±20°
        [SerializeField] float engageAirspeedMs    = 55f;
        [SerializeField] float patrolBankClampRad  = 0.3f;  // ±17.2°
        [SerializeField] float patrolAirspeedMs    = 40f;
        [SerializeField] float climbPitchRad       = 0.3f;
        [SerializeField] float climbAirspeedMs     = 45f;

        // Three firing windows (any matching window fires when burst is on).
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
        [SerializeField] float disengageDurationS = 5f;
        [SerializeField] float disengageAltitudeFloorM = 300f;
        [SerializeField] float evadeDurationS = 3f;

        // Climb→Patrol exit threshold (state-machine transition, not a
        // runtime control override). 700m chosen so Climb exits when
        // re-engagement is viable, not when full patrol altitude is
        // recovered — the difference between a 20-second pass cycle and
        // a 60-second one.
        [SerializeField] float climbExitAltitudeM = 700f;

        // Engage tactical abandon: transitions Engage→Climb when pursuit
        // costs too much altitude. This is a state-machine decision, not a
        // per-tick safety override — it stays.
        [SerializeField] float engageAbandonAltitudeDropM = 500f;
        [SerializeField] float engageAbandonDescentRateMs = 25f;

        // Recover state: attitude-based hard interrupt that replaces the
        // altitude-floor and stall-recovery overrides deleted in Commit 2.
        // Triggers on dangerous attitude regardless of current state;
        // exits when wings near level, not descending hard, above the
        // hard-floor altitude.
        [SerializeField] float recoverBankTriggerRad     = 1.05f;   // ≈ 60°
        [SerializeField] float recoverPitchTriggerRad    = -0.785f; // ≈ -45°
        [SerializeField] float recoverDescentTriggerMs   = 30f;
        [SerializeField] float recoverDescentTriggerAltM = 500f;
        [SerializeField] float recoverBankExitRad        = 0.26f;   // ≈ 15°
        [SerializeField] float recoverVyExitMs           = -5f;
        [SerializeField] float recoverExitAltitudeM      = 300f;
        [SerializeField] float recoverAirspeedMs         = 70f;
        [SerializeField] float recoverPitchRad           = 0.3f;    // killer-test setpoint

        AircraftController _ctrl;
        Rigidbody _rb;
        Rigidbody _targetRb;
        FlightStabilizer _stabilizer;

        State _state = State.Patrol;
        float _stateEnteredTime;
        float _lastDecisionTime;
        float _noFiringSolutionTime;
        float _burstTimer;
        bool _burstOn;
        float _engageEntryAltitude;
        float _lastCmdLogTime = -10f;

        // Diagnostic state cached per-tick for the OnGUI overlay.
        float _diagRange;
        float _diagDeflectionDeg;
        float _diagSpeed;
        float _diagAltitude;
        double _diagThrottle;
        float _lastFireTime = -10f;
        int _initialAmmoTotal;

        public State CurrentState => _state;
        public Transform Target { get => target; set { target = value; _targetRb = target != null ? target.GetComponent<Rigidbody>() : null; } }

        void Awake()
        {
            _ctrl = GetComponent<AircraftController>();
            _rb = GetComponent<Rigidbody>();
            _stabilizer = new FlightStabilizer();
            if (target == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null) target = playerGo.transform;
            }
            if (target != null) _targetRb = target.GetComponent<Rigidbody>();
        }

        void Start()
        {
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

            UpdateBurst((float)dt);
            FlightSetpoint setpoint = ComputeDesiredSetpoint();

            ControlInput cmd = _rb != null
                ? _stabilizer.Stabilize(setpoint, _rb, dt)
                : new ControlInput { Throttle = 0.7, Fire = setpoint.Fire };
            cmd.Fire = setpoint.Fire;

            // Cache diagnostic state for the OnGUI overlay.
            if (_rb != null)
            {
                _diagSpeed = _rb.linearVelocity.magnitude;
                _diagAltitude = _rb.position.y;
            }
            _diagThrottle = cmd.Throttle;
            if (cmd.Fire) _lastFireTime = Time.time;

            // Throttled per-tick log: setpoint → stabilizer output. Same
            // 0.5 Hz cadence and logStateTransitions gate as before.
            if (logStateTransitions && Time.time - _lastCmdLogTime >= 0.5f)
            {
                _lastCmdLogTime = Time.time;
                float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
                Debug.Log($"[AI-CMD] state={_state}  set(bnk={setpoint.DesiredBankRad:F2} ptc={setpoint.DesiredPitchRad:F2} spd={setpoint.DesiredAirspeedMs:F0})  out(ail={cmd.Aileron:F2} elv={cmd.Elevator:F2} thr={cmd.Throttle:F2})  alt={_diagAltitude:F0}  vy={_rb?.linearVelocity.y ?? 0f:F1}  v={speed:F1}");
            }

            return cmd;
        }

        // ============================================================
        // State transitions (called at 5 Hz)
        // ============================================================

        void UpdateStateTransitions()
        {
            if (_rb == null) return;

            // Recover is the highest-priority state. Attitude-based hard
            // interrupt that supersedes any tactical decision. Replaces
            // the climb-from-anywhere and altitude-floor pull-up triggers
            // that were deleted in Commit 2.
            if (_state != State.Recover
                && ShouldEnterRecover(
                    ExtractBankAndPitch(_rb.rotation),
                    _rb.linearVelocity.y,
                    _rb.position.y,
                    recoverBankTriggerRad, recoverPitchTriggerRad,
                    recoverDescentTriggerMs, recoverDescentTriggerAltM))
            {
                TransitionIfChanged(State.Recover);
                return;
            }

            if (target == null) { TransitionIfChanged(State.Patrol); return; }

            float range = Vector3.Distance(target.position, _rb.position);
            float bearing = BearingToTargetDeg();

            switch (_state)
            {
                case State.Recover:
                    if (ShouldExitRecover(
                            ExtractBankAndPitch(_rb.rotation).bank,
                            _rb.linearVelocity.y, _rb.position.y,
                            recoverBankExitRad, recoverVyExitMs, recoverExitAltitudeM))
                    {
                        TransitionIfChanged(State.Patrol);
                    }
                    break;

                case State.Climb:
                    if (ShouldExitClimb(_rb.position.y, _rb.linearVelocity.y, climbExitAltitudeM))
                        TransitionIfChanged(State.Patrol);
                    break;

                case State.Patrol:
                case State.Search:
                    if (range < visualRangeM && bearing < frontHemisphereDeg)
                        TransitionIfChanged(State.Engage);
                    break;

                case State.Engage:
                    // Tactical altitude-bleed abandon: if pursuit costs too
                    // much altitude or vy is too negative, transition to
                    // Climb instead of pressing further. This is FSM-level
                    // energy management, not a per-tick control override.
                    if (_engageEntryAltitude - _rb.position.y > engageAbandonAltitudeDropM
                        || _rb.linearVelocity.y < -engageAbandonDescentRateMs)
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
            if (newState == State.Engage && _rb != null)
                _engageEntryAltitude = _rb.position.y;

            // Reset stabilizer history on state change. Setpoint
            // discontinuities (e.g., Patrol→Engage jumps bank from
            // ±0.3 to ±0.7) should not smear through PID derivative
            // and integral terms.
            _stabilizer?.Reset();

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
        // Per-state setpoint emitters
        // ============================================================

        FlightSetpoint ComputeDesiredSetpoint()
        {
            switch (_state)
            {
                case State.Recover:   return DoRecover();
                case State.Engage:    return DoEngage();
                case State.Evade:     return DoEvade();
                case State.Disengage: return DoDisengage();
                case State.Climb:     return DoClimb();
                case State.Patrol:
                case State.Search:
                default:              return DoPatrol();
            }
        }

        // Maximum-aggression recovery: wings level, hard pitch up, full
        // airspeed. Same setpoint shape that the Commit 1 killer test
        // verified can recover a 170° inverted descent at 30 m/s in
        // 6 seconds with the chosen PID gains.
        FlightSetpoint DoRecover()
        {
            return new FlightSetpoint
            {
                DesiredBankRad    = 0.0,
                DesiredPitchRad   = recoverPitchRad,
                DesiredAirspeedMs = recoverAirspeedMs,
                Fire = false,
            };
        }

        FlightSetpoint DoClimb()
        {
            return new FlightSetpoint
            {
                DesiredBankRad    = 0.0,
                DesiredPitchRad   = climbPitchRad,
                DesiredAirspeedMs = climbAirspeedMs,
                Fire = false,
            };
        }

        FlightSetpoint DoPatrol()
        {
            // Bank gently toward target if it's within sight; otherwise
            // straight and level. The stabilizer's roll PID drives toward
            // this bank setpoint and naturally smooths transitions.
            double bank = 0.0;
            if (target != null && _rb != null)
            {
                Vector3 toTargetWorld = target.position - _rb.position;
                float trackRange = visualRangeM * 1.5f;
                if (toTargetWorld.sqrMagnitude < trackRange * trackRange)
                {
                    Vector3 toTargetBody = Quaternion.Inverse(_rb.rotation) * toTargetWorld;
                    bank = Mathf.Atan2(toTargetBody.z, toTargetBody.x);
                    bank = Mathf.Clamp((float)bank, -patrolBankClampRad, patrolBankClampRad);
                }
            }
            return new FlightSetpoint
            {
                DesiredBankRad    = bank,
                DesiredPitchRad   = 0.0,
                DesiredAirspeedMs = patrolAirspeedMs,
                Fire = false,
            };
        }

        FlightSetpoint DoEngage()
        {
            if (target == null || _rb == null)
                return new FlightSetpoint { DesiredAirspeedMs = patrolAirspeedMs };

            float range = Vector3.Distance(target.position, _rb.position);
            Vector3 leadPoint = ComputeLeadPoint(
                target.position,
                _targetRb != null ? _targetRb.linearVelocity : Vector3.zero,
                _rb.position,
                MuzzleVelocity());

            Vector3 toLeadWorld = (leadPoint - _rb.position).normalized;
            Vector3 toLeadBody  = Quaternion.Inverse(_rb.rotation) * toLeadWorld;

            // Body frame: +x forward, +y up, +z right.
            // atan2(z, x) is the yaw angle to lead point: positive when
            // lead is to the right → bank right (positive setpoint).
            // atan2(y, x) is the pitch angle to lead: positive when lead
            // is above the body x-axis → pitch up.
            double bank  = Mathf.Atan2(toLeadBody.z, toLeadBody.x);
            double pitch = Mathf.Atan2(toLeadBody.y, toLeadBody.x);
            bank  = Mathf.Clamp((float)bank,  -engageBankClampRad,  engageBankClampRad);
            pitch = Mathf.Clamp((float)pitch, -engagePitchClampRad, engagePitchClampRad);

            // Multi-window firing decision (unchanged).
            float deflectionDeg = Vector3.Angle(_rb.rotation * new Vector3(1f, 0f, 0f), toLeadWorld);
            float dt = Time.fixedDeltaTime;
            if (deflectionDeg > 30f) _noFiringSolutionTime += dt;
            else _noFiringSolutionTime = Mathf.Max(0f, _noFiringSolutionTime - dt);

            bool fire = ShouldFireMultiWindow(
                deflectionDeg, range, _burstOn,
                farFireRangeM,   farFireDeflectionDeg,
                closeFireRangeM, closeFireDeflectionDeg,
                snapFireRangeM,  snapFireDeflectionDeg);

            _diagRange = range;
            _diagDeflectionDeg = deflectionDeg;

            return new FlightSetpoint
            {
                DesiredBankRad    = bank,
                DesiredPitchRad   = pitch,
                DesiredAirspeedMs = engageAirspeedMs,
                Fire = fire,
            };
        }

        FlightSetpoint DoEvade()
        {
            // Hard right turn with slight nose-down, full airspeed.
            return new FlightSetpoint
            {
                DesiredBankRad    = 0.7,
                DesiredPitchRad   = -0.2,
                DesiredAirspeedMs = 55.0,
            };
        }

        FlightSetpoint DoDisengage()
        {
            // Low altitude: lateral escape (no diving).
            if (_rb != null && _rb.position.y < disengageAltitudeFloorM)
            {
                return new FlightSetpoint
                {
                    DesiredBankRad    = 0.5,
                    DesiredPitchRad   = 0.0,
                    DesiredAirspeedMs = 55.0,
                };
            }
            // Standard: dive away straight ahead.
            return new FlightSetpoint
            {
                DesiredBankRad    = 0.0,
                DesiredPitchRad   = -0.3,
                DesiredAirspeedMs = 60.0,
            };
        }

        // ============================================================
        // Debug overlay
        // ============================================================

        void OnGUI()
        {
            if (!showAIDebug) return;
            const int W = 240;
            const int H = 150;
            const int LH = 18;
            int x = 12, y = 12;
            GUI.Box(new Rect(x, y, W, H), "AI Debug");
            y += 22;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"State:      {_state}"); y += LH;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Range:      {_diagRange,6:F0} m"); y += LH;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Deflection: {_diagDeflectionDeg,6:F1}°"); y += LH;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Speed:      {_diagSpeed,6:F1} m/s"); y += LH;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Altitude:   {_diagAltitude * 3.28084f,6:F0} ft"); y += LH;
            bool firedRecently = Time.time - _lastFireTime < 1.0f;
            GUI.Label(new Rect(x + 8, y, W - 16, LH), $"Throttle:   {_diagThrottle,6:F2}   Fire: {(firedRecently ? "●" : "○")}");
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

            var ws = GetComponent<WeaponSystem>();
            int currentAmmo = 0;
            if (ws != null && ws.Guns != null)
                foreach (var g in ws.Guns) currentAmmo += g.Rounds;
            if (_initialAmmoTotal > 0 && (float)currentAmmo / _initialAmmoTotal < ammoLowFraction)
                conditions++;

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

            if (_rb != null && _rb.linearVelocity.magnitude < energyLowSpeedMs
                            && _rb.position.y < energyLowAltitudeM)
                conditions++;

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
        // Static testable helpers — preserved with their tests. The
        // first three are no longer called from runtime code; the
        // stabilizer's cos(bank) gate and PID rate limits handle what
        // they used to handle. ShouldExitClimb is still live.
        // ============================================================

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

        // No longer called from runtime — the stabilizer's pitch PID
        // with cos(bank) gate replaces the per-state hard interrupt
        // for "below floor" recovery. Kept here because its tests
        // exercise correct pure-math behaviour.
        public static bool ShouldEnterClimb(
            float altitudeAGL, float verticalVelocityMs, float excessDescentSustainS,
            float entryAltitudeM, float entryDescentRateMs, float entryDescentSustainS)
        {
            if (altitudeAGL < entryAltitudeM) return true;
            if (verticalVelocityMs < -entryDescentRateMs && excessDescentSustainS > entryDescentSustainS) return true;
            return false;
        }

        // Live: drives the Climb→Patrol state transition.
        public static bool ShouldExitClimb(float altitudeAGL, float verticalVelocityMs, float exitAltitudeM)
        {
            return altitudeAGL > exitAltitudeM && verticalVelocityMs > 0f;
        }

        // Live: Recover state hard interrupt. Fires from any state when
        // attitude is dangerous (deep bank, deep nose-down) or when the
        // aircraft is descending fast below a hard altitude threshold.
        public static bool ShouldEnterRecover(
            (float bank, float pitch) attitude,
            float vy, float altitudeAGL,
            float bankTriggerRad, float pitchTriggerRad,
            float descentTriggerMs, float descentTriggerAltM)
        {
            if (Mathf.Abs(attitude.bank) > bankTriggerRad) return true;
            if (attitude.pitch < pitchTriggerRad) return true;
            if (vy < -descentTriggerMs && altitudeAGL < descentTriggerAltM) return true;
            return false;
        }

        // Live: Recover→Patrol state transition once the aircraft is
        // safely recovered (wings near level, no longer descending hard,
        // safely above the hard-floor altitude).
        public static bool ShouldExitRecover(
            float bank, float vy, float altitudeAGL,
            float bankExitRad, float vyExitMs, float exitAltitudeM)
        {
            return Mathf.Abs(bank) < bankExitRad
                && vy > vyExitMs
                && altitudeAGL > exitAltitudeM;
        }

        // Convenience wrapper: cast the stabilizer's double tuple to
        // floats so the public static triggers don't need to take
        // doubles. The conversion only ever loses precision beyond the
        // 0.001 rad / 0.06° threshold, well below the triggers' bands.
        static (float bank, float pitch) ExtractBankAndPitch(Quaternion bodyToWorld)
        {
            var (bank, pitch) = FlightStabilizer.ExtractAttitude(bodyToWorld);
            return ((float)bank, (float)pitch);
        }

        // No longer called from runtime — the stabilizer's PID + rate
        // limits keep pitch and bank within structural envelope without
        // a per-tick post-hoc clamp.
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

        // No longer called from runtime — see ApplyAltitudeFloor.
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
    }
}

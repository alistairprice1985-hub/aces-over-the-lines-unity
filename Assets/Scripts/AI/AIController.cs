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

        // Engagement pursuit modes selected by SelectPursuitMode (§6.2 of
        // docs/AI-STATE-OF-PLAY.md). Lead = nose ahead of target for gun
        // solution; Lag = nose behind target's track, preserve energy and
        // re-position; Pure = nose on target, close range fast at the cost
        // of energy. Round 6 Commit 1.
        public enum PursuitMode { Lead, Lag, Pure }

        [SerializeField] Transform target;
        [SerializeField] float decisionRateHz = 5f;
        [SerializeField] bool showAIDebug = true;
        // Diagnostic: log every state machine transition with range, altitude,
        // descent rate, and dwell time. Public so PlayMode runs can toggle
        // it without recompile; default off to avoid log spam in production.
        public bool logStateTransitions = false;

        // Engagement geometry thresholds.
        [SerializeField] float visualRangeM = 1000f;
        [SerializeField] float frontHemisphereDeg = 180f;  // 360° engagement awareness — engage any target within visual range

        // Setpoint clamps (per state).
        [SerializeField] float engageBankClampRad  = 0.7f;  // ±40° — leaves headroom below recoverBankTriggerRad=1.05 to avoid spurious Recover triggers
        [SerializeField] float engagePitchClampRad = 0.50f; // ±28.6° — leaves headroom below recoverPitchTriggerRad=-0.785 to avoid spurious Recover triggers
        [SerializeField] float engageAirspeedMs    = 55f;
        [SerializeField] float patrolBankClampRad   = 0.45f;  // ±25.8°
        [SerializeField] float patrolPitchRad       = 0.15f;  // ≈8.6° — pitch at full bank (target tracking)
        [SerializeField] float patrolCruisePitchRad = 0.05f;  // ≈2.9° — pitch at wings level (cruise, no bank-coupled lift loss)
        [SerializeField] float patrolAirspeedMs     = 55f;   // matches engageAirspeedMs — keep throttle PID active
        [SerializeField] float climbPitchRad       = 0.3f;
        // 2026-05-17 playtest fix (Issue 3a): bumped from 45 to 60 so the
        // throttle PID commands ~full power during Climb. The previous 45
        // setpoint was below typical Engage→Climb entry speed (~53–55 m/s),
        // so the PID idled the throttle (logged thr=0.00) just when the AI
        // needed to climb out hardest.
        [SerializeField] float climbAirspeedMs     = 60f;

        // Three firing windows (any matching window fires when burst is on).
        [SerializeField] float farFireRangeM = 300f;
        [SerializeField] float farFireDeflectionDeg = 25f;   // widened — AI clamp saturation prevents tighter geometry
        [SerializeField] float closeFireRangeM = 100f;
        [SerializeField] float closeFireDeflectionDeg = 15f;   // widened — allow close-range bursts during maneuver fights
        [SerializeField] float snapFireRangeM = 50f;
        [SerializeField] float snapFireDeflectionDeg = 60f;   // widened — WW1-realistic close-range spray fire

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
        [SerializeField] float disengageAltitudeFloorM = 1000f;   // dive branch only safe above 1000m; 600 caused terminal dive
        [SerializeField] float engageBreakOffRangeM = 60f;   // force break-off before head-on collision
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
        [SerializeField] float engageAbandonDescentRateMs = 45f;   // allow committed dive into firing range; 25 caused bailout before farFireRangeM

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

        // Round 6 Commit 2 — pathological-behaviour fixes from 2026-05-17 playtest.

        [Header("Engage limits")]
        [SerializeField] float engageStalemateTimeout = 25f;   // seconds — forced reset if Engage dwells without a firing solution
        [SerializeField] float noFiringSolutionTimeout = 12f;  // seconds — entry-cone gap that counts as "no firing solution"

        [Header("Disengage exit gates")]
        [SerializeField] float disengageMinRange = 600f;        // metres
        [SerializeField] float disengageMinDwell = 8f;          // seconds
        [SerializeField] float disengageMinHeadingDelta = 70f;  // degrees off bandit bearing

        [Header("Pursuit-mode hysteresis")]
        [SerializeField] float modeSwitchHoldTime = 0.5f;   // seconds the raw mode must persist before committing
        [SerializeField] float lagBleedMaxDeltaE = 100f;    // metres — above this ΔE, Lag re-positions instead of bleeding energy

        [Header("Gunnery (cone-latch fire)")]
        [SerializeField] float burstMinDuration = 0.40f;   // seconds — minimum trigger-hold once entry cone is satisfied
        [SerializeField] float entryConeDeg     = 2.0f;    // degrees — angle-off required to START a burst
        [SerializeField] float holdConeDeg      = 4.5f;    // degrees — angle-off allowed to CONTINUE a started burst
        [SerializeField] float maxFireRangeM    = 250f;    // metres — hard range cap for any firing

        [Header("Recovery floors")]
        [SerializeField] float climbFloorAltitude = 700f;  // metres AGL — absolute altitude triggers Engage → Climb
        [SerializeField] float climbFloorVy       = -20f;  // m/s — descent rate triggers Engage → Climb (less negative than the legacy abandon trigger)

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

        // Round 6 Commit 2 — stalemate-timeout tracker and burst-fire latch.
        float _timeSinceFiringSolution;
        float _burstUntil;
        PursuitMode _committedMode = PursuitMode.Lead;
        PursuitMode _modeCandidate = PursuitMode.Lead;
        float _modeCandidateSince;

        // Diagnostic state cached per-tick for the OnGUI overlay.
        float _diagRange;
        float _diagDeflectionDeg;
        float _diagSpeed;
        float _diagAltitude;
        double _diagThrottle;
        PursuitMode _diagPursuitMode = PursuitMode.Lead;
        double _diagDeltaE = 0.0;
        float _lastFireTime = -10f;
        int _initialAmmoTotal;

        public State CurrentState => _state;
        public Transform Target { get => target; set { target = value; _targetRb = target != null ? target.GetComponent<Rigidbody>() : null; } }

        // Testability hook (added 2026-05-17 alongside the playtest harness
        // work). NowSecondsSource defaults to UnityEngine.Time.time so
        // production behaviour is byte-identical to before the indirection.
        // EditMode playtest harnesses replace it with a deterministic
        // counter so every clock-driven gate (decision rate, dwell timers,
        // mode-switch hysteresis, burst-fire latch) advances under explicit
        // control instead of wall-clock. Instance-level (not static) so
        // parallel test fixtures cannot pollute each other's clocks.
        public System.Func<float> NowSecondsSource { get; set; } = () => UnityEngine.Time.time;
        float NowSeconds => NowSecondsSource();

        // Internal observable for the clock-injection test; never read
        // from production code. Promoted from a bare _stateEnteredTime
        // read via InternalsVisibleTo rather than going public.
        internal float StateEnteredTime => _stateEnteredTime;

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
            if (NowSeconds - _lastDecisionTime >= 1f / decisionRateHz)
            {
                _lastDecisionTime = NowSeconds;
                UpdateStateTransitions();
            }

            UpdateBurst((float)dt);
            FlightSetpoint setpoint = ComputeDesiredSetpoint();

            ControlInput cmd = _rb != null
                ? _stabilizer.Stabilize(setpoint, _rb, dt)
                : new ControlInput { Throttle = 0.7, Fire = setpoint.Fire };
            cmd.Fire = setpoint.Fire;

            // 2026-05-17 playtest fix (Issue 3b): cap aileron during Climb.
            // Climb inherits whatever bank the aircraft was carrying out of
            // Engage. FlightStabilizer.Reset already zeros the roll PID on
            // state change, but the rate-limited output still ramped to
            // ail=0.80 in playtest as the PID worked to null a large bank
            // error. Clamping the output during the recovery state prevents
            // the stick-slam without changing the PID's internal behaviour.
            if (_state == State.Climb)
            {
                if (cmd.Aileron >  0.30) cmd.Aileron =  0.30;
                if (cmd.Aileron < -0.30) cmd.Aileron = -0.30;
            }

            // Cache diagnostic state for the OnGUI overlay.
            if (_rb != null)
            {
                _diagSpeed = _rb.linearVelocity.magnitude;
                _diagAltitude = _rb.position.y;
            }
            _diagThrottle = cmd.Throttle;
            if (cmd.Fire) _lastFireTime = NowSeconds;

            // Throttled per-tick log: setpoint → stabilizer output. Same
            // 0.5 Hz cadence and logStateTransitions gate as before.
            if (logStateTransitions && NowSeconds - _lastCmdLogTime >= 0.5f)
            {
                _lastCmdLogTime = NowSeconds;
                float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
                string engageDiag = _state == State.Engage
                    ? $"  mode={_diagPursuitMode}  ΔE={_diagDeltaE:F0}m"
                    : "";
                Debug.Log($"[AI-CMD] state={_state}  set(bnk={setpoint.DesiredBankRad:F2} ptc={setpoint.DesiredPitchRad:F2} spd={setpoint.DesiredAirspeedMs:F0} fire={(setpoint.Fire ? 1 : 0)})  out(ail={cmd.Aileron:F2} elv={cmd.Elevator:F2} thr={cmd.Throttle:F2})  alt={_diagAltitude:F0}  vy={_rb?.linearVelocity.y ?? 0f:F1}  v={speed:F1}{engageDiag}");
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
                    // 2026-05-17 playtest fix (Issue 1): the legacy
                    // (range < visualRangeM && bearing < frontHemisphereDeg)
                    // gate turned Patrol into a permanent dead-end once
                    // the target drifted past 1000 m. ShouldEnterEngage
                    // FromPatrol now expresses the policy: target alone
                    // is sufficient. The earlier-out above already routes
                    // back to Patrol when target == null, so target is
                    // non-null here.
                    if (ShouldEnterEngageFromPatrol(target != null))
                        TransitionIfChanged(State.Engage);
                    break;

                case State.Engage:
                {
                    float engageDwell = NowSeconds - _stateEnteredTime;

                    // Fix 1: forced reset if Engage has dwelled past the
                    // stalemate timeout AND no firing solution has been
                    // produced for too long. Without this, Engage has no
                    // upper bound and the 2026-05-17 playtest saw 40s and
                    // 197s Engage dwells with no resolution.
                    if (engageDwell > engageStalemateTimeout
                        && _timeSinceFiringSolution > noFiringSolutionTimeout)
                    {
                        TransitionIfChanged(State.Disengage);
                        return;
                    }

                    // Collision avoidance: if we've closed inside the break-off
                    // range, force Disengage immediately. Prevents the AI from
                    // flying head-on into the target on overshoot.
                    if (range < engageBreakOffRangeM)
                    {
                        TransitionIfChanged(State.Disengage);
                    }
                    // Fix 5: Engage → Climb floor on absolute altitude or
                    // descent rate. Replaces the legacy
                    // (entryAltitudeDrop > 500m) OR (vy < -45m/s) predicate,
                    // which only caught catastrophic descents. Below Fix 1
                    // so a stalemated AI does not oscillate Engage↔Climb.
                    else if (_rb.linearVelocity.y <= climbFloorVy
                        || _rb.position.y <= climbFloorAltitude)
                    {
                        TransitionIfChanged(State.Climb);
                    }
                    else if (ShouldDisengage())
                    {
                        TransitionIfChanged(State.Disengage);
                    }
                    break;
                }

                case State.Evade:
                    if (NowSeconds - _stateEnteredTime > evadeDurationS)
                        TransitionIfChanged(State.Engage);
                    break;

                case State.Disengage:
                {
                    // Fix 2: require real geometric separation, not just
                    // own-ship recovery time. The legacy exit fired ~5s
                    // after entry regardless of whether the AI was still
                    // co-located with the bandit. BearingToTargetDeg()
                    // returns the angle between body-forward and the
                    // direction to target, which is the same scalar as
                    // |DeltaAngle(ownHeading, bearingToTarget)|.
                    float disengageDwell = NowSeconds - _stateEnteredTime;
                    float headingDelta = BearingToTargetDeg();
                    if (range > disengageMinRange
                        && disengageDwell > disengageMinDwell
                        && headingDelta > disengageMinHeadingDelta)
                    {
                        TransitionIfChanged(State.Search);
                    }
                    break;
                }

                case State.RTB:
                    break;
            }
        }

        void TransitionIfChanged(State newState)
        {
            if (_state == newState) return;
            var prevState = _state;
            float elapsedInPrev = NowSeconds - _stateEnteredTime;
            _state = newState;
            _stateEnteredTime = NowSeconds;
            _noFiringSolutionTime = 0f;
            _timeSinceFiringSolution = 0f;
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
            // Bank-dependent pitch: a banked turn loses vertical lift in
            // proportion to (1 - cos(bank)). To hold altitude across bank
            // states, raise pitch as bank magnitude grows. At wings level
            // use cruise pitch (near-level flight). At full bank use the
            // higher tracking pitch. Linear interpolation is an empirical
            // approximation to the cos(bank)-driven relationship that fits
            // the observed playtest data well enough for tactical purposes.
            double bankFraction = Mathf.Abs((float)bank) / patrolBankClampRad;
            double pitch = Mathf.Lerp(patrolCruisePitchRad, patrolPitchRad, (float)bankFraction);
            return new FlightSetpoint
            {
                DesiredBankRad    = bank,
                DesiredPitchRad   = pitch,
                DesiredAirspeedMs = patrolAirspeedMs,
                Fire = false,
            };
        }

        // Round 6 Commit 1 — §6 DoEngage rewrite.
        // Computes energy state, selects pursuit mode (Lead/Lag/Pure), and emits
        // a setpoint computed from the chosen pursuit point. Multi-window firing
        // decision is unchanged from the previous implementation — fire-window
        // deflection caps naturally filter shots that lag-mode produces, so no
        // explicit mode-based fire gate is needed. See docs/AI-STATE-OF-PLAY.md
        // §6.3.
        //
        // Airspeed multipliers per §6.3:
        //   Lead = 1.00 × engageAirspeedMs
        //   Lag  = 0.85 × engageAirspeedMs (bleed off speed to tighten lag turn)
        //   Pure = 1.10 × engageAirspeedMs (close fast, capped at sustainable
        //          airframe limit — §6.3 specified 1.15 but Fokker D.VII level-
        //          flight top speed ≈ 1.1 × cruise; the 1.15 setpoint cannot be
        //          achieved in straight-and-level so we cap at the achievable
        //          1.10 to avoid PID throttle wind-up).
        FlightSetpoint DoEngage()
        {
            if (target == null || _rb == null)
                return new FlightSetpoint { DesiredAirspeedMs = patrolAirspeedMs };

            // --- Energy state ---
            double selfAlt = _rb.position.y;
            double selfSpd = _rb.linearVelocity.magnitude;
            double selfEnergy = ComputeEnergyState(selfAlt, selfSpd);

            double targetAlt = target.position.y;
            double targetSpd = _targetRb != null ? _targetRb.linearVelocity.magnitude : 0.0;
            double targetEnergy = ComputeEnergyState(targetAlt, targetSpd);

            double deltaE = selfEnergy - targetEnergy;

            // --- Geometry ---
            float range = Vector3.Distance(target.position, _rb.position);
            Vector3 targetForward = target.forward;
            Vector3 targetToSelf  = _rb.position - target.position;
            float aspectDeg = ComputeAspectAngleDeg(targetForward, targetToSelf);

            // --- Pursuit mode (with hysteresis, Fix 3a) ---
            // SelectPursuitMode is pure-math; the hysteresis lives here so
            // the static helper stays trivially testable. The raw decision
            // must persist continuously for modeSwitchHoldTime before it
            // overrides the committed mode — stops the per-tick Pure↔Lag
            // flapping that drove throttle 1.00↔0.00 in playtest.
            PursuitMode rawMode = SelectPursuitMode(
                deltaE, aspectDeg, range, closeFireRangeM, visualRangeM);
            if (rawMode != _committedMode)
            {
                if (rawMode != _modeCandidate)
                {
                    _modeCandidate = rawMode;
                    _modeCandidateSince = NowSeconds;
                }
                else if (NowSeconds - _modeCandidateSince >= modeSwitchHoldTime)
                {
                    _committedMode = rawMode;
                }
            }
            else
            {
                _modeCandidate = rawMode;
            }
            PursuitMode mode = _committedMode;

            // --- Pursuit point and setpoint ---
            Vector3 targetVel = _targetRb != null ? _targetRb.linearVelocity : Vector3.zero;
            Vector3 pursuitPoint = ComputePursuitPoint(
                mode, target.position, targetVel, _rb.position, MuzzleVelocity());

            Vector3 toPursuitWorld = (pursuitPoint - _rb.position).normalized;
            Vector3 toPursuitBody  = Quaternion.Inverse(_rb.rotation) * toPursuitWorld;

            // Body frame: +x forward, +y up, +z right.
            // atan2(z, x) is the yaw angle to pursuit point: positive when point
            // is to the right → bank right (positive setpoint). atan2(y, x) is
            // the pitch angle to pursuit point: positive when above the body
            // x-axis → pitch up.
            double bank  = Mathf.Atan2(toPursuitBody.z, toPursuitBody.x);
            double pitch = Mathf.Atan2(toPursuitBody.y, toPursuitBody.x);
            bank  = Mathf.Clamp((float)bank,  -engageBankClampRad,  engageBankClampRad);
            pitch = Mathf.Clamp((float)pitch, -engagePitchClampRad, engagePitchClampRad);

            // 2026-05-17 playtest fix (Issue 2): mode-independent
            // energy-bleed gate. The legacy Lag-only gate left Lead and
            // Pure free to command ptc=-0.50 spd=47 with +498m of energy
            // already ahead of the target. The gate now applies to all
            // three pursuit modes; mode still determines bank (via
            // pursuit-point geometry), pitch/airspeed are ΔE-driven.
            var bleed = ApplyEnergyBleedGate(deltaE, lagBleedMaxDeltaE);
            pitch = bleed.pitch;
            double airspeed = bleed.airspeed;

            // --- Firing decision (Fix 4: cone-latch burst) ---
            // Replaces the per-tick burst-cycle toggle that produced
            // single-tick fire=1 events in playtest. Trigger latches for
            // burstMinDuration once the tight entry cone is satisfied;
            // wider hold cone lets gunsight wobble continue the burst.
            float deflectionDeg = Vector3.Angle(
                _rb.rotation * new Vector3(1f, 0f, 0f), toPursuitWorld);
            float dt = Time.fixedDeltaTime;
            if (deflectionDeg > 30f) _noFiringSolutionTime += dt;
            else _noFiringSolutionTime = Mathf.Max(0f, _noFiringSolutionTime - dt);

            bool inEntryCone = deflectionDeg <= entryConeDeg && range <= maxFireRangeM;
            bool inHoldCone  = deflectionDeg <= holdConeDeg  && range <= maxFireRangeM;
            bool fire;
            if (inEntryCone)
            {
                _burstUntil = Mathf.Max(_burstUntil, NowSeconds + burstMinDuration);
                _timeSinceFiringSolution = 0f;     // Fix 1 hook
                fire = true;
            }
            else
            {
                fire = NowSeconds < _burstUntil && inHoldCone;
                if (!fire) _timeSinceFiringSolution += dt;
            }

            // --- Diagnostic state cache ---
            _diagRange = range;
            _diagDeflectionDeg = deflectionDeg;
            _diagPursuitMode = mode;
            _diagDeltaE = deltaE;

            return new FlightSetpoint
            {
                DesiredBankRad    = bank,
                DesiredPitchRad   = pitch,
                DesiredAirspeedMs = airspeed,
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
            // Low altitude: lateral escape with gentle climb (no diving).
            if (_rb != null && _rb.position.y < disengageAltitudeFloorM)
            {
                return new FlightSetpoint
                {
                    DesiredBankRad    = 0.5,
                    DesiredPitchRad   = 0.10,
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
            bool firedRecently = NowSeconds - _lastFireTime < 1.0f;
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

        // ============================================================
        // §6 DoEngage rewrite — static testable helpers (Round 6 Commit 1)
        // ============================================================

        // Specific energy in altitude-equivalent metres: E_s = h + v² / (2g).
        // Collapses altitude and airspeed into a single comparable scalar so
        // two aircraft's relative energy state can be expressed as a delta.
        // Per §6.1 of docs/AI-STATE-OF-PLAY.md.
        public static double ComputeEnergyState(double altitudeM, double speedMs)
        {
            const double G = 9.81;
            return altitudeM + (speedMs * speedMs) / (2.0 * G);
        }

        // Aspect angle: off-tail angle of attacker as seen from target's velocity
        // vector. 0° = directly astern of target (best gun position), 90° = beam,
        // 180° = head-on. Per §6.2 of docs/AI-STATE-OF-PLAY.md.
        public static float ComputeAspectAngleDeg(Vector3 targetForward, Vector3 targetToSelf)
        {
            return Vector3.Angle(targetForward, targetToSelf);
        }

        // Pursuit-mode selection per §6.2's decision tree (first match wins):
        //   1. aspect > 120°                              → Lag (doctrinal floor)
        //   2. ΔE < 0                                     → Lag (preserve energy)
        //   3. aspect > 90° AND range > closeFireRangeM   → Lag (re-position)
        //   4. range > 0.4 × visualRangeM AND ΔE > 50m    → Pure (close)
        //   5. else                                       → Lead (gun)
        public static PursuitMode SelectPursuitMode(
            double deltaE, float aspectDeg, float rangeM,
            float closeFireRangeM, float visualRangeM)
        {
            if (aspectDeg > 120f)                                       return PursuitMode.Lag;
            if (deltaE < 0.0)                                           return PursuitMode.Lag;
            if (aspectDeg > 90f && rangeM > closeFireRangeM)            return PursuitMode.Lag;
            if (rangeM > 0.4f * visualRangeM && deltaE > 50.0)          return PursuitMode.Pure;
            return PursuitMode.Lead;
        }

        // 2026-05-17 playtest fix (Issue 1). Patrol/Search → Engage policy.
        // The legacy gate (range < visualRangeM && bearing < frontHemisphereDeg)
        // deadlocked Patrol once the target drifted past visual range — the AI
        // would fly straight indefinitely while the bandit escaped. Policy now:
        // a target reference is sufficient. UpdateStateTransitions routes back
        // to Patrol when target == null at its top-of-tick early-out.
        public static bool ShouldEnterEngageFromPatrol(bool targetExists)
        {
            return targetExists;
        }

        // 2026-05-17 playtest fix (Issue 2). Energy-bleed gate, mode-independent.
        // When the AI has an energy advantage above lagBleedMaxDeltaE, soften
        // the pitch/airspeed setpoint to reposition horizontally rather than
        // dump altitude. Pre-fix this gate lived only in the Lag branch, so
        // Lead and Pure could command ptc=-0.50 spd=47 with +498m of energy
        // already ahead of the target.
        public static (double pitch, double airspeed) ApplyEnergyBleedGate(
            double deltaE, float lagBleedMaxDeltaE)
        {
            bool allowEnergyBleed = deltaE < lagBleedMaxDeltaE;
            return (
                allowEnergyBleed ? -0.50 : -0.10,
                allowEnergyBleed ?  47.0 :  55.0
            );
        }

        // Compute the world-frame point the attacker should point its nose at,
        // given the pursuit mode. Lead extrapolates the target's track forward
        // by bullet time-of-flight (existing math). Lag extrapolates BACKWARD
        // by the same magnitude so the nose tracks behind the target. Pure
        // points directly at the target's current position. Per §6.3.
        public static Vector3 ComputePursuitPoint(
            PursuitMode mode,
            Vector3 targetPos, Vector3 targetVel,
            Vector3 firerPos, float muzzleVelocity)
        {
            switch (mode)
            {
                case PursuitMode.Pure:
                    return targetPos;
                case PursuitMode.Lag:
                {
                    float range = Vector3.Distance(targetPos, firerPos);
                    float bulletTime = range / Mathf.Max(1f, muzzleVelocity);
                    return targetPos - targetVel * bulletTime;
                }
                case PursuitMode.Lead:
                default:
                    return ComputeLeadPoint(targetPos, targetVel, firerPos, muzzleVelocity);
            }
        }
    }
}

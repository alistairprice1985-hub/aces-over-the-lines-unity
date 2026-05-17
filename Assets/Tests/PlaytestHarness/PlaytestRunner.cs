using System;
using System.Reflection;
using UnityEngine;
using AcesOverTheLines.AI;
using AcesOverTheLines.Flight;

namespace AcesOverTheLines.PlaytestHarness
{
    // Headless harness for deterministic AI scenarios. Given a PlaytestScenario,
    // Run spawns an AI aircraft and a kinematic ScriptedPlayer puppet, steps
    // physics by hand for the scenario's duration, records per-tick telemetry,
    // and evaluates the scenario's pass criterion against that telemetry.
    //
    // Determinism contract:
    //   * Physics.simulationMode = Script — no hidden physics ticks.
    //   * fixedDeltaTime is read ONCE at start.
    //   * AI clock is driven by an explicit `simulatedClock` counter injected
    //     via AIController.NowSecondsSource — never by editor wall-clock.
    //   * AircraftEntity._rng is reseeded from scenario.Seed.
    //   * No UnityEngine.Random anywhere in this file.
    //
    // The harness invokes private Unity lifecycle methods (Awake, FixedUpdate)
    // by reflection. This is deliberate: it keeps production code free of
    // `*ForTest` seams and contains the brittleness in this one file. If the
    // method names ever change in AircraftController/AIController, the
    // MethodInfo caches throw on first scenario run and every test fails
    // loudly rather than silently degrading to no-op.
    public static class PlaytestRunner
    {
        // Cached reflection handles. Resolved once on first call.
        static MethodInfo _aiAwake;
        static MethodInfo _aircraftAwake;
        static MethodInfo _aircraftFixedUpdate;
        static FieldInfo _aircraftInitialSpeed;
        static FieldInfo _aircraftEntityField;
        static FieldInfo _entityRngField;

        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static void EnsureReflectionCache()
        {
            if (_aiAwake != null) return;
            _aiAwake             = typeof(AIController).GetMethod("Awake", PrivateInstance);
            _aircraftAwake       = typeof(AircraftController).GetMethod("Awake", PrivateInstance);
            _aircraftFixedUpdate = typeof(AircraftController).GetMethod("FixedUpdate", PrivateInstance);
            _aircraftInitialSpeed = typeof(AircraftController).GetField("initialSpeedMs", PrivateInstance);
            _aircraftEntityField  = typeof(AircraftController).GetField("_entity", PrivateInstance);
            _entityRngField       = typeof(AircraftEntity).GetField("_rng", PrivateInstance);

            // Fail loud — a silent null here would cascade into a 130-tick no-op loop.
            if (_aiAwake == null) throw new InvalidOperationException("PlaytestRunner: AIController.Awake not found via reflection.");
            if (_aircraftAwake == null) throw new InvalidOperationException("PlaytestRunner: AircraftController.Awake not found via reflection.");
            if (_aircraftFixedUpdate == null) throw new InvalidOperationException("PlaytestRunner: AircraftController.FixedUpdate not found via reflection.");
            if (_aircraftInitialSpeed == null) throw new InvalidOperationException("PlaytestRunner: AircraftController.initialSpeedMs SerializeField not found.");
            if (_aircraftEntityField == null) throw new InvalidOperationException("PlaytestRunner: AircraftController._entity field not found.");
            if (_entityRngField == null) throw new InvalidOperationException("PlaytestRunner: AircraftEntity._rng field not found.");
        }

        public static PlaytestResult Run(PlaytestScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            EnsureReflectionCache();

            var result = new PlaytestResult { ScenarioName = scenario.Name };

            // ---- Capture Debug.Log into the result so [AI-CMD] / [AI] /
            // [AI-GATE] lines emitted during the scenario are persisted. ----
            void LogHandler(string condition, string stackTrace, LogType type) {
                result.CapturedLogs.Add(condition);
            }
            Application.logMessageReceived += LogHandler;

            // ---- Switch to scripted physics simulation. ----
            var previousSimMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            GameObject aiGo = null;
            GameObject playerGo = null;

            try
            {
                float fixedDt = UnityEngine.Time.fixedDeltaTime;  // read ONCE
                int totalTicks = Mathf.Max(1, Mathf.RoundToInt(scenario.DurationSeconds / fixedDt));

                // ---- Spawn scripted player first so we can wire it as the
                // AI's target before invoking AIController.Awake (skipping
                // its FindWithTag("Player") branch). ScriptedPlayer is a
                // plain class (not a MonoBehaviour) because the harness
                // assembly is Editor-only and Unity refuses to attach
                // Editor-only MonoBehaviours to GameObjects. ----
                playerGo = new GameObject("PlaytestHarness_ScriptedPlayer");
                var scriptedPlayer = new ScriptedPlayer(playerGo, scenario.PlayerTrajectory);

                // ---- Spawn AI rig. Set transform pose BEFORE adding
                // AircraftController so its Awake honours the scenario start
                // position/heading. Set initialSpeedMs via reflection so the
                // entity is built with the scenario's airspeed. ----
                aiGo = new GameObject("PlaytestHarness_AI");
                aiGo.transform.position = scenario.AiStart;
                aiGo.transform.rotation = Quaternion.AngleAxis(scenario.AiHeadingDeg, Vector3.up);
                aiGo.AddComponent<Rigidbody>();
                var aircraftCtrl = aiGo.AddComponent<AircraftController>();
                var ai = aiGo.AddComponent<AIController>();

                _aircraftInitialSpeed.SetValue(aircraftCtrl, scenario.AiSpeedMs);

                // AircraftController.Awake builds AircraftEntity from transform
                // pose + initialSpeedMs and sets rb.position/rotation/velocity.
                _aircraftAwake.Invoke(aircraftCtrl, null);

                // Reseed AircraftEntity._rng so spin/pilot-out/fuel-fire
                // branches (if they fire) produce identical torques across
                // runs of the same scenario. See determinism contract above.
                var entity = _aircraftEntityField.GetValue(aircraftCtrl);
                if (entity != null)
                {
                    _entityRngField.SetValue(entity, new System.Random(scenario.Seed));
                }

                // Wire target BEFORE AIController.Awake so its
                // FindWithTag("Player") branch is short-circuited.
                ai.Target = scriptedPlayer.Transform;

                // ---- Clock injection. The override invariant — every AI in
                // the harness reads simulated time, never wall-clock — is
                // checked immediately so a future refactor that makes
                // NowSecondsSource read-only fails fast. Without this check,
                // a busted setter would silently fall back to Time.time and
                // the harness would produce drift-by-real-time telemetry that
                // looks plausible until you ran it twice. ----
                float simulatedClock = 0f;
                ai.NowSecondsSource = () => simulatedClock;
                if (ai.NowSecondsSource() != simulatedClock)
                    throw new InvalidOperationException(
                        "PlaytestRunner: NowSecondsSource override did not take effect. " +
                        "Harness determinism is broken — the AI is reading some other clock. " +
                        "Check that AIController.NowSecondsSource is still public { get; set; }.");

                _aiAwake.Invoke(ai, null);

                // ---- Main simulation loop. ----
                var aiRb = aiGo.GetComponent<Rigidbody>();
                for (int tick = 0; tick < totalTicks; tick++)
                {
                    simulatedClock += fixedDt;
                    scriptedPlayer.Step(fixedDt);
                    _aircraftFixedUpdate.Invoke(aircraftCtrl, null);
                    Physics.Simulate(fixedDt);
                    result.Telemetry.Add(SampleTelemetry(simulatedClock, ai, aircraftCtrl, aiRb, scriptedPlayer));
                }

                // ---- Evaluate pass criterion. ----
                var (passed, reason) = scenario.PassCriterion(result);
                result.Passed = passed;
                result.FailureReason = reason;
            }
            finally
            {
                Application.logMessageReceived -= LogHandler;
                Physics.simulationMode = previousSimMode;
                if (aiGo != null) UnityEngine.Object.DestroyImmediate(aiGo);
                if (playerGo != null) UnityEngine.Object.DestroyImmediate(playerGo);
            }

            return result;
        }

        // ---- Telemetry sampling ----
        // Reads the AI's per-tick state via a mix of public properties and
        // reflection-cached private fields. The private reads (pursuit mode,
        // ΔE) are observational only — the harness never writes them.
        static FieldInfo _diagPursuitMode;
        static FieldInfo _diagDeltaE;

        static TelemetryRow SampleTelemetry(
            float simTime, AIController ai, AircraftController ac, Rigidbody aiRb, ScriptedPlayer player)
        {
            if (_diagPursuitMode == null)
            {
                _diagPursuitMode = typeof(AIController).GetField("_diagPursuitMode", PrivateInstance);
                _diagDeltaE      = typeof(AIController).GetField("_diagDeltaE", PrivateInstance);
            }

            Vector3 aiPos = aiRb != null ? aiRb.position : Vector3.zero;
            float aiSpd = aiRb != null ? aiRb.linearVelocity.magnitude : 0f;
            float aiVy = aiRb != null ? aiRb.linearVelocity.y : 0f;

            Vector3 playerPos = player.Transform.position;
            float range = Vector3.Distance(aiPos, playerPos);

            var controls = ac.LastControls;

            // Compute ΔE from telemetry (matches AIController's formula) so
            // the row is meaningful even when AI is not in Engage state and
            // _diagDeltaE has not been refreshed.
            double playerSpd = player.AirspeedMs;
            double aiEnergy = aiPos.y + (aiSpd * aiSpd) / (2.0 * 9.81);
            double playerEnergy = playerPos.y + (playerSpd * playerSpd) / (2.0 * 9.81);
            double deltaE = aiEnergy - playerEnergy;

            var mode = (AIController.PursuitMode)(_diagPursuitMode != null
                ? _diagPursuitMode.GetValue(ai)
                : AIController.PursuitMode.Lead);

            return new TelemetryRow(
                simTime: simTime,
                aiPos: aiPos,
                aiAlt: aiPos.y,
                aiSpd: aiSpd,
                aiVy: aiVy,
                state: ai.CurrentState,
                bankCmd: controls.Aileron,
                pitchCmd: controls.Elevator,
                throttleCmd: controls.Throttle,
                fire: controls.Fire,
                playerPos: playerPos,
                range: range,
                deltaE: deltaE,
                mode: mode);
        }
    }
}

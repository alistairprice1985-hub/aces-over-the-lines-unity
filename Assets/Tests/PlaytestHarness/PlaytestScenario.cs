using System;
using UnityEngine;

namespace AcesOverTheLines.PlaytestHarness
{
    // A single playtest scenario: initial conditions for both aircraft, the
    // scripted player's trajectory, simulation duration, and a pass criterion
    // delegate that examines the resulting telemetry.
    //
    // Designed for inspectability: every field is read directly off the scenario
    // and reported on failure. No hidden state, no scenario "setup" methods —
    // a scenario is data + a predicate.
    public sealed class PlaytestScenario
    {
        public string Name { get; }

        // AI initial conditions.
        public Vector3 AiStart { get; }
        public float AiHeadingDeg { get; }
        public float AiSpeedMs { get; }

        // Player initial pose is implicit in the trajectory's Sample(0).
        public IPlayerTrajectory PlayerTrajectory { get; }

        public float DurationSeconds { get; }

        // Deterministic seed for AircraftEntity._rng (spin / pilot-out /
        // fuel-fire torques). Same seed = same physics outcome under
        // identical inputs. Default 42 is arbitrary but pinned.
        public int Seed { get; }

        // Pass criterion. Returns (passed, reason). Inspect telemetry
        // however the scenario likes.
        public Func<PlaytestResult, (bool passed, string reason)> PassCriterion { get; }

        public PlaytestScenario(
            string name,
            Vector3 aiStart, float aiHeadingDeg, float aiSpeedMs,
            IPlayerTrajectory playerTrajectory,
            float durationSeconds,
            Func<PlaytestResult, (bool, string)> passCriterion,
            int seed = 42)
        {
            Name = name;
            AiStart = aiStart;
            AiHeadingDeg = aiHeadingDeg;
            AiSpeedMs = aiSpeedMs;
            PlayerTrajectory = playerTrajectory;
            DurationSeconds = durationSeconds;
            PassCriterion = passCriterion;
            Seed = seed;
        }
    }
}

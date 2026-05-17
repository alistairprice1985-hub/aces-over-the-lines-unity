using System;
using UnityEngine;

namespace AcesOverTheLines.PlaytestHarness
{
    // Five canonical playtest scenarios. Each scenario is constructed from data
    // (initial conditions, trajectory, duration) plus a pass criterion delegate
    // that inspects the resulting telemetry. Scenarios are stateless and safe
    // to invoke repeatedly.
    //
    // Coordinate system: +Y up, +Z north (heading 000 → forward = +X, since
    // AircraftController.Awake treats body-forward as +X per the codebase
    // convention). Units: metres, m/s, seconds, degrees.
    public static class Scenarios
    {
        // Player trajectory: constant heading, constant altitude, constant speed.
        sealed class StraightLevelTrajectory : IPlayerTrajectory
        {
            readonly Vector3 _start; readonly Vector3 _vel; readonly Quaternion _rot; readonly float _spd;
            public StraightLevelTrajectory(Vector3 start, float headingDeg, float airspeedMs)
            {
                _start = start;
                _spd = airspeedMs;
                _rot = Quaternion.AngleAxis(headingDeg, Vector3.up);
                float h = headingDeg * Mathf.Deg2Rad;
                _vel = new Vector3(Mathf.Cos(h) * airspeedMs, 0f, -Mathf.Sin(h) * airspeedMs);
            }
            public PlayerTrajectoryState Sample(float t)
                => new PlayerTrajectoryState(_start + _vel * t, _rot, _vel, _spd);
        }

        // Constant heading, constant vy (climb or dive), constant airspeed-along-track.
        sealed class StraightVerticalTrajectory : IPlayerTrajectory
        {
            readonly Vector3 _start; readonly Vector3 _vel; readonly Quaternion _rot; readonly float _spd;
            public StraightVerticalTrajectory(Vector3 start, float headingDeg, float horizontalMs, float vyMs)
            {
                _start = start;
                float h = headingDeg * Mathf.Deg2Rad;
                _vel = new Vector3(Mathf.Cos(h) * horizontalMs, vyMs, -Mathf.Sin(h) * horizontalMs);
                _spd = _vel.magnitude;
                _rot = Quaternion.AngleAxis(headingDeg, Vector3.up);
            }
            public PlayerTrajectoryState Sample(float t)
                => new PlayerTrajectoryState(_start + _vel * t, _rot, _vel, _spd);
        }

        // Constant-altitude circular orbit at fixed radius, fixed speed.
        // Direction +1 = counter-clockwise (left turn from the orbiter's POV
        // when viewed from above). Origin is the orbit centre.
        sealed class LevelOrbitTrajectory : IPlayerTrajectory
        {
            readonly Vector3 _centre; readonly float _radius; readonly float _spd; readonly float _omega; readonly float _phase0;
            public LevelOrbitTrajectory(Vector3 centre, float radius, float airspeedMs, float direction, float startPhaseDeg)
            {
                _centre = centre;
                _radius = radius;
                _spd = airspeedMs;
                _omega = (direction * airspeedMs) / radius;  // rad/s
                _phase0 = startPhaseDeg * Mathf.Deg2Rad;
            }
            public PlayerTrajectoryState Sample(float t)
            {
                float phase = _phase0 + _omega * t;
                float cos = Mathf.Cos(phase);
                float sin = Mathf.Sin(phase);
                Vector3 pos = _centre + new Vector3(_radius * cos, 0f, _radius * sin);
                // Velocity is the tangent: derivative of pos w.r.t. t.
                Vector3 vel = new Vector3(-_radius * sin * _omega, 0f, _radius * cos * _omega);
                // Rotation: face along velocity (heading-only, level wings).
                float headingDeg = Mathf.Atan2(vel.x, -vel.z) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.AngleAxis(headingDeg, Vector3.up);
                return new PlayerTrajectoryState(pos, rot, vel, _spd);
            }
        }

        // Alternating left/right hard turns. straightSeconds straight, then
        // hard left for turnSeconds, then hard right for turnSeconds, repeat.
        sealed class HardEvaderTrajectory : IPlayerTrajectory
        {
            readonly Vector3 _start; readonly float _spd; readonly float _straightS; readonly float _turnS; readonly float _turnRadius;
            public HardEvaderTrajectory(Vector3 start, float airspeedMs, float straightSeconds, float turnSeconds, float turnRadius)
            {
                _start = start;
                _spd = airspeedMs;
                _straightS = straightSeconds;
                _turnS = turnSeconds;
                _turnRadius = turnRadius;
            }
            public PlayerTrajectoryState Sample(float t)
            {
                // Integrate by hand: straight phase, left arc, right arc, repeat.
                Vector3 pos = _start;
                float headingRad = 0f; // initial heading 000 → forward = +X
                float remaining = t;
                float cycle = _straightS + 2f * _turnS;
                int wholeCycles = Mathf.FloorToInt(t / cycle);
                remaining -= wholeCycles * cycle;

                // First, advance through wholeCycles complete cycles. Over one
                // cycle, position advances by (straight phase displacement)
                // and net heading returns to start (left turn then equal-and-
                // opposite right turn cancel). So position += wholeCycles *
                // (straight-phase displacement) and heading unchanged.
                Vector3 forward0 = new Vector3(Mathf.Cos(headingRad), 0f, -Mathf.Sin(headingRad));
                pos += forward0 * (_spd * _straightS) * wholeCycles;

                Vector3 vel;
                Quaternion rot;
                // Now handle the partial cycle.
                if (remaining < _straightS)
                {
                    // Straight phase.
                    pos += forward0 * (_spd * remaining);
                    vel = forward0 * _spd;
                    rot = Quaternion.AngleAxis(headingRad * Mathf.Rad2Deg, Vector3.up);
                    return new PlayerTrajectoryState(pos, rot, vel, _spd);
                }
                remaining -= _straightS;
                pos += forward0 * (_spd * _straightS);

                if (remaining < _turnS)
                {
                    // Left arc. omega = v/r, sign = +1 (CCW viewed from +Y).
                    float omega = _spd / _turnRadius;
                    return ArcSample(pos, headingRad, omega, remaining, _spd);
                }
                remaining -= _turnS;
                // After full left arc, position and heading are at the arc end.
                var leftEnd = ArcSample(pos, headingRad, _spd / _turnRadius, _turnS, _spd);
                pos = leftEnd.Position;
                headingRad += (_spd / _turnRadius) * _turnS;

                // Right arc.
                float omegaR = -_spd / _turnRadius;
                return ArcSample(pos, headingRad, omegaR, remaining, _spd);
            }

            static PlayerTrajectoryState ArcSample(Vector3 startPos, float startHeadingRad, float omega, float t, float spd)
            {
                // Arc-integrated position from constant-radius turn.
                float headingRad = startHeadingRad + omega * t;
                float r = spd / Mathf.Abs(omega);
                // Centre of rotation is perpendicular-left (if omega > 0) of starting heading.
                Vector3 startForward = new Vector3(Mathf.Cos(startHeadingRad), 0f, -Mathf.Sin(startHeadingRad));
                Vector3 startLeft = new Vector3(-Mathf.Sin(startHeadingRad), 0f, -Mathf.Cos(startHeadingRad));
                Vector3 centre = startPos + Mathf.Sign(omega) * startLeft * r;
                // Current position is centre + radius * (rotated from start).
                float dPhase = omega * t;
                Vector3 fromCentreStart = startPos - centre;
                float cos = Mathf.Cos(dPhase);
                float sin = Mathf.Sin(dPhase);
                Vector3 fromCentre = new Vector3(
                    fromCentreStart.x * cos + fromCentreStart.z * sin,
                    0f,
                    -fromCentreStart.x * sin + fromCentreStart.z * cos);
                Vector3 pos = centre + fromCentre;
                Vector3 vel = new Vector3(Mathf.Cos(headingRad) * spd, 0f, -Mathf.Sin(headingRad) * spd);
                Quaternion rot = Quaternion.AngleAxis(headingRad * Mathf.Rad2Deg, Vector3.up);
                return new PlayerTrajectoryState(pos, rot, vel, spd);
            }
        }

        // -------------------------------------------------------------------
        // Pass-criterion helpers. Each scenario builds its predicate from
        // these so the criterion is inspectable when a test fails.
        // -------------------------------------------------------------------

        // Achieved firing-solution: at any tick the AI deflection was within
        // its entry cone AND range under maxFireRangeM AND fire was true.
        // Sustained for `sustainSeconds` means consecutive fire=true ticks.
        static (bool passed, string reason) HasSustainedFiringSolution(
            PlaytestResult r, float maxRange, float sustainSeconds, float fixedDt)
        {
            int requiredTicks = Mathf.Max(1, Mathf.RoundToInt(sustainSeconds / fixedDt));
            int streak = 0; float streakStart = 0f;
            foreach (var row in r.Telemetry)
            {
                if (row.Fire && row.Range < maxRange) { if (streak == 0) streakStart = row.SimTime; streak++; }
                else streak = 0;
                if (streak >= requiredTicks)
                    return (true, $"sustained fire={requiredTicks}+ ticks starting t={streakStart:F2}s");
            }
            float minRange = float.PositiveInfinity;
            foreach (var row in r.Telemetry) if (row.Range < minRange) minRange = row.Range;
            return (false, $"no sustained firing solution; min range over run = {minRange:F0}m, max range = {MaxRange(r):F0}m");
        }

        static float MinRange(PlaytestResult r) { float m = float.PositiveInfinity; foreach (var row in r.Telemetry) if (row.Range < m) m = row.Range; return m; }
        static float MaxRange(PlaytestResult r) { float m = 0f; foreach (var row in r.Telemetry) if (row.Range > m) m = row.Range; return m; }
        static float MaxAltitude(PlaytestResult r) { float m = 0f; foreach (var row in r.Telemetry) if (row.AiAltitude > m) m = row.AiAltitude; return m; }
        static float MinAltitude(PlaytestResult r) { float m = float.PositiveInfinity; foreach (var row in r.Telemetry) if (row.AiAltitude < m) m = row.AiAltitude; return m; }

        // 30-second window range-trend: returns the largest range increase
        // observed over any 30s sub-window. Positive = AI bled distance; we
        // want this to be <= 0 (non-increasing) for the climber scenario.
        static float MaxThirtySecondRangeIncrease(PlaytestResult r, float windowSeconds, float fixedDt)
        {
            int win = Mathf.Max(1, Mathf.RoundToInt(windowSeconds / fixedDt));
            float worst = float.NegativeInfinity;
            for (int i = 0; i + win < r.Telemetry.Count; i++)
            {
                float delta = r.Telemetry[i + win].Range - r.Telemetry[i].Range;
                if (delta > worst) worst = delta;
            }
            return worst;
        }

        // -------------------------------------------------------------------
        // The five canonical scenarios.
        // -------------------------------------------------------------------

        public static PlaytestScenario StraightLevel_Decoy => new PlaytestScenario(
            name: "StraightLevel_Decoy",
            aiStart: new Vector3(0f, 1000f, 0f),
            aiHeadingDeg: 0f,
            aiSpeedMs: 50f,
            playerTrajectory: new StraightLevelTrajectory(
                start: new Vector3(0f, 1000f, 700f),
                headingDeg: 0f,
                airspeedMs: 35f),
            durationSeconds: 90f,
            passCriterion: r => {
                // AI must achieve range < 250m AND off-axis < 5° AND sustain
                // 0.5s. Off-axis < 5° within the entry cone equates to the
                // AI's burst-fire latch firing. So: range < 250 AND fire = true
                // for >= 0.5s consecutive.
                float fixedDt = 1f / 120f;
                int requiredTicks = Mathf.RoundToInt(0.5f / fixedDt);
                int streak = 0; float streakStart = 0f;
                foreach (var row in r.Telemetry)
                {
                    if (row.Fire && row.Range < 250f) { if (streak == 0) streakStart = row.SimTime; streak++; }
                    else streak = 0;
                    if (streak >= requiredTicks)
                        return (true, $"firing solution sustained {requiredTicks * fixedDt:F2}s starting t={streakStart:F2}s");
                }
                return (false, $"no sustained firing solution. min range = {MinRange(r):F0}m, max range = {MaxRange(r):F0}m");
            });

        public static PlaytestScenario StraightClimber => new PlaytestScenario(
            name: "StraightClimber",
            aiStart: new Vector3(0f, 1000f, 0f),
            aiHeadingDeg: 0f,
            aiSpeedMs: 50f,
            playerTrajectory: new StraightVerticalTrajectory(
                start: new Vector3(0f, 1000f, 700f),
                headingDeg: 0f,
                horizontalMs: 31f,
                vyMs: 5f),
            durationSeconds: 120f,
            passCriterion: r => {
                // Hard pass: range < 400m at any point.
                if (MinRange(r) < 400f) return (true, $"hard pass: min range = {MinRange(r):F0}m");
                // Soft pass: range monotonically non-increasing on average over any 30s window.
                float fixedDt = 1f / 120f;
                float worst = MaxThirtySecondRangeIncrease(r, 30f, fixedDt);
                if (worst <= 0f) return (true, $"soft pass: max 30s range increase = {worst:F0}m (closing on average)");
                return (false, $"AI bleeding distance: max 30s range increase = {worst:F0}m; min range = {MinRange(r):F0}m, final range = {r.Telemetry[r.Telemetry.Count - 1].Range:F0}m");
            });

        public static PlaytestScenario LevelOrbit => new PlaytestScenario(
            name: "LevelOrbit",
            aiStart: new Vector3(0f, 1000f, 0f),
            aiHeadingDeg: 0f,
            aiSpeedMs: 50f,
            // Orbit centre at (100, 1000, 0); radius 400; left orbit (CCW from +Y); start phase puts player at +Z side initially, so (500, 1000, 0) ≈ centre + (400,0,0)... actually for the spec we need start at (500,1000,0). Centre at (100,1000,0), radius 400, start phase 0 (cos=1, sin=0) → (100+400, 1000, 0) = (500, 1000, 0). ✓
            playerTrajectory: new LevelOrbitTrajectory(
                centre: new Vector3(100f, 1000f, 0f),
                radius: 400f,
                airspeedMs: 40f,
                direction: +1f,
                startPhaseDeg: 0f),
            durationSeconds: 120f,
            passCriterion: r => {
                // Pass A: range < 300m sustained 5s.
                float fixedDt = 1f / 120f;
                int requiredTicks = Mathf.RoundToInt(5f / fixedDt);
                int streak = 0; float streakStart = 0f;
                foreach (var row in r.Telemetry)
                {
                    if (row.Range < 300f) { if (streak == 0) streakStart = row.SimTime; streak++; }
                    else streak = 0;
                    if (streak >= requiredTicks)
                        return (true, $"close-range sustain: range < 300m for {streak * fixedDt:F2}s starting t={streakStart:F2}s");
                }
                // Pass B: any firing solution at any point.
                foreach (var row in r.Telemetry)
                    if (row.Fire) return (true, $"firing solution achieved at t={row.SimTime:F2}s, range={row.Range:F0}m");
                return (false, $"no firing solution and no close-range sustain. min range = {MinRange(r):F0}m");
            });

        public static PlaytestScenario DivingExtender => new PlaytestScenario(
            name: "DivingExtender",
            aiStart: new Vector3(0f, 1000f, 0f),
            aiHeadingDeg: 0f,
            aiSpeedMs: 50f,
            playerTrajectory: new StraightVerticalTrajectory(
                start: new Vector3(0f, 1500f, 700f),
                headingDeg: 0f,
                horizontalMs: 55f,
                vyMs: -10f),
            durationSeconds: 60f,
            passCriterion: r => {
                // AI must lose no more than 200m of altitude AND end with range < 800m.
                float aiInitialAlt = r.Telemetry[0].AiAltitude;
                float aiMinAlt = MinAltitude(r);
                float altLoss = aiInitialAlt - aiMinAlt;
                float finalRange = r.Telemetry[r.Telemetry.Count - 1].Range;
                if (altLoss > 200f)
                    return (false, $"AI cratered: lost {altLoss:F0}m altitude (limit 200m); final range = {finalRange:F0}m");
                if (finalRange >= 800f)
                    return (false, $"AI extended out: final range = {finalRange:F0}m (limit 800m); alt loss = {altLoss:F0}m");
                return (true, $"alt loss = {altLoss:F0}m, final range = {finalRange:F0}m");
            });

        public static PlaytestScenario HardEvader => new PlaytestScenario(
            name: "HardEvader",
            aiStart: new Vector3(0f, 1000f, 0f),
            aiHeadingDeg: 0f,
            aiSpeedMs: 50f,
            playerTrajectory: new HardEvaderTrajectory(
                start: new Vector3(0f, 1000f, 600f),
                airspeedMs: 40f,
                straightSeconds: 10f,
                turnSeconds: 8f,
                turnRadius: 150f),  // tight ~40 m/s × 8 s arc ≈ 320m perimeter at r=150m
            durationSeconds: 90f,
            passCriterion: r => {
                // AI stays within 1000m of player for the ENTIRE duration AND
                // achieves at least one firing solution.
                bool everBeyond1000 = false; float maxRangeSeen = 0f; float maxRangeT = 0f;
                foreach (var row in r.Telemetry)
                {
                    if (row.Range > 1000f) { everBeyond1000 = true; if (row.Range > maxRangeSeen) { maxRangeSeen = row.Range; maxRangeT = row.SimTime; } }
                }
                if (everBeyond1000)
                    return (false, $"AI lost contact: range exceeded 1000m (peak = {maxRangeSeen:F0}m at t={maxRangeT:F2}s)");
                bool fired = false; float firstFireT = 0f;
                foreach (var row in r.Telemetry)
                    if (row.Fire) { fired = true; firstFireT = row.SimTime; break; }
                if (!fired)
                    return (false, $"AI stayed within 1000m but never achieved firing solution. min range = {MinRange(r):F0}m");
                return (true, $"in-range throughout AND first firing solution at t={firstFireT:F2}s; min range = {MinRange(r):F0}m");
            });

        // Convenience: all five in canonical order for batch iteration.
        public static PlaytestScenario[] All => new[]
        {
            StraightLevel_Decoy, StraightClimber, LevelOrbit, DivingExtender, HardEvader
        };
    }
}

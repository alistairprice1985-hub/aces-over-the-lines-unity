using UnityEngine;

namespace AcesOverTheLines.PlaytestHarness
{
    // A scripted trajectory that the harness samples per fixed-step to drive
    // ScriptedPlayer. Trajectories are pure functions of simulated time so
    // the same scenario at the same tick produces identical state across
    // runs — that's the determinism contract of the harness.
    public interface IPlayerTrajectory
    {
        PlayerTrajectoryState Sample(float simulatedTime);
    }

    public readonly struct PlayerTrajectoryState
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Velocity;
        public readonly float AirspeedMs;

        public PlayerTrajectoryState(Vector3 position, Quaternion rotation, Vector3 velocity, float airspeedMs)
        {
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            AirspeedMs = airspeedMs;
        }
    }
}

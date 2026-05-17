using UnityEngine;

namespace AcesOverTheLines.PlaytestHarness
{
    // Kinematic puppet aircraft used as the AI's target in playtest scenarios.
    // Owns a GameObject (Transform + kinematic Rigidbody) and drives it from a
    // pre-computed trajectory, never from physics simulation. The AI reads
    // target.position, target.forward, and target.GetComponent<Rigidbody>().
    // linearVelocity — all three are sourced from the trajectory each step.
    //
    // NOT a MonoBehaviour. The harness lives in an Editor-only assembly and
    // Unity refuses to attach Editor-only MonoBehaviours to GameObjects. A
    // plain class that drives an external Transform/Rigidbody achieves the
    // same outcome without that restriction.
    //
    // Lifecycle is owned by PlaytestRunner. There is no Update/FixedUpdate
    // on purpose: the harness drives Step(dt) explicitly so simulated time
    // and wall-clock time are decoupled.
    public sealed class ScriptedPlayer
    {
        public IPlayerTrajectory Trajectory { get; private set; }
        public float SimulatedTime { get; private set; }
        public float AirspeedMs { get; private set; }
        public GameObject GameObject { get; }
        public Transform Transform => GameObject.transform;

        readonly Rigidbody _rb;

        public ScriptedPlayer(GameObject host, IPlayerTrajectory trajectory)
        {
            GameObject = host;
            _rb = host.GetComponent<Rigidbody>();
            if (_rb == null) _rb = host.AddComponent<Rigidbody>();
            // NOT kinematic: Unity 6 rejects linearVelocity writes on
            // kinematic bodies, and the AI reads .linearVelocity off the
            // target Rigidbody for lead-pursuit math. Non-kinematic with
            // gravity off and no collider gives us a free-floating body
            // whose velocity we can set; Physics.Simulate then integrates
            // position += velocity * dt, exactly matching trajectory
            // (acceleration is zero between our explicit writes).
            _rb.isKinematic = false;
            _rb.useGravity = false;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            Trajectory = trajectory;
            // Initialise to the trajectory's t=0 state so the AI sees correct
            // geometry on tick 0 before Step is called.
            var s = trajectory.Sample(0f);
            host.transform.position = s.Position;
            host.transform.rotation = s.Rotation;
            AirspeedMs = s.AirspeedMs;
            _rb.position = s.Position;
            _rb.rotation = s.Rotation;
            _rb.linearVelocity = s.Velocity;
        }

        // Called explicitly by PlaytestRunner each fixed step. Reads the
        // trajectory at the current simulated time, then writes position,
        // rotation, and Rigidbody velocity.
        public void Step(float dt)
        {
            SimulatedTime += dt;
            var state = Trajectory.Sample(SimulatedTime);
            GameObject.transform.position = state.Position;
            GameObject.transform.rotation = state.Rotation;
            AirspeedMs = state.AirspeedMs;
            _rb.position = state.Position;
            _rb.rotation = state.Rotation;
            _rb.linearVelocity = state.Velocity;
        }
    }
}

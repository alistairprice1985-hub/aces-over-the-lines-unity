using UnityEngine;
using AcesOverTheLines.Flight;

namespace AcesOverTheLines.AI
{
    // Inner-loop attitude/speed stabilizer. Three PIDs (roll, pitch,
    // throttle) drive the aircraft toward a FlightSetpoint.
    //
    // The single load-bearing design choice: the pitch PID output is
    // multiplied by cos(bank). When banked 90° pitch authority is zero,
    // so the only way to gain altitude is for the roll PID to bring the
    // wings level first. This is the mathematical fix that makes
    // graveyard spirals impossible — the architecture cannot command
    // "pull elevator while inverted" and dig itself deeper.
    //
    // Rate limits on each output channel prevent the "slam to the stops"
    // pathology that destabilised the FSM in rounds 4a–4h.
    public class FlightStabilizer
    {
        public class Pid
        {
            public double Kp, Ki, Kd;
            double _integral, _prevError;

            public Pid(double kp, double ki, double kd) { Kp = kp; Ki = ki; Kd = kd; }

            public double Update(double error, double dt)
            {
                _integral += error * dt;
                double safeDt = dt > 1e-4 ? dt : 1e-4;
                double deriv = (error - _prevError) / safeDt;
                _prevError = error;
                return Kp * error + Ki * _integral + Kd * deriv;
            }

            public void Reset() { _integral = 0; _prevError = 0; }
        }

        public Pid RollPid     { get; private set; }
        public Pid PitchPid    { get; private set; }
        public Pid ThrottlePid { get; private set; }

        // Per-second deflection rate caps. Aileron is fastest because
        // wings-level recovery is the safety-critical path; throttle is
        // slowest because engine response is the slow physical variable.
        public double MaxAileronRate  = 3.0;
        public double MaxElevatorRate = 2.0;
        public double MaxThrottleRate = 3.0;

        double _lastAileron, _lastElevator, _lastThrottle;

        public FlightStabilizer()
        {
            // Starting gains (Round 5 Commit 1). Tuning happens in Commit 4.
            RollPid     = new Pid(2.0, 0.0, 0.4);
            PitchPid    = new Pid(1.5, 0.0, 0.3);
            ThrottlePid = new Pid(0.05, 0.01, 0.0);
        }

        public void Reset()
        {
            RollPid.Reset();
            PitchPid.Reset();
            ThrottlePid.Reset();
            _lastAileron = _lastElevator = _lastThrottle = 0;
        }

        // Extracts (bank, pitch) Tait-Bryan angles from a body-to-world
        // quaternion in this codebase's body convention (+x forward, +y
        // up, +z right). pitch is the angle of body +x above the world
        // horizontal plane; bank is the rotation about that forward axis
        // needed to align body +y with world +y.
        //
        //   forwardInWorld = R * (1, 0, 0)   →   pitch = asin(forward.y)
        //   upInWorld      = R * (0, 1, 0)
        //   rightInWorld   = R * (0, 0, 1)   →   bank  = atan2(-right.y, up.y)
        //
        // pitch ∈ [-π/2, π/2]; bank ∈ (-π, π], +ve = right wing down.
        public static (double bank, double pitch) ExtractAttitude(Quaternion bodyToWorld)
        {
            Vector3 forwardInWorld = bodyToWorld * new Vector3(1f, 0f, 0f);
            Vector3 upInWorld      = bodyToWorld * new Vector3(0f, 1f, 0f);
            Vector3 rightInWorld   = bodyToWorld * new Vector3(0f, 0f, 1f);
            double pitch = Mathf.Asin(Mathf.Clamp(forwardInWorld.y, -1f, 1f));
            double bank  = Mathf.Atan2(-rightInWorld.y, upInWorld.y);
            return (bank, pitch);
        }

        // Pure-math stabilizer entry point. Takes attitude and airspeed
        // directly rather than a Rigidbody so it can be driven from an
        // EditMode test harness with a kinematic flight model.
        public ControlInput Stabilize(FlightSetpoint sp, double bank, double pitch, double speed, double dt)
        {
            double rollError = WrapToPi(sp.DesiredBankRad - bank);
            double pitchError = sp.DesiredPitchRad - pitch;
            double speedError = sp.DesiredAirspeedMs - speed;

            double rollCmd  = RollPid.Update(rollError, dt);
            double pitchRaw = PitchPid.Update(pitchError, dt);
            double thrRaw   = ThrottlePid.Update(speedError, dt);

            // cos(bank) attenuation — see class header. Below bank≈90°
            // pitch authority degrades to zero; the system relies on the
            // roll PID (unaffected by bank) to recover wings-level first.
            double pitchCmd = pitchRaw * Mathf.Cos((float)bank);

            // Pre-clamp the PID outputs before rate-limiting so we don't
            // store excursions outside the actuator range.
            rollCmd   = Mathf.Clamp((float)rollCmd,   -1f, 1f);
            pitchCmd  = Mathf.Clamp((float)pitchCmd,  -1f, 1f);
            double thrCmd = Mathf.Clamp01((float)thrRaw);

            // Rate-limit relative to the previous commanded value.
            rollCmd  = RateLimit(_lastAileron,  rollCmd,  MaxAileronRate  * dt);
            pitchCmd = RateLimit(_lastElevator, pitchCmd, MaxElevatorRate * dt);
            thrCmd   = RateLimit(_lastThrottle, thrCmd,   MaxThrottleRate * dt);

            _lastAileron  = rollCmd;
            _lastElevator = pitchCmd;
            _lastThrottle = thrCmd;

            return new ControlInput
            {
                Aileron  = rollCmd,
                Elevator = pitchCmd,
                Rudder   = 0.0,
                Throttle = thrCmd,
                Fire     = sp.Fire,
            };
        }

        // Convenience overload that reads attitude and speed from a
        // Rigidbody. Used in the live AI integration in Commit 2.
        public ControlInput Stabilize(FlightSetpoint sp, Rigidbody rb, double dt)
        {
            var (bank, pitch) = ExtractAttitude(rb.rotation);
            double speed = rb.linearVelocity.magnitude;
            return Stabilize(sp, bank, pitch, speed, dt);
        }

        static double RateLimit(double prev, double target, double maxStep)
        {
            double delta = target - prev;
            if (delta >  maxStep) return prev + maxStep;
            if (delta < -maxStep) return prev - maxStep;
            return target;
        }

        static double WrapToPi(double a)
        {
            while (a >  Mathf.PI) a -= 2.0 * Mathf.PI;
            while (a < -Mathf.PI) a += 2.0 * Mathf.PI;
            return a;
        }
    }
}

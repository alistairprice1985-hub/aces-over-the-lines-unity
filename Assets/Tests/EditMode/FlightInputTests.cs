using NUnit.Framework;
using AcesOverTheLines.Input;

namespace AcesOverTheLines.Input.Tests
{
    public class FlightInputTests
    {
        // Ported verbatim from src/flight/atmosphere.test.js — "control smoothing (rampTo)"
        // describe block. The four assertions characterise the 250 ms linear
        // ramp used by the input smoother.

        [Test]
        public void LinearRampReachesNinetyPercentBetween200And300ms()
        {
            double v = 0;
            const double dt = 1.0 / 120.0;
            double t = 0;
            double? crossedAt = null;
            while (t <= 0.5)
            {
                v = FlightInput.RampTo(v, 1.0, dt);
                t += dt;
                if (!crossedAt.HasValue && v >= 0.9) crossedAt = t;
            }
            Assert.IsTrue(crossedAt.HasValue, "ramp never reached 0.9");
            Assert.GreaterOrEqual(crossedAt.Value, 0.2);
            Assert.LessOrEqual(crossedAt.Value, 0.3);
        }

        [Test]
        public void LinearRampReachesTargetAtRampTimeWithinOneStep()
        {
            double v = 0;
            const double dt = 1.0 / 120.0;
            double t = 0;
            while (t <= FlightInput.RAMP_TIME_S + 2.0 * dt)
            {
                v = FlightInput.RampTo(v, 1.0, dt);
                t += dt;
            }
            Assert.AreEqual(1.0, v, 5e-7);
        }

        [Test]
        public void RampIsSymmetricForNegativeTargets()
        {
            double v = 0;
            const double dt = 1.0 / 120.0;
            // After 30 steps × 1/120 s = 0.25 s exactly, should reach -1.
            for (int i = 0; i < 30; i++) v = FlightInput.RampTo(v, -1.0, dt);
            Assert.AreEqual(-1.0, v, 5e-7);
        }

        [Test]
        public void RampDoesNotOvershootWhenDeltaIsSmallerThanSingleStepMax()
        {
            // Single-step max at dt=1/120 is RAMP_RATE/120 = (1/0.25)/120 ≈ 0.0333.
            double v = FlightInput.RampTo(0.99, 1.0, 1.0 / 120.0);
            Assert.AreEqual(1.0, v);
            double v2 = FlightInput.RampTo(-0.99, -1.0, 1.0 / 120.0);
            Assert.AreEqual(-1.0, v2);
        }
    }
}

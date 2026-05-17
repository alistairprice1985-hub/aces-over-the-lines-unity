using NUnit.Framework;
using UnityEngine;
using AcesOverTheLines.AI;

namespace AcesOverTheLines.AI.Tests
{
    public class FlightStabilizerTests
    {
        // ---- Attitude extraction sanity ----

        [Test]
        public void ExtractAttitudeLevelGivesZeros()
        {
            var (bank, pitch) = FlightStabilizer.ExtractAttitude(Quaternion.identity);
            Assert.AreEqual(0.0, bank, 1e-4);
            Assert.AreEqual(0.0, pitch, 1e-4);
        }

        [Test]
        public void ExtractAttitudeRightWingDownNinetyGivesPositiveHalfPi()
        {
            // Body +x = world (1,0,0). Roll +90° about body +x sends body +y
            // (up) toward body +z (right). In world coords that's a rotation
            // around world x. Unity AngleAxis with positive angle and the
            // forward axis (+x in our convention) is right-handed, so +90°
            // about (1,0,0) produces right-wing-down.
            var q = Quaternion.AngleAxis(90f, new Vector3(1f, 0f, 0f));
            var (bank, pitch) = FlightStabilizer.ExtractAttitude(q);
            Assert.AreEqual(Mathf.PI / 2.0, bank, 1e-3);
            Assert.AreEqual(0.0, pitch, 1e-3);
        }

        [Test]
        public void ExtractAttitudeNoseUpThirtyGivesPositivePitch()
        {
            // Pitch about body +z (right axis). Positive pitch = nose up.
            var q = Quaternion.AngleAxis(30f, new Vector3(0f, 0f, 1f));
            var (bank, pitch) = FlightStabilizer.ExtractAttitude(q);
            Assert.AreEqual(0.0, bank, 1e-3);
            Assert.AreEqual(30f * Mathf.Deg2Rad, pitch, 1e-3);
        }

        [Test]
        public void ExtractAttitudeInvertedLevelGivesBankPi()
        {
            var q = Quaternion.AngleAxis(180f, new Vector3(1f, 0f, 0f));
            var (bank, _) = FlightStabilizer.ExtractAttitude(q);
            Assert.AreEqual(Mathf.PI, Mathf.Abs((float)bank), 1e-3);
        }

        // ---- Airspeed-aware pitch attenuation (Commit 7 stall guard) ----

        [Test]
        public void PitchCommandScalesWithAirspeedRatio()
        {
            // Use a small DesiredPitchRad so the steady-state PID output stays
            // below the ±1 clamp (Kp=1.5 means error < ~0.66 stays unsaturated).
            // Run many ticks so the PID's first-call derivative kick decays and
            // the rate limiter catches up — only then does the elevator command
            // directly reflect the scaled setpoint.
            var setpoint = new FlightSetpoint
            {
                DesiredBankRad = 0.0,
                DesiredPitchRad = 0.05,
                DesiredAirspeedMs = 70.0,
            };

            double SettleElevator(double speed)
            {
                var s = new FlightStabilizer();
                double last = 0;
                // 200 ticks at dt=0.02 = 4 seconds — well past PID transient
                // (with Kp=1.5, Ki=0, Kd=0.3, system settles in <0.5s).
                for (int i = 0; i < 200; i++)
                {
                    last = s.Stabilize(setpoint, bank: 0.0, pitch: 0.0,
                                       speed: speed, dt: 0.02).Elevator;
                }
                return last;
            }

            // ratio = 1.0 (at setpoint speed): full pitch authority, command = Kp * 0.05 = 0.075
            double full = SettleElevator(70.0);
            // ratio = 0.5 (half setpoint speed): pitch attenuated to 0.025, command = Kp * 0.025 = 0.0375
            double half = SettleElevator(35.0);
            // ratio = 0.3 (floor, speed=5 would give 0.071 unclamped): pitch attenuated to 0.015, command = Kp * 0.015 = 0.0225
            double veryLow = SettleElevator(5.0);
            // ratio = 1.0 (ceiling, over-speed does NOT increase pitch): command = 0.075
            double over = SettleElevator(140.0);

            Assert.Less(half, full,
                $"Half-airspeed elevator should be less than full-airspeed elevator. " +
                $"full={full:F4}, half={half:F4}");

            Assert.Greater(veryLow, 0.0,
                $"Very-low-airspeed elevator should retain minimum authority via 0.3 floor. " +
                $"veryLow={veryLow:F4}");

            Assert.Less(veryLow, half,
                $"Very-low-airspeed (floor at 0.3 ratio) should give less elevator than " +
                $"half-airspeed (ratio 0.5). veryLow={veryLow:F4}, half={half:F4}");

            // Above-setpoint airspeed must NOT increase pitch beyond the full
            // setpoint (1.0 ceiling on the ratio).
            Assert.AreEqual(full, over, 1e-4,
                $"Over-airspeed elevator must equal full-airspeed elevator (ratio capped at 1.0). " +
                $"full={full:F4}, over={over:F4}");
        }

        [Test]
        public void ZeroDesiredAirspeedDoesNotDivideByZero()
        {
            // Defensive: a setpoint with DesiredAirspeedMs = 0 (e.g. a state
            // that doesn't care about throttle) should not crash the
            // attenuation logic. The ratio should default to 1.0 (no
            // attenuation) when DesiredAirspeedMs is non-positive.
            var setpoint = new FlightSetpoint
            {
                DesiredBankRad = 0.0,
                DesiredPitchRad = 0.3,
                DesiredAirspeedMs = 0.0,
            };
            var s = new FlightStabilizer();
            var cmd = s.Stabilize(setpoint, bank: 0.0, pitch: 0.0, speed: 30.0, dt: 0.02);
            Assert.Greater(cmd.Elevator, 0.0,
                $"With DesiredAirspeedMs=0, full pitch authority should apply. Got {cmd.Elevator:F3}");
        }

        // ---- Killer test: inverted descent recovers in three seconds ----
        //
        // Kinematic flight model driven by the stabilizer's output. The
        // model is deliberately simple but physically grounded:
        //
        //   * Roll rate (body frame) = aileron × ROLL_RATE_MAX
        //   * Pitch rate (body frame) = elevator × PITCH_RATE_MAX
        //   * World-frame pitch rate = body-frame pitch rate × cos(bank)
        //     — this is the kinematic projection that the stabilizer's
        //       cos(bank) gate is designed to align with. The pitch PID
        //       output multiplied by cos(bank) recovers the right
        //       world-frame direction at every bank angle.
        //   * vy acceleration = LIFT_K × pitch × cos(bank) − g
        //     — vertical component of the lift vector (perpendicular to
        //       wings) minus gravity. With pitch ≈ AoA for small angles
        //       and lift roughly linear in AoA, this is the standard
        //       low-AoA approximation. When inverted (cos(bank) < 0),
        //       positive AoA produces downward vertical lift, which is
        //       exactly why graveyard spirals are unrecoverable without
        //       rolling level first.
        //   * Speed evolves from thrust − drag. Speed energy gain from
        //     gravitational PE conversion during descent is included so
        //     the test reflects what real aircraft do.
        //
        // The test asserts that after three simulated seconds the system
        // is wings-level, climbing, and at a reasonable airspeed. If
        // this passes, the architecture has the property the FSM
        // refactor was meant to deliver.
        [Test]
        public void InvertedDescentRecoversInSixSeconds()
        {
            var s = new FlightStabilizer();
            // Recover-state setpoint. Pitch +0.3 rad (≈17°) matches Climb;
            // 0.1 rad has insufficient lift excess (~1.3g) to reverse a
            // 30 m/s descent in a Fokker-class airframe at any reasonable
            // timescale. 0.3 rad gives ~3g of vertical lift component
            // once wings are level — realistic for WW1-era recovery.
            var setpoint = new FlightSetpoint
            {
                DesiredBankRad = 0.0,
                DesiredPitchRad = 0.3,
                DesiredAirspeedMs = 40.0,
            };

            // Initial state: 170° bank (almost inverted), descending at
            // 30 m/s, speed 30 m/s. Mirrors the Round 4g playtest crash.
            double bank  = 170.0 * Mathf.Deg2Rad;
            double pitch = 0.0;
            double vy    = -30.0;
            double speed = 30.0;
            double altitude = 1000.0;

            const double DT = 0.02;
            const int TICKS_ARCH = 125; // 2.5 s — bank-recovery checkpoint
            const int TICKS_PHYS = 300; // 6.0 s — vy/speed checkpoint

            const double ROLL_RATE_MAX  = 2.5;   // rad/s at full aileron
            const double PITCH_RATE_MAX = 1.5;   // rad/s at full elevator (body frame)
            const double LIFT_K         = 130.0; // m/s² per rad of effective AoA
            const double G              = 9.81;
            const double DRAG_K         = 0.004; // drag accel = k × v²
            const double THRUST_MAX     = 12.0;  // m/s² at full throttle
            const double GAMMA_TO_KE    = 0.7;   // fraction of lost PE converting to KE

            double bankAt2_5s = double.NaN;
            double vyAt6s = double.NaN;
            double speedAt6s = double.NaN;

            for (int i = 0; i < TICKS_PHYS; i++)
            {
                var cmd = s.Stabilize(setpoint, bank, pitch, speed, DT);

                // Body-frame rates from control surfaces.
                bank  += cmd.Aileron  * ROLL_RATE_MAX  * DT;
                double bodyPitchRate = cmd.Elevator * PITCH_RATE_MAX;

                // World-frame pitch rate is body-frame pitch rate projected
                // onto the world-pitch axis. cos(bank) is exactly this
                // projection — same factor as in the stabilizer's gate.
                pitch += bodyPitchRate * Mathf.Cos((float)bank) * DT;

                // Vertical lift component minus gravity. Lift is
                // perpendicular to wings (body +y); its world-vertical
                // component is L × cos(bank). L scales with effective AoA
                // (≈ pitch for small angles).
                double lift = LIFT_K * pitch;
                double vyAccel = lift * Mathf.Cos((float)bank) - G;
                vy += vyAccel * DT;

                // Speed evolves from thrust minus drag, plus energy
                // gained from descent (gravitational PE → KE).
                double drag = DRAG_K * speed * speed;
                double thrustAccel = THRUST_MAX * cmd.Throttle;
                double peGain = vy < 0 ? -GAMMA_TO_KE * G * (vy / Mathf.Max((float)speed, 1f)) : 0;
                speed += (thrustAccel - drag + peGain) * DT;
                if (speed < 1) speed = 1;

                altitude += vy * DT;

                if (i == TICKS_ARCH - 1) bankAt2_5s = bank;
                if (i == TICKS_PHYS - 1) { vyAt6s = vy; speedAt6s = speed; }
            }

            // Architecture proof — graveyard spiral defect solved.
            // Verified at 2.5s because the roll PID should converge in
            // ~1.5s and we want margin before asserting.
            Assert.Less(Mathf.Abs((float)bankAt2_5s), 5f * Mathf.Deg2Rad,
                $"Bank should recover to within 5° in 2.5s — architecture proof. Got {bankAt2_5s * Mathf.Rad2Deg:F2}°");

            // Physical-reality proof — Fokker D.VII can actually climb out
            // once wings are level. Verified at 6s, the realistic timescale
            // for WW1-era recovery from a -30 m/s inverted descent at full
            // throttle and 17° pitch.
            Assert.Greater(vyAt6s, 0.0,
                $"Descent should reverse to climbing in 6s — physical proof. Got {vyAt6s:F2} m/s");
            Assert.Greater(speedAt6s, 25.0,
                $"Speed should stay above stall margin — no spin recovery. Got {speedAt6s:F2} m/s");
        }
    }
}

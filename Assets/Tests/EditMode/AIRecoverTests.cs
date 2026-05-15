using NUnit.Framework;
using UnityEngine;
using AcesOverTheLines.AI;

namespace AcesOverTheLines.AI.Tests
{
    // Trigger / exit unit tests for the Recover state added in Round 5
    // Commit 3. Recover is the attitude-based hard interrupt that
    // replaces the altitude-floor and stall-recovery overrides deleted
    // in Commit 2.
    public class AIRecoverTests
    {
        const float BANK_TRIG  = 1.05f;   // ≈ 60°
        const float PITCH_TRIG = -0.785f; // ≈ -45°
        const float VY_TRIG    = 30f;     // vy < -30 enters
        const float ALT_TRIG   = 500f;    // and altitude < 500

        const float BANK_EXIT  = 0.26f;   // ≈ 15°
        const float VY_EXIT    = -5f;     // vy must be > -5
        const float ALT_EXIT   = 300f;

        [Test]
        public void EntersRecoverWhenBankExceedsSixtyDegrees()
        {
            // 70° bank — well over the 60° trigger; benign vy and altitude.
            bool enter = AIController.ShouldEnterRecover(
                attitude: (bank: 70f * Mathf.Deg2Rad, pitch: 0f),
                vy: 0f, altitudeAGL: 1000f,
                bankTriggerRad: BANK_TRIG, pitchTriggerRad: PITCH_TRIG,
                descentTriggerMs: VY_TRIG, descentTriggerAltM: ALT_TRIG);
            Assert.IsTrue(enter);
        }

        [Test]
        public void EntersRecoverWhenPitchBelowMinusFortyFive()
        {
            // -50° pitch — past the -45° trigger.
            bool enter = AIController.ShouldEnterRecover(
                attitude: (bank: 0f, pitch: -50f * Mathf.Deg2Rad),
                vy: 0f, altitudeAGL: 1000f,
                bankTriggerRad: BANK_TRIG, pitchTriggerRad: PITCH_TRIG,
                descentTriggerMs: VY_TRIG, descentTriggerAltM: ALT_TRIG);
            Assert.IsTrue(enter);
        }

        [Test]
        public void EntersRecoverOnHighDescentRateBelowAltitudeFloor()
        {
            // vy = -35 below 500m — both conditions met.
            bool enter = AIController.ShouldEnterRecover(
                attitude: (bank: 0f, pitch: 0f),
                vy: -35f, altitudeAGL: 400f,
                bankTriggerRad: BANK_TRIG, pitchTriggerRad: PITCH_TRIG,
                descentTriggerMs: VY_TRIG, descentTriggerAltM: ALT_TRIG);
            Assert.IsTrue(enter);
        }

        [Test]
        public void DoesNotEnterRecoverOnHighDescentRateAboveAltitudeFloor()
        {
            // vy = -35 but altitude 600 — descent condition needs BOTH.
            bool enter = AIController.ShouldEnterRecover(
                attitude: (bank: 0f, pitch: 0f),
                vy: -35f, altitudeAGL: 600f,
                bankTriggerRad: BANK_TRIG, pitchTriggerRad: PITCH_TRIG,
                descentTriggerMs: VY_TRIG, descentTriggerAltM: ALT_TRIG);
            Assert.IsFalse(enter);
        }

        [Test]
        public void ExitsRecoverWhenAllThreeExitConditionsMet()
        {
            // 10° bank, vy +5, alt 350 — all good.
            bool exit = AIController.ShouldExitRecover(
                bank: 10f * Mathf.Deg2Rad, vy: 5f, altitudeAGL: 350f,
                bankExitRad: BANK_EXIT, vyExitMs: VY_EXIT, exitAltitudeM: ALT_EXIT);
            Assert.IsTrue(exit);
        }

        [Test]
        public void DoesNotExitRecoverWhenStillBanked()
        {
            // 20° bank — past the 15° exit threshold.
            bool exit = AIController.ShouldExitRecover(
                bank: 20f * Mathf.Deg2Rad, vy: 5f, altitudeAGL: 350f,
                bankExitRad: BANK_EXIT, vyExitMs: VY_EXIT, exitAltitudeM: ALT_EXIT);
            Assert.IsFalse(exit);
        }

        [Test]
        public void DoesNotExitRecoverWhenStillDescendingFast()
        {
            // vy = -10 — past the -5 exit threshold.
            bool exit = AIController.ShouldExitRecover(
                bank: 10f * Mathf.Deg2Rad, vy: -10f, altitudeAGL: 350f,
                bankExitRad: BANK_EXIT, vyExitMs: VY_EXIT, exitAltitudeM: ALT_EXIT);
            Assert.IsFalse(exit);
        }

        [Test]
        public void DoesNotExitRecoverWhenStillBelowAltitudeFloor()
        {
            // alt 250 — below the 300 exit threshold.
            bool exit = AIController.ShouldExitRecover(
                bank: 10f * Mathf.Deg2Rad, vy: 5f, altitudeAGL: 250f,
                bankExitRad: BANK_EXIT, vyExitMs: VY_EXIT, exitAltitudeM: ALT_EXIT);
            Assert.IsFalse(exit);
        }
    }
}

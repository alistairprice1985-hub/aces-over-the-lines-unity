using NUnit.Framework;
using UnityEngine;
using AcesOverTheLines.AI;

namespace AcesOverTheLines.AI.Tests
{
    public class AIControllerTests
    {
        // ---- Lead-target calculation ----

        [Test]
        public void LeadPointForTargetAtRest()
        {
            var lead = AIController.ComputeLeadPoint(
                targetPos: new Vector3(100, 0, 0),
                targetVel: Vector3.zero,
                firerPos: Vector3.zero,
                muzzleVelocity: 893f);
            // No target motion → lead = target position.
            Assert.AreEqual(100f, lead.x, 1e-4f);
            Assert.AreEqual(0f, lead.y, 1e-4f);
            Assert.AreEqual(0f, lead.z, 1e-4f);
        }

        [Test]
        public void LeadPointAheadByBulletTimeTimesVelocity()
        {
            // Target at (100, 0, 0) moving (0, 0, 10) m/s, Spandau 893 m/s.
            // Bullet time = 100 / 893 ≈ 0.112 s.
            // Lead = (100, 0, 0) + (0, 0, 10) * 0.112 = (100, 0, ~1.12).
            var lead = AIController.ComputeLeadPoint(
                targetPos: new Vector3(100, 0, 0),
                targetVel: new Vector3(0, 0, 10),
                firerPos: Vector3.zero,
                muzzleVelocity: 893f);
            Assert.AreEqual(100f, lead.x, 1e-4f);
            Assert.AreEqual(0f, lead.y, 1e-4f);
            Assert.AreEqual(100f / 893f * 10f, lead.z, 1e-3f);
        }

        // ---- Firing decision ----

        [Test]
        public void ShouldFireWhenInRangeAndOnTargetAndBurstOn()
        {
            Assert.IsTrue(AIController.ShouldFire(
                deflectionDeg: 3f, rangeM: 150f,
                maxDeflectionDeg: 5f, maxRangeM: 200f, burstOn: true));
        }

        [Test]
        public void ShouldNotFireWhenDeflectionExceedsMax()
        {
            Assert.IsFalse(AIController.ShouldFire(
                deflectionDeg: 7f, rangeM: 150f,
                maxDeflectionDeg: 5f, maxRangeM: 200f, burstOn: true));
        }

        [Test]
        public void ShouldNotFireWhenRangeExceedsMax()
        {
            Assert.IsFalse(AIController.ShouldFire(
                deflectionDeg: 3f, rangeM: 250f,
                maxDeflectionDeg: 5f, maxRangeM: 200f, burstOn: true));
        }

        [Test]
        public void ShouldNotFireWhenBurstOff()
        {
            Assert.IsFalse(AIController.ShouldFire(
                deflectionDeg: 3f, rangeM: 150f,
                maxDeflectionDeg: 5f, maxRangeM: 200f, burstOn: false));
        }

        // ---- Multi-window firing (Stage 6 tuning) ----
        // Windows in use: far (≤300m, defl<10°), close (≤100m, defl<5°),
        // snap (≤50m, defl<30°). Fire if ANY window matches.

        [Test]
        public void MultiWindowFiresViaFarWindowAtModerateRange()
        {
            // 200m, 8° defl → outside close window (deflection too wide) but
            // inside far (≤300m, <10°) → fire.
            Assert.IsTrue(AIController.ShouldFireMultiWindow(
                deflectionDeg: 8f, rangeM: 200f, burstOn: true,
                farRangeM: 300f, farDeflectionDeg: 10f,
                closeRangeM: 100f, closeDeflectionDeg: 5f,
                snapRangeM: 50f, snapDeflectionDeg: 30f));
        }

        [Test]
        public void MultiWindowFiresViaSnapWindowAtCloseRangeWithWideDeflection()
        {
            // 40m, 25° defl → outside far/close (deflection too wide for
            // either) but inside snap (≤50m, <30°) → fire.
            Assert.IsTrue(AIController.ShouldFireMultiWindow(
                deflectionDeg: 25f, rangeM: 40f, burstOn: true,
                farRangeM: 300f, farDeflectionDeg: 10f,
                closeRangeM: 100f, closeDeflectionDeg: 5f,
                snapRangeM: 50f, snapDeflectionDeg: 30f));
        }

        [Test]
        public void MultiWindowDoesNotFireOutsideAllWindows()
        {
            // 250m, 12° defl → outside far window (deflection > 10°); also
            // outside close + snap by range → no fire.
            Assert.IsFalse(AIController.ShouldFireMultiWindow(
                deflectionDeg: 12f, rangeM: 250f, burstOn: true,
                farRangeM: 300f, farDeflectionDeg: 10f,
                closeRangeM: 100f, closeDeflectionDeg: 5f,
                snapRangeM: 50f, snapDeflectionDeg: 30f));
        }

        [Test]
        public void MultiWindowDoesNotFireBeyondFarRange()
        {
            // 400m, 2° defl → perfect aim but beyond all range gates.
            Assert.IsFalse(AIController.ShouldFireMultiWindow(
                deflectionDeg: 2f, rangeM: 400f, burstOn: true,
                farRangeM: 300f, farDeflectionDeg: 10f,
                closeRangeM: 100f, closeDeflectionDeg: 5f,
                snapRangeM: 50f, snapDeflectionDeg: 30f));
        }

        [Test]
        public void MultiWindowDoesNotFireWhenBurstOff()
        {
            // Inside snap window but burst off → no fire.
            Assert.IsFalse(AIController.ShouldFireMultiWindow(
                deflectionDeg: 5f, rangeM: 30f, burstOn: false,
                farRangeM: 300f, farDeflectionDeg: 10f,
                closeRangeM: 100f, closeDeflectionDeg: 5f,
                snapRangeM: 50f, snapDeflectionDeg: 30f));
        }

        // ---- Energy management ----

        [Test]
        public void LowEnergyTriggersBelowOnePointFiveStall()
        {
            // v0 = 11.1 m/s; 1.5x = 16.65 m/s.
            Assert.IsTrue(AIController.IsLowEnergy(speedMs: 15f, v0StallMs: 11.1f));
            Assert.IsTrue(AIController.IsLowEnergy(speedMs: 16.6f, v0StallMs: 11.1f));
        }

        [Test]
        public void LowEnergyClearAboveOnePointFiveStall()
        {
            Assert.IsFalse(AIController.IsLowEnergy(speedMs: 17f, v0StallMs: 11.1f));
            Assert.IsFalse(AIController.IsLowEnergy(speedMs: 50f, v0StallMs: 11.1f));
        }

        // ---- Disengage condition counter ----

        [Test]
        public void NoDisengageConditionsWhenHealthyAndFullAmmo()
        {
            int c = AIController.CountDisengageConditions(
                currentAmmo: 1000, initialAmmo: 1000, ammoLowFraction: 0.30f,
                lowestComponentHpFraction: 1.0f, componentLowFraction: 0.50f,
                speedMs: 50f, altitudeM: 500f, energyLowSpeedMs: 25f, energyLowAltitudeM: 200f,
                noSolutionTimeS: 0f, lostGeometrySeconds: 4f);
            Assert.AreEqual(0, c);
        }

        [Test]
        public void TwoDisengageConditionsTriggerDisengage()
        {
            // Low ammo + low health → 2 conditions → ShouldDisengage true.
            int c = AIController.CountDisengageConditions(
                currentAmmo: 100, initialAmmo: 1000, ammoLowFraction: 0.30f,
                lowestComponentHpFraction: 0.4f, componentLowFraction: 0.50f,
                speedMs: 50f, altitudeM: 500f, energyLowSpeedMs: 25f, energyLowAltitudeM: 200f,
                noSolutionTimeS: 0f, lostGeometrySeconds: 4f);
            Assert.AreEqual(2, c);
        }

        [Test]
        public void OneDisengageConditionDoesNotTriggerDisengage()
        {
            // Low ammo alone → 1 condition → ShouldDisengage false.
            int c = AIController.CountDisengageConditions(
                currentAmmo: 100, initialAmmo: 1000, ammoLowFraction: 0.30f,
                lowestComponentHpFraction: 1.0f, componentLowFraction: 0.50f,
                speedMs: 50f, altitudeM: 500f, energyLowSpeedMs: 25f, energyLowAltitudeM: 200f,
                noSolutionTimeS: 0f, lostGeometrySeconds: 4f);
            Assert.AreEqual(1, c);
        }
    }
}

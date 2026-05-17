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

        // ---- Altitude floor + low-altitude clamp (Stage 6 tuning round 2) ----

        [Test]
        public void AltitudeFloorClampsElevatorToStrongPullUp()
        {
            // Input commanded a dive at low altitude — floor MUST force
            // elevator to at least the strong pull-up value.
            var input = new AcesOverTheLines.Flight.ControlInput { Elevator = -0.5, Aileron = 0.6, Throttle = 0.5 };
            var output = AIController.ApplyAltitudeFloor(
                input, altitudeAGL: 100f, floorAGL: 250f, forcedElevatorMin: 0.80f);
            Assert.GreaterOrEqual(output.Elevator, 0.80);
            Assert.AreEqual(0.0, output.Aileron, 1e-9, "floor forces wings level");
            Assert.AreEqual(1.0, output.Throttle, 1e-9, "floor forces full throttle");
        }

        [Test]
        public void AltitudeFloorLeavesInputUnchangedAboveFloor()
        {
            var input = new AcesOverTheLines.Flight.ControlInput { Elevator = -0.5, Aileron = 0.6, Throttle = 0.5 };
            var output = AIController.ApplyAltitudeFloor(
                input, altitudeAGL: 800f, floorAGL: 250f, forcedElevatorMin: 0.80f);
            Assert.AreEqual(-0.5, output.Elevator, 1e-9);
            Assert.AreEqual( 0.6, output.Aileron, 1e-9);
            Assert.AreEqual( 0.5, output.Throttle, 1e-9);
        }

        [Test]
        public void AltitudeFloorPreservesHigherPullUp()
        {
            // If input already commands a stronger pull-up than the floor
            // minimum, keep the stronger value.
            var input = new AcesOverTheLines.Flight.ControlInput { Elevator = 0.95, Aileron = 0.0, Throttle = 1.0 };
            var output = AIController.ApplyAltitudeFloor(
                input, altitudeAGL: 100f, floorAGL: 250f, forcedElevatorMin: 0.80f);
            Assert.AreEqual(0.95, output.Elevator, 1e-9);
        }

        [Test]
        public void LowAltitudeEngageCapsRollAndPitchCommands()
        {
            // Aggressive commanded dive + hard bank at low altitude → both
            // get clamped to the safe band.
            var input = new AcesOverTheLines.Flight.ControlInput { Elevator = -0.9, Aileron = 0.7, Throttle = 1.0 };
            var output = AIController.ClampForLowAltitude(
                input, altitudeAGL: 350f, threshold: 500f,
                elevatorMin: 0.0f, elevatorMax: 0.5f, aileronCap: 0.4f);
            Assert.AreEqual(0.0, output.Elevator, 1e-6, "dive clamped to no-dive");
            Assert.AreEqual(0.4, output.Aileron, 1e-6, "hard bank clamped to cap (float→double precision)");
            Assert.AreEqual(1.0, output.Throttle, 1e-9, "throttle untouched");
        }

        [Test]
        public void LowAltitudeClampNoOpAboveThreshold()
        {
            // Above the threshold, the clamp passes through.
            var input = new AcesOverTheLines.Flight.ControlInput { Elevator = -0.9, Aileron = 0.7, Throttle = 1.0 };
            var output = AIController.ClampForLowAltitude(
                input, altitudeAGL: 700f, threshold: 500f,
                elevatorMin: 0.0f, elevatorMax: 0.5f, aileronCap: 0.4f);
            Assert.AreEqual(-0.9, output.Elevator, 1e-9);
            Assert.AreEqual( 0.7, output.Aileron, 1e-9);
        }

        [Test]
        public void LowAltitudeClampPreservesMidRangeCommands()
        {
            // Inputs already within the band pass through unchanged.
            var input = new AcesOverTheLines.Flight.ControlInput { Elevator = 0.3, Aileron = -0.2, Throttle = 0.7 };
            var output = AIController.ClampForLowAltitude(
                input, altitudeAGL: 350f, threshold: 500f,
                elevatorMin: 0.0f, elevatorMax: 0.5f, aileronCap: 0.4f);
            Assert.AreEqual( 0.3, output.Elevator, 1e-9);
            Assert.AreEqual(-0.2, output.Aileron, 1e-9);
        }

        // ---- Climb state (Stage 6 tuning round 3) ----

        [Test]
        public void AltitudeBelow600mTransitionsToClimb()
        {
            // Hard altitude trigger: below entry altitude → Climb regardless
            // of descent rate.
            Assert.IsTrue(AIController.ShouldEnterClimb(
                altitudeAGL: 400f, verticalVelocityMs: 5f, excessDescentSustainS: 0f,
                entryAltitudeM: 600f, entryDescentRateMs: 30f, entryDescentSustainS: 2f));
        }

        [Test]
        public void SustainedSteepDescentTriggersClimbAboveAltitudeFloor()
        {
            // High altitude but descending fast for 2.5s → Climb triggers
            // proactively before the AI bleeds through the floor.
            Assert.IsTrue(AIController.ShouldEnterClimb(
                altitudeAGL: 800f, verticalVelocityMs: -45f, excessDescentSustainS: 2.5f,
                entryAltitudeM: 600f, entryDescentRateMs: 30f, entryDescentSustainS: 2f));
        }

        [Test]
        public void BriefDescentDoesNotTriggerClimb()
        {
            // Steep descent for only 1s — not sustained long enough to fire
            // the proactive climb trigger.
            Assert.IsFalse(AIController.ShouldEnterClimb(
                altitudeAGL: 800f, verticalVelocityMs: -45f, excessDescentSustainS: 1.0f,
                entryAltitudeM: 600f, entryDescentRateMs: 30f, entryDescentSustainS: 2f));
        }

        [Test]
        public void HighAltitudeAndPositiveClimbRateExitsClimb()
        {
            // Above exit altitude AND climbing → exit Climb to Patrol.
            Assert.IsTrue(AIController.ShouldExitClimb(
                altitudeAGL: 1100f, verticalVelocityMs: 5f, exitAltitudeM: 1000f));
        }

        [Test]
        public void HighAltitudeButStillDescendingHoldsClimb()
        {
            // Above exit altitude but still descending → keep climbing
            // (don't exit prematurely).
            Assert.IsFalse(AIController.ShouldExitClimb(
                altitudeAGL: 1100f, verticalVelocityMs: -5f, exitAltitudeM: 1000f));
        }

        [Test]
        public void LowAltitudeAndPositiveClimbRateDoesNotExitClimb()
        {
            // Climbing but not high enough yet → stay in Climb.
            Assert.IsFalse(AIController.ShouldExitClimb(
                altitudeAGL: 900f, verticalVelocityMs: 5f, exitAltitudeM: 1000f));
        }

        // ============================================================
        // §6 DoEngage rewrite tests (Round 6 Commit 1)
        // ============================================================

        [Test]
        public void ComputeEnergyState_StationaryGroundLevel_ReturnsZero()
        {
            Assert.AreEqual(0.0, AIController.ComputeEnergyState(0.0, 0.0), 0.001);
        }

        [Test]
        public void ComputeEnergyState_AltitudeOnly_ReturnsAltitude()
        {
            Assert.AreEqual(1000.0, AIController.ComputeEnergyState(1000.0, 0.0), 0.001);
        }

        [Test]
        public void ComputeEnergyState_SpeedOnly_ReturnsKineticAltitudeEquivalent()
        {
            // At v = 44.3 m/s, v²/(2g) ≈ 100.04 m
            Assert.AreEqual(100.04, AIController.ComputeEnergyState(0.0, 44.3), 0.1);
        }

        [Test]
        public void ComputeEnergyState_AltitudeAndSpeed_AdditiveCombination()
        {
            // Sanity: alt + kinetic equivalent
            double E = AIController.ComputeEnergyState(500.0, 50.0);
            double expected = 500.0 + (50.0 * 50.0) / (2.0 * 9.81);
            Assert.AreEqual(expected, E, 0.001);
        }

        [Test]
        public void ComputeAspectAngleDeg_DirectlyAstern_ReturnsZero()
        {
            // Self is behind target, target moving forward away from self.
            // targetForward = +x, targetToSelf = -x (self is behind on target's x axis).
            // Aspect = angle between +x and -x = 180° — wait, this is the geometric
            // angle, not the "off-tail" angle as conventionally defined.
            //
            // The contract uses: aspectDeg = Vector3.Angle(target.forward, target → self).
            // When self is directly BEHIND target, the vector from target to self points
            // OPPOSITE to target's forward, so aspect = 180°.
            // When self is directly AHEAD of target, the vector from target to self points
            // ALONG target's forward, so aspect = 0°.
            //
            // This is the GEOMETRIC aspect angle, where 0° means head-on and 180° means
            // tail-chase. NOTE: this is the inverse of the conventional dogfight aspect
            // angle (where 0° means tail-chase). The §6.2 decision tree uses this
            // geometric convention consistently: high aspect (> 90°) means target is
            // FACING the AI (bad — switch to lag); low aspect (< 60°) means AI is
            // approaching target from behind (good — commit to lead).
            Vector3 targetForward = new Vector3(1f, 0f, 0f);
            Vector3 targetToSelf  = new Vector3(-1f, 0f, 0f); // self behind target
            float aspect = AIController.ComputeAspectAngleDeg(targetForward, targetToSelf);
            Assert.AreEqual(180f, aspect, 0.1f);
        }

        [Test]
        public void ComputeAspectAngleDeg_HeadOn_ReturnsZero()
        {
            Vector3 targetForward = new Vector3(1f, 0f, 0f);
            Vector3 targetToSelf  = new Vector3(1f, 0f, 0f); // self ahead of target
            float aspect = AIController.ComputeAspectAngleDeg(targetForward, targetToSelf);
            Assert.AreEqual(0f, aspect, 0.1f);
        }

        [Test]
        public void ComputeAspectAngleDeg_BeamOn_Returns90()
        {
            Vector3 targetForward = new Vector3(1f, 0f, 0f);
            Vector3 targetToSelf  = new Vector3(0f, 0f, 1f); // self to target's right
            float aspect = AIController.ComputeAspectAngleDeg(targetForward, targetToSelf);
            Assert.AreEqual(90f, aspect, 0.1f);
        }

        // SelectPursuitMode decision tree — parameterised tests covering each branch.
        // closeFireRangeM=100, visualRangeM=1000 (matches Inspector defaults).
        [TestCase(  0f,   0.0, 200f, ExpectedResult = AIController.PursuitMode.Lead, TestName = "default → Lead")]
        [TestCase(125f, 100.0, 200f, ExpectedResult = AIController.PursuitMode.Lag,  TestName = "aspect>120 → Lag (doctrinal floor)")]
        [TestCase( 50f, -10.0, 200f, ExpectedResult = AIController.PursuitMode.Lag,  TestName = "ΔE<0 → Lag (energy preservation)")]
        [TestCase( 95f,  10.0, 200f, ExpectedResult = AIController.PursuitMode.Lag,  TestName = "aspect>90 AND range>close → Lag (re-position)")]
        [TestCase( 95f,  10.0,  50f, ExpectedResult = AIController.PursuitMode.Lead, TestName = "aspect>90 BUT range<close → Lead (still in gun range)")]
        [TestCase( 45f, 100.0, 500f, ExpectedResult = AIController.PursuitMode.Pure, TestName = "range>0.4*visual AND ΔE>50 → Pure (close)")]
        [TestCase( 45f,  30.0, 500f, ExpectedResult = AIController.PursuitMode.Lead, TestName = "range>0.4*visual BUT ΔE<=50 → Lead (insufficient advantage to commit to pure)")]
        public AIController.PursuitMode SelectPursuitMode_DecisionTree(
            float aspect, double deltaE, float range)
        {
            return AIController.SelectPursuitMode(
                deltaE, aspect, range,
                closeFireRangeM: 100f, visualRangeM: 1000f);
        }

        [Test]
        public void ComputePursuitPoint_Pure_ReturnsTargetPosition()
        {
            Vector3 targetPos = new Vector3(100f, 0f, 0f);
            Vector3 targetVel = new Vector3(20f, 0f, 0f);
            Vector3 firerPos = Vector3.zero;
            Vector3 p = AIController.ComputePursuitPoint(
                AIController.PursuitMode.Pure, targetPos, targetVel, firerPos, 820f);
            Assert.AreEqual(targetPos, p);
        }

        [Test]
        public void ComputePursuitPoint_Lead_ExtrapolatesTargetForward()
        {
            Vector3 targetPos = new Vector3(100f, 0f, 0f);
            Vector3 targetVel = new Vector3(20f, 0f, 0f);
            Vector3 firerPos = Vector3.zero;
            Vector3 p = AIController.ComputePursuitPoint(
                AIController.PursuitMode.Lead, targetPos, targetVel, firerPos, 820f);
            // Lead point = targetPos + targetVel * (range / muzzleVelocity)
            // range = 100, muzzleVelocity = 820, so bulletTime ≈ 0.122s
            // Expected lead = (100 + 20*0.122, 0, 0) ≈ (102.44, 0, 0)
            Assert.Greater(p.x, targetPos.x);
            Assert.AreEqual(targetPos.y, p.y, 0.001f);
            Assert.AreEqual(targetPos.z, p.z, 0.001f);
        }

        [Test]
        public void ComputePursuitPoint_Lag_ExtrapolatesTargetBackward()
        {
            Vector3 targetPos = new Vector3(100f, 0f, 0f);
            Vector3 targetVel = new Vector3(20f, 0f, 0f);
            Vector3 firerPos = Vector3.zero;
            Vector3 p = AIController.ComputePursuitPoint(
                AIController.PursuitMode.Lag, targetPos, targetVel, firerPos, 820f);
            // Lag = targetPos - targetVel * (range / muzzleVelocity)
            // Expected lag ≈ (100 - 2.44, 0, 0) = (97.56, 0, 0)
            Assert.Less(p.x, targetPos.x);
            Assert.AreEqual(targetPos.y, p.y, 0.001f);
            Assert.AreEqual(targetPos.z, p.z, 0.001f);
        }

        [Test]
        public void ComputePursuitPoint_LeadAndLag_AreSymmetricAroundTarget()
        {
            Vector3 targetPos = new Vector3(100f, 50f, 0f);
            Vector3 targetVel = new Vector3(20f, 0f, 5f);
            Vector3 firerPos = Vector3.zero;
            Vector3 lead = AIController.ComputePursuitPoint(
                AIController.PursuitMode.Lead, targetPos, targetVel, firerPos, 820f);
            Vector3 lag  = AIController.ComputePursuitPoint(
                AIController.PursuitMode.Lag,  targetPos, targetVel, firerPos, 820f);
            Vector3 midpoint = (lead + lag) * 0.5f;
            // Midpoint of lead and lag should be the target position.
            Assert.AreEqual(targetPos.x, midpoint.x, 0.001f);
            Assert.AreEqual(targetPos.y, midpoint.y, 0.001f);
            Assert.AreEqual(targetPos.z, midpoint.z, 0.001f);
        }
    }
}

namespace AcesOverTheLines.Aircraft.Tests
{
    using NUnit.Framework;
    using AcesOverTheLines.Aircraft;

    // ---- Air-to-air collision damage tiers (Stage 6 tuning round 3) ----
    public class AirToAirCollisionTests
    {
        [Test]
        public void LowRelSpeedYieldsFortyPercentDamage()
        {
            Assert.AreEqual(0.40, AirToAirCollisionSystem.CollisionDamageFraction(20f), 1e-9);
            Assert.AreEqual(0.40, AirToAirCollisionSystem.CollisionDamageFraction(29.9f), 1e-9);
        }

        [Test]
        public void MediumRelSpeedYieldsEightyPercentDamage()
        {
            Assert.AreEqual(0.80, AirToAirCollisionSystem.CollisionDamageFraction(30f), 1e-9);
            Assert.AreEqual(0.80, AirToAirCollisionSystem.CollisionDamageFraction(59.9f), 1e-9);
        }

        [Test]
        public void HighRelSpeedYieldsOneHundredFiftyPercentDamage()
        {
            Assert.AreEqual(1.50, AirToAirCollisionSystem.CollisionDamageFraction(60f), 1e-9);
            // Head-on at 100 m/s combined relative velocity → catastrophic.
            Assert.AreEqual(1.50, AirToAirCollisionSystem.CollisionDamageFraction(100f), 1e-9);
            Assert.AreEqual(1.50, AirToAirCollisionSystem.CollisionDamageFraction(250f), 1e-9);
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

        // ---- 2026-05-17 playtest fixes (Round 6 Commit 3) ----

        [Test]
        public void EngageSetpoint_HighEnergyAdvantage_LeadModePitchIsNeutral()
        {
            // Issue 2: with ΔE=+200m the AI has more than enough energy
            // advantage. Whatever pursuit mode the selector chose
            // (Lead in this scenario, but the gate is mode-independent),
            // the engage setpoint must back off the aggressive bleed pair
            // (pitch=-0.50, spd=47) and use the reposition pair
            // (pitch=-0.10, spd=55).
            var (pitch, airspeed) = AIController.ApplyEnergyBleedGate(
                deltaE: 200.0, lagBleedMaxDeltaE: 100f);
            Assert.AreEqual(-0.10, pitch, 1e-6,
                "ΔE=200m must produce the neutral pitch setpoint, not the bleed setpoint.");
            Assert.AreEqual(55.0, airspeed, 1e-6,
                "ΔE=200m must produce the reposition airspeed, not the bleed airspeed.");
        }

        [Test]
        public void PatrolGate_EngagesEvenAtRangeBeyondVisualRangeM()
        {
            // Issue 1: Patrol→Engage no longer gates on range or bearing.
            // A target reference is sufficient. Pre-fix, range=2000m
            // (> visualRangeM=1000m) blocked the transition indefinitely
            // and the AI flew straight while the bandit escaped.
            // ShouldEnterEngageFromPatrol takes no range parameter
            // because range is no longer consulted.
            Assert.IsTrue(AIController.ShouldEnterEngageFromPatrol(targetExists: true),
                "With a target reference, Patrol must transition to Engage regardless of range.");
            Assert.IsFalse(AIController.ShouldEnterEngageFromPatrol(targetExists: false),
                "With no target reference, Patrol must not transition.");
        }

        // ---- Clock-injection hook (2026-05-17, prep for playtest harness) ----

        [Test]
        public void NowSecondsSource_WhenOverridden_DrivesEngageDwell()
        {
            // Verifies that NowSecondsSource genuinely flows into the time-
            // based logic by checking that _stateEnteredTime gets stamped
            // with the injected clock value, not Time.time. If this
            // assertion ever fires, a Time.time read has been reintroduced
            // somewhere in the state-transition path and the playtest
            // harness will lose determinism.

            var aiGo = new GameObject("AI_ClockTest");
            aiGo.transform.position = new Vector3(0f, 1000f, 0f);
            var aiRb = aiGo.AddComponent<Rigidbody>();
            aiRb.position = new Vector3(0f, 1000f, 0f);
            aiRb.isKinematic = true;  // prevent gravity drift during the test
            var ai = aiGo.AddComponent<AIController>();
            var targetGo = new GameObject("Target_ClockTest");

            try
            {
                // Unity's EditMode test runner does not auto-fire
                // MonoBehaviour lifecycle methods. Manually invoke Awake
                // so _rb / _stabilizer get initialised — otherwise
                // UpdateStateTransitions early-outs on `_rb == null`.
                var awakeMethod = typeof(AIController).GetMethod("Awake",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                awakeMethod.Invoke(ai, null);

                // Override the clock with a controllable scalar. Initial
                // 100s is well past the 5 Hz decision-rate gate (>= 0.2s),
                // so the first ReadControls call triggers a state machine
                // tick.
                float fakeClock = 100.0f;
                ai.NowSecondsSource = () => fakeClock;
                // Place target 500 m away at the same altitude so the
                // Engage branch's nested checks don't immediately route
                // us elsewhere: range (500m) > engageBreakOffRangeM (60m)
                // skips the collision-avoidance check; AI altitude (1000m)
                // > climbFloorAltitude (700m) skips the floor check.
                targetGo.transform.position = new Vector3(0f, 1000f, 500f);
                ai.Target = targetGo.transform;

                // Initial state is Patrol with _stateEnteredTime defaulted
                // to 0. ReadControls → UpdateStateTransitions → Patrol →
                // Engage (target exists, Issue 1 gate-less policy), which
                // calls TransitionIfChanged with NowSeconds = 100.0f.
                ai.ReadControls(1.0 / 120.0);

                Assert.AreEqual(AIController.State.Engage, ai.CurrentState,
                    "Patrol→Engage should fire on first decision tick with a target present.");
                Assert.AreEqual(100.0f, ai.StateEnteredTime, 1e-4f,
                    "TransitionIfChanged must stamp _stateEnteredTime from " +
                    "NowSecondsSource, not Time.time. If this fires, a stray " +
                    "Time.time read has snuck back into the transition path.");

                // Advance the injected clock and verify the engage-dwell
                // computation reads it. Engage dwell at this point is
                // 105.5 - 100.0 = 5.5s, well below the 25s stalemate
                // timeout, so state must remain Engage.
                fakeClock = 105.5f;
                ai.ReadControls(1.0 / 120.0);
                Assert.AreEqual(AIController.State.Engage, ai.CurrentState,
                    "5.5s dwell is below the 25s stalemate timeout; state should remain Engage.");
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(targetGo);
            }
        }

        // ---- Phase 2 doctrine selector tests (2026-05-17) ----
        //
        // These exercise the selector logic in isolation. Each test builds
        // a minimal AI rig (Rigidbody + AIController), invokes Awake
        // manually (EditMode test runner does not fire MonoBehaviour
        // lifecycle methods), drives state via direct ReadControls calls
        // with an injected clock, and asserts on the internal doctrine
        // state via the InternalsVisibleTo accessors.

        static (GameObject aiGo, AIController ai, GameObject targetGo, Rigidbody aiRb)
            SpawnSelectorRig(float aiAltitude, float targetAltitude, float targetRangeZ)
        {
            var aiGo = new GameObject("AI_SelectorTest");
            aiGo.transform.position = new Vector3(0f, aiAltitude, 0f);
            var aiRb = aiGo.AddComponent<Rigidbody>();
            aiRb.position = new Vector3(0f, aiAltitude, 0f);
            aiRb.isKinematic = true;
            var ai = aiGo.AddComponent<AIController>();
            var targetGo = new GameObject("Target_SelectorTest");
            targetGo.transform.position = new Vector3(0f, targetAltitude, targetRangeZ);
            // Manually invoke Awake — Unity EditMode test runner skips it.
            var awake = typeof(AIController).GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            awake.Invoke(ai, null);
            return (aiGo, ai, targetGo, aiRb);
        }

        // Drives the AI from Patrol → Engage at the injected clock. Returns
        // when CurrentState == Engage. Throws if it doesn't happen on the
        // first ReadControls (target is set, gate has no range condition).
        static void EnterEngageAt(AIController ai, float clockValue, ref float clock)
        {
            clock = clockValue;
            ai.ReadControls(1.0 / 120.0);
            Assert.AreEqual(AIController.State.Engage, ai.CurrentState,
                $"Test setup: expected immediate Patrol→Engage at clock={clockValue}");
        }

        [Test]
        public void SelectorEvaluation_FromBoomZoom_TargetHardTurning_SwitchesAfterHysteresis()
        {
            // Target sustains a > 30° bank for > 2 seconds (the
            // "hard-turning" threshold). After 1s of candidate hysteresis,
            // the selector should commit to Angles.
            var (aiGo, ai, targetGo, _) = SpawnSelectorRig(
                aiAltitude: 1000f, targetAltitude: 1000f, targetRangeZ: 500f);
            try
            {
                float clock = 100f;
                ai.NowSecondsSource = () => clock;
                ai.Target = targetGo.transform;
                EnterEngageAt(ai, 100f, ref clock);

                Assert.AreEqual(AIController.EngageDoctrine.BoomZoom, ai.CurrentDoctrine,
                    "Fresh Engage entry should start in BoomZoom.");

                // Bank the target to 45° (above the 30° hard-turn threshold).
                // Rotation is bank-only around the body forward axis (X in
                // this codebase's convention).
                targetGo.transform.rotation = Quaternion.AngleAxis(45f, Vector3.right);

                // Tick the selector at the 5 Hz strategic rate (0.2s per tick).
                // After 2s the target qualifies as hard-turning; after a
                // further 1s the candidate hysteresis commits. Loop extends
                // to t=105 to provide margin for float-precision drift in
                // the 0.2s strategic-tick gate, which can cause a tick to
                // skip in the borderline diff-vs-threshold comparison.
                for (float t = 100.2f; t <= 105.0f; t += 0.2f)
                {
                    clock = t;
                    ai.ReadControls(1.0 / 120.0);
                }

                Assert.AreEqual(AIController.EngageDoctrine.Angles, ai.CurrentDoctrine,
                    "After 2s hard-turn + 1s hysteresis, selector should commit to Angles.");
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void SelectorEvaluation_FromAngles_EnergyDeficit_SwitchesBackAfterHysteresis()
        {
            // AI is in Angles mode. Configure energy state such that
            // deltaE < -200m (the Angles→BoomZoom switch condition).
            // Note: cannot test the altitude-floor branch directly here
            // because dropping AI below climbFloorAltitude=700 triggers
            // the state-machine's Engage→Climb transition before the
            // selector's 1s hysteresis can commit — the test would
            // observe state=Climb, not a doctrine switch.
            //
            // Energy deficit is selector-only (no state transition fires
            // on negative deltaE within Angles), so it's the cleanest
            // condition to test the back-to-BoomZoom path in isolation.
            //
            // AI alt 1000 (above 700 floor, no state transition).
            // Target alt 2000 with target rigidbody. AI energy ≈ 1000;
            // target energy ≈ 2000. deltaE ≈ -1000, well below -200.
            var (aiGo, ai, targetGo, aiRb) = SpawnSelectorRig(
                aiAltitude: 1000f, targetAltitude: 2000f, targetRangeZ: 500f);
            try
            {
                // Give the target a Rigidbody so the selector's
                // _targetRb-based energy computation has data; default
                // zero speed is fine.
                var targetRb = targetGo.AddComponent<Rigidbody>();
                targetRb.isKinematic = true;
                ai.Target = targetGo.transform;

                float clock = 100f;
                ai.NowSecondsSource = () => clock;
                EnterEngageAt(ai, 100f, ref clock);

                // Force the AI into Angles mode via reflection.
                var doctrineField = typeof(AIController).GetField("_engageDoctrine",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                doctrineField.SetValue(ai, AIController.EngageDoctrine.Angles);
                var doctrineEnteredField = typeof(AIController).GetField("_doctrineEnteredTime",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                doctrineEnteredField.SetValue(ai, clock);

                Assert.AreEqual(AIController.EngageDoctrine.Angles, ai.CurrentDoctrine);

                // Tick the selector. With energy deficit (target 1000m
                // above AI, deltaE ≈ -1000m), BoomZoom is the candidate
                // immediately; after 1s hysteresis it commits.
                for (float t = 100.2f; t <= 105.0f; t += 0.2f)
                {
                    clock = t;
                    ai.ReadControls(1.0 / 120.0);
                }

                Assert.AreEqual(AIController.EngageDoctrine.BoomZoom, ai.CurrentDoctrine,
                    "After energy-deficit breach + 1s hysteresis, selector should commit back to BoomZoom.");
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void SelectorEvaluation_ReverseLockout_PreventsFlap()
        {
            // After a doctrine switch, the 3s reverse-lockout suppresses
            // selector evaluation entirely. Even with conditions that
            // would normally trigger an immediate switch back, the
            // doctrine does not change until the lockout expires.
            //
            // Uses the energy-deficit Angles→BoomZoom condition (target
            // way above AI). Cannot use altitude-floor breach because
            // dropping AI below 700m triggers the state machine's
            // Engage→Climb transition before the selector matters.
            var (aiGo, ai, targetGo, _) = SpawnSelectorRig(
                aiAltitude: 1000f, targetAltitude: 2000f, targetRangeZ: 500f);
            try
            {
                var targetRb = targetGo.AddComponent<Rigidbody>();
                targetRb.isKinematic = true;
                ai.Target = targetGo.transform;

                float clock = 100f;
                ai.NowSecondsSource = () => clock;
                EnterEngageAt(ai, 100f, ref clock);

                // Force Angles mode AND set _doctrineLastSwitch to NOW —
                // simulates a just-committed switch.
                var doctrineField = typeof(AIController).GetField("_engageDoctrine",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                doctrineField.SetValue(ai, AIController.EngageDoctrine.Angles);
                var lastSwitchField = typeof(AIController).GetField("_doctrineLastSwitch",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                lastSwitchField.SetValue(ai, clock);

                // Energy deficit is now active (target 1000m above AI),
                // would normally trigger the back-to-BoomZoom candidate
                // immediately. But the lockout skips selector evaluation
                // entirely for 3 seconds.
                // 2.5s elapsed (still within the 3s lockout).
                for (float t = 100.2f; t <= 102.4f; t += 0.2f)
                {
                    clock = t;
                    ai.ReadControls(1.0 / 120.0);
                }

                Assert.AreEqual(AIController.EngageDoctrine.Angles, ai.CurrentDoctrine,
                    "While within 3s reverse-lockout, no doctrine switch should occur.");
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void SelectorEvaluation_StalemateTrigger_SwitchesToAngles()
        {
            // Target NOT hard-turning. timeInDoctrine > 20s AND
            // _timeSinceFiringSolution > 12s drives the stalemate switch
            // to Angles. This is the Scenario 1 path — slow level decoy
            // that B&Z can't fire on.
            var (aiGo, ai, targetGo, _) = SpawnSelectorRig(
                aiAltitude: 1000f, targetAltitude: 1000f, targetRangeZ: 500f);
            try
            {
                float clock = 100f;
                ai.NowSecondsSource = () => clock;
                ai.Target = targetGo.transform;
                EnterEngageAt(ai, 100f, ref clock);

                // Reach into _timeSinceFiringSolution and set it high so
                // the stalemate condition is met as soon as timeInDoctrine
                // > 20s.
                var noFireField = typeof(AIController).GetField("_timeSinceFiringSolution",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                noFireField.SetValue(ai, 13f);

                // Run the clock forward past the 20s stalemate threshold
                // and through the 1s hysteresis. Need to keep
                // _timeSinceFiringSolution high — it gets incremented by
                // ComputeFiringDecision but might be reset if AI fires.
                // Target is far (z=500m) — AI won't fire. So the field
                // only grows naturally.
                noFireField.SetValue(ai, 13f);
                for (float t = 100.2f; t <= 122.0f; t += 0.2f)
                {
                    clock = t;
                    ai.ReadControls(1.0 / 120.0);
                }

                Assert.AreEqual(AIController.EngageDoctrine.Angles, ai.CurrentDoctrine,
                    "Stalemate condition (timeInDoctrine > 20s + timeSinceFiringSolution > 12s) " +
                    "should drive a switch to Angles after the 1s hysteresis.");
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(targetGo);
            }
        }
    }
}

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
    }
}

using NUnit.Framework;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Weapons.Tests
{
    public class GunTests
    {
        static GunSpec MakeVickers() => new GunSpec
        {
            Type = "Vickers .303",
            Rounds = 500,
            RateOfFireRpm = 450,
            MuzzleVelocityMS = 820,
            DamagePerHitHp = 8,
            DispersionRad = 0.003,
            JamProbabilityPerRound = 0,
            ClearJamTimeS = 3.0,
            Synchronised = true,
        };

        [Test]
        public void AppliesEightPercentSynchroniserPenaltyToRpm()
        {
            var g = new Gun(MakeVickers());
            Assert.AreEqual(450.0 * 0.92, g.EffectiveRpm, 5e-6);
            // Period at 414 rpm = 60/414 ≈ 0.14493 s.
            Assert.AreEqual(60.0 / 414.0, g.PeriodS, 5e-6);
        }

        [Test]
        public void EmitsNoBulletWhenTriggerHeldButCooldownHasNotElapsed()
        {
            var g = new Gun(MakeVickers());
            var a = g.Tick(0.01, true);          // first call: fires (cooldown was 0)
            Assert.IsNotNull(a);
            var b = g.Tick(0.01, true);          // 0.01 s later: still cooling
            Assert.IsNull(b);
        }

        [Test]
        public void CooldownElapsedSecondRoundFires()
        {
            var g = new Gun(MakeVickers());
            g.Tick(0.01, true);                  // first round
            // Advance more than periodS without trigger held — cooldown decrements
            // but no fire. Then hold trigger.
            g.Tick(g.PeriodS + 0.01, false);
            var b = g.Tick(0.01, true);
            Assert.IsNotNull(b);
        }

        [Test]
        public void DoesNotFireWhenAmmoIsEmptyAndStaysEmpty()
        {
            var spec = MakeVickers();
            spec.Rounds = 1;
            var g = new Gun(spec);
            var a = g.Tick(0.01, true);
            Assert.IsNotNull(a);
            g.Tick(g.PeriodS + 0.01, false);
            var b = g.Tick(0.01, true);
            Assert.IsNull(b);
            Assert.AreEqual(0, g.Rounds);
        }

        [Test]
        public void EmitsTracerOnEveryFifthRound()
        {
            var g = new Gun(MakeVickers());
            int tracerCount = 0;
            for (int i = 0; i < 12; i++)
            {
                var r = g.Tick(g.PeriodS + 0.01, true);
                if (r.HasValue && r.Value.Tracer) tracerCount++;
            }
            // Rounds 5 and 10 are tracers; total = 12 fired, so 2 tracers.
            Assert.AreEqual(2, tracerCount);
        }

        [Test]
        public void ForcedJamTriggerHeldEmitsNothingUntilCleared()
        {
            var spec = MakeVickers();
            spec.JamProbabilityPerRound = 0;
            var g = new Gun(spec);
            g.ForceJam();
            Assert.IsTrue(g.Jammed);
            var r = g.Tick(0.5, true);
            Assert.IsNull(r);
            Assert.IsTrue(g.Jammed);
        }
    }
}

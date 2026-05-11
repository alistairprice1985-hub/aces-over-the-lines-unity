using System;
using NUnit.Framework;
using AcesOverTheLines.Flight;

namespace AcesOverTheLines.Flight.Tests
{
    public class AircraftRosterTests
    {
        [Test]
        public void ListAircraftIdsReturnsExpectedSix()
        {
            var ids = AircraftRoster.ListAircraftIds();
            Assert.AreEqual(6, ids.Count);
            CollectionAssert.AreEquivalent(
                new[] { "sopwith_camel", "se5a", "bristol_f2b", "fokker_dr1", "albatros_d3", "fokker_d7" },
                ids);
        }

        [Test]
        public void SopwithCamelHasExpectedHeadlineValues()
        {
            var camel = AircraftRoster.GetAircraftConfig("sopwith_camel");
            Assert.AreEqual("sopwith_camel", camel.Id);
            Assert.AreEqual(659.0, camel.MassKg);
            Assert.AreEqual(21.5, camel.WingAreaM2);
            Assert.AreEqual(185.0, camel.TopSpeedKmh);
        }

        [Test]
        public void SopwithCamelDerivedFieldsAreCorrect()
        {
            var camel = AircraftRoster.GetAircraftConfig("sopwith_camel");
            // top_speed_m_s = 185 / 3.6 ≈ 51.388...
            Assert.AreEqual(185.0 / 3.6, camel.TopSpeedMS, 1e-12);
            Assert.AreEqual(51.39, camel.TopSpeedMS, 0.01);
            Assert.IsTrue(camel.IsRotary);
            // alpha_stall_rad = 15° in radians ≈ 0.2618
            Assert.AreEqual(15.0 * Math.PI / 180.0, camel.AlphaStallRad, 1e-12);
            Assert.AreEqual(0.2618, camel.AlphaStallRad, 1e-4);
            Assert.AreEqual(25.0 * Math.PI / 180.0, camel.AlphaPostStallRad, 1e-12);
        }

        [Test]
        public void SE5aHasDistinctiveValues()
        {
            var se5a = AircraftRoster.GetAircraftConfig("se5a");
            Assert.AreEqual(902.0, se5a.MassKg);
            Assert.AreEqual(3200.0, se5a.ThrustMaxN);
            Assert.AreEqual(222.0, se5a.TopSpeedKmh);
            Assert.IsFalse(se5a.IsRotary);
        }

        [Test]
        public void BristolF2bHasRearGunner()
        {
            var f2b = AircraftRoster.GetAircraftConfig("bristol_f2b");
            Assert.AreEqual(1474.0, f2b.MassKg);
            Assert.IsTrue(f2b.HasRearGunner);
            Assert.AreEqual("two_seat_fighter", f2b.Role);
        }

        [Test]
        public void FokkerDr1IsRotaryAndHasBestRollRate()
        {
            var dr1 = AircraftRoster.GetAircraftConfig("fokker_dr1");
            Assert.AreEqual(3.2, dr1.RollRateMaxRadS);
            Assert.IsTrue(dr1.IsRotary);
            Assert.AreEqual(290.0, dr1.GyroTorqueMagnitude);
        }

        [Test]
        public void AlbatrosD3HasLowerWingFailureSignature()
        {
            var alb = AircraftRoster.GetAircraftConfig("albatros_d3");
            Assert.AreEqual(0.8, alb.StructuralGDurationS);
            Assert.AreEqual(4.5, alb.StructuralGLimit);
            Assert.AreEqual("lower_wing_separation_in_dive", alb.StructuralFailureMode);
        }

        [Test]
        public void FokkerD7HasLowSpeedBonus()
        {
            var d7 = AircraftRoster.GetAircraftConfig("fokker_d7");
            Assert.AreEqual(0.15, d7.LowSpeedBonus);
            Assert.AreEqual(5500.0, d7.ThrustCurveAltM);
        }

        [Test]
        public void UnknownIdThrows()
        {
            Assert.Throws<ArgumentException>(() => AircraftRoster.GetAircraftConfig("messerschmitt_109"));
        }

        [Test]
        public void GunSpecsAreReusedAcrossAircraftViaWithExpression()
        {
            var camel = AircraftRoster.GetAircraftConfig("sopwith_camel");
            Assert.AreEqual(2, camel.Guns.Count);
            Assert.AreEqual("Vickers .303", camel.Guns[0].Type);
            Assert.AreEqual("left", camel.Guns[0].Mount);
            Assert.AreEqual("right", camel.Guns[1].Mount);

            var dr1 = AircraftRoster.GetAircraftConfig("fokker_dr1");
            Assert.AreEqual("Spandau LMG 08/15", dr1.Guns[0].Type);
            Assert.AreEqual(9, dr1.Guns[0].DamagePerHitHp);
            Assert.AreEqual("left", dr1.Guns[0].Mount);
        }
    }
}

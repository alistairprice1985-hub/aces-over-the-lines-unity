using System;
using NUnit.Framework;
using UnityEngine;
using AcesOverTheLines.Flight;

namespace AcesOverTheLines.Flight.Tests
{
    public class AircraftEntityTests
    {
        static AircraftConfig Camel() => AircraftRoster.GetAircraftConfig("sopwith_camel");
        static AircraftConfig D7()    => AircraftRoster.GetAircraftConfig("fokker_d7");

        // ---- ThrustAtAltitude ----

        [Test]
        public void ThrustAtAltitudeAtSeaLevelIsFullThrust()
        {
            var cfg = Camel();
            // factor = 1 at h = 0 → T = thrust_max_N * prop_efficiency * 1 * throttle
            double thrust = AircraftEntity.ThrustAtAltitude(cfg, 0.0, 1.0);
            Assert.AreEqual(cfg.ThrustMaxN * cfg.PropEfficiency, thrust, 1e-6);
        }

        [Test]
        public void ThrustAtAltitudeHalfwayUpCurveIsHalf()
        {
            var cfg = Camel(); // ThrustCurveAltM = 4500
            // h = 2250 → factor = 0.5
            double thrust = AircraftEntity.ThrustAtAltitude(cfg, 2250.0, 1.0);
            Assert.AreEqual(cfg.ThrustMaxN * cfg.PropEfficiency * 0.5, thrust, 1e-6);
        }

        [Test]
        public void ThrustAtAltitudeFlooredAtThirtyPercentAboveCurve()
        {
            var cfg = Camel();
            // At h = 4500 the linear term hits 0, so the floor of 0.3 takes over.
            Assert.AreEqual(cfg.ThrustMaxN * cfg.PropEfficiency * 0.3,
                            AircraftEntity.ThrustAtAltitude(cfg, 4500.0, 1.0), 1e-6);
            // Well above the curve — still 0.3.
            Assert.AreEqual(cfg.ThrustMaxN * cfg.PropEfficiency * 0.3,
                            AircraftEntity.ThrustAtAltitude(cfg, 10000.0, 1.0), 1e-6);
        }

        [Test]
        public void ThrustAtAltitudeClampsThrottleAndAltitude()
        {
            var cfg = Camel();
            // throttle > 1 clamps to 1.
            Assert.AreEqual(cfg.ThrustMaxN * cfg.PropEfficiency,
                            AircraftEntity.ThrustAtAltitude(cfg, 0.0, 2.0), 1e-6);
            // throttle < 0 clamps to 0.
            Assert.AreEqual(0.0, AircraftEntity.ThrustAtAltitude(cfg, 0.0, -0.5), 1e-6);
            // Negative altitude treated as sea level.
            Assert.AreEqual(cfg.ThrustMaxN * cfg.PropEfficiency,
                            AircraftEntity.ThrustAtAltitude(cfg, -100.0, 1.0), 1e-6);
        }

        // ---- Authority ramp ----

        [Test]
        public void AuthorityZeroAtOrBelowV0()
        {
            Assert.AreEqual(0.0, AircraftEntity.Authority(5.0, 11.1, 33.0));
            Assert.AreEqual(0.0, AircraftEntity.Authority(11.1, 11.1, 33.0));
        }

        [Test]
        public void AuthorityOneAtOrAboveV1()
        {
            Assert.AreEqual(1.0, AircraftEntity.Authority(33.0, 11.1, 33.0));
            Assert.AreEqual(1.0, AircraftEntity.Authority(50.0, 11.1, 33.0));
        }

        [Test]
        public void AuthorityRampsLinearlyBetween()
        {
            // midpoint of [11.1, 33.0] = 22.05 → 0.5
            Assert.AreEqual(0.5, AircraftEntity.Authority(22.05, 11.1, 33.0), 1e-6);
            // quarter point
            double q = 11.1 + 0.25 * (33.0 - 11.1);
            Assert.AreEqual(0.25, AircraftEntity.Authority(q, 11.1, 33.0), 1e-6);
        }

        // ---- StallThreshold ----

        [Test]
        public void StallThresholdNoLowSpeedBonus()
        {
            var cfg = Camel(); // LowSpeedBonus = 0
            Assert.AreEqual(cfg.AlphaStallRad, AircraftEntity.StallThreshold(cfg), 1e-12);
        }

        [Test]
        public void StallThresholdAppliesLowSpeedBonus()
        {
            var cfg = D7(); // LowSpeedBonus = 0.15
            double expected = cfg.AlphaStallRad * (1.0 - 0.15 * 0.5);
            Assert.AreEqual(expected, AircraftEntity.StallThreshold(cfg), 1e-12);
            Assert.Less(AircraftEntity.StallThreshold(cfg), cfg.AlphaStallRad);
        }

        // ---- State-machine: damage consequences ----

        [Test]
        public void DamagingPilotToZeroFlagsIncapacitated()
        {
            var go = new GameObject("AETestPilot");
            var rb = go.AddComponent<Rigidbody>();
            try
            {
                var e = new AircraftEntity(Camel(), rb);
                Assert.IsFalse(e.Status().PilotIncapacitated);
                var r = e.DamageComponent("pilot", 80.0);
                Assert.AreEqual(80.0, r.Applied, 1e-12);
                Assert.IsTrue(r.Destroyed);
                Assert.IsTrue(e.Status().PilotIncapacitated);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void DamagingFuelTankToZeroActivatesFire()
        {
            var go = new GameObject("AETestFuel");
            var rb = go.AddComponent<Rigidbody>();
            try
            {
                var e = new AircraftEntity(Camel(), rb);
                Assert.IsFalse(e.FuelFireActive);
                e.DamageComponent("fuel_tank", 60.0);
                Assert.IsTrue(e.FuelFireActive);
                Assert.IsTrue(e.Status().FuelTankDestroyed);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void EightSecondsOfFuelFireRegistersCrashed()
        {
            var go = new GameObject("AETestFire");
            var rb = go.AddComponent<Rigidbody>();
            try
            {
                var e = new AircraftEntity(Camel(), rb);
                e.DamageComponent("fuel_tank", 60.0);
                Assert.IsTrue(e.FuelFireActive);

                var controls = new ControlInput { Throttle = 0.0 };
                const double dt = 1.0 / 120.0;
                for (int i = 0; i < 1200 && !e.Crashed; i++)
                {
                    e.Update(dt, controls);
                }
                Assert.IsTrue(e.Crashed, "expected crash within ~8 s of fuel fire");
                Assert.IsFalse(e.FuelFireActive);
                Assert.GreaterOrEqual(e.FuelFireTimer, 8.0);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void DamagingEngineToZeroZeroesEngineHealth()
        {
            var go = new GameObject("AETestEngine");
            var rb = go.AddComponent<Rigidbody>();
            try
            {
                var e = new AircraftEntity(Camel(), rb);
                Assert.AreEqual(1.0, e.EngineHealth);
                e.DamageComponent("engine", 100.0);
                Assert.AreEqual(0.0, e.EngineHealth);
                Assert.IsTrue(e.Status().EngineDestroyed);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void RegisterCrashIfBelowSnapsAndFreezes()
        {
            var go = new GameObject("AETestGround");
            var rb = go.AddComponent<Rigidbody>();
            try
            {
                var e = new AircraftEntity(Camel(), rb,
                    position: new Vector3(0f, -5f, 0f),
                    velocity: new Vector3(50f, 0f, 0f));
                bool crashed = e.RegisterCrashIfBelow(0.0);
                Assert.IsTrue(crashed);
                Assert.IsTrue(e.Crashed);
                Assert.AreEqual(0f, rb.position.y, 1e-6);
                Assert.AreEqual(Vector3.zero, rb.linearVelocity);
                Assert.AreEqual(Vector3.zero, rb.angularVelocity);
                Assert.Greater(e.CrashSpeedMS, 0.0);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}

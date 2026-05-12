using NUnit.Framework;
using UnityEngine;
using AcesOverTheLines.Aircraft;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Aircraft.Tests
{
    public class DroneTests
    {
        Drone MakeDrone(out GameObject go)
        {
            go = new GameObject("DroneTest");
            var d = go.AddComponent<Drone>();
            d.Initialize();  // Awake doesn't fire on AddComponent in EditMode.
            return d;
        }

        [Test]
        public void DamageEngineToHalfReducesHpCorrectly()
        {
            var drone = MakeDrone(out var go);
            try
            {
                int engineMax = DamageModel.COMPONENT_HP["engine"]; // 100
                var r = drone.DamageComponent("engine", engineMax / 2.0);
                Assert.AreEqual(engineMax / 2.0, r.Applied, 1e-12);
                Assert.IsFalse(r.Destroyed);
                Assert.AreEqual(engineMax / 2.0, drone.Components["engine"].hp, 1e-12);
                Assert.IsFalse(drone.Destroyed);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DamageEngineToZeroDestroysDrone()
        {
            var drone = MakeDrone(out var go);
            try
            {
                int engineMax = DamageModel.COMPONENT_HP["engine"]; // 100
                var r = drone.DamageComponent("engine", engineMax);
                Assert.AreEqual(engineMax, r.Applied, 1e-12);
                Assert.IsTrue(r.Destroyed, "expected destruction transition on this tick");
                Assert.IsTrue(drone.Destroyed);
                Assert.AreEqual(0.0, drone.Components["engine"].hp, 1e-12);
                Assert.IsTrue(drone.Status().EngineDestroyed);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DamagePilotToZeroDestroysDrone()
        {
            var drone = MakeDrone(out var go);
            try
            {
                int pilotMax = DamageModel.COMPONENT_HP["pilot"]; // 80
                var r = drone.DamageComponent("pilot", pilotMax);
                Assert.AreEqual(pilotMax, r.Applied, 1e-12);
                Assert.IsTrue(r.Destroyed);
                Assert.IsTrue(drone.Destroyed);
                Assert.IsTrue(drone.Status().PilotDestroyed);
                Assert.IsFalse(drone.Status().EngineDestroyed);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DamagingEveryComponentToZeroReportsAllZero()
        {
            var drone = MakeDrone(out var go);
            try
            {
                foreach (var kv in DamageModel.COMPONENT_HP)
                {
                    drone.DamageComponent(kv.Key, kv.Value);
                }
                Assert.IsTrue(drone.Destroyed);
                var s = drone.Status();
                Assert.IsTrue(s.AllComponentsZero);
                Assert.IsTrue(s.PilotDestroyed);
                Assert.IsTrue(s.EngineDestroyed);
                foreach (var kv in drone.Components)
                {
                    Assert.AreEqual(0.0, kv.Value.hp, 1e-12, $"component {kv.Key} should be at 0 HP");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DamagingDestroyedComponentReturnsZero()
        {
            var drone = MakeDrone(out var go);
            try
            {
                drone.DamageComponent("engine", DamageModel.COMPONENT_HP["engine"]);
                var r = drone.DamageComponent("engine", 50);
                Assert.AreEqual(0.0, r.Applied, 1e-12);
                Assert.IsFalse(r.Destroyed, "no second destruction transition");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

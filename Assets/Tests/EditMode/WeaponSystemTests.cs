using NUnit.Framework;
using UnityEngine;
using AcesOverTheLines.Weapons;
using AcesOverTheLines.Aircraft;

namespace AcesOverTheLines.Weapons.Tests
{
    public class WeaponSystemTests
    {
        // Helper: spawn a real Drone with hitbox children at the given world
        // position, sync the physics broadphase, return useful handles.
        class DroneSetup
        {
            public GameObject Root;
            public Drone Drone;
            public Transform EngineHitbox;
            public int LayerMask;

            public static DroneSetup Build(Vector3 worldPos)
            {
                var go = new GameObject("BulletTestDrone");
                go.transform.position = worldPos;
                var d = go.AddComponent<Drone>();
                d.Initialize();
                Physics.SyncTransforms();

                var engineTf = go.transform.Find("Hitbox_engine");
                int childLayer = engineTf != null ? engineTf.gameObject.layer : 0;
                return new DroneSetup
                {
                    Root = go,
                    Drone = d,
                    EngineHitbox = engineTf,
                    LayerMask = 1 << childLayer,
                };
            }

            public void Cleanup() => Object.DestroyImmediate(Root);
        }

        // ---- Bullet hit + damage maths ----

        [Test]
        public void PointBlankBulletDealsApproximatelyFullDamage()
        {
            var setup = DroneSetup.Build(Vector3.zero);
            try
            {
                // Engine hitbox center is at body (2.5, 0.05, 0); drone at
                // origin → engine collider entry face is around x ≈ 1.8.
                // Bullet starts at x=1.0 so the raycast hits at ~0.8 m.
                var bullet = new Bullet
                {
                    Position = new Vector3(1.0f, 0.05f, 0f),
                    Velocity = new Vector3(820f, 0f, 0f),
                    BaseDamage = 8.0,
                    FiringAircraftPos = Vector3.zero,
                };
                var hit = bullet.Step(1.0 / 120.0, setup.LayerMask);
                Assert.IsTrue(hit.HasValue, "expected hit on engine collider");
                Assert.AreEqual("engine", hit.Value.Receiver.ComponentName);
                // Damage at ~0.8 m range = 8 * (1 - 0.0008) ≈ 7.9936 — well
                // within "essentially 8".
                Assert.AreEqual(8.0, hit.Value.Damage, 0.05);
                Assert.IsTrue(bullet.Expired);
            }
            finally { setup.Cleanup(); }
        }

        [Test]
        public void FiveHundredMeterBulletDealsHalfDamage()
        {
            var setup = DroneSetup.Build(Vector3.zero);
            try
            {
                // Pre-load DistanceTraveled = 499.5 m and place the bullet
                // 0.5 m short of the engine entry face. Hit registers at
                // distance 0.5 m → total = 500 m → damage = 4 exactly.
                var bullet = new Bullet
                {
                    Position = new Vector3(1.3f, 0.05f, 0f),
                    Velocity = new Vector3(820f, 0f, 0f),
                    BaseDamage = 8.0,
                    FiringAircraftPos = Vector3.zero,
                    DistanceTraveled = 499.5,
                };
                var hit = bullet.Step(1.0 / 120.0, setup.LayerMask);
                Assert.IsTrue(hit.HasValue);
                Assert.AreEqual(4.0, hit.Value.Damage, 0.05);
            }
            finally { setup.Cleanup(); }
        }

        [Test]
        public void BulletBeyondMaxRangeDealsZeroDamage()
        {
            var setup = DroneSetup.Build(Vector3.zero);
            try
            {
                var bullet = new Bullet
                {
                    Position = new Vector3(1.3f, 0.05f, 0f),
                    Velocity = new Vector3(820f, 0f, 0f),
                    BaseDamage = 8.0,
                    FiringAircraftPos = Vector3.zero,
                    DistanceTraveled = 1099.5, // beyond MAX_RANGE (1000 m); also > damage falloff cap
                };
                var hit = bullet.Step(1.0 / 120.0, setup.LayerMask);
                if (hit.HasValue)
                {
                    Assert.AreEqual(0.0, hit.Value.Damage, 1e-12);
                }
                Assert.IsTrue(bullet.Expired);
            }
            finally { setup.Cleanup(); }
        }

        [Test]
        public void BulletWithFirerOutsideDetectionRangeMisses()
        {
            var setup = DroneSetup.Build(Vector3.zero);
            try
            {
                // Bullet is right next to the engine but the firing aircraft
                // was 1000 m away (> TARGET_DETECTION_RANGE_M = 800). The
                // raycast finds the collider but the post-filter rejects it.
                var bullet = new Bullet
                {
                    Position = new Vector3(1.3f, 0.05f, 0f),
                    Velocity = new Vector3(820f, 0f, 0f),
                    BaseDamage = 8.0,
                    FiringAircraftPos = new Vector3(0f, 0f, 1000f),
                };
                var hit = bullet.Step(1.0 / 120.0, setup.LayerMask);
                Assert.IsFalse(hit.HasValue, "expected detection-range cull to reject the hit");
            }
            finally { setup.Cleanup(); }
        }

        // ---- Gun firing rate ----

        [Test]
        public void VickersTriggerHeldOneSecondFiresAboutSevenRounds()
        {
            var spec = new GunSpec
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
            var gun = new Gun(spec);
            int emitted = 0;
            const double dt = 1.0 / 120.0;
            for (int i = 0; i < 120; i++)
            {
                var shot = gun.Tick(dt, triggerHeld: true);
                if (shot.HasValue) emitted++;
            }
            // Effective RPM = 450 * 0.92 = 414 → ~6.9 rounds/s → 6 or 7
            // emissions in 1 simulated second depending on phase alignment.
            Assert.That(emitted, Is.InRange(6, 7),
                $"expected 6–7 rounds in 1 s of trigger-held Vickers, got {emitted}");
            Assert.AreEqual(500 - emitted, gun.Rounds);
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Weapons.Tests
{
    public class BallisticsTests
    {
        static Bounds Box(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            Bounds b = new Bounds();
            b.SetMinMax(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
            return b;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        [Test]
        public void RayAABBHitsUnitCubeAtExpectedTAlongPlusX()
        {
            double t = Ballistics.RayAABB(V(-5, 0, 0), V(1, 0, 0), Box(0, -1, -1, 1, 1, 1));
            Assert.AreEqual(5.0, t, 5e-7);
        }

        [Test]
        public void RayAABBReturnsInfinityWhenRayIsParallelAndOffset()
        {
            double t = Ballistics.RayAABB(V(0, 5, 0), V(1, 0, 0), Box(0, -1, -1, 10, 1, 1));
            Assert.AreEqual(double.PositiveInfinity, t);
        }

        [Test]
        public void RayAABBReturnsInfinityWhenRayPointsAwayFromBox()
        {
            double t = Ballistics.RayAABB(V(-5, 0, 0), V(-1, 0, 0), Box(0, -1, -1, 1, 1, 1));
            Assert.AreEqual(double.PositiveInfinity, t);
        }

        [Test]
        public void RayAABBReturnsZeroWhenOriginIsInsideTheBox()
        {
            double t = Ballistics.RayAABB(V(0.5f, 0.5f, 0.5f), V(1, 0, 0), Box(0, 0, 0, 1, 1, 1));
            Assert.AreEqual(0.0, t);
        }

        [Test]
        public void RayAABBHandlesDiagonalRayThroughCenteredCube()
        {
            // First slab boundary at -1, normalised by direction's magnitude per axis.
            double t = Ballistics.RayAABB(V(-2, -2, -2), V(1, 1, 1), Box(-1, -1, -1, 1, 1, 1));
            Assert.AreEqual(1.0, t, 5e-7);
        }

        [Test]
        public void RayAABBReturnsInfinityWhenGrazingMissAboveBox()
        {
            double t = Ballistics.RayAABB(V(-5, 2, 0), V(1, 0, 0), Box(0, -1, -1, 5, 1, 1));
            Assert.AreEqual(double.PositiveInfinity, t);
        }

        [Test]
        public void DamageReturnsFullDamageAtZeroRange()
        {
            Assert.AreEqual(8.0, Ballistics.DamageAtRange(8, 0));
        }

        [Test]
        public void DamageReturnsZeroAtMaxRange()
        {
            Assert.AreEqual(0.0, Ballistics.DamageAtRange(8, Ballistics.MAX_DAMAGE_RANGE_M));
        }

        [Test]
        public void DamageReturnsHalfDamageAtHalfMaxRange()
        {
            Assert.AreEqual(4.0, Ballistics.DamageAtRange(8, Ballistics.MAX_DAMAGE_RANGE_M / 2.0), 5e-7);
        }

        [Test]
        public void DamageClampsToZeroPastMaxRange()
        {
            Assert.AreEqual(0.0, Ballistics.DamageAtRange(8, 1500));
        }

        [Test]
        public void DamageClampsToBaseDamageAtNegativeRange()
        {
            Assert.AreEqual(8.0, Ballistics.DamageAtRange(8, -100));
        }
    }
}

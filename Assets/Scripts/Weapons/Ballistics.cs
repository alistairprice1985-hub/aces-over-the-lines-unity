using System;
using UnityEngine;

namespace AcesOverTheLines.Weapons
{
    // Pure ballistics math.
    //
    // RayAABB(origin, direction, box) — slab method. Returns t (parametric
    // along direction) of the FIRST entry into the box, or +Infinity if no
    // hit. If the origin is inside the box, returns 0. direction does NOT
    // need to be normalised; t is parametric in direction's units.
    //
    // DamageAtRange(baseDamage, rangeM) — brief §6: damage_per_hit *
    // (1 − range/1000), clamped to [0, baseDamage].
    public static class Ballistics
    {
        public const double MAX_DAMAGE_RANGE_M = 1000.0;
        public const double TARGET_DETECTION_RANGE_M = 800.0;

        public static double RayAABB(Vector3 origin, Vector3 direction, Bounds box)
        {
            double tmin = double.NegativeInfinity;
            double tmax = double.PositiveInfinity;
            Vector3 boxMin = box.min;
            Vector3 boxMax = box.max;
            for (int i = 0; i < 3; i++)
            {
                double o    = i == 0 ? origin.x    : i == 1 ? origin.y    : origin.z;
                double d    = i == 0 ? direction.x : i == 1 ? direction.y : direction.z;
                double minB = i == 0 ? boxMin.x    : i == 1 ? boxMin.y    : boxMin.z;
                double maxB = i == 0 ? boxMax.x    : i == 1 ? boxMax.y    : boxMax.z;
                if (Math.Abs(d) < 1e-12)
                {
                    // Ray parallel to this slab — must lie within the slab to
                    // ever hit.
                    if (o < minB || o > maxB) return double.PositiveInfinity;
                }
                else
                {
                    double t1 = (minB - o) / d;
                    double t2 = (maxB - o) / d;
                    double tNear = Math.Min(t1, t2);
                    double tFar  = Math.Max(t1, t2);
                    if (tNear > tmin) tmin = tNear;
                    if (tFar  < tmax) tmax = tFar;
                    if (tmin > tmax) return double.PositiveInfinity;
                }
            }
            if (tmax < 0) return double.PositiveInfinity;
            return Math.Max(0.0, tmin);
        }

        public static double DamageAtRange(double baseDamage, double rangeM, double maxRange = MAX_DAMAGE_RANGE_M)
        {
            if (rangeM <= 0.0) return baseDamage;
            if (rangeM >= maxRange) return 0.0;
            return baseDamage * (1.0 - rangeM / maxRange);
        }
    }
}

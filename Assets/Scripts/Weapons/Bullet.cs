using UnityEngine;

namespace AcesOverTheLines.Weapons
{
    // In-flight bullet. Moves per tick at Velocity, ray-casts the segment
    // origin → end-of-tick against the enemy hitbox layer. Damage at hit
    // is Ballistics.DamageAtRange(BaseDamage, DistanceTraveled). Bullets
    // beyond MAX_RANGE (TARGET_DETECTION_RANGE + 200 m) expire on the
    // next tick. Ports src/weapons/bullet.js.
    public class Bullet
    {
        public Vector3 Position;
        public Vector3 Velocity;          // metres/s, world frame
        public double BaseDamage;
        public bool Tracer;
        public double DistanceTraveled;
        public bool Expired;
        public Vector3 FiringAircraftPos; // snapshot at fire time, for the 800 m detection-range cull
        public GameObject TracerMesh;     // optional visual; WeaponSystem owns the pool

        public const double MAX_RANGE = Ballistics.TARGET_DETECTION_RANGE_M + 200.0;

        public struct HitInfo
        {
            public Collider Collider;
            public IDamageReceiver Receiver; // resolved from collider during Step
            public Vector3 HitPoint;
            public double Damage;
            public double Range;
            public bool Tracer;
        }

        // Step one physics tick. Returns hit info on impact, or null if the
        // bullet missed / is beyond range. Expired is set on hit or after
        // MAX_RANGE is exceeded.
        public HitInfo? Step(double dt, int hitboxLayerMask)
        {
            if (Expired) return null;

            float segLen = (float)(Velocity.magnitude * dt);
            Vector3 dirNorm = Velocity.sqrMagnitude > 0f ? Velocity.normalized : Vector3.forward;

            bool didHit = Physics.Raycast(Position, dirNorm, out RaycastHit hit, segLen, hitboxLayerMask);

            IDamageReceiver receiver = null;
            if (didHit)
            {
                receiver = hit.collider.GetComponent<IDamageReceiver>();
                if (receiver == null)
                {
                    didHit = false;
                }
                else
                {
                    // 800 m detection range, measured from the firing aircraft
                    // to the target's transform — matches JS bullet.tick().
                    Transform target = hit.collider.transform;
                    while (target.parent != null) target = target.parent;
                    float distFromFirer = Vector3.Distance(FiringAircraftPos, target.position);
                    if (distFromFirer > Ballistics.TARGET_DETECTION_RANGE_M)
                    {
                        didHit = false;
                    }
                }
            }

            if (didHit)
            {
                DistanceTraveled += hit.distance;
                Position = hit.point;
                Expired = true;
                if (TracerMesh != null) TracerMesh.transform.position = Position;

                return new HitInfo
                {
                    Collider = hit.collider,
                    Receiver = receiver,
                    HitPoint = Position,
                    Damage = Ballistics.DamageAtRange(BaseDamage, DistanceTraveled),
                    Range = DistanceTraveled,
                    Tracer = Tracer,
                };
            }
            else
            {
                Position += dirNorm * segLen;
                DistanceTraveled += segLen;
                if (TracerMesh != null) TracerMesh.transform.position = Position;
                if (DistanceTraveled > MAX_RANGE) Expired = true;
                return null;
            }
        }
    }
}

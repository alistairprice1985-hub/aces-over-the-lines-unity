using UnityEngine;
using AcesOverTheLines.Flight;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Aircraft
{
    // Per-tick air-to-air collision damage system. Ports core/main.js's
    // applyAirToAirCollision: each FixedUpdate, iterates pairs of aircraft
    // in the scene, checks per-component hitbox AABB intersection, and
    // applies damage proportional to relative velocity.
    //
    // Damage tiers from the JS source:
    //   relSpeed <  30 m/s          → 40% of component max HP per tick
    //   30 ≤ relSpeed < 60 m/s      → 80%
    //   relSpeed ≥ 60 m/s           → 150%   (effectively destruction)
    //
    // Fallback: if the aircraft body envelopes (parent BoxColliders)
    // intersect but no per-component hitbox pair does, apply the
    // tier-fraction damage to engine + left_wing_spar on both aircraft.
    // Matches the JS source's "we definitely collided, distribute
    // realistic component damage" fallback.
    public class AirToAirCollisionSystem : MonoBehaviour
    {
        [SerializeField] float lowSpeedThresholdMs = 30f;
        [SerializeField] float highSpeedThresholdMs = 60f;
        [SerializeField] double lowDamageFraction = 0.40;
        [SerializeField] double mediumDamageFraction = 0.80;
        [SerializeField] double highDamageFraction = 1.50;

        AircraftHitboxes[] _all;

        void Awake()
        {
            _all = FindObjectsByType<AircraftHitboxes>(FindObjectsSortMode.None);
        }

        // Recompute aircraft list at runtime if scene changes (cheap for
        // a 2-aircraft demo).
        public void Rescan()
        {
            _all = FindObjectsByType<AircraftHitboxes>(FindObjectsSortMode.None);
        }

        void FixedUpdate()
        {
            if (_all == null || _all.Length < 2) return;
            for (int i = 0; i < _all.Length; i++)
            {
                for (int j = i + 1; j < _all.Length; j++)
                {
                    CheckPair(_all[i], _all[j]);
                }
            }
        }

        void CheckPair(AircraftHitboxes a, AircraftHitboxes b)
        {
            if (a == null || b == null) return;
            if (a.Hitboxes == null || b.Hitboxes == null) return;

            var aRb = a.GetComponent<Rigidbody>();
            var bRb = b.GetComponent<Rigidbody>();
            if (aRb == null || bRb == null) return;

            float relSpeed = (aRb.linearVelocity - bRb.linearVelocity).magnitude;
            double damageFrac = CollisionDamageFraction(
                relSpeed, lowSpeedThresholdMs, highSpeedThresholdMs,
                lowDamageFraction, mediumDamageFraction, highDamageFraction);

            bool anyHit = false;
            for (int ia = 0; ia < a.Hitboxes.Count; ia++)
            {
                var ha = a.Hitboxes[ia];
                if (ha == null) continue;
                var aColl = ha.GetComponent<BoxCollider>();
                if (aColl == null) continue;
                var aBounds = aColl.bounds;
                int aMax = DamageModel.COMPONENT_HP.TryGetValue(ha.ComponentName, out var aHp) ? aHp : 0;

                for (int ib = 0; ib < b.Hitboxes.Count; ib++)
                {
                    var hb = b.Hitboxes[ib];
                    if (hb == null) continue;
                    var bColl = hb.GetComponent<BoxCollider>();
                    if (bColl == null) continue;
                    if (!aBounds.Intersects(bColl.bounds)) continue;

                    int bMax = DamageModel.COMPONENT_HP.TryGetValue(hb.ComponentName, out var bHp) ? bHp : 0;
                    if (aMax > 0) ha.TakeDamage(aMax * damageFrac);
                    if (bMax > 0) hb.TakeDamage(bMax * damageFrac);
                    anyHit = true;
                }
            }

            if (!anyHit)
            {
                // Fallback: parent envelope check. If the aircraft bodies
                // overlap but no specific hitbox pair did, distribute
                // damage to engine + left_wing_spar on both. The JS source
                // does this so a body-to-body brush still costs something.
                var aBody = a.GetComponent<BoxCollider>();
                var bBody = b.GetComponent<BoxCollider>();
                if (aBody != null && bBody != null && aBody.bounds.Intersects(bBody.bounds))
                {
                    ApplyFallback(a, damageFrac);
                    ApplyFallback(b, damageFrac);
                }
            }
        }

        static void ApplyFallback(AircraftHitboxes h, double frac)
        {
            if (h.Hitboxes == null) return;
            foreach (var box in h.Hitboxes)
            {
                if (box == null) continue;
                if (box.ComponentName == "engine" || box.ComponentName == "left_wing_spar")
                {
                    int max = DamageModel.COMPONENT_HP.TryGetValue(box.ComponentName, out var hp) ? hp : 0;
                    if (max > 0) box.TakeDamage(max * frac);
                }
            }
        }

        // Static testable damage-tier helper. Returns the fraction of
        // component max HP to apply per impacting hitbox pair.
        public static double CollisionDamageFraction(float relSpeed)
        {
            return CollisionDamageFraction(relSpeed, 30f, 60f, 0.40, 0.80, 1.50);
        }

        public static double CollisionDamageFraction(
            float relSpeed,
            float lowSpeedThresholdMs, float highSpeedThresholdMs,
            double lowFrac, double medFrac, double highFrac)
        {
            if (relSpeed < lowSpeedThresholdMs)  return lowFrac;
            if (relSpeed < highSpeedThresholdMs) return medFrac;
            return highFrac;
        }
    }
}

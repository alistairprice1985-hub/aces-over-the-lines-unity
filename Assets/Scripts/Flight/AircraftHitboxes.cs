using System.Collections.Generic;
using UnityEngine;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Flight
{
    // Sibling component on an aircraft GameObject. After AircraftController
    // has built the AircraftEntity, AircraftHitboxes.Initialize(entity) is
    // called to spawn per-component hitbox child GameObjects using
    // DamageModel.BodyFrameHitboxes() for sizing. Each child carries an
    // AircraftHitbox script that routes bullet damage to
    // AircraftEntity.DamageComponent.
    //
    // hitboxLayer is the Unity layer the children are placed on — typically
    // "PlayerHitbox" for the player and "EnemyHitbox" for the AI, so the two
    // sides' WeaponSystems raycast against each other's layer and avoid
    // friendly-fire.
    public class AircraftHitboxes : MonoBehaviour
    {
        [SerializeField] string hitboxLayer = "PlayerHitbox";

        bool _initialized;
        readonly List<AircraftHitbox> _hitboxes = new List<AircraftHitbox>();

        public IReadOnlyList<AircraftHitbox> Hitboxes => _hitboxes;

        public void Initialize(AircraftEntity entity)
        {
            if (_initialized) return;
            if (entity == null) return;
            _initialized = true;

            int layer = LayerMask.NameToLayer(hitboxLayer);
            if (layer < 0) layer = 0;

            var hitboxBounds = DamageModel.BodyFrameHitboxes();
            foreach (var kvp in hitboxBounds)
            {
                var child = new GameObject("Hitbox_" + kvp.Key);
                child.transform.SetParent(transform, worldPositionStays: false);
                child.transform.localPosition = kvp.Value.center;
                child.transform.localRotation = Quaternion.identity;
                child.layer = layer;

                var box = child.AddComponent<BoxCollider>();
                box.size = kvp.Value.size;

                var hb = child.AddComponent<AircraftHitbox>();
                hb.Init(entity, kvp.Key);
                _hitboxes.Add(hb);
            }
        }
    }
}

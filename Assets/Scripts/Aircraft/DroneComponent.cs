using UnityEngine;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Aircraft
{
    // Attached to each per-component hitbox child of a Drone. Implements
    // IDamageReceiver so the bullet system (Weapons assembly) can apply
    // damage without a direct type reference to Drone/Aircraft. The bullet
    // raycast-hits this collider, gets the IDamageReceiver via
    // GetComponent, and calls TakeDamage() — which routes back to the
    // owning Drone.
    public class DroneComponent : MonoBehaviour, IDamageReceiver
    {
        public Drone Owner { get; private set; }
        public string ComponentName { get; private set; }

        public void Init(Drone owner, string componentName)
        {
            Owner = owner;
            ComponentName = componentName;
        }

        public DamageInfo TakeDamage(double damage)
        {
            if (Owner == null) return default;
            return Owner.DamageComponent(ComponentName, damage);
        }
    }
}

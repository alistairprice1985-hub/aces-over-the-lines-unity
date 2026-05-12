using UnityEngine;

namespace AcesOverTheLines.Aircraft
{
    // Attached to each per-component hitbox child of a Drone. The bullet
    // collider system (Stage 4h) raycast-hits these, reads the component
    // name, and calls Damage() to apply HP loss + hit flash on the owner.
    public class DroneComponent : MonoBehaviour
    {
        public Drone Owner { get; private set; }
        public string ComponentName { get; private set; }

        public void Init(Drone owner, string componentName)
        {
            Owner = owner;
            ComponentName = componentName;
        }

        public Drone.DamageResult Damage(double dmg)
        {
            return Owner != null ? Owner.DamageComponent(ComponentName, dmg)
                                 : new Drone.DamageResult { Applied = 0.0, Destroyed = false };
        }
    }
}

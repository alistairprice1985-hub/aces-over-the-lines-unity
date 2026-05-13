using UnityEngine;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Flight
{
    // Per-component hitbox child of an aircraft. Implements IDamageReceiver
    // so the bullet system can resolve a Physics.Raycast hit on the hitbox
    // collider to an AircraftEntity component name and apply damage.
    //
    // Parallel to Aircraft.DroneComponent (which forwards to a Drone's
    // static damage state). The choice between AircraftHitbox and
    // DroneComponent is made at spawn time by AircraftHitboxes (for flying
    // aircraft using AircraftEntity) vs Drone.SpawnHitboxes (for static
    // target drones with their own damage state).
    public class AircraftHitbox : MonoBehaviour, IDamageReceiver
    {
        public AircraftEntity Owner { get; private set; }
        public string ComponentName { get; private set; }

        public void Init(AircraftEntity owner, string componentName)
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

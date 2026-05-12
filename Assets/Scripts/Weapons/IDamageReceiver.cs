namespace AcesOverTheLines.Weapons
{
    // Result of applying damage to a target component. Applied is the actual
    // damage taken (clamped to remaining HP); Destroyed is true only on the
    // transition tick where the owning entity first becomes destroyed.
    public struct DamageInfo
    {
        public double Applied;
        public bool Destroyed;
    }

    // Implemented by any component that can be hit by a bullet. The bullet
    // system looks up implementations via GetComponent<IDamageReceiver>() on
    // the hit collider — this lets the Weapons assembly stay decoupled from
    // the Aircraft assembly that owns the actual damage state (Drone etc.).
    public interface IDamageReceiver
    {
        string ComponentName { get; }
        DamageInfo TakeDamage(double damage);
    }
}

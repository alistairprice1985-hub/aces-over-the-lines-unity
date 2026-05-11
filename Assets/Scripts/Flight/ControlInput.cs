namespace AcesOverTheLines.Flight
{
    // Snapshot of pilot control inputs for one tick. Elevator / Aileron /
    // Rudder are in [-1, 1]; Throttle is in [0, 1] (clamped on read). Fire
    // is the trigger state for the weapon system.
    public struct ControlInput
    {
        public double Elevator;
        public double Aileron;
        public double Rudder;
        public double Throttle;
        public bool Fire;
    }
}

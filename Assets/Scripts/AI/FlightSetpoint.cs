namespace AcesOverTheLines.AI
{
    // Desired attitude and airspeed produced by the tactical FSM and
    // consumed by the inner-loop FlightStabilizer. Replaces the direct
    // ControlInput emission that the FSM used through rounds 4a–4h —
    // states no longer command control surface deflections.
    public struct FlightSetpoint
    {
        public double DesiredBankRad;     // target roll, +ve = right wing down
        public double DesiredPitchRad;    // target pitch (nose above horizon), +ve = nose up
        public double DesiredAirspeedMs;  // target forward airspeed
        public bool Fire;                 // weapons trigger pass-through
    }
}

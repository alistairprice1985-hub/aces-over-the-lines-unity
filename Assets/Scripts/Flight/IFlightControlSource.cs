namespace AcesOverTheLines.Flight
{
    // Source of per-tick pilot input. AircraftController auto-discovers an
    // implementation on the same GameObject via GetComponent<>. Interface
    // lookup works in Unity even though Flight does not reference Input.
    public interface IFlightControlSource
    {
        ControlInput ReadControls(double dt);
    }
}

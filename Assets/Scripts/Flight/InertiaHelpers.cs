namespace AcesOverTheLines.Flight
{
    public static class InertiaHelpers
    {
        public struct Inertia
        {
            public double Ixx; // roll  (about body +x)
            public double Iyy; // yaw   (about body +y)
            public double Izz; // pitch (about body +z)
        }

        // Diagonal inertia tensor for a uniform-density rectangular block.
        // Convention matches the JS sim (+x forward, +y up, +z right) so that
        // Ixx is roll inertia, Iyy is yaw inertia, Izz is pitch inertia.
        public static Inertia BlockInertia(double mass, double lengthM, double spanM, double heightM)
        {
            return new Inertia
            {
                Ixx = mass * (spanM   * spanM   + heightM * heightM) / 12.0,
                Iyy = mass * (lengthM * lengthM + spanM   * spanM  ) / 12.0,
                Izz = mass * (lengthM * lengthM + heightM * heightM) / 12.0,
            };
        }
    }
}

using System;

namespace AcesOverTheLines.Flight
{
    // ISA-style atmosphere helper used by the flight model.
    //
    // Brief §5: ρ(h) = 1.225 · (1 − 0.0000226·h)^4.256, capped at 0 above 11,000 m.
    //
    // The base formula already returns ~0.36 kg/m³ at the troposphere ceiling
    // (11 km), so the cap is a hard floor above that altitude rather than a
    // realistic stratosphere model. WW1 aircraft never reached high enough for
    // this to matter (highest service ceiling in the roster is 6,095 m).
    public static class Atmosphere
    {
        public const double RHO_SEA_LEVEL = 1.225;
        public const double TROPOPAUSE_M = 11000.0;

        public static double AirDensity(double altitudeMetres)
        {
            if (altitudeMetres >= TROPOPAUSE_M) return 0.0;
            if (altitudeMetres <= 0.0) return RHO_SEA_LEVEL;
            double factor = 1.0 - 0.0000226 * altitudeMetres;
            if (factor <= 0.0) return 0.0;
            return RHO_SEA_LEVEL * Math.Pow(factor, 4.256);
        }

        public static double MsToKmh(double v) => v * 3.6;
        public static double KmhToMs(double v) => v / 3.6;
    }
}

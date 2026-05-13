using System;
using UnityEngine;

namespace AcesOverTheLines.UI
{
    // Unit conversions + heading math for the HUD. Ports the helpers and
    // constants from src/ui/hud.js (M_S_TO_MPH, M_TO_FT, headingDegFromQuat).
    public static class HudMath
    {
        public const double M_S_TO_MPH = 2.2369362;
        public const double M_TO_FT    = 3.2808399;

        // Heading in degrees (0–360, 0 = north / world −Z) from an aircraft
        // body-orientation quaternion (+x forward, +y up, +z right body).
        // Body forward = (1, 0, 0) rotated by q. Heading = atan2(east, north)
        // = atan2(fx, -fz). Matches the JS port line for line.
        public static double HeadingDegFromQuat(Quaternion q)
        {
            double fx = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);
            double fz = 2.0 * (q.x * q.z - q.w * q.y);
            double h = Math.Atan2(fx, -fz) * 180.0 / Math.PI;
            if (h < 0.0) h += 360.0;
            return h;
        }
    }
}

using System;
using UnityEngine;

namespace AcesOverTheLines.Flight
{
    // Aerodynamics: Cl/Cd as a function of angle-of-attack and aircraft geometry.
    //
    // Brief §5 specifies:
    //   - Cl_α = 2π · AR / (AR + 2)            (lifting-line correction)
    //   - Cl rises linearly to α_stall (15°), then linearly decays to Cl_max·0.4
    //     by 25°.
    //   - Cd = Cd_0 + Cl² / (π · AR · e)       (parasite + induced)
    //   - Lift = 0.5 ρ V² S Cl, perpendicular to velocity in the lift plane.
    //   - Drag = 0.5 ρ V² S Cd, opposite velocity.
    //
    // Our convention is +y body-up, so the lift plane is the body x-y plane and
    // AoA α is the angle between body +x and the velocity vector projected onto
    // that plane.
    public static class Aerodynamics
    {
        public struct AirflowResult
        {
            public double alpha;
            public double beta;
            public double speed;
            public Vector3 vBody;
        }

        public struct ForceResult
        {
            public Vector3 lift;
            public Vector3 drag;
            public double L;
            public double D;
        }

        public static double ClAlphaPerRad(double aspectRatio)
        {
            return (2.0 * Math.PI * aspectRatio) / (aspectRatio + 2.0);
        }

        // Returns Cl as a function of α (radians). α can be negative.
        //
        // Brief §5 specifies the curve up to α_post_stall (25°) explicitly. Beyond
        // that we taper Cl smoothly toward zero by 90° and hold it near zero out
        // to 180°, so deep-stall attitudes do not produce spurious lift (see
        // DEFECTS S2-002). The taper is documented in README under "Deviations
        // from brief".
        public static double LiftCoefficient(double alphaRad, double aspectRatio, double ClMax, double alphaStall, double alphaPostStall)
        {
            double Cla = ClAlphaPerRad(aspectRatio);
            double a = alphaRad;
            double sign = a < 0 ? -1.0 : 1.0;
            double aMag = Math.Abs(a);
            if (aMag <= alphaStall)
            {
                return sign * Math.Min(Cla * aMag, ClMax);
            }
            double ClStall = Math.Min(Cla * alphaStall, ClMax);
            double ClPost = ClMax * 0.4;
            if (aMag <= alphaPostStall)
            {
                double t = (aMag - alphaStall) / (alphaPostStall - alphaStall);
                return sign * (ClStall + (ClPost - ClStall) * t);
            }
            // Brief-faithful piece ends at alphaPostStall. From here to π/2 we
            // taper linearly to 0; past π/2 (flying tail-first) Cl is taken to
            // be ~0.
            double halfPi = Math.PI / 2.0;
            if (aMag <= halfPi)
            {
                double t = (aMag - alphaPostStall) / (halfPi - alphaPostStall);
                return sign * ClPost * (1.0 - t);
            }
            return 0.0;
        }

        public static double DragCoefficient(double Cl, double Cd0, double aspectRatio, double oswald)
        {
            return Cd0 + (Cl * Cl) / (Math.PI * aspectRatio * oswald);
        }

        // Compute angle-of-attack and sideslip given world velocity and orientation.
        //
        //   alpha = angle between body +x and velocity, projected onto body x-y
        //           plane. Positive when nose is above the velocity vector
        //           (i.e. body-y component of velocity is negative).
        //   beta  = sideslip; angle between body +x and velocity projected onto
        //           body x-z plane. Positive when velocity comes from the right
        //           (body z component positive).
        public static AirflowResult Airflow(Vector3 velocityWorld, Quaternion orientation)
        {
            Vector3 vBody = Quaternion.Inverse(orientation) * velocityWorld;
            double speed = velocityWorld.magnitude;
            if (speed < 1e-3)
            {
                return new AirflowResult { alpha = 0.0, beta = 0.0, speed = 0.0, vBody = vBody };
            }
            double alpha = Math.Atan2(-vBody.y, vBody.x);
            double beta = Math.Atan2(vBody.z, Math.Sqrt((double)vBody.x * vBody.x + (double)vBody.y * vBody.y));
            return new AirflowResult { alpha = alpha, beta = beta, speed = speed, vBody = vBody };
        }

        // Lift direction: perpendicular to velocity, in the body x-y plane (the
        // vertical plane of the aircraft). We get it as cross(body_right, vHat)
        // so it points along the body +y direction when AoA is positive.
        public static ForceResult LiftDragForces(Vector3 velocityWorld, Quaternion orientation, double density, double area, double Cl, double Cd)
        {
            double speed2 = velocityWorld.sqrMagnitude;
            if (speed2 < 1e-6 || density <= 0.0)
            {
                return new ForceResult { lift = Vector3.zero, drag = Vector3.zero, L = 0.0, D = 0.0 };
            }
            double speed = Math.Sqrt(speed2);
            double q = 0.5 * density * speed2;
            double L = q * area * Cl;
            double D = q * area * Cd;

            Vector3 vHat = velocityWorld * (float)(1.0 / speed);
            Vector3 bodyRightWorld = orientation * new Vector3(0f, 0f, 1f);
            Vector3 liftDir = Vector3.Cross(bodyRightWorld, vHat);
            double liftDirLen = liftDir.magnitude;
            if (liftDirLen < 1e-6)
            {
                // Velocity parallel to right-wing (extreme sideslip); skip lift.
                return new ForceResult { lift = Vector3.zero, drag = vHat * (float)(-D), L = 0.0, D = D };
            }
            liftDir = liftDir * (float)(1.0 / liftDirLen);
            Vector3 lift = liftDir * (float)L;
            Vector3 drag = vHat * (float)(-D);
            return new ForceResult { lift = lift, drag = drag, L = L, D = D };
        }
    }
}

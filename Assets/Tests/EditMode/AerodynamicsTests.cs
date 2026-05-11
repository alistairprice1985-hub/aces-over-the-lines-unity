using System;
using NUnit.Framework;
using AcesOverTheLines.Flight;

namespace AcesOverTheLines.Flight.Tests
{
    public class AerodynamicsTests
    {
        const double AR = 4.0;
        const double Cl_max = 1.3;
        static readonly double stall = (15.0 * Math.PI) / 180.0;
        static readonly double post = (25.0 * Math.PI) / 180.0;

        [Test]
        public void ClAlphaScalesWithAspectRatioPerLiftingLineTheory()
        {
            double cla4 = Aerodynamics.ClAlphaPerRad(4);
            double cla8 = Aerodynamics.ClAlphaPerRad(8);
            // Both are below 2π and increase with AR.
            Assert.Less(cla4, 2.0 * Math.PI);
            Assert.Less(cla8, 2.0 * Math.PI);
            Assert.Greater(cla8, cla4);
        }

        [Test]
        public void ClIsZeroAtAlphaZeroAndRisesLinearlyUntilStall()
        {
            Assert.AreEqual(0.0, Aerodynamics.LiftCoefficient(0, AR, Cl_max, stall, post));
            double cla = Aerodynamics.ClAlphaPerRad(AR);
            double small = 0.05;
            Assert.AreEqual(cla * small, Aerodynamics.LiftCoefficient(small, AR, Cl_max, stall, post), 5e-5);
        }

        [Test]
        public void ClDropsPastStallAndReachesPointFourClMaxAtPostStall()
        {
            double peak = Aerodynamics.LiftCoefficient(stall - 0.01, AR, Cl_max, stall, post);
            double atPost = Aerodynamics.LiftCoefficient(post, AR, Cl_max, stall, post);
            Assert.Less(atPost, peak);
            Assert.AreEqual(0.4 * Cl_max, atPost, 5e-6);
        }

        [Test]
        public void ClTapersTowardZeroAtDeepStall()
        {
            // Past π/2 the wing is moving backwards relative to the airflow; brief
            // is silent on this regime, our extension forces lift to zero.
            Assert.AreEqual(0.0, Aerodynamics.LiftCoefficient(Math.PI / 2.0, AR, Cl_max, stall, post), 5e-6);
            Assert.AreEqual(0.0, Aerodynamics.LiftCoefficient(Math.PI - 0.01, AR, Cl_max, stall, post));
            Assert.AreEqual(0.0, Aerodynamics.LiftCoefficient(-Math.PI / 2.0, AR, Cl_max, stall, post), 5e-6);
        }

        [Test]
        public void ClIsOddInAlpha()
        {
            double a = 0.1;
            Assert.AreEqual(
                -Aerodynamics.LiftCoefficient(a, AR, Cl_max, stall, post),
                Aerodynamics.LiftCoefficient(-a, AR, Cl_max, stall, post),
                5e-6);
        }

        [Test]
        public void CdHasParabolicInducedDragTerm()
        {
            double cd0 = 0.03;
            double e = 0.7;
            double Cl = 0.6;
            double expected = cd0 + (Cl * Cl) / (Math.PI * AR * e);
            Assert.AreEqual(expected, Aerodynamics.DragCoefficient(Cl, cd0, AR, e), 5e-9);
        }
    }
}

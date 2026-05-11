using System;
using NUnit.Framework;
using AcesOverTheLines.Flight;

namespace AcesOverTheLines.Flight.Tests
{
    public class AtmosphereTests
    {
        [Test]
        public void ReturnsSeaLevelDensityAtAltitudeZero()
        {
            Assert.AreEqual(Atmosphere.RHO_SEA_LEVEL, Atmosphere.AirDensity(0.0), 1e-5);
        }

        [Test]
        public void DensityDecreasesWithAltitude()
        {
            double r1000 = Atmosphere.AirDensity(1000);
            double r5000 = Atmosphere.AirDensity(5000);
            Assert.Less(r1000, Atmosphere.RHO_SEA_LEVEL);
            Assert.Less(r5000, r1000);
        }

        [Test]
        public void CapsAtZeroAboveTropopause()
        {
            Assert.AreEqual(0.0, Atmosphere.AirDensity(Atmosphere.TROPOPAUSE_M));
            Assert.AreEqual(0.0, Atmosphere.AirDensity(Atmosphere.TROPOPAUSE_M + 1000));
        }

        [Test]
        public void MatchesIsaValueAt5000mWithin5Percent()
        {
            // Standard atmosphere ρ at 5 km ≈ 0.7364 kg/m³
            double r = Atmosphere.AirDensity(5000);
            Assert.Less(Math.Abs(r - 0.7364) / 0.7364, 0.05);
        }
    }
}

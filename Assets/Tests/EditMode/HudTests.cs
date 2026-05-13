using System;
using NUnit.Framework;
using UnityEngine;
using AcesOverTheLines.UI;

namespace AcesOverTheLines.UI.Tests
{
    // Ports the "HUD conversions" and "HUD heading from quaternion"
    // describe blocks from src/flight/atmosphere.test.js — these were
    // skipped during the original atmosphere port (Stage 4a) and land
    // now alongside the HUD code.
    public class HudTests
    {
        // ---- HUD conversions ----

        [Test]
        public void MsToMphConstantMatchesNistPrecision()
        {
            Assert.AreEqual(2.2369362, HudMath.M_S_TO_MPH, 5e-7);
        }

        [Test]
        public void MToFtConstantMatchesNistPrecision()
        {
            Assert.AreEqual(3.2808399, HudMath.M_TO_FT, 5e-7);
        }

        // ---- HUD heading from quaternion ----
        // World convention: +X east, −Z north. Body forward = +X.
        // Identity orientation → body forward = world +X = east → heading 90°.

        [Test]
        public void IdentityQuaternionGivesHeadingEast()
        {
            Assert.AreEqual(90.0, HudMath.HeadingDegFromQuat(Quaternion.identity), 5e-5);
        }

        [Test]
        public void YawPlus90AboutYGivesHeadingNorth()
        {
            // q = setFromAxisAngle(Y, +π/2): body +X rotates to world −Z = north.
            var q = Quaternion.AngleAxis(90f, Vector3.up);
            Assert.AreEqual(0.0, HudMath.HeadingDegFromQuat(q), 5e-5);
        }

        [Test]
        public void YawPlus180AboutYGivesHeadingWest()
        {
            // q = (0, 1, 0, 0): body +X = world −X = west → heading 270°.
            var q = new Quaternion(0f, 1f, 0f, 0f);
            Assert.AreEqual(270.0, HudMath.HeadingDegFromQuat(q), 5e-5);
        }

        [Test]
        public void YawMinus90AboutYGivesHeadingSouth()
        {
            // q = setFromAxisAngle(Y, -π/2): body +X = world +Z = south → heading 180°.
            var q = Quaternion.AngleAxis(-90f, Vector3.up);
            Assert.AreEqual(180.0, HudMath.HeadingDegFromQuat(q), 5e-5);
        }
    }
}

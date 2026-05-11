using System.Collections.Generic;
using UnityEngine;

namespace AcesOverTheLines.Flight
{
    // Aircraft configuration data records. Field names follow brief §4 in
    // PascalCase; comments in AircraftRoster.cs match the JS source.
    //
    // Coordinate convention (body frame, see Aerodynamics.cs):
    //   +x forward, +y up, +z right.
    // GyroTorqueAxis is the direction of engine angular momentum vector H.
    // GyroTorqueMagnitude is |H| in kg·m²/s. For non-rotary engines, the
    // magnitude is 0. EngineTorqueRollPerThrottle is the constant
    // prop-reaction torque about body x at full throttle (negative = roll
    // left).

    public record GunSpec
    {
        public string Type { get; init; }
        public double CaliberMm { get; init; }
        public int Rounds { get; init; }
        public int SpareDrums { get; init; }
        public double ReloadTimeS { get; init; }
        public int RateOfFireRpm { get; init; }
        public double MuzzleVelocityMS { get; init; }
        public int DamagePerHitHp { get; init; }
        public double DispersionRad { get; init; }
        public double JamProbabilityPerRound { get; init; }
        public double ClearJamTimeS { get; init; } = 3.0;
        public bool Synchronised { get; init; }
        public string Position { get; init; }
        public string Mount { get; init; }
    }

    public record LiveryPalette
    {
        public Color Fuselage { get; init; }
        public Color WingTop { get; init; }
        public Color WingBottom { get; init; }
        public Color Accent { get; init; }
    }

    public record AircraftConfig
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string Nation { get; init; }
        public string Role { get; init; }
        public int Year { get; init; }
        public double MassKg { get; init; }
        public double EmptyMassKg { get; init; }
        public double WingAreaM2 { get; init; }
        public double WingSpanM { get; init; }
        public double AspectRatio { get; init; }
        public double ClMax { get; init; }
        public double? ClAlphaPerRad { get; init; }
        public double Cd0 { get; init; }
        public double OswaldE { get; init; }
        public double ThrustMaxN { get; init; }
        public double ThrustCurveAltM { get; init; }
        public double PropEfficiency { get; init; }
        public double RollRateMaxRadS { get; init; }
        public double PitchRateMaxRadS { get; init; }
        public double YawRateMaxRadS { get; init; }
        public Vector3 GyroTorqueAxis { get; init; }
        public double GyroTorqueMagnitude { get; init; }
        public double EngineTorqueRollPerThrottle { get; init; }
        public double StructuralGLimit { get; init; }
        public double StructuralGDurationS { get; init; }
        public string StructuralFailureMode { get; init; }
        public IReadOnlyList<GunSpec> Guns { get; init; }
        public double PilotArmourFactor { get; init; }
        public double FuelCapacityKg { get; init; }
        public double FuelBurnKgS { get; init; }
        public bool HasRearGunner { get; init; }
        public double LowSpeedBonus { get; init; }
        public string HandlingNotes { get; init; }
        public double TopSpeedKmh { get; init; }
        public double ServiceCeilingM { get; init; }
        public double InitialClimbMPerMin { get; init; }
        public string LiveryMarking { get; init; }
        public LiveryPalette LiveryPalette { get; init; }
        public string GeometryKind { get; init; }

        // Derived at roster build time.
        public double TopSpeedMS { get; init; }
        public bool IsRotary { get; init; }
        public double AlphaStallRad { get; init; }
        public double AlphaPostStallRad { get; init; }
    }
}

// init-only setters need IsExternalInit; provide a stub for .NET Standard
// 2.1 (Unity's default target) so the compiler accepts record types.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AcesOverTheLines.Flight
{
    // Numerical values follow historical references; calibration choices
    // (e.g. translating engine power to ThrustMaxN) are noted in README
    // under "Deviations from brief".
    public static class AircraftRoster
    {
        // ---- Reusable gun specs ----

        static readonly GunSpec VICKERS_303 = new GunSpec
        {
            Type = "Vickers .303",
            CaliberMm = 7.7,
            Rounds = 500,
            RateOfFireRpm = 450,
            MuzzleVelocityMS = 820,
            DamagePerHitHp = 8,
            DispersionRad = 0.003,
            JamProbabilityPerRound = 0.00005,
            ClearJamTimeS = 3.0,
            Synchronised = true,
            Position = "nose",
        };

        static readonly GunSpec SPANDAU_792 = VICKERS_303 with
        {
            Type = "Spandau LMG 08/15",
            CaliberMm = 7.92,
            RateOfFireRpm = 500,
            MuzzleVelocityMS = 893,
            DamagePerHitHp = 9,
        };

        static readonly GunSpec LEWIS_303_FOSTER = new GunSpec
        {
            Type = "Lewis .303 (Foster mount)",
            CaliberMm = 7.7,
            Rounds = 97,
            SpareDrums = 1,
            ReloadTimeS = 4.0,
            RateOfFireRpm = 550,
            MuzzleVelocityMS = 740,
            DamagePerHitHp = 8,
            DispersionRad = 0.0035,
            JamProbabilityPerRound = 0.00006,
            ClearJamTimeS = 3.0,
            Synchronised = false,
            Position = "upper_wing",
        };

        static readonly GunSpec LEWIS_SCARFF_REAR = LEWIS_303_FOSTER with
        {
            Type = "Lewis .303 (Scarff rear)",
            Rounds = 97,
            SpareDrums = 3,
            Position = "rear_scarff",
        };

        // ---- Helpers ----

        static double Ar(double span, double area) => (span * span) / area;

        static Color FromHex(int hex) =>
            new Color(
                ((hex >> 16) & 0xFF) / 255.0f,
                ((hex >>  8) & 0xFF) / 255.0f,
                ( hex        & 0xFF) / 255.0f,
                1.0f);

        // ---- Roster ----

        static readonly IReadOnlyDictionary<string, AircraftConfig> AIRCRAFT = BuildRoster();
        static readonly IReadOnlyList<string> AIRCRAFT_IDS = AIRCRAFT.Keys.ToList();

        public static AircraftConfig GetAircraftConfig(string id)
        {
            if (!AIRCRAFT.TryGetValue(id, out var ac))
                throw new ArgumentException($"Unknown aircraft id: {id}");
            return ac;
        }

        public static IReadOnlyList<string> ListAircraftIds() => AIRCRAFT_IDS;

        static IReadOnlyDictionary<string, AircraftConfig> BuildRoster()
        {
            var raw = new Dictionary<string, AircraftConfig>
            {
                ["sopwith_camel"] = new AircraftConfig
                {
                    Name = "Sopwith Camel",
                    Nation = "Entente",
                    Role = "fighter",
                    Year = 1917,
                    MassKg = 659,
                    EmptyMassKg = 422,
                    WingAreaM2 = 21.5,
                    WingSpanM = 8.53,
                    AspectRatio = Ar(8.53, 21.5),
                    ClMax = 1.30,
                    ClAlphaPerRad = null,            // computed by model from AR
                    Cd0 = 0.034,
                    OswaldE = 0.72,
                    ThrustMaxN = 2300,               // Clerget 130 hp; static thrust calibrated to top-speed level flight
                    ThrustCurveAltM = 4500,          // thrust falls off ~linearly to 0.5× at this altitude
                    PropEfficiency = 0.72,
                    RollRateMaxRadS = 2.6,
                    PitchRateMaxRadS = 1.6,
                    YawRateMaxRadS = 1.2,
                    GyroTorqueAxis = new Vector3(-1, 0, 0),  // Clerget rotary spins clockwise viewed from front → H along −x
                    GyroTorqueMagnitude = 320,
                    EngineTorqueRollPerThrottle = 0,         // rotary; no constant prop torque (the gyro does it)
                    StructuralGLimit = 6.0,
                    StructuralGDurationS = 3.0,
                    Guns = new[]
                    {
                        VICKERS_303 with { Mount = "left"  },
                        VICKERS_303 with { Mount = "right" },
                    },
                    PilotArmourFactor = 0.0,
                    FuelCapacityKg = 30,
                    FuelBurnKgS = 0.0042,
                    HandlingNotes = "Strong gyroscopic torque to the right; rookie AI may spin.",
                    TopSpeedKmh = 185,
                    ServiceCeilingM = 5800,
                    InitialClimbMPerMin = 305,
                    LiveryMarking = "rfc_roundel",
                    LiveryPalette = new LiveryPalette
                    {
                        Fuselage   = FromHex(0xb6a25a),
                        WingTop    = FromHex(0xc9b56a),
                        WingBottom = FromHex(0xe8d49b),
                        Accent     = FromHex(0x1f1f1f),
                    },
                    GeometryKind = "sopwith_camel",
                },

                ["se5a"] = new AircraftConfig
                {
                    Name = "SE5a",
                    Nation = "Entente",
                    Role = "fighter",
                    Year = 1917,
                    MassKg = 902,
                    EmptyMassKg = 639,
                    WingAreaM2 = 22.7,
                    WingSpanM = 8.11,
                    AspectRatio = Ar(8.11, 22.7),
                    ClMax = 1.25,
                    Cd0 = 0.030,
                    OswaldE = 0.75,
                    ThrustMaxN = 3200,               // Hispano-Suiza / Wolseley Viper 200 hp
                    ThrustCurveAltM = 4800,
                    PropEfficiency = 0.78,
                    RollRateMaxRadS = 2.0,
                    PitchRateMaxRadS = 1.5,
                    YawRateMaxRadS = 1.1,
                    GyroTorqueAxis = new Vector3(0, 0, 0),
                    GyroTorqueMagnitude = 0,
                    EngineTorqueRollPerThrottle = -45,
                    StructuralGLimit = 6.5,
                    StructuralGDurationS = 4.0,
                    Guns = new[]
                    {
                        VICKERS_303 with { Mount = "nose", Rounds = 400 },
                        LEWIS_303_FOSTER,
                    },
                    PilotArmourFactor = 0.0,
                    FuelCapacityKg = 50,
                    FuelBurnKgS = 0.0058,
                    HandlingNotes = "Stable gun platform, strong in the dive.",
                    TopSpeedKmh = 222,
                    ServiceCeilingM = 5185,
                    InitialClimbMPerMin = 250,
                    LiveryMarking = "rfc_roundel",
                    LiveryPalette = new LiveryPalette
                    {
                        Fuselage   = FromHex(0x9f8853),
                        WingTop    = FromHex(0x8c7a4a),
                        WingBottom = FromHex(0xcfbf8a),
                        Accent     = FromHex(0x141414),
                    },
                    GeometryKind = "se5a",
                },

                ["bristol_f2b"] = new AircraftConfig
                {
                    Name = "Bristol F2B",
                    Nation = "Entente",
                    Role = "two_seat_fighter",
                    Year = 1917,
                    MassKg = 1474,
                    EmptyMassKg = 975,
                    WingAreaM2 = 37.6,
                    WingSpanM = 11.96,
                    AspectRatio = Ar(11.96, 37.6),
                    ClMax = 1.30,
                    Cd0 = 0.034,
                    OswaldE = 0.72,
                    ThrustMaxN = 4400,               // Rolls-Royce Falcon III 275 hp
                    ThrustCurveAltM = 4800,
                    PropEfficiency = 0.78,
                    RollRateMaxRadS = 1.5,
                    PitchRateMaxRadS = 1.2,
                    YawRateMaxRadS = 0.9,
                    GyroTorqueAxis = new Vector3(0, 0, 0),
                    GyroTorqueMagnitude = 0,
                    EngineTorqueRollPerThrottle = -65,
                    StructuralGLimit = 5.5,
                    StructuralGDurationS = 4.0,
                    Guns = new[]
                    {
                        VICKERS_303 with { Mount = "nose" },
                        LEWIS_SCARFF_REAR,
                    },
                    PilotArmourFactor = 0.0,
                    FuelCapacityKg = 75,
                    FuelBurnKgS = 0.0078,
                    HandlingNotes = "Two-seater; rear gunner is an AI defensive turret.",
                    TopSpeedKmh = 198,
                    ServiceCeilingM = 5485,
                    InitialClimbMPerMin = 230,
                    HasRearGunner = true,
                    LiveryMarking = "rfc_roundel",
                    LiveryPalette = new LiveryPalette
                    {
                        Fuselage   = FromHex(0x8a7644),
                        WingTop    = FromHex(0x7a6a44),
                        WingBottom = FromHex(0xcfbf8a),
                        Accent     = FromHex(0x141414),
                    },
                    GeometryKind = "bristol_f2b",
                },

                ["fokker_dr1"] = new AircraftConfig
                {
                    Name = "Fokker Dr.I",
                    Nation = "Central",
                    Role = "fighter",
                    Year = 1917,
                    MassKg = 586,
                    EmptyMassKg = 406,
                    WingAreaM2 = 18.7,               // total area of the three wings
                    WingSpanM = 7.19,
                    AspectRatio = Ar(7.19, 18.7),
                    ClMax = 1.45,                    // three wings + thick aerofoil
                    Cd0 = 0.040,                     // three wings = more parasite drag
                    OswaldE = 0.70,
                    ThrustMaxN = 2050,               // Oberursel 110 hp
                    ThrustCurveAltM = 4500,
                    PropEfficiency = 0.70,
                    RollRateMaxRadS = 3.2,           // best turn rate in roster
                    PitchRateMaxRadS = 2.0,
                    YawRateMaxRadS = 1.4,
                    GyroTorqueAxis = new Vector3(-1, 0, 0),
                    GyroTorqueMagnitude = 290,
                    EngineTorqueRollPerThrottle = 0,
                    StructuralGLimit = 5.5,
                    StructuralGDurationS = 3.0,
                    Guns = new[]
                    {
                        SPANDAU_792 with { Mount = "left"  },
                        SPANDAU_792 with { Mount = "right" },
                    },
                    PilotArmourFactor = 0.0,
                    FuelCapacityKg = 35,
                    FuelBurnKgS = 0.0040,
                    HandlingNotes = "Slow but turns inside almost everything.",
                    TopSpeedKmh = 165,
                    ServiceCeilingM = 6095,
                    InitialClimbMPerMin = 340,
                    LiveryMarking = "german_cross",
                    LiveryPalette = new LiveryPalette
                    {
                        Fuselage   = FromHex(0xa83a3a),
                        WingTop    = FromHex(0xa83a3a),
                        WingBottom = FromHex(0xe0c89a),
                        Accent     = FromHex(0x1a1a1a),
                    },
                    GeometryKind = "fokker_dr1",
                },

                ["albatros_d3"] = new AircraftConfig
                {
                    Name = "Albatros D.III",
                    Nation = "Central",
                    Role = "fighter",
                    Year = 1916,
                    MassKg = 886,
                    EmptyMassKg = 695,
                    WingAreaM2 = 20.5,
                    WingSpanM = 9.05,
                    AspectRatio = Ar(9.05, 20.5),
                    ClMax = 1.20,
                    Cd0 = 0.030,
                    OswaldE = 0.70,
                    ThrustMaxN = 2900,               // Mercedes D.IIIa 175 hp
                    ThrustCurveAltM = 4500,
                    PropEfficiency = 0.76,
                    RollRateMaxRadS = 2.0,
                    PitchRateMaxRadS = 1.4,
                    YawRateMaxRadS = 1.0,
                    GyroTorqueAxis = new Vector3(0, 0, 0),
                    GyroTorqueMagnitude = 0,
                    EngineTorqueRollPerThrottle = -55,
                    StructuralGLimit = 4.5,          // famous lower-wing weakness
                    StructuralGDurationS = 0.8,
                    StructuralFailureMode = "lower_wing_separation_in_dive",
                    Guns = new[]
                    {
                        SPANDAU_792 with { Mount = "left"  },
                        SPANDAU_792 with { Mount = "right" },
                    },
                    PilotArmourFactor = 0.0,
                    FuelCapacityKg = 45,
                    FuelBurnKgS = 0.0052,
                    HandlingNotes = "Lower wing fails after >4.5g for >0.8s in a dive.",
                    TopSpeedKmh = 175,
                    ServiceCeilingM = 5500,
                    InitialClimbMPerMin = 230,
                    LiveryMarking = "german_cross",
                    LiveryPalette = new LiveryPalette
                    {
                        Fuselage   = FromHex(0x6f7d4a),
                        WingTop    = FromHex(0x4a5236),
                        WingBottom = FromHex(0xe0c89a),
                        Accent     = FromHex(0x1a1a1a),
                    },
                    GeometryKind = "albatros_d3",
                },

                ["fokker_d7"] = new AircraftConfig
                {
                    Name = "Fokker D.VII",
                    Nation = "Central",
                    Role = "fighter",
                    Year = 1918,
                    MassKg = 880,
                    EmptyMassKg = 670,
                    WingAreaM2 = 20.5,
                    WingSpanM = 8.90,
                    AspectRatio = Ar(8.90, 20.5),
                    ClMax = 1.40,                    // forgiving stall
                    Cd0 = 0.031,
                    OswaldE = 0.74,
                    ThrustMaxN = 3050,               // BMW IIIa 185 hp
                    ThrustCurveAltM = 5500,          // famously held power at altitude
                    PropEfficiency = 0.78,
                    RollRateMaxRadS = 2.1,
                    PitchRateMaxRadS = 1.5,
                    YawRateMaxRadS = 1.05,
                    GyroTorqueAxis = new Vector3(0, 0, 0),
                    GyroTorqueMagnitude = 0,
                    EngineTorqueRollPerThrottle = -50,
                    StructuralGLimit = 6.0,
                    StructuralGDurationS = 4.0,
                    Guns = new[]
                    {
                        SPANDAU_792 with { Mount = "left"  },
                        SPANDAU_792 with { Mount = "right" },
                    },
                    PilotArmourFactor = 0.05,
                    FuelCapacityKg = 50,
                    FuelBurnKgS = 0.0055,
                    // "Hung on its propeller" — controllable at low airspeed;
                    // modelled by pulling stall buffet onset 15% closer to
                    // Cl_max (see Aerodynamics.cs).
                    LowSpeedBonus = 0.15,
                    HandlingNotes = "Retains control at low airspeed and high altitude.",
                    TopSpeedKmh = 189,
                    ServiceCeilingM = 6000,
                    InitialClimbMPerMin = 365,
                    LiveryMarking = "german_cross",
                    LiveryPalette = new LiveryPalette
                    {
                        Fuselage   = FromHex(0x4a6a8a),
                        WingTop    = FromHex(0x3a4f6f),
                        WingBottom = FromHex(0xe0c89a),
                        Accent     = FromHex(0x1a1a1a),
                    },
                    GeometryKind = "fokker_d7",
                },
            };

            // Compute derivatives — top speed in m/s, identify rotary types,
            // and bake the stall / post-stall AoA constants. Stamp id from
            // the dictionary key.
            double alphaStall = 15.0 * Math.PI / 180.0;
            double alphaPostStall = 25.0 * Math.PI / 180.0;
            var final = new Dictionary<string, AircraftConfig>();
            foreach (var kvp in raw)
            {
                final[kvp.Key] = kvp.Value with
                {
                    Id = kvp.Key,
                    TopSpeedMS = Atmosphere.KmhToMs(kvp.Value.TopSpeedKmh),
                    IsRotary = kvp.Value.GyroTorqueMagnitude > 0,
                    AlphaStallRad = alphaStall,
                    AlphaPostStallRad = alphaPostStall,
                };
            }
            return final;
        }
    }
}

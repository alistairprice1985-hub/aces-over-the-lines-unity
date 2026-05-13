using System;
using System.Collections.Generic;
using UnityEngine;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Flight
{
    // MonoBehaviour wrapper around AircraftEntity. Attach to a GameObject
    // with a Rigidbody. Reads the aircraft id from the inspector, builds the
    // entity in Awake, and ticks it from FixedUpdate using a stubbed
    // ControlInput. Real input wiring comes when flightInput.js is ported.
    [RequireComponent(typeof(Rigidbody))]
    public class AircraftController : MonoBehaviour
    {
        [SerializeField] string aircraftId = "sopwith_camel";
        [SerializeField] float initialAltitudeM = 1500f;
        [SerializeField] float initialHeadingDeg = 0f;
        [SerializeField] float initialSpeedMs = 0f;
        [SerializeField] bool useStubControls = false;
        [SerializeField, Range(0f, 1f)] float stubThrottle = 0.7f;
        [SerializeField, Range(-1f, 1f)] float stubElevator = 0f;
        [SerializeField, Range(-1f, 1f)] float stubAileron = 0f;
        [SerializeField, Range(-1f, 1f)] float stubRudder = 0f;

        Rigidbody _rb;
        AircraftEntity _entity;
        IFlightControlSource _controlSource;
        WeaponSystem _weaponSystem;

        public AircraftEntity Entity => _entity;
        public AircraftConfig Config => _entity != null ? _entity.Config : AircraftRoster.GetAircraftConfig(aircraftId);
        // Latest ControlInput passed to the flight model. HUD reads this so
        // it doesn't have to call ReadControls() itself (which would advance
        // the smoother state twice per tick).
        public ControlInput LastControls { get; private set; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            // Interface lookup: finds any MonoBehaviour on this GameObject
            // implementing IFlightControlSource (e.g. FlightInput in the
            // AcesOverTheLines.Input assembly). Flight does not reference
            // Input — GetComponent<TInterface>() resolves at runtime.
            _controlSource = GetComponent<IFlightControlSource>();
            var config = AircraftRoster.GetAircraftConfig(aircraftId);
            var pos = new Vector3(0f, initialAltitudeM, 0f);
            double headingRad = initialHeadingDeg * Math.PI / 180.0;
            Vector3? initialVel = null;
            if (initialSpeedMs > 0f)
            {
                // Body-forward in world: heading=0 → +X.
                initialVel = new Vector3(
                    (float)( Math.Cos(headingRad) * initialSpeedMs),
                    0f,
                    (float)(-Math.Sin(headingRad) * initialSpeedMs));
            }
            _entity = new AircraftEntity(config, _rb, position: pos, velocity: initialVel, heading: headingRad);

            // Wire weapons: convert Flight.GunSpec → Weapons.GunSpec and
            // hand the loadout to WeaponSystem. Done here (rather than in
            // WeaponSystem.Awake) to avoid a circular Weapons → Flight
            // asmdef reference for the AircraftConfig type.
            _weaponSystem = GetComponent<WeaponSystem>();
            if (_weaponSystem != null)
            {
                var weaponSpecs = new List<AcesOverTheLines.Weapons.GunSpec>(config.Guns.Count);
                foreach (var g in config.Guns)
                {
                    weaponSpecs.Add(new AcesOverTheLines.Weapons.GunSpec
                    {
                        Type = g.Type,
                        Rounds = g.Rounds,
                        RateOfFireRpm = g.RateOfFireRpm,
                        MuzzleVelocityMS = g.MuzzleVelocityMS,
                        DamagePerHitHp = g.DamagePerHitHp,
                        DispersionRad = g.DispersionRad,
                        JamProbabilityPerRound = g.JamProbabilityPerRound,
                        ClearJamTimeS = g.ClearJamTimeS,
                        Synchronised = g.Synchronised,
                        Mount = g.Mount,
                    });
                }
                _weaponSystem.Initialize(config.GeometryKind, weaponSpecs);
            }
        }

        void FixedUpdate()
        {
            if (_entity == null) return;
            ControlInput controls;
            if (useStubControls || _controlSource == null)
            {
                controls = new ControlInput
                {
                    Elevator = stubElevator,
                    Aileron  = stubAileron,
                    Rudder   = stubRudder,
                    Throttle = stubThrottle,
                };
            }
            else
            {
                controls = _controlSource.ReadControls(Time.fixedDeltaTime);
            }
            LastControls = controls;
            _entity.Update(Time.fixedDeltaTime, controls);
            if (_weaponSystem != null) _weaponSystem.Tick(Time.fixedDeltaTime, controls.Fire);
        }
    }
}

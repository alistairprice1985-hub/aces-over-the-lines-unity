using System;
using UnityEngine;

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
        [SerializeField, Range(0f, 1f)] float stubThrottle = 0.7f;
        [SerializeField, Range(-1f, 1f)] float stubElevator = 0f;
        [SerializeField, Range(-1f, 1f)] float stubAileron = 0f;
        [SerializeField, Range(-1f, 1f)] float stubRudder = 0f;

        Rigidbody _rb;
        AircraftEntity _entity;

        public AircraftEntity Entity => _entity;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
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
        }

        void FixedUpdate()
        {
            if (_entity == null) return;
            var controls = new ControlInput
            {
                Elevator = stubElevator,
                Aileron = stubAileron,
                Rudder = stubRudder,
                Throttle = stubThrottle,
            };
            _entity.Update(Time.fixedDeltaTime, controls);
        }
    }
}

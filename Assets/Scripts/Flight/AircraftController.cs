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
        [SerializeField, Range(0f, 1f)] float stubThrottle = 0.7f;

        Rigidbody _rb;
        AircraftEntity _entity;

        public AircraftEntity Entity => _entity;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            var config = AircraftRoster.GetAircraftConfig(aircraftId);
            var pos = new Vector3(0f, initialAltitudeM, 0f);
            double headingRad = initialHeadingDeg * Math.PI / 180.0;
            _entity = new AircraftEntity(config, _rb, position: pos, heading: headingRad);
        }

        void FixedUpdate()
        {
            if (_entity == null) return;
            var controls = new ControlInput
            {
                Elevator = 0.0,
                Aileron = 0.0,
                Rudder = 0.0,
                Throttle = stubThrottle,
            };
            _entity.Update(Time.fixedDeltaTime, controls);
        }
    }
}

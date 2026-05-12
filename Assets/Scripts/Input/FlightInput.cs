using System;
using UnityEngine;
using UnityEngine.InputSystem;
using AcesOverTheLines.Flight;

namespace AcesOverTheLines.Input
{
    // Flight control input. Reads from the AcesPlay action map (defined in
    // AcesPlay.inputactions, wrapped by AcesPlay.cs) and produces a
    // ControlInput per tick. Mirrors src/input/flightInput.js readControls().
    //
    // Brief §11 keyboard mapping (revised per DEFECTS S2-009):
    //   ArrowUp     pitch nose-down  (push)
    //   ArrowDown   pitch nose-up    (pull)              ← INVERTED on purpose
    //   ArrowLeft   rudder left
    //   ArrowRight  rudder right
    //   A           roll left   (keyboard-only) / rudder left  (hybrid)
    //   D           roll right  (keyboard-only) / rudder right (hybrid)
    //   Shift       throttle up
    //   Ctrl        throttle down
    //   M           toggle mouse mode
    //   Space       fire
    //
    // Joystick (Logitech Extreme 3D Pro), HID generic Joystick layout:
    //   stick/x  (axis 0) → roll
    //   stick/y  (axis 1) → pitch (Unity inverts HID Y so pulled-back > 0
    //                              → nose up; matches JS convention)
    //   twist    (axis 2) → rudder
    //   slider   (axis 3) → throttle absolute — bound but not read, matching
    //                       the JS source (L3DPro reports slider inactive on
    //                       this hardware). Set _readSliderThrottle = true
    //                       to enable.
    //   trigger  (button 0) → fire
    //
    // Mouse mode (DEFECTS S2-005): mouse delta drives a persistent virtual
    // stick. Keyboard pitch/roll are ignored while in mouse mode. Joystick
    // presence bypasses mouse mode entirely.
    //
    // Control smoothing (DEFECTS S2-008): elevator/aileron/rudder ramp
    // linearly from previous applied value toward target at 1/RAMP_TIME_S
    // = 4 axis-units/s, so 0 → ±1 takes 250 ms. Applied to all input
    // sources so tests and gameplay see the same dynamics. Throttle
    // bypasses smoothing.
    public class FlightInput : MonoBehaviour, IFlightControlSource
    {
        public const double RAMP_TIME_S = 0.25;
        const double RAMP_RATE = 1.0 / RAMP_TIME_S;
        const double THROTTLE_RATE = 0.6;
        const float MOUSE_GAIN_PITCH = 0.0035f;
        const float MOUSE_GAIN_ROLL  = 0.0035f;

        [SerializeField] double initialThrottle = 0.7;
        [SerializeField] bool readSliderThrottle = false;

        AcesPlay _actions;

        double _throttle;
        bool _mouseMode;
        double _mouseStickPitch;
        double _mouseStickRoll;
        double _smoothedElevator;
        double _smoothedAileron;
        double _smoothedRudder;
        bool _inputDisabled;

        public bool MouseMode => _mouseMode;
        public double Throttle => _throttle;
        public bool Hybrid => Joystick.current != null;

        // Linear ramp toward target at RAMP_RATE per second. Tests cover this.
        public static double RampTo(double curr, double target, double dt)
        {
            double maxStep = RAMP_RATE * dt;
            double delta = target - curr;
            if (Math.Abs(delta) <= maxStep) return target;
            return curr + Math.Sign(delta) * maxStep;
        }

        void Awake()
        {
            _throttle = initialThrottle;
            _actions = new AcesPlay();
        }

        void OnEnable()
        {
            _actions.Enable();
            _actions.AcesPlayMap.MouseMode.performed += OnMouseModeToggle;
        }

        void OnDisable()
        {
            if (_actions != null)
            {
                _actions.AcesPlayMap.MouseMode.performed -= OnMouseModeToggle;
                _actions.Disable();
            }
        }

        void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
        }

        void OnMouseModeToggle(InputAction.CallbackContext ctx)
        {
            _mouseMode = !_mouseMode;
            if (!_mouseMode)
            {
                _mouseStickPitch = 0;
                _mouseStickRoll = 0;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void Update()
        {
            // Accumulate mouse delta into the virtual stick while in mouse
            // mode. Sign of pitch is intentionally negated relative to
            // delta.y: Unity's mouse delta.y is positive when the cursor
            // moves UP the screen, but moving the mouse forward (cursor up)
            // should match keyboard ArrowUp (push) → nose down → negative
            // elevator. So delta.y > 0 must drive pitch toward -1.
            if (_mouseMode)
            {
                Vector2 d = _actions.AcesPlayMap.MouseDelta.ReadValue<Vector2>();
                _mouseStickPitch = Clamp1(_mouseStickPitch - d.y * MOUSE_GAIN_PITCH);
                _mouseStickRoll  = Clamp1(_mouseStickRoll  + d.x * MOUSE_GAIN_ROLL);
            }
        }

        public ControlInput ReadControls(double dt)
        {
            if (_inputDisabled)
            {
                return new ControlInput { Elevator = 0, Aileron = 0, Rudder = 0, Throttle = 0, Fire = false };
            }

            var a = _actions.AcesPlayMap;

            // Throttle: keyboard accumulation always; optionally read slider
            // absolute (off by default — L3DPro slider is reported inactive
            // on this hardware per the JS source).
            if (a.ThrottleUp.IsPressed())   _throttle = Math.Min(1.0, _throttle + THROTTLE_RATE * dt);
            if (a.ThrottleDown.IsPressed()) _throttle = Math.Max(0.0, _throttle - THROTTLE_RATE * dt);
            if (readSliderThrottle)
            {
                float slider = a.ThrottleAxis.ReadValue<float>();
                // L3DPro slider raw range is [-1, 1] with +1 = forward (full),
                // -1 = back (idle). Match the JS (1 + axis) / 2 mapping.
                _throttle = Math.Max(0.0, Math.Min(1.0, (1.0 + slider) * 0.5));
            }

            bool hasJoystick = Joystick.current != null;
            double targetElevator, targetAileron, targetRudder;
            bool fire;

            if (hasJoystick)
            {
                // Hybrid mode: stick X/Y → roll/pitch, A/D → rudder.
                targetElevator = a.Pitch.ReadValue<float>();
                targetAileron  = a.Roll.ReadValue<float>();
                targetRudder   = a.AD.ReadValue<float>();
                fire = a.Fire.IsPressed();
            }
            else
            {
                // Keyboard-only mode.
                if (_mouseMode)
                {
                    targetElevator = _mouseStickPitch;
                    targetAileron  = _mouseStickRoll;
                }
                else
                {
                    targetElevator = a.Pitch.ReadValue<float>();
                    targetAileron  = a.AD.ReadValue<float>();
                }
                targetRudder = a.Rudder.ReadValue<float>();
                fire = a.Fire.IsPressed();
            }

            targetElevator = Clamp1(targetElevator);
            targetAileron  = Clamp1(targetAileron);
            targetRudder   = Clamp1(targetRudder);

            _smoothedElevator = RampTo(_smoothedElevator, targetElevator, dt);
            _smoothedAileron  = RampTo(_smoothedAileron,  targetAileron,  dt);
            _smoothedRudder   = RampTo(_smoothedRudder,   targetRudder,   dt);

            return new ControlInput
            {
                Elevator = _smoothedElevator,
                Aileron  = _smoothedAileron,
                Rudder   = _smoothedRudder,
                Throttle = _throttle,
                Fire     = fire,
            };
        }

        // Reset smoothing state on respawn so a fresh spawn does not inherit
        // an in-progress ramp.
        public void Reset()
        {
            _smoothedElevator = 0;
            _smoothedAileron = 0;
            _smoothedRudder = 0;
            _mouseStickPitch = 0;
            _mouseStickRoll = 0;
            _inputDisabled = false;
        }

        // Disable / re-enable input — used after a crash.
        public void SetInputDisabled(bool disabled)
        {
            _inputDisabled = disabled;
            if (disabled)
            {
                _smoothedElevator = 0;
                _smoothedAileron = 0;
                _smoothedRudder = 0;
                _mouseStickPitch = 0;
                _mouseStickRoll = 0;
            }
        }

        public void SetThrottle(double t) { _throttle = Math.Max(0.0, Math.Min(1.0, t)); }

        static double Clamp1(double v) => v < -1.0 ? -1.0 : (v > 1.0 ? 1.0 : v);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Flight
{
    // AircraftEntity — combines a Unity Rigidbody, an aerodynamic model, an
    // engine, a stall/spin state, and per-component damage. Update() is
    // called once per physics tick (Time.fixedDeltaTime = 1/120 s) with a
    // ControlInput snapshot. It applies aerodynamic forces and torques, the
    // engine torque, and the gyro torque, then Unity integrates.
    //
    // Coordinate convention: body frame is +x forward, +y up, +z right (the
    // JS sim's convention). transform.rotation is used as the body→world
    // quaternion regardless of Unity's local-axis convention. Forces and
    // torques are applied in WORLD space via Rigidbody.AddForce / AddTorque,
    // with explicit body→world rotation per application.
    //
    // Gravity is delegated to Unity (Rigidbody.useGravity = true) — the JS
    // had its own manual gravity term; we drop it to avoid doubling.
    public class AircraftEntity
    {
        public struct DamageResult
        {
            public double Applied;
            public bool Destroyed;
        }

        public record ComponentStatus
        {
            public bool PilotIncapacitated { get; init; }
            public bool EngineDestroyed { get; init; }
            public bool LeftWingOut { get; init; }
            public bool RightWingOut { get; init; }
            public bool BothWingsOut { get; init; }
            public bool ElevatorOut { get; init; }
            public bool RudderOut { get; init; }
            public bool LeftAileronOut { get; init; }
            public bool RightAileronOut { get; init; }
            public bool FuelTankDestroyed { get; init; }
            public bool FuelFireActive { get; init; }
            public double FuelFireTimer { get; init; }
            public bool AllComponentsZero { get; init; }
        }

        public readonly AircraftConfig Config;
        public readonly Rigidbody Rb;
        public InertiaHelpers.Inertia Inertia;

        public IReadOnlyDictionary<string, DamageModel.ComponentHP> Components => _components;
        readonly Dictionary<string, DamageModel.ComponentHP> _components;

        public double FuelKg;
        public double EngineHealth;
        public bool Stalled;
        public bool Spinning;
        public double SpinRecoverTimer;
        public double TimeAlive;
        public bool Crashed;
        public double CrashSpeedMS;
        public bool FuelFireActive;
        public bool FuelFireExtinguished;
        public double FuelFireTimer;

        readonly Vector3 _H; // gyro angular momentum vector in body frame
        readonly System.Random _rng;
        double _stallBuffetPhase;

        static readonly Vector3 BODY_FORWARD = new Vector3(1f, 0f, 0f);

        public AircraftEntity(AircraftConfig config, Rigidbody rb,
            Vector3? position = null, Vector3? velocity = null, double heading = 0.0,
            System.Random rng = null)
        {
            Config = config;
            Rb = rb;
            _rng = rng ?? new System.Random();

            double lengthM = Math.Max(5.0, config.WingSpanM * 0.75);
            double heightM = Math.Max(2.0, config.WingSpanM * 0.18);
            Inertia = InertiaHelpers.BlockInertia(config.MassKg, lengthM, config.WingSpanM, heightM);

            // Heading: yaw about world +Y by heading radians.
            Quaternion orientation = Quaternion.AngleAxis((float)(heading * 180.0 / Math.PI), Vector3.up);

            Vector3 initPos = position ?? new Vector3(0f, 1500f, 0f);
            Vector3 initVel = velocity ?? new Vector3(
                (float)( Math.Cos(heading) * config.TopSpeedMS * 0.6),
                0f,
                (float)(-Math.Sin(heading) * config.TopSpeedMS * 0.6));

            rb.mass = (float)config.MassKg;
            rb.position = initPos;
            rb.rotation = orientation;
            rb.linearVelocity = initVel;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            // Manual diagonal inertia tensor in body frame; disables Unity's
            // auto-recomputation from colliders.
            rb.inertiaTensorRotation = Quaternion.identity;
            rb.inertiaTensor = new Vector3((float)Inertia.Ixx, (float)Inertia.Iyy, (float)Inertia.Izz);

            FuelKg = config.FuelCapacityKg;
            EngineHealth = 1.0;
            _stallBuffetPhase = 0.0;
            Stalled = false;
            Spinning = false;
            SpinRecoverTimer = 0.0;
            TimeAlive = 0.0;
            Crashed = false;
            CrashSpeedMS = 0.0;

            _components = DamageModel.CreateComponentHPs();
            FuelFireActive = false;
            FuelFireExtinguished = false;
            FuelFireTimer = 0.0;

            _H = new Vector3(
                (float)(config.GyroTorqueAxis.x * config.GyroTorqueMagnitude),
                (float)(config.GyroTorqueAxis.y * config.GyroTorqueMagnitude),
                (float)(config.GyroTorqueAxis.z * config.GyroTorqueMagnitude));
        }

        // Thrust model: linear falloff with altitude, floored at 30% to
        // avoid the flame-out cliff. h_curve is brief §4 thrust_curve_alt_m.
        public static double ThrustAtAltitude(AircraftConfig cfg, double altitudeM, double throttle)
        {
            double h = Math.Max(0.0, altitudeM);
            double factor = Math.Max(0.3, 1.0 - h / cfg.ThrustCurveAltM);
            double t = Math.Max(0.0, Math.Min(1.0, throttle));
            return cfg.ThrustMaxN * cfg.PropEfficiency * factor * t;
        }

        // Smooth ramp from 0 to 1 over [v0, v1] m/s — maps to control authority.
        public static double Authority(double speed, double v0, double v1)
        {
            if (speed <= v0) return 0.0;
            if (speed >= v1) return 1.0;
            return (speed - v0) / (v1 - v0);
        }

        // Stall threshold accounting for low-speed bonus (D.VII trick).
        public static double StallThreshold(AircraftConfig cfg)
        {
            return cfg.AlphaStallRad * (1.0 - cfg.LowSpeedBonus * 0.5);
        }

        public void Update(double dt, ControlInput controls)
        {
            if (Crashed) return;
            TimeAlive += dt;

            // ---- component-loss flags ----
            bool pilotOut     = _components["pilot"].hp           <= 0;
            bool engineOut    = _components["engine"].hp          <= 0;
            bool leftWingOut  = _components["left_wing_spar"].hp  <= 0;
            bool rightWingOut = _components["right_wing_spar"].hp <= 0;
            bool bothWingsOut = leftWingOut && rightWingOut;
            bool elevatorOut  = _components["elevator"].hp        <= 0;
            bool rudderOut    = _components["rudder"].hp          <= 0;
            bool lAileronOut  = _components["left_aileron"].hp    <= 0;
            bool rAileronOut  = _components["right_aileron"].hp   <= 0;

            // Pilot incapacitated: ignore controls; nose-down + slow random
            // roll torques are applied later. Failed individual surfaces
            // zero their corresponding command at the input layer.
            double cmdElevator = pilotOut ? 0.0 : controls.Elevator;
            double cmdAileron  = pilotOut ? 0.0 : controls.Aileron;
            double cmdRudder   = pilotOut ? 0.0 : controls.Rudder;
            double cmdThrottle = pilotOut ? 0.0 : Math.Max(0.0, Math.Min(1.0, controls.Throttle));
            if (elevatorOut) cmdElevator = 0.0;
            if (rudderOut)   cmdRudder   = 0.0;
            // Asymmetric aileron damage: only one side responds, biased
            // toward the failed side.
            if (lAileronOut && rAileronOut)      cmdAileron = 0.0;
            else if (lAileronOut)                cmdAileron = Math.Max(0.0, cmdAileron);
            else if (rAileronOut)                cmdAileron = Math.Min(0.0, cmdAileron);

            // ---- atmosphere & airflow ----
            Vector3 posWorld = Rb.position;
            Vector3 velWorld = Rb.linearVelocity;
            Quaternion rotWorld = Rb.rotation;
            double altitude = posWorld.y;
            double rho = Atmosphere.AirDensity(altitude);
            var flow = Aerodynamics.Airflow(velWorld, rotWorld);
            double speed = flow.speed;

            // ---- lift / drag ----
            var cfg = Config;
            double Cl = Aerodynamics.LiftCoefficient(flow.alpha, cfg.AspectRatio, cfg.ClMax,
                                                     cfg.AlphaStallRad, cfg.AlphaPostStallRad);
            // Sideslip drag (deviation from brief §5; without it skids cost
            // no energy). Tuned so 2 s full rudder at cruise bleeds ~10–15
            // km/h. Documented in README.
            double sinBeta = Math.Sin(flow.beta);
            double CdBeta = 0.030 * sinBeta * sinBeta;
            double Cd = Aerodynamics.DragCoefficient(Cl, cfg.Cd0, cfg.AspectRatio, cfg.OswaldE) + CdBeta;
            var ld = Aerodynamics.LiftDragForces(velWorld, rotWorld, rho, cfg.WingAreaM2, Cl, Cd);

            // Wing-spar consequences: zero lift if both gone, half if one,
            // +50% drag if either is broken.
            double liftScale = 1.0;
            if (bothWingsOut) liftScale = 0.0;
            else if (leftWingOut || rightWingOut) liftScale = 0.5;
            double dragScale = (leftWingOut || rightWingOut) ? 1.5 : 1.0;
            Rb.AddForce(ld.lift * (float)liftScale, ForceMode.Force);
            Rb.AddForce(ld.drag * (float)dragScale, ForceMode.Force);

            // ---- thrust along body +x ----
            bool haveFuel = FuelKg > 0.0 && !engineOut;
            double throttle = haveFuel ? cmdThrottle : 0.0;
            double T = ThrustAtAltitude(cfg, altitude, throttle) * EngineHealth;
            if (T > 0.0 && !engineOut)
            {
                Vector3 forwardWorld = rotWorld * BODY_FORWARD;
                Rb.AddForce(forwardWorld * (float)T, ForceMode.Force);
                FuelKg = Math.Max(0.0, FuelKg - cfg.FuelBurnKgS * throttle * dt);
                // Pilot mass is folded into empty mass.
                Rb.mass = (float)(cfg.EmptyMassKg + FuelKg);
            }

            // Gravity: delegated to Unity (rb.useGravity = true).

            // ---- stall detection ----
            double aMag = Math.Abs(flow.alpha);
            double stallThresh = cfg.AlphaStallRad * (1.0 - cfg.LowSpeedBonus * 0.5);
            Stalled = aMag > stallThresh;

            // ---- spin entry: uncoordinated stall (|β| > 8°) ----
            if (!Spinning && Stalled && Math.Abs(flow.beta) > (8.0 * Math.PI / 180.0))
            {
                Spinning = true;
                SpinRecoverTimer = 0.0;
            }

            // ω read from Rigidbody (world frame) — convert to body frame for
            // the JS-convention math.
            Vector3 omegaWorld = Rb.angularVelocity;
            Vector3 omegaBody = Quaternion.Inverse(rotWorld) * omegaWorld;
            double wx = omegaBody.x;
            double wy = omegaBody.y;
            double wz = omegaBody.z;

            if (Spinning)
            {
                double spinDir = Math.Sign(wy);
                if (spinDir == 0.0) spinDir = 1.0;
                double oppositeRudder = -spinDir * cmdRudder;
                bool neutralStick = Math.Abs(cmdElevator) < 0.2 && Math.Abs(cmdAileron) < 0.2;
                if (neutralStick && oppositeRudder > 0.3)
                {
                    SpinRecoverTimer += dt;
                    if (SpinRecoverTimer > 0.8) { Spinning = false; SpinRecoverTimer = 0.0; }
                }
                else
                {
                    SpinRecoverTimer = 0.0;
                }
            }

            // ---- control torques (rate-targeting) ----
            const double v0 = 11.1;  // 40 km/h: zero authority below this
            const double v1 = 33.0;  // ~120 km/h: full authority by here
            double auth = Authority(speed, v0, v1);

            double aileronCmd = cmdAileron;
            double elevatorCmd = cmdElevator;
            double rudderCmd  = cmdRudder;

            if (Stalled)
            {
                aileronCmd *= 0.4;                                 // -60% roll authority
                _stallBuffetPhase += dt * 18.0;
                elevatorCmd += Math.Sin(_stallBuffetPhase) * 0.12; // pitch buffet
            }
            if (Spinning)
            {
                elevatorCmd *= 0.2;
                aileronCmd  *= 0.2;
                double spinDir = Math.Sign(wy);
                if (spinDir == 0.0) spinDir = 1.0;
                double targetYaw = 1.5 * spinDir;
                double yawErr = targetYaw - wy;
                ApplyTorqueBody(rotWorld, new Vector3(0f, (float)(yawErr * Inertia.Iyy * 4.0), 0f));
            }

            //   aileron + → roll right → +x rotation
            //   elevator + → pitch up   → +z rotation
            //   rudder + → yaw right    → −y rotation
            double targetWx = aileronCmd  * cfg.RollRateMaxRadS  * auth;
            double targetWz = elevatorCmd * cfg.PitchRateMaxRadS * auth;
            double targetWy = -rudderCmd  * cfg.YawRateMaxRadS   * auth;

            double responsiveness = pilotOut ? 0.0 : 6.0; // 1/s
            double rollAuthScale = bothWingsOut ? 0.0 : ((leftWingOut || rightWingOut) ? 0.4 : 1.0);
            double tx = (targetWx - wx) * Inertia.Ixx * responsiveness * rollAuthScale;
            double ty = (targetWy - wy) * Inertia.Iyy * responsiveness;
            double tz = (targetWz - wz) * Inertia.Izz * responsiveness;
            ApplyTorqueBody(rotWorld, new Vector3((float)tx, (float)ty, (float)tz));

            // ---- constant prop-reaction roll torque (non-rotary engines) ----
            if (cfg.EngineTorqueRollPerThrottle != 0.0)
            {
                ApplyTorqueBody(rotWorld, new Vector3((float)(cfg.EngineTorqueRollPerThrottle * throttle), 0f, 0f));
            }

            // ---- gyroscopic torque: τ = ω × H (body frame) ----
            if (cfg.GyroTorqueMagnitude > 0.0)
            {
                double gx = wy * _H.z - wz * _H.y;
                double gy = wz * _H.x - wx * _H.z;
                double gz = wx * _H.y - wy * _H.x;
                ApplyTorqueBody(rotWorld, new Vector3((float)gx, (float)gy, (float)gz));
            }

            // ---- aerodynamic angular damping ----
            double dampCoef = 0.6 * rho * speed * cfg.WingAreaM2;
            ApplyTorqueBody(rotWorld, new Vector3(
                (float)(-wx * dampCoef * 1.2 * cfg.WingSpanM * 0.5),
                (float)(-wy * dampCoef * 0.6 * cfg.WingSpanM * 0.5),
                (float)(-wz * dampCoef * 1.0 * cfg.WingSpanM * 0.5)));

            // ---- static aerodynamic stability (pitch + yaw weathervaning) ----
            //
            // Real aircraft self-align with the velocity vector via tail
            // surfaces. Brief §5 does not include this term, but without
            // it the sim has absorbing deep-stall states (DEFECTS S2-001).
            // Tuned so full pilot input overcomes stability in normal
            // flight, but dominates at extreme α/β where authority drops.
            double q = 0.5 * rho * speed * speed;
            double tailArm = Math.Max(2.0, cfg.WingSpanM * 0.5);
            double tailEffective = q * cfg.WingAreaM2 * 0.18 * tailArm;
            const double PITCH_STAB = 0.45;
            const double YAW_STAB   = 0.55;
            ApplyTorqueBody(rotWorld, new Vector3(
                0f,
                (float)(-flow.beta  * tailEffective * YAW_STAB),
                (float)(-flow.alpha * tailEffective * PITCH_STAB)));

            // ---- consequence torques ----
            // Asymmetric wing-spar break: surviving wing keeps lifting, dead
            // wing doesn't → roll toward dead side. Body +x torque rolls right.
            if (leftWingOut && !rightWingOut)
            {
                ApplyTorqueBody(rotWorld, new Vector3((float)(-cfg.RollRateMaxRadS * 1.5 * Inertia.Ixx), 0f, 0f));
            }
            else if (rightWingOut && !leftWingOut)
            {
                ApplyTorqueBody(rotWorld, new Vector3((float)(+cfg.RollRateMaxRadS * 1.5 * Inertia.Ixx), 0f, 0f));
            }
            // Both wings gone: ballistic + tumble. Add random torque on every
            // axis so the airframe spins about an unpredictable axis on its
            // way down.
            if (bothWingsOut)
            {
                double tumble = Inertia.Ixx * 4.0;
                ApplyTorqueBody(rotWorld, new Vector3(
                    (float)((_rng.NextDouble() - 0.5) * tumble),
                    (float)((_rng.NextDouble() - 0.5) * tumble),
                    (float)((_rng.NextDouble() - 0.5) * tumble)));
            }
            // Pilot incapacitated: pitch-down torque (slumped on the stick)
            // plus a small random roll perturbation so the aircraft drifts
            // off heading.
            if (pilotOut)
            {
                ApplyTorqueBody(rotWorld, new Vector3(
                    (float)((_rng.NextDouble() - 0.5) * 0.05 * Inertia.Ixx),
                    0f,
                    (float)(-0.5 * cfg.PitchRateMaxRadS * Inertia.Izz)));
            }
            // Single-side aileron loss: small constant roll bias toward
            // failed side.
            if (lAileronOut != rAileronOut)
            {
                double sign = lAileronOut ? -1.0 : +1.0;
                ApplyTorqueBody(rotWorld, new Vector3(
                    (float)(sign * 0.08 * cfg.RollRateMaxRadS * Inertia.Ixx), 0f, 0f));
            }

            // ---- fuel fire: 8 s burn timer; >300 km/h dive has 40%/s extinguish chance ----
            if (FuelFireActive)
            {
                FuelFireTimer += dt;
                if (speed > 83.3)
                {
                    double pTick = 1.0 - Math.Pow(0.6, dt); // 40%/s → per-tick prob
                    if (_rng.NextDouble() < pTick)
                    {
                        FuelFireActive = false;
                        FuelFireExtinguished = true;
                    }
                }
                if (FuelFireTimer >= 8.0)
                {
                    FuelFireActive = false;
                    Crashed = true;
                    CrashSpeedMS = Rb.linearVelocity.magnitude;
                    Rb.linearVelocity = Vector3.zero;
                    Rb.angularVelocity = Vector3.zero;
                }
            }
        }

        void ApplyTorqueBody(Quaternion rotation, Vector3 torqueBody)
        {
            Rb.AddTorque(rotation * torqueBody, ForceMode.Force);
        }

        // Apply damage to a named component. Returns the actual amount
        // applied (clamped to remaining HP) and a Destroyed flag that is
        // true only on the transition tick where HP first reaches 0.
        public DamageResult DamageComponent(string name, double dmg)
        {
            if (!_components.TryGetValue(name, out var c))
                return new DamageResult { Applied = 0.0, Destroyed = false };
            if (c.hp <= 0)
                return new DamageResult { Applied = 0.0, Destroyed = false };
            double applied = Math.Min(dmg, c.hp);
            c.hp -= applied;
            bool destroyed = c.hp <= 0;
            // Side-effects on first destruction.
            if (destroyed && name == "fuel_tank" && !FuelFireExtinguished)
            {
                FuelFireActive = true;
                FuelFireTimer = 0.0;
            }
            if (destroyed && name == "engine")
            {
                EngineHealth = 0.0;
            }
            return new DamageResult { Applied = applied, Destroyed = destroyed };
        }

        public ComponentStatus Status()
        {
            bool lwo = _components["left_wing_spar"].hp  <= 0;
            bool rwo = _components["right_wing_spar"].hp <= 0;
            bool allZero = true;
            foreach (var kvp in _components)
            {
                if (kvp.Value.hp > 0) { allZero = false; break; }
            }
            return new ComponentStatus
            {
                PilotIncapacitated = _components["pilot"].hp           <= 0,
                EngineDestroyed    = _components["engine"].hp          <= 0,
                LeftWingOut        = lwo,
                RightWingOut       = rwo,
                BothWingsOut       = lwo && rwo,
                ElevatorOut        = _components["elevator"].hp        <= 0,
                RudderOut          = _components["rudder"].hp          <= 0,
                LeftAileronOut     = _components["left_aileron"].hp    <= 0,
                RightAileronOut    = _components["right_aileron"].hp   <= 0,
                FuelTankDestroyed  = _components["fuel_tank"].hp       <= 0,
                FuelFireActive     = FuelFireActive,
                FuelFireTimer      = FuelFireTimer,
                AllComponentsZero  = allZero,
            };
        }

        // Ground collision check, called from external code that does the
        // terrain raycast. Crash registers if the aircraft has dipped at or
        // below terrain — any contact counts (no landing gear logic in S2).
        public bool RegisterCrashIfBelow(double groundY)
        {
            if (Crashed) return false;
            if (Rb.position.y > groundY) return false;
            CrashSpeedMS = Rb.linearVelocity.magnitude;
            Crashed = true;
            // Snap to terrain and freeze.
            var p = Rb.position;
            p.y = (float)groundY;
            Rb.position = p;
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
            return true;
        }
    }
}

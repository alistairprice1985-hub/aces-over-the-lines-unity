using System.Collections.Generic;
using UnityEngine;

namespace AcesOverTheLines.Weapons
{
    // Weapon system — owns the player's guns, all in-flight bullets, and
    // muzzle / tracer / spark visuals. Ticked once per physics step by
    // AircraftController AFTER AircraftEntity.Update so weapons see the
    // same physics tick as the flight model.
    //
    // Decoupled from Flight + Aircraft via parameters: AircraftController
    // hands Initialize() a converted gun spec list + geometry-kind string,
    // so the Weapons assembly stays leaf-level (no upward references).
    // Bullet hits resolve damage via the IDamageReceiver interface, which
    // DroneComponent implements.
    public class WeaponSystem : MonoBehaviour
    {
        [SerializeField] string enemyHitboxLayer = "EnemyHitbox";
        [SerializeField] int tracerPoolSize = 16;
        [SerializeField] int muzzleFlashPoolSize = 16;
        [SerializeField] int sparkPoolSize = 16;

        // Sopwith Camel twin Vickers cowl mounts — body-frame, matching
        // src/weapons/weaponSystem.js CAMEL_GUN_MOUNTS. Other aircraft
        // fall back to a single centered mount at (1.0, 0.5, 0).
        static readonly Vector3[] CAMEL_GUN_MOUNTS =
        {
            new Vector3(1.0f, 0.55f, -0.13f),
            new Vector3(1.0f, 0.55f,  0.13f),
        };

        int _hitboxMask;
        Rigidbody _rb;
        List<Gun> _guns;
        List<Vector3> _bodyMounts;
        List<Bullet> _bullets;
        bool _initialized;

        // Visual pools.
        ParticleSystem _muzzleFlash;
        ParticleSystem _sparks;
        GameObject[] _tracerPool;
        bool[] _tracerInUse;

        public int Fired { get; private set; }
        public int Hits { get; private set; }
        public IReadOnlyList<Gun> Guns => _guns;
        public IReadOnlyList<Bullet> Bullets => _bullets;

        void Awake()
        {
            // Self-init only if no caller provided the loadout yet — keeps
            // Awake usable in scenarios where WeaponSystem stands alone.
            // The usual path is AircraftController.Awake calling
            // Initialize(geometryKind, gunSpecs).
            _rb = GetComponent<Rigidbody>();
            int layer = LayerMask.NameToLayer(enemyHitboxLayer);
            _hitboxMask = layer >= 0 ? (1 << layer) : 0;
        }

        // Called by AircraftController (Flight assembly) with the converted
        // gun list. Idempotent.
        public void Initialize(string geometryKind, IReadOnlyList<GunSpec> gunSpecs)
        {
            if (_initialized) return;
            _initialized = true;

            if (_rb == null) _rb = GetComponent<Rigidbody>();
            int layer = LayerMask.NameToLayer(enemyHitboxLayer);
            _hitboxMask = layer >= 0 ? (1 << layer) : 0;

            _guns = new List<Gun>(gunSpecs.Count);
            _bodyMounts = new List<Vector3>(gunSpecs.Count);
            for (int i = 0; i < gunSpecs.Count; i++)
            {
                _guns.Add(new Gun(gunSpecs[i]));
                _bodyMounts.Add(GetMount(geometryKind, i));
            }
            _bullets = new List<Bullet>();

            CreateMuzzleFlashSystem();
            CreateSparkSystem();
            CreateTracerPool();
        }

        Vector3 GetMount(string geometryKind, int gunIndex)
        {
            if (geometryKind == "sopwith_camel" && gunIndex < CAMEL_GUN_MOUNTS.Length)
                return CAMEL_GUN_MOUNTS[gunIndex];
            return new Vector3(1.0f, 0.5f, 0f);
        }

        void CreateMuzzleFlashSystem()
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.SetParent(transform, worldPositionStays: false);
            _muzzleFlash = go.AddComponent<ParticleSystem>();

            var main = _muzzleFlash.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.10f;
            main.startSize = 1.2f;
            main.startSpeed = 0f;
            main.startColor = new Color(1f, 0.82f, 0.38f, 1f);
            main.maxParticles = muzzleFlashPoolSize;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _muzzleFlash.emission;
            emission.rateOverTime = 0; // burst only
            emission.enabled = true;

            var sizeOverLifetime = _muzzleFlash.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var shrink = new AnimationCurve(new Keyframe(0f, 1.3f), new Keyframe(1f, 0.3f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrink);

            // Additive blend renderer + on-top draw so the flash isn't
            // occluded by fuselage geometry.
            var renderer = _muzzleFlash.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 999;
            // Material: use a simple additive default. If URP particle
            // shader isn't available, fall back to whatever Unity provides.
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = main.startColor.color;
                renderer.material = mat;
            }

            _muzzleFlash.Play();
        }

        void CreateSparkSystem()
        {
            var go = new GameObject("HitSparks");
            go.transform.SetParent(transform, worldPositionStays: false);
            _sparks = go.AddComponent<ParticleSystem>();

            var main = _sparks.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.60f;
            main.startSize = 3.5f;
            main.startSpeed = 0f;
            main.startColor = new Color(1f, 0.94f, 0.63f, 1f);
            main.maxParticles = sparkPoolSize;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _sparks.emission;
            emission.rateOverTime = 0;
            emission.enabled = true;

            var sizeOverLifetime = _sparks.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var shrink = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrink);

            var colorOverLifetime = _sparks.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.94f, 0.63f), 0f), new GradientColorKey(new Color(1f, 0.7f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var renderer = _sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 1000;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = main.startColor.color;
                renderer.material = mat;
            }

            _sparks.Play();
        }

        void CreateTracerPool()
        {
            _tracerPool = new GameObject[tracerPoolSize];
            _tracerInUse = new bool[tracerPoolSize];
            for (int i = 0; i < tracerPoolSize; i++)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
                t.name = "Tracer_" + i;
                // Strip collider — we don't want tracers to participate in physics.
                var c = t.GetComponent<Collider>();
                if (c != null) Destroy(c);
                // 1.4 × 1.4 m cross-section; scale .x sized per-bullet (segment length).
                t.transform.SetParent(transform, worldPositionStays: false);
                t.transform.localScale = new Vector3(7f, 0.4f, 0.4f);
                var rend = t.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) shader = Shader.Find("Unlit/Color");
                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        mat.color = new Color(1f, 0.82f, 0.44f, 1f);
                        rend.material = mat;
                    }
                }
                t.SetActive(false);
                _tracerPool[i] = t;
            }
        }

        GameObject AcquireTracer()
        {
            for (int i = 0; i < tracerPoolSize; i++)
            {
                if (!_tracerInUse[i])
                {
                    _tracerInUse[i] = true;
                    _tracerPool[i].SetActive(true);
                    return _tracerPool[i];
                }
            }
            return null; // pool exhausted; skip the tracer visual
        }

        void ReleaseTracer(GameObject go)
        {
            for (int i = 0; i < tracerPoolSize; i++)
            {
                if (_tracerPool[i] == go)
                {
                    _tracerInUse[i] = false;
                    go.SetActive(false);
                    return;
                }
            }
        }

        // Per-tick, invoked by AircraftController.FixedUpdate AFTER
        // AircraftEntity.Update so weapons see the same physics state as
        // the flight model on this tick. `fire` is the trigger; the
        // caller extracts it from ControlInput (Weapons stays decoupled
        // from Flight to avoid a circular asmdef reference).
        public void Tick(double dt, bool fire)
        {
            if (!_initialized) return;

            bool triggerHeld = fire;
            Vector3 aircraftPos = _rb.position;
            Vector3 aircraftVel = _rb.linearVelocity;
            Quaternion aircraftRot = _rb.rotation;
            Vector3 forwardWorld = aircraftRot * new Vector3(1f, 0f, 0f);
            Vector3 rightWorld   = aircraftRot * new Vector3(0f, 0f, 1f);
            Vector3 upWorld      = aircraftRot * new Vector3(0f, 1f, 0f);

            // Step guns + fire bullets.
            for (int i = 0; i < _guns.Count; i++)
            {
                var shotMaybe = _guns[i].Tick(dt, triggerHeld);
                if (!shotMaybe.HasValue) continue;
                var shot = shotMaybe.Value;

                Vector3 mountWorld = aircraftPos + aircraftRot * _bodyMounts[i];
                FireBullet(shot, mountWorld, forwardWorld, rightWorld, upWorld, aircraftVel, aircraftPos);
                EmitMuzzleFlash(mountWorld + forwardWorld * 0.55f);
                Fired++;
            }

            // Step bullets in-flight (reverse iterate for safe removal).
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                var hit = bullet.Step(dt, _hitboxMask);
                if (hit.HasValue)
                {
                    hit.Value.Receiver?.TakeDamage(hit.Value.Damage);
                    EmitSpark(hit.Value.HitPoint);
                    Hits++;
                }
                if (bullet.Expired)
                {
                    if (bullet.TracerMesh != null)
                    {
                        ReleaseTracer(bullet.TracerMesh);
                        bullet.TracerMesh = null;
                    }
                    _bullets.RemoveAt(i);
                }
            }
        }

        void FireBullet(Shot shot, Vector3 mountWorld, Vector3 fwd, Vector3 right, Vector3 up, Vector3 aircraftVel, Vector3 aircraftPos)
        {
            // Uniform dispersion within ±dispersion_rad on body right + up axes.
            double sample = shot.DispersionRad;
            float ax = (float)((Random.value * 2.0 - 1.0) * sample);
            float ay = (float)((Random.value * 2.0 - 1.0) * sample);
            Vector3 dir = (fwd + right * ax + up * ay).normalized;

            float muzzle = (float)shot.MuzzleVelocity;
            Vector3 velocity = aircraftVel + dir * muzzle;

            var bullet = new Bullet
            {
                Position = mountWorld,
                Velocity = velocity,
                BaseDamage = shot.BaseDamage,
                Tracer = shot.Tracer,
                FiringAircraftPos = aircraftPos,
            };

            if (shot.Tracer)
            {
                var tracer = AcquireTracer();
                if (tracer != null)
                {
                    tracer.transform.position = mountWorld;
                    // Orient along velocity direction.
                    tracer.transform.rotation = Quaternion.LookRotation(velocity.normalized) * Quaternion.Euler(0f, 90f, 0f);
                    // Length scaled to per-tick segment length at 120 Hz so the
                    // streak roughly matches the displacement.
                    float segLen = velocity.magnitude / 120f;
                    tracer.transform.localScale = new Vector3(segLen, 0.4f, 0.4f);
                    bullet.TracerMesh = tracer;
                }
            }

            _bullets.Add(bullet);
        }

        void EmitMuzzleFlash(Vector3 pos)
        {
            if (_muzzleFlash == null) return;
            var ep = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true };
            _muzzleFlash.Emit(ep, 1);
        }

        void EmitSpark(Vector3 pos)
        {
            if (_sparks == null) return;
            var ep = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true };
            _sparks.Emit(ep, 1);
        }
    }
}

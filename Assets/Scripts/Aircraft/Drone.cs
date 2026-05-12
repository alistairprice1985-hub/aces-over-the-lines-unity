using System;
using System.Collections.Generic;
using UnityEngine;
using AcesOverTheLines.Flight;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.Aircraft
{
    // Static target drone for slice S4 — fixed-position aircraft with full
    // per-component HP tracking. Doesn't fly (no AircraftEntity). The
    // weapon system (Stage 4h) hits one of the per-component hitbox
    // children to call DamageComponent on the owner. Visuals: white hit
    // flash on damage; soot tint + 30° nose-down tilt + smoke plume on
    // destruction. Ports src/aircraft/drone.js.
    public class Drone : MonoBehaviour
    {
        [SerializeField] string aircraftId = "albatros_d3";
        [SerializeField] float initialNoseDownDeg = 0f;
        [SerializeField, Range(0f, 1f)] float flashDurationS = 0.30f;
        [SerializeField] string hitboxLayer = "EnemyHitbox";

        public struct ComponentStatus
        {
            public bool PilotDestroyed;
            public bool EngineDestroyed;
            public bool AllComponentsZero;
        }

        public bool Destroyed { get; private set; }
        public AircraftConfig Config { get; private set; }
        public IReadOnlyDictionary<string, DamageModel.ComponentHP> Components => _components;

        Dictionary<string, DamageModel.ComponentHP> _components;
        List<Renderer> _renderers;
        Color[] _originalBaseColor;
        Color[] _originalEmission;
        MaterialPropertyBlock _block;
        float _flashTimer;
        bool _destroyedVisualApplied;
        ParticleSystem _smoke;

        static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId   = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        void Awake() => Initialize();

        // Public so EditMode tests can fire it explicitly — Awake only runs
        // at play-mode-entry / scene-load in Unity, not on AddComponent in
        // edit mode. Idempotent.
        public void Initialize()
        {
            if (_components != null) return;
            Config = AircraftRoster.GetAircraftConfig(aircraftId);
            _components = DamageModel.CreateComponentHPs();
            _block = new MaterialPropertyBlock();

            SpawnHitboxes();
            CacheRenderers();
            CreateSmokeParticleSystem();

            if (initialNoseDownDeg != 0f)
            {
                // Apply nose-down tilt about body-z (lateral). transform.rotation
                // is the body→world quaternion (JS sim convention); composing
                // on the right applies the tilt in body frame.
                transform.rotation = transform.rotation * Quaternion.AngleAxis(-initialNoseDownDeg, new Vector3(0f, 0f, 1f));
            }
        }

        void SpawnHitboxes()
        {
            var hitboxes = DamageModel.BodyFrameHitboxes();
            int layer = LayerMask.NameToLayer(hitboxLayer);
            if (layer < 0) layer = 0; // fall back to Default if layer not yet created

            foreach (var kvp in hitboxes)
            {
                var child = new GameObject("Hitbox_" + kvp.Key);
                child.transform.SetParent(transform, worldPositionStays: false);
                child.transform.localPosition = kvp.Value.center;
                child.transform.localRotation = Quaternion.identity;
                child.layer = layer;

                var box = child.AddComponent<BoxCollider>();
                box.size = kvp.Value.size;

                var comp = child.AddComponent<DroneComponent>();
                comp.Init(this, kvp.Key);
            }
        }

        void CacheRenderers()
        {
            // Capture all renderers currently in the hierarchy. The smoke
            // particle system isn't created yet, so its renderer won't be
            // included — that's intentional (we don't want to flash-tint
            // the smoke plume). Hitbox children have no renderers.
            _renderers = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                _renderers.Add(r);
            }
            _originalBaseColor = new Color[_renderers.Count];
            _originalEmission = new Color[_renderers.Count];
            for (int i = 0; i < _renderers.Count; i++)
            {
                var mat = _renderers[i].sharedMaterial;
                if (mat == null) continue;
                _originalBaseColor[i] =
                    mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId)
                  : mat.HasProperty(LegacyColorId) ? mat.GetColor(LegacyColorId)
                  : Color.white;
                _originalEmission[i] = mat.HasProperty(EmissionColorId) ? mat.GetColor(EmissionColorId) : Color.black;
            }
        }

        void CreateSmokeParticleSystem()
        {
            var smokeGo = new GameObject("Smoke");
            smokeGo.transform.SetParent(transform, worldPositionStays: false);
            // Engine area: body (+2.5, 0, 0) — front of fuselage.
            smokeGo.transform.localPosition = new Vector3(2.5f, 0f, 0f);
            smokeGo.transform.localRotation = Quaternion.identity;

            _smoke = smokeGo.AddComponent<ParticleSystem>();

            var main = _smoke.main;
            main.startLifetime = 4.5f;
            main.startSize = 0.8f;
            main.startSpeed = 0f;
            main.startColor = new Color(0.28f, 0.28f, 0.27f, 0.60f);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = _smoke.emission;
            emission.rateOverTime = 25f; // ~25 puffs/s (JS pool cap)

            var velocityOverLifetime = _smoke.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-1.25f, -0.75f); // drift back along body +x
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve( 1.5f,  2.3f);   // rise
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            var sizeOverLifetime = _smoke.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var growCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.3f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, growCurve);

            var colorOverLifetime = _smoke.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.28f, 0.28f, 0.27f), 0.0f),
                    new GradientColorKey(new Color(0.40f, 0.40f, 0.38f), 1.0f),
                },
                new[]
                {
                    new GradientAlphaKey(0.60f, 0.0f),
                    new GradientAlphaKey(0.00f, 1.0f),
                });
            colorOverLifetime.color = gradient;

            _smoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        // Apply damage to a named component. Clamps to remaining HP and
        // (on first destruction) flips Destroyed and queues the destroyed
        // visual via the flash-timer expiry. Always triggers a hit flash.
        // Returns DamageInfo (Weapons namespace) — Applied is the actual
        // damage taken, Destroyed is true only on the transition tick where
        // the drone first becomes destroyed.
        public DamageInfo DamageComponent(string name, double dmg)
        {
            if (!_components.TryGetValue(name, out var c))
                return default;
            if (c.hp <= 0.0)
                return default;

            double applied = Math.Min(dmg, c.hp);
            c.hp -= applied;

            FlashWhite();
            _flashTimer = flashDurationS;

            bool wasDestroyed = Destroyed;
            if (!wasDestroyed && (_components["pilot"].hp <= 0.0 || _components["engine"].hp <= 0.0))
            {
                Destroyed = true;
            }
            return new DamageInfo { Applied = applied, Destroyed = Destroyed && !wasDestroyed };
        }

        public ComponentStatus Status()
        {
            bool allZero = true;
            foreach (var kv in _components)
            {
                if (kv.Value.hp > 0.0) { allZero = false; break; }
            }
            return new ComponentStatus
            {
                PilotDestroyed = _components["pilot"].hp <= 0.0,
                EngineDestroyed = _components["engine"].hp <= 0.0,
                AllComponentsZero = allZero,
            };
        }

        void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    if (Destroyed) ApplyDestroyedVisual();
                    else           RevertFlashToOriginal();
                }
            }
            else if (Destroyed && !_destroyedVisualApplied)
            {
                ApplyDestroyedVisual();
            }
        }

        void FlashWhite()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId,     Color.white);
                _block.SetColor(LegacyColorId,   Color.white);
                _block.SetColor(EmissionColorId, Color.white);
                r.SetPropertyBlock(_block);
            }
        }

        void RevertFlashToOriginal()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId,     _originalBaseColor[i]);
                _block.SetColor(LegacyColorId,   _originalBaseColor[i]);
                _block.SetColor(EmissionColorId, _originalEmission[i]);
                r.SetPropertyBlock(_block);
            }
        }

        void ApplyDestroyedVisual()
        {
            if (_destroyedVisualApplied) return;
            _destroyedVisualApplied = true;
            // Soot tint all renderers; matches JS applySoot multipliers.
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                Color soot = SootTint(_originalBaseColor[i]);
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId,     soot);
                _block.SetColor(LegacyColorId,   soot);
                _block.SetColor(EmissionColorId, Color.black);
                r.SetPropertyBlock(_block);
            }
            // Tilt 30° nose-down about body-z (composed on the right ⇒ body frame).
            transform.rotation = transform.rotation * Quaternion.AngleAxis(-30f, new Vector3(0f, 0f, 1f));
            // Start the smoke plume.
            if (_smoke != null) _smoke.Play();
        }

        // Soot multipliers from src/aircraft/drone.js applySoot().
        static Color SootTint(Color c) => new Color(c.r * 0.30f, c.g * 0.28f, c.b * 0.25f, c.a);
    }
}

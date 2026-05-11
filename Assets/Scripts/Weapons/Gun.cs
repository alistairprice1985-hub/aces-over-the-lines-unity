using System;

namespace AcesOverTheLines.Weapons
{
    // Per-gun state machine. Implements brief §7:
    //   rounds_remaining, rate_of_fire_rpm, muzzle_velocity_m_s,
    //   dispersion_rad, jam_probability_per_round, clear_jam_time_s.
    //
    //   - Synchronised guns are credited an 8% rate-of-fire penalty
    //     automatically (synchroniser duty cycle).
    //   - Holding the trigger while jammed does nothing.
    //   - When ammo reaches 0 the gun stays empty for the rest of the mission
    //     (the SE5a Lewis reload exception will land when we wire that
    //     aircraft's input — out of scope for S4's player Sopwith Camel).
    //
    // Tracer rounds: every 5th round emitted has Tracer = true. The
    // weaponSystem decides whether to render a streak.

    public class GunSpec
    {
        public string Type;
        public int Rounds;
        public double RateOfFireRpm;
        public double MuzzleVelocityMS;
        public double DamagePerHitHp;
        public double DispersionRad = 0.003;
        public double JamProbabilityPerRound = 0.0;
        public double ClearJamTimeS = 3.0;
        public bool Synchronised = false;
        public string Mount;

        public GunSpec Clone() => (GunSpec)MemberwiseClone();
    }

    public struct Bullet
    {
        public bool Tracer;
        public double DispersionRad;
        public double MuzzleVelocity;
        public double BaseDamage;
        public string GunMount;
        public Func<(double x, double y)> GaussianSample;
    }

    public class Gun
    {
        public const double SYNC_PENALTY = 0.08;   // brief §7

        public GunSpec Spec { get; }
        public double EffectiveRpm { get; }
        public double PeriodS { get; }
        public int Rounds => _rounds;
        public bool Jammed => _jammed;
        public int FiredTotal => _firedTotal;
        public int TracerTotal => _tracerTotal;

        readonly double _jamP;
        readonly double _clearT;
        readonly Random _rng;

        int _rounds;
        double _cooldown;
        bool _jammed;
        double _clearProgress;
        int _firedTotal;
        int _tracerTotal;

        public Gun(GunSpec spec, Random rng = null)
        {
            Spec = spec;
            bool synch = spec.Synchronised;
            double rpm = spec.RateOfFireRpm * (synch ? 1.0 - SYNC_PENALTY : 1.0);
            EffectiveRpm = rpm;
            PeriodS = 60.0 / rpm;
            _jamP = spec.JamProbabilityPerRound;
            _clearT = spec.ClearJamTimeS;
            _rng = rng ?? new Random();
            _rounds = spec.Rounds;
        }

        // Per-tick step. Returns null or a Bullet this tick (a single gun
        // never emits more than one per tick at our 1/120 s tick rate, since
        // the fastest Vickers period is ~0.145 s).
        public Bullet? Tick(double dt, bool triggerHeld, bool clearJamHeld = false)
        {
            _cooldown = Math.Max(0.0, _cooldown - dt);
            if (_jammed)
            {
                // Holding R clears jam at 60% chance after clear_jam_time_s.
                if (clearJamHeld)
                {
                    _clearProgress += dt;
                    if (_clearProgress >= _clearT)
                    {
                        _clearProgress = 0.0;
                        if (_rng.NextDouble() < 0.60) _jammed = false;
                    }
                }
                else
                {
                    _clearProgress = 0.0;
                }
                return null;
            }
            if (!triggerHeld || _rounds <= 0 || _cooldown > 0.0) return null;

            // Fire one round.
            _rounds--;
            _firedTotal++;
            _cooldown = PeriodS;
            // Jam roll.
            if (_rng.NextDouble() < _jamP) _jammed = true;
            bool tracer = (_firedTotal % 5) == 0;
            if (tracer) _tracerTotal++;

            Random rng = _rng;
            return new Bullet
            {
                Tracer = tracer,
                DispersionRad = Spec.DispersionRad,
                MuzzleVelocity = Spec.MuzzleVelocityMS,
                BaseDamage = Spec.DamagePerHitHp,
                GunMount = Spec.Mount,
                GaussianSample = () => (Gaussian(rng), Gaussian(rng)),
            };
        }

        // Test hooks.
        public void ForceJam() { _jammed = true; }
        public void SetRounds(int n) { _rounds = n; }

        // Box-Muller-ish: cheap Gaussian. Returns approximately N(0, 1).
        static double Gaussian(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}

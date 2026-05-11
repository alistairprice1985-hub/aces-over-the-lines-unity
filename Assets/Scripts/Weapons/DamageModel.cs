using System.Collections.Generic;
using UnityEngine;

namespace AcesOverTheLines.Weapons
{
    // Brief §6 component damage model.
    //
    // Each aircraft has the components below. At slice S4 we register hit
    // boxes and deplete HP on hits. Visual / behavioural consequences (smoke,
    // flame, wing detach, dead-stick, control jitter, uncontrolled dive)
    // come online at S6 along with the player's own damage state — for now
    // drones just track HP and flip a destroyed flag at thresholds.
    public static class DamageModel
    {
        // Default (max) HP per component.
        public static readonly IReadOnlyDictionary<string, int> COMPONENT_HP =
            new Dictionary<string, int>
            {
                { "engine",          100 },
                { "fuel_tank",        60 },
                { "pilot",            80 },
                { "left_wing_spar",  120 },
                { "right_wing_spar", 120 },
                { "elevator",         50 },
                { "rudder",           40 },
                { "left_aileron",     40 },
                { "right_aileron",    40 },
            };

        public class Component
        {
            public double hp;
            public double hpMax;
            public Bounds hitbox;
        }

        public class ComponentHP
        {
            public double hp;
            public double hpMax;
        }

        // Body-frame hit boxes (min/max in metres). Dimensions are crude
        // approximations sized to the procedural geometry — wide enough that
        // reasonable aim hits, narrow enough that miss is meaningful. All
        // boxes are oriented along body axes (+x forward, +y up, +z right) —
        // this is the JS sim's body frame, preserved as an internal
        // convention to this module.
        //
        // A rotated drone gets a loose world AABB computed at spawn (see
        // drone.js). For 0° / 90° / 180° / 270° headings the loose box
        // equals the exact rotated box; at intermediate angles it grows by
        // ≤√2.
        public static Dictionary<string, Bounds> BodyFrameHitboxes()
        {
            return new Dictionary<string, Bounds>
            {
                { "engine",          BoxFromMinMax(new Vector3( 1.8f, -0.45f, -0.45f), new Vector3( 3.2f,  0.55f,  0.45f)) },
                { "fuel_tank",       BoxFromMinMax(new Vector3( 0.5f, -0.30f, -0.35f), new Vector3( 1.8f,  0.55f,  0.35f)) },
                { "pilot",           BoxFromMinMax(new Vector3(-0.7f,  0.20f, -0.40f), new Vector3( 0.5f,  0.95f,  0.40f)) },
                { "left_wing_spar",  BoxFromMinMax(new Vector3(-0.6f, -0.50f, -4.30f), new Vector3( 0.6f,  1.20f, -1.20f)) },
                { "right_wing_spar", BoxFromMinMax(new Vector3(-0.6f, -0.50f,  1.20f), new Vector3( 0.6f,  1.20f,  4.30f)) },
                { "elevator",        BoxFromMinMax(new Vector3(-3.20f, -0.10f, -1.50f), new Vector3(-2.20f,  0.40f,  1.50f)) },
                { "rudder",          BoxFromMinMax(new Vector3(-3.20f,  0.10f, -0.10f), new Vector3(-2.30f,  2.00f,  0.10f)) },
                { "left_aileron",    BoxFromMinMax(new Vector3( 0.10f, -0.50f, -4.30f), new Vector3( 0.55f, -0.30f, -2.50f)) },
                { "right_aileron",   BoxFromMinMax(new Vector3( 0.10f, -0.50f,  2.50f), new Vector3( 0.55f, -0.30f,  4.30f)) },
            };
        }

        // Compute a world-frame AABB enclosing the rotated body-frame box.
        // For our drones the rotation is heading-only (about world +Y); the
        // AABB is exact for 0/π/2 multiples and otherwise loose by ≤√2 in
        // horizontal extents.
        public static Bounds WorldAABBFromBody(Bounds localBox, Vector3 position, Quaternion quaternion)
        {
            Vector3 lmin = localBox.min;
            Vector3 lmax = localBox.max;
            bool init = false;
            Vector3 wmin = Vector3.zero;
            Vector3 wmax = Vector3.zero;
            for (int xi = 0; xi < 2; xi++)
            {
                float x = xi == 0 ? lmin.x : lmax.x;
                for (int yi = 0; yi < 2; yi++)
                {
                    float y = yi == 0 ? lmin.y : lmax.y;
                    for (int zi = 0; zi < 2; zi++)
                    {
                        float z = zi == 0 ? lmin.z : lmax.z;
                        Vector3 c = quaternion * new Vector3(x, y, z) + position;
                        if (!init) { wmin = c; wmax = c; init = true; }
                        else
                        {
                            wmin = Vector3.Min(wmin, c);
                            wmax = Vector3.Max(wmax, c);
                        }
                    }
                }
            }
            Bounds result = new Bounds();
            result.SetMinMax(wmin, wmax);
            return result;
        }

        // Make a fresh component table { engine: {hp, hpMax, hitbox}, ... }
        // for a drone instance. Hit boxes are world-frame AABBs, computed
        // once at spawn.
        public static Dictionary<string, Component> CreateComponents(Vector3 position, Quaternion quaternion)
        {
            var local = BodyFrameHitboxes();
            var output = new Dictionary<string, Component>();
            foreach (var kvp in COMPONENT_HP)
            {
                output[kvp.Key] = new Component
                {
                    hp = kvp.Value,
                    hpMax = kvp.Value,
                    hitbox = WorldAABBFromBody(local[kvp.Key], position, quaternion),
                };
            }
            return output;
        }

        // Round-5: a lighter component table for the player aircraft. No
        // per-component hitboxes are baked in (the player's body moves every
        // tick, so hitboxes for collision are recomputed on demand from
        // BodyFrameHitboxes); we just need HP / hpMax to track damage state.
        public static Dictionary<string, ComponentHP> CreateComponentHPs()
        {
            var output = new Dictionary<string, ComponentHP>();
            foreach (var kvp in COMPONENT_HP)
            {
                output[kvp.Key] = new ComponentHP { hp = kvp.Value, hpMax = kvp.Value };
            }
            return output;
        }

        static Bounds BoxFromMinMax(Vector3 min, Vector3 max)
        {
            Bounds b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }
    }
}

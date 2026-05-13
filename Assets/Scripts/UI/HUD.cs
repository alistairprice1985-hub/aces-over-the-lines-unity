using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using AcesOverTheLines.Flight;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.UI
{
    // Top-right HUD overlay. Reads airspeed / altitude / heading / throttle
    // / fuel / per-gun ammo / component-status warnings from the target
    // AircraftController + WeaponSystem and renders a single
    // monospaced-friendly Text block with rich-text colour tags.
    //
    // Ports src/ui/hud.js (DOM-based) to uGUI. Layout matches the JS
    // source's top-right semi-transparent panel; ammo + warnings appended
    // below the gauge rows.
    [RequireComponent(typeof(Canvas))]
    public class HUD : MonoBehaviour
    {
        [SerializeField] AircraftController target;
        [SerializeField] WeaponSystem weapons;
        [SerializeField] int fontSize = 14;
        [SerializeField] Vector2 panelSize = new Vector2(280f, 280f);
        [SerializeField] Vector2 panelMargin = new Vector2(12f, 12f);
        [SerializeField] Color panelBg = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] Color textColor = new Color(0.87f, 0.87f, 0.87f, 1f);

        Text _text;
        CanvasGroup _canvasGroup;
        string[] _gunLabels;
        readonly StringBuilder _sb = new StringBuilder(512);

        void Awake()
        {
            BuildHud();
        }

        void Start()
        {
            if (target != null) RebuildGunLabels();
        }

        void BuildHud()
        {
            // Background panel anchored top-right.
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(transform, false);
            var bg = panelGo.AddComponent<Image>();
            bg.color = panelBg;
            bg.raycastTarget = false;
            var rect = bg.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-panelMargin.x, -panelMargin.y);
            rect.sizeDelta = panelSize;

            // Single Text child filling the panel.
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            _text = textGo.AddComponent<Text>();
            _text.font = LoadMonospacedFont();
            _text.fontSize = fontSize;
            _text.color = textColor;
            _text.alignment = TextAnchor.UpperLeft;
            _text.supportRichText = true;
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            var trect = _text.rectTransform;
            trect.anchorMin = Vector2.zero;
            trect.anchorMax = Vector2.one;
            trect.sizeDelta = Vector2.zero;
            trect.offsetMin = new Vector2(14f, 8f);
            trect.offsetMax = new Vector2(-14f, -8f);

            // CanvasGroup on the panel so we can grey out on pilot incap.
            _canvasGroup = panelGo.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        static Font LoadMonospacedFont()
        {
            // Try common monospaced families across Win / macOS / Linux.
            var f = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Menlo", "Courier New", "Liberation Mono", "DejaVu Sans Mono" }, 14);
            if (f != null) return f;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        void RebuildGunLabels()
        {
            var guns = target.Config.Guns;
            _gunLabels = GunLabels(guns);
        }

        // Matches src/ui/hud.js gunLabels(): if all types are unique, use
        // the first word of each type uppercased; otherwise generic
        // "GUN 1" / "GUN 2".
        static string[] GunLabels(IReadOnlyList<AcesOverTheLines.Flight.GunSpec> specs)
        {
            if (specs == null || specs.Count == 0) return new string[0];
            var seen = new HashSet<string>();
            foreach (var s in specs) seen.Add(s.Type ?? "GUN");
            string[] labels = new string[specs.Count];
            if (seen.Count == specs.Count)
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    string t = specs[i].Type ?? "GUN";
                    int sp = t.IndexOf(' ');
                    labels[i] = (sp > 0 ? t.Substring(0, sp) : t).ToUpperInvariant();
                }
            }
            else
            {
                for (int i = 0; i < specs.Count; i++) labels[i] = "GUN " + (i + 1);
            }
            return labels;
        }

        void Update()
        {
            if (target == null || target.Entity == null) return;
            if (_gunLabels == null) RebuildGunLabels();

            var entity = target.Entity;
            var rb = target.GetComponent<Rigidbody>();
            var config = target.Config;

            double speedMs = rb != null ? rb.linearVelocity.magnitude : 0.0;
            double altM = rb != null ? rb.position.y : 0.0;
            double heading = HudMath.HeadingDegFromQuat(rb != null ? rb.rotation : Quaternion.identity);
            double throttle = target.LastControls.Throttle;
            double fuelFrac = config.FuelCapacityKg > 0.0 ? entity.FuelKg / config.FuelCapacityKg : 0.0;

            int airspeedMph = Mathf.RoundToInt((float)(speedMs * HudMath.M_S_TO_MPH));
            int altitudeFt  = Mathf.RoundToInt((float)(altM * HudMath.M_TO_FT));
            int headingDeg  = ((Mathf.RoundToInt((float)heading) % 360) + 360) % 360;
            int throttlePct = Mathf.Clamp(Mathf.RoundToInt((float)(throttle * 100.0)), 0, 100);
            int fuelPct     = Mathf.Clamp(Mathf.RoundToInt((float)(fuelFrac * 100.0)), 0, 100);

            var status = entity.Status();

            _sb.Length = 0;
            AppendRow(_sb, "AIRSPEED", $"{airspeedMph,5} mph");
            AppendRow(_sb, "ALTITUDE", $"{altitudeFt,7:N0} ft");
            AppendRow(_sb, "HEADING",  $"{headingDeg,5:D3}°");
            // Engine destroyed → red throttle row.
            string throttleVal = $"{throttlePct,5}%";
            if (status.EngineDestroyed) throttleVal = $"<color=#ff7070>{throttleVal}</color>";
            AppendRow(_sb, "THROTTLE", throttleVal);
            AppendRow(_sb, "FUEL",     $"{fuelPct,5}%");

            // Ammo block.
            if (weapons != null && weapons.Guns != null && weapons.Guns.Count > 0)
            {
                _sb.AppendLine("─────────────────");
                for (int i = 0; i < weapons.Guns.Count; i++)
                {
                    var gun = weapons.Guns[i];
                    int rounds = gun.Rounds;
                    int max = gun.Spec.Rounds;
                    string label = (_gunLabels != null && i < _gunLabels.Length) ? _gunLabels[i] : ("GUN " + (i + 1));
                    string body;
                    if (gun.Jammed)        body = $"<color=#ddc060>{rounds,4} / {max,-4}  (JAM — R)</color>";
                    else if (rounds <= 0)  body = $"<color=#e06868>0 / {max}</color>";
                    else                   body = $"{rounds,4} / {max,-4}";
                    AppendRow(_sb, label, body);
                }
            }

            // Warnings block.
            var warnings = new List<string>();
            if (status.PilotIncapacitated) warnings.Add("PILOT INCAPACITATED");
            if (status.EngineDestroyed)    warnings.Add("ENGINE OUT");
            if (status.BothWingsOut)       warnings.Add("WINGS DESTROYED");
            else if (status.LeftWingOut)   warnings.Add("LEFT WING DESTROYED");
            else if (status.RightWingOut)  warnings.Add("RIGHT WING DESTROYED");
            if (status.ElevatorOut)        warnings.Add("ELEVATOR DESTROYED");
            if (status.RudderOut)          warnings.Add("RUDDER DESTROYED");
            if (status.LeftAileronOut)     warnings.Add("LEFT AILERON DESTROYED");
            if (status.RightAileronOut)    warnings.Add("RIGHT AILERON DESTROYED");
            if (status.FuelFireActive)
            {
                double remaining = System.Math.Max(0.0, 8.0 - status.FuelFireTimer);
                warnings.Add($"FUEL FIRE — {remaining:F1}s");
            }
            if (warnings.Count > 0)
            {
                _sb.AppendLine("─────────────────");
                foreach (var w in warnings) _sb.AppendLine($"<color=#ff7070>{w}</color>");
            }

            _text.text = _sb.ToString();

            // Grey out when pilot is incapacitated — instruments still show
            // values but the player has no input authority.
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = status.PilotIncapacitated ? 0.45f : 1.0f;
            }
        }

        static void AppendRow(StringBuilder sb, string label, string value)
        {
            // 10-char label + 1 space + value, right-padded so a monospaced
            // font lines values up. Proportional fonts will look slightly
            // off but still readable.
            sb.Append(label.PadRight(10));
            sb.Append(' ');
            sb.AppendLine(value);
        }
    }
}

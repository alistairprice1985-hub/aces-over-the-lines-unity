using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using AcesOverTheLines.AI;

namespace AcesOverTheLines.PlaytestHarness
{
    // Per-tick telemetry sample. One row per fixed step.
    public readonly struct TelemetryRow
    {
        public readonly float SimTime;
        public readonly Vector3 AiPos;
        public readonly float AiAltitude;
        public readonly float AiAirspeed;
        public readonly float AiVy;
        public readonly AIController.State State;
        public readonly double BankCmd;
        public readonly double PitchCmd;
        public readonly double ThrottleCmd;
        public readonly bool Fire;
        public readonly Vector3 PlayerPos;
        public readonly float Range;
        public readonly double DeltaE;
        public readonly AIController.PursuitMode Mode;

        public TelemetryRow(
            float simTime, Vector3 aiPos, float aiAlt, float aiSpd, float aiVy,
            AIController.State state, double bankCmd, double pitchCmd, double throttleCmd, bool fire,
            Vector3 playerPos, float range, double deltaE, AIController.PursuitMode mode)
        {
            SimTime = simTime;
            AiPos = aiPos;
            AiAltitude = aiAlt;
            AiAirspeed = aiSpd;
            AiVy = aiVy;
            State = state;
            BankCmd = bankCmd;
            PitchCmd = pitchCmd;
            ThrottleCmd = throttleCmd;
            Fire = fire;
            PlayerPos = playerPos;
            Range = range;
            DeltaE = deltaE;
            Mode = mode;
        }
    }

    public sealed class PlaytestResult
    {
        public string ScenarioName { get; set; }
        public bool Passed { get; set; }
        public string FailureReason { get; set; }
        public List<TelemetryRow> Telemetry { get; } = new List<TelemetryRow>();
        public List<string> CapturedLogs { get; } = new List<string>();

        // CSV columns are stable across runs so two byte-identical telemetry
        // captures hash identically. Culture-invariant formatting prevents
        // locale-dependent decimal-separator drift.
        public string ToCsv()
        {
            var sb = new StringBuilder(256 + Telemetry.Count * 128);
            sb.AppendLine("simTime,aiX,aiY,aiZ,aiAlt,aiSpd,aiVy,state,bankCmd,pitchCmd,throttleCmd,fire,playerX,playerY,playerZ,range,deltaE,mode");
            var inv = CultureInfo.InvariantCulture;
            foreach (var r in Telemetry)
            {
                sb.Append(r.SimTime.ToString("F6", inv)).Append(',');
                sb.Append(r.AiPos.x.ToString("F4", inv)).Append(',');
                sb.Append(r.AiPos.y.ToString("F4", inv)).Append(',');
                sb.Append(r.AiPos.z.ToString("F4", inv)).Append(',');
                sb.Append(r.AiAltitude.ToString("F4", inv)).Append(',');
                sb.Append(r.AiAirspeed.ToString("F4", inv)).Append(',');
                sb.Append(r.AiVy.ToString("F4", inv)).Append(',');
                sb.Append(r.State).Append(',');
                sb.Append(r.BankCmd.ToString("F6", inv)).Append(',');
                sb.Append(r.PitchCmd.ToString("F6", inv)).Append(',');
                sb.Append(r.ThrottleCmd.ToString("F6", inv)).Append(',');
                sb.Append(r.Fire ? 1 : 0).Append(',');
                sb.Append(r.PlayerPos.x.ToString("F4", inv)).Append(',');
                sb.Append(r.PlayerPos.y.ToString("F4", inv)).Append(',');
                sb.Append(r.PlayerPos.z.ToString("F4", inv)).Append(',');
                sb.Append(r.Range.ToString("F4", inv)).Append(',');
                sb.Append(r.DeltaE.ToString("F4", inv)).Append(',');
                sb.AppendLine(r.Mode.ToString());
            }
            return sb.ToString();
        }

        // Writes telemetry to disk under Assets/Tests/PlaytestHarness/Output/.
        // Filename uses scenario name + a deterministic discriminator passed
        // in by the caller (so two runs of the same scenario can be diffed).
        public string WriteCsv(string discriminator = null)
        {
            string outDir = Path.Combine(Application.dataPath, "Tests", "PlaytestHarness", "Output");
            Directory.CreateDirectory(outDir);
            string suffix = string.IsNullOrEmpty(discriminator) ? "" : ("_" + discriminator);
            string filename = $"{ScenarioName}{suffix}.csv";
            string path = Path.Combine(outDir, filename);
            File.WriteAllText(path, ToCsv());
            return path;
        }
    }
}

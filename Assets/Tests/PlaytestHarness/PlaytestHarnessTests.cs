using NUnit.Framework;

namespace AcesOverTheLines.PlaytestHarness.Tests
{
    // Each [Test] runs one canonical scenario through PlaytestRunner and
    // asserts its pass criterion. On failure we emit the per-tick telemetry
    // CSV under Assets/Tests/PlaytestHarness/Output/ so the failure mode is
    // inspectable post-run. The CSV is suppressed on pass to avoid Output/
    // churn on every green run.
    //
    // Naming: `Scenario<N>_<ScenarioName>_<Expectation>` — the expectation
    // names match the user-supplied scenario spec. Scenarios 2-5 are
    // expected to fail informatively against the current AI; the test
    // failure message is the diagnostic value.
    public class PlaytestHarnessTests
    {
        [Test] public void Scenario1_StraightLevelDecoy_AiAchievesFiringSolution()
        {
            var result = PlaytestRunner.Run(Scenarios.StraightLevel_Decoy);
            if (!result.Passed) result.WriteCsv();
            Assert.IsTrue(result.Passed, result.FailureReason);
        }

        [Test] public void Scenario2_StraightClimber_AiClosesRange()
        {
            var result = PlaytestRunner.Run(Scenarios.StraightClimber);
            if (!result.Passed) result.WriteCsv();
            Assert.IsTrue(result.Passed, result.FailureReason);
        }

        [Test] public void Scenario3_LevelOrbit_AiAchievesCloseRangeOrFiringSolution()
        {
            var result = PlaytestRunner.Run(Scenarios.LevelOrbit);
            if (!result.Passed) result.WriteCsv();
            Assert.IsTrue(result.Passed, result.FailureReason);
        }

        [Test] public void Scenario4_DivingExtender_AiStaysHighAndClose()
        {
            var result = PlaytestRunner.Run(Scenarios.DivingExtender);
            if (!result.Passed) result.WriteCsv();
            Assert.IsTrue(result.Passed, result.FailureReason);
        }

        [Test] public void Scenario5_HardEvader_AiStaysInRangeAndFires()
        {
            var result = PlaytestRunner.Run(Scenarios.HardEvader);
            if (!result.Passed) result.WriteCsv();
            Assert.IsTrue(result.Passed, result.FailureReason);
        }

        // Determinism check: same scenario, twice in a row, byte-identical CSV.
        // If this fires, the harness has a non-determinism leak (likely Time.time
        // somewhere or an unseeded RNG path) and every scenario result above is
        // suspect.
        [Test] public void Determinism_TwoRunsProduceByteIdenticalTelemetry()
        {
            var first  = PlaytestRunner.Run(Scenarios.StraightLevel_Decoy);
            var second = PlaytestRunner.Run(Scenarios.StraightLevel_Decoy);

            string firstCsv  = first.ToCsv();
            string secondCsv = second.ToCsv();

            if (firstCsv != secondCsv)
            {
                // Persist both so the diff can be inspected.
                first.WriteCsv("run1");
                second.WriteCsv("run2");
                int firstDiff = -1;
                int minLen = System.Math.Min(firstCsv.Length, secondCsv.Length);
                for (int i = 0; i < minLen; i++)
                    if (firstCsv[i] != secondCsv[i]) { firstDiff = i; break; }
                Assert.Fail(
                    $"Harness non-deterministic: two runs of {Scenarios.StraightLevel_Decoy.Name} " +
                    $"produced different telemetry. First divergence at char index {firstDiff} " +
                    $"(of {firstCsv.Length} vs {secondCsv.Length} chars). " +
                    $"CSVs emitted to Output/ as *_run1.csv and *_run2.csv.");
            }
            Assert.Pass($"Two runs produced byte-identical {firstCsv.Length}-byte CSV.");
        }
    }
}

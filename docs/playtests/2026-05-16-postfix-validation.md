# Post-Commit-7 validation playtests, 2026-05-16

**Branch / HEAD at time of playtests:** `main` post Round 5 Commits 7 and 8.
**Build:** Unity 6000.4.6f1.
**Purpose:** Empirically verify that Round 5 Commit 7 (Recover stall guard — airspeed-aware pitch setpoint attenuation in FlightStabilizer) resolves the death-spiral failure mode documented in `2026-05-16-baseline-pre-doengage-rewrite.md`, and capture the new dominant failure modes that the §6 DoEngage rewrite will need to address.

This file pairs with the raw log at `playtest-5-postfix-extended.log` (the most informative post-fix playtest; intermediate playtests 3 and 4 are summarised below but not separately archived).

---

## Slice summary

Round 5 Commit 7 added a second universal aerodynamic constraint to `FlightStabilizer.Stabilize`:

```csharp
double airspeedRatio = sp.DesiredAirspeedMs > 0.0
    ? Mathf.Clamp((float)(speed / sp.DesiredAirspeedMs), 0.3f, 1.0f)
    : 1.0f;
double effectivePitchSetpoint = sp.DesiredPitchRad * airspeedRatio;
```

Analogous to the existing cos(bank) gate but in the airspeed dimension. When airspeed is below the setpoint's target, the PID is told a softer pitch target rather than asked to reach the full setpoint it cannot achieve. Three EditMode tests cover the new behaviour (`PitchCommandScalesWithAirspeedRatio`, `ZeroDesiredAirspeedDoesNotDivideByZero`, plus the unchanged killer test `InvertedDescentRecoversInSixSeconds`). All 117 tests pass.

Round 5 Commit 8 added 1 Hz diagnostic logging at the Patrol/Search → Engage gate, emitting `[AI-GATE]` lines when the gate fails so the cause (target null / range / bearing) is captured. Defensive instrumentation against a "AI patrols indefinitely with player in range" symptom that was observed in earlier playtests but did not recur reliably.

---

## Playtest 3 — quick Recover-fix verification

Scenario identical to baseline playtest 2 (player flies, no shots fired, gives the AI tactical opportunities). Result:

- Engage entry alt 1000, vy -1.4 → 11.86s dive to alt 622, vy -45.6 → Engage→Climb
- Climb entry alt 618, vy -45.8 → **clean recovery in 12.32s** to alt 700, vy +4.8
- Climb→Patrol → Patrol→Engage → cycle 2 starts

The 12.32s clean Climb recovery is the critical evidence: identical entry conditions to baseline playtest 2's failed Recover (which crashed the AI), but with Commit 7 in place the architecture arrests the dive cleanly and the AI lives on. No Recover state ever triggers.

The playtest ended with the AI patrolling at lower-than-cruise altitude (~620m, descending at -0.5 m/s) — Ali subsequently observed the AI maintained constant ~112m HUD-reported range from the player without re-engaging. Original concern that the Patrol→Engage gate was not firing was the trigger for Commit 8's diagnostic logging.

## Playtest 4 — the new dominant failure mode (Engage-while-depleted)

Player flew aggressively to exercise the AI. Result:

- Engage cycle 1 ended in clean Climb
- **Engage cycle 3 entered at vy +0.2, speed 31 m/s** — extreme low-energy state
- DoEngage commanded full bank ±0.70 and full pitch ±0.50 from 31 m/s; airframe could not deliver the demanded control rates at that speed
- Bank rolled past 60° → Recover triggered NOT by descent but by **excessive bank** (loss of attitude control)
- Recover entered with no kinetic energy to give; recovery initially worked but elevator saturated again at low altitude, aircraft crashed

This crash has a different root cause from baseline playtests 1 and 2:

- Baseline crashes: Recover failed during a high-energy steep dive recovery. Mechanism: stall during pull-out. Fix: Commit 7's airspeed-aware pitch scaling.
- Playtest 4 crash: AI entered Engage from a depleted energy state (post-long-Climb at minimum power speed) and demanded manoeuvres beyond available control authority. Mechanism: control saturation at low speed inducing bank/stall coupling. **Commit 7 helps but does not fully address this case** — the airspeed-aware scaling reduces the demand but doesn't prevent the AI from committing to the engagement in the first place.

This empirically confirms the §6 DoEngage rewrite is the necessary next step: with energy-state awareness, the AI would not have committed to Engage in cycle 3 from speed 31 m/s.

## Playtest 5 — extended validation (no crash)

Same scenario, longer engagement. Result:

| # | State | Duration | Entry vy | Entry speed | Exit | Notes |
|---|---|---|---|---|---|---|
| 1 | Engage | 9.36s | -1.4 | 49.8 | alt 716, vy -45 | clean abandon |
| 1 | Climb | 3.75s | -45 | 76.1 | alt 701, vy +15 | clean, no stall |
| 2 | Engage | 6.17s | 13.8 | 61.6 | alt 533, vy -45 | clean abandon, lower exit alt |
| 2 | Climb | **39.37s** | -45 | 73.3 | alt 700, vy +6.6 | **the critical test** |
| 3 | Engage | **52.28s** | 6.1 | 39.3 | alt 668, vy -45 | very long, multiple bank/pitch reversals |
| 3 | Climb | 7.76s | -41 | 73.8 | alt 701, vy +9.9 | clean |
| 4 | Engage | ongoing | 9.4 | 44.3 | log ends at alt 686, vy +3.6 | AI alive |

**Cycle 2's Climb is the decisive evidence.** It dwelled 39.37 seconds — comparable to baseline playtest 2 cycle 6's 30.42s Climb that left the AI depleted and led to the eventual crash. With Commit 7 in place, the same long-Climb scenario reproduces successfully:

- Speed bled from 73 m/s to a minimum of 28.8 m/s (well into the stall regime)
- Airspeed-aware scaling reduced effective pitch setpoint to ~0.192 rad at the speed nadir (vs commanded 0.30)
- vy stayed positive throughout the entire 39-second climb
- Aircraft recovered to 700m without stalling, without Recover ever triggering

Without the fix, this would have been an identical death spiral to baseline playtest 2. With the fix, the AI survives and continues operating.

**Cycle 3's Engage is the new headline inefficiency.** 52.28 seconds of continuous Engage, with the AI oscillating bank ±0.70 and pitch ±0.50 chasing a manoeuvring player whose energy state it could never match. Speed never recovered above 53 m/s during this entire engagement. Eventually broke off via the descent-rate trigger after a final dive. This is exactly the pattern §6's energy-state model is designed to prevent.

---

## Validation against the §6.4 McCudden cycle benchmark

Per `docs/AI-STATE-OF-PLAY.md` §6.4, the AI should complete one full engage → ΔE drop → break-off → recover → re-engage cycle within 2 minutes 30 seconds.

Playtest 5 cycle times:

- Cycle 1: 13.11s (Engage 9.36 + Climb 3.75) — well within 2:30 ✓
- Cycle 2: 45.54s (Engage 6.17 + Climb 39.37) — over 2:30 ✗ (Climb is the bottleneck)
- Cycle 3: 60.04s (Engage 52.28 + Climb 7.76) — way over 2:30 ✗ (Engage is the bottleneck)

The AI cycles, but inefficiently. Both inefficiencies trace directly to the absence of energy-state awareness — the AI doesn't know when to refuse engagement (cycle 3) or how to climb efficiently without bleeding into low-speed regime (cycle 2).

---

## Conclusions

1. **Round 5 Commit 7 resolves the death-spiral failure mode.** Verified across 3 post-fix playtests. The architectural safety net (Recover state + cos(bank) gate + airspeed-aware pitch gate) is now sound enough to support tactical-layer improvements.

2. **The AI's failure mode has migrated from "crashes" to "engages badly".** This is the intended next-slice scope. The remaining inefficiencies (long Engage dwells, extended low-speed Climb cycles, control authority loss at low energy) are exactly what the §6 DoEngage rewrite is designed to address through energy-state awareness and pursuit-mode selection.

3. **Round 6 Commit 1 (§6 DoEngage rewrite) is unambiguously the next slice.** Per the §6.4 benchmark, the rewrite is the path to meeting the McCudden cycle benchmark consistently.

4. **The Patrol→Engage gate diagnostic logging (Commit 8) did not fire during playtests 3-5** — the gate always succeeded. The originally-observed "AI patrols indefinitely" symptom did not recur. Keep the diagnostic in for future scenarios where it might manifest; if `[AI-GATE]` lines appear in a future log, the cause will be definitively captured.

---

## Files in this directory

- `playtest-1-with-fire.log` — baseline raw log, with player fire (cycle 16 fatal crash, pilot incapacitation).
- `playtest-2-no-fire.log` — baseline raw log, no fire (cycle 10 fatal crash, architectural stall during Recover).
- `playtest-5-postfix-extended.log` — post-Commit-7 raw log, AI alive across 4 full cycles.
- `2026-05-16-baseline-pre-doengage-rewrite.md` — analysis of playtests 1 and 2.
- `2026-05-16-postfix-validation.md` — this document, analysis of playtests 3, 4, 5.

# Baseline playtests, 2026-05-16

**Branch / HEAD at time of playtest:** `main` @ `9f5605e`
**Build:** Unity 6000.4.6f1, Stage 6 Round 5 Commit 6 architecture (pre-DoEngage rewrite, pre-stall-guard).
**Purpose:** Document the AI's tactical behaviour against the §6.4 McCudden cycle benchmark in `docs/AI-STATE-OF-PLAY.md`, and capture the empirical failure modes that the next code slices must address.

This file pairs with the raw logs at `playtest-1-with-fire.log` and `playtest-2-no-fire.log` (in this directory).

---

## Playtest 1 — engagement with player fire

Player flew Sopwith Camel, attempted to engage AI Fokker D.VII, scored hits on or around the cockpit during a beam-on pass. AI subsequently crashed.

### Cycle timeline

| # | State | Duration | Alt change | vy at exit | Trigger |
|---|---|---|---|---|---|
| 1 | Patrol | 0.2s | — | — | initial |
| 2 | Engage | 8.94s | 1000→728m | -45 | engageAbandonDescentRateMs |
| 3 | Climb | 2.92s | 728→701m (recovery) | +14 | climbExitAltitudeM=700 |
| 4 | Patrol→Engage | 0.21s | — | — | re-engage |
| 5 | Engage | 32.72s | 704→621m (multi-phase) | -22 | engageBreakOffRangeM=60 (range=49) |
| 6 | Disengage | 5.09s | 621→586m | -3.4 | disengageDurationS=5 |
| 7 | Search→Engage | 0.21s | — | — | target visible |
| 8 | Engage | 22.02s | 585→508m (multi-phase, ends in dive) | -45 | engageAbandonDescentRateMs |
| 9 | Climb | 0.20s | — | -46 | **Recover hijacked** (vy<-30 AND alt<500) |
| 10 | Recover | 1.43s | 499→461m | -1.6 | exit conditions met |
| 11 | Patrol→Engage | 0.20s | — | — | target visible |
| 12 | Engage | 17.98s | 462→569m (climb then dive) | +3.4 | engageBreakOffRangeM=60 (range=59) |
| 13 | Disengage | 5.11s | 574→510m | -37 | timer |
| 14 | Search→Engage | 0.20s | — | — | target visible |
| 15 | Engage | 0.21s | — | -41 | **Recover hijacked** immediately |
| 16 | Recover | ~5s | 480→ground impact at alt 4-9m | -70 | **fatal crash** |

### Key findings — playtest 1

**Cycle pattern is forced break-off, not strategic disengagement.** Across cycles 2, 5, 8, 12, 15 the AI exited Engage via `engageAbandonDescentRateMs` (steep dive) or `engageBreakOffRangeM` (collision avoidance) — never because of a tactical decision to preserve energy or re-position for advantage.

**Long-Engage energy depletion.** Cycle 5 was 32.72s of continuous Engage, during which throttle ramped from 0.05 → 1.00 (saturated) while speed dropped 63.8 → 44.2 m/s and altitude oscillated by ±80m without net gain. This is the textbook negative-ΔE chase that §6 of the design contract is meant to prevent.

**Zero meaningful firing windows.** `fire=1` appeared on only 2 ticks (cycle 5, during a steep dive). No close-fire or snap-fire window achieved despite the AI getting to 49m and 59m range twice — at those ranges the AI was breaking off geometrically, not firing.

**Recover state failure (cycle 16) — initially diagnosed as stall, then attributed to pilot incapacitation from player's cockpit hits.** Playtest 2 (no fire) reproduced the same failure, definitively isolating it as a Recover-state architectural bug rather than a damage consequence. See playtest 2 analysis below.

---

## Playtest 2 — no shots fired (controlled experiment)

Player flew but did not fire. No damage applied to AI. Same engagement scenario, same starting conditions.

### Cycle timeline

| # | State | Duration | Alt change | vy at exit | Notes |
|---|---|---|---|---|---|
| 1 | Patrol | 0.2s | — | — | initial |
| 2 | Engage | 8.52s | 1000→741m | -45 | clean dive |
| 3 | Climb | 1.67s | 741→701m | +3 | clean recovery |
| 4 | Patrol→Engage | 0.21s | — | — | re-engage |
| 5 | Engage | 4.78s | 702→567m | -45 | dive |
| 6 | Climb | **30.42s** | 535→701m | +5 | speed bled to 25 m/s |
| 7 | Patrol→Engage | 0.21s | — | — | re-engage from energy-depleted state |
| 8 | Engage | 17.73s | 702→730m (climb+dive) | -1.4 | bank oscillating ±0.70, speed at 28-38 m/s throughout |
| 9 | Disengage | 4.48s | 730→632m | -44 | tried to climb at low speed, lost vy |
| 10 | Recover | ~6s | 626→ground impact at alt 1m | -68 | **fatal crash, no damage** |

### Recover failure — definitive isolation

The fatal Recover sequence in playtest 2 reproduces the exact failure pattern of playtest 1 cycle 16, but with no damage applied. The mechanism is therefore architectural, not damage-related.

Recover entry: alt=626m, vy=-45.2, speed=50.9 m/s.

```
alt=626 vy=-45.2 v=50.9   elv=0.32  (entry, speed already low)
alt=602 vy=-49.7 v=54.5   elv=0.30  (speed building from dive)
alt=576 vy=-53.5 v=58.1   elv=0.27
alt=521 vy=-52.0 v=63.9   elv=0.23  (vy starting to improve)
alt=498 vy=-41.6 v=64.5   elv=0.16  (speed peak)
alt=479 vy=-34.5 v=65.2   elv=0.10
alt=464 vy=-24.7 v=64.8   elv=0.07
alt=454 vy=-16.3 v=63.9   elv=0.06
alt=447 vy=-11.6 v=62.6   elv=0.14  (recovery progressing, vy near zero)
alt=440 vy=-16.0 v=61.6   elv=1.00  ← INFLECTION: elv saturates, vy regresses
alt=431 vy=-21.0 v=61.1   elv=1.00
alt=419 vy=-26.4 v=61.2   elv=1.00  ail=-1.00 (both saturated)
alt=404 vy=-34.5 v=61.9   elv=0.83
alt=384 vy=-45.9 v=63.3   elv=-0.17 (elv now reversed)
alt=358 vy=-57.6 v=64.7   elv=-0.44
... continues until ground impact at alt=1
```

The recovery proceeds correctly through the inverted-pitch resolution (cos(bank) gate doing its job), then breaks down when:
1. The pitch PID asks for full +0.30 rad pitch to climb out of dive.
2. The climb induces drag, bleeding airspeed even with full thrust applied.
3. Once airspeed drops below the threshold at which the wings can produce the demanded lift, the wings effectively stall.
4. The pitch PID, seeing vy worsen, slams elevator to maximum (1.00).
5. A stalled wing produces no additional lift no matter how hard the elevator commands more AoA. More AoA → more drag → more speed loss → deeper stall.
6. Death spiral.

The cos(bank) gate is mathematically correct for the inverted-pull problem (Round 5's stated purpose). It does not address aerodynamic stall — that is a different failure mode that simply was not on the Round 5 radar.

### Why playtest 2 reached this state without enemy fire

Cycle 6 (Climb, 30.42s) is the upstream cause. During that long Climb, airspeed dropped from 76 → 25 m/s while alt only crept 535 → 701m. The aircraft was flying near minimum-power speed for half a minute. This left the AI energy-depleted entering cycle 7's Engage, which left it further depleted entering cycle 8's manoeuvring fight (bank oscillating ±0.70 at speed 28-38 m/s — close to stall throughout), which left it with insufficient energy when Recover finally triggered in cycle 10.

The Recover failure is the proximal cause of the crash. The energy mismanagement across cycles 6-8 is the distal cause. **Both motivate the §6 DoEngage rewrite (energy-state awareness would have prevented the AI from entering cycle 7 from a depleted state) and the Round 5 Commit 7 stall guard (Recover must work even from low-energy entry as a safety net).**

---

## Implications

### Validates the §6 design contract

The §6.4 McCudden cycle benchmark — "AI should complete at least one full engage → ΔE drop → break-off → recover → re-engage cycle within 2:30, demonstrating strategic rather than forced disengagement" — is **definitively not met today**.

Cycles do happen, but they are mechanically wrong:
- Driven by forced break-off triggers, not energy-state decisions
- Entry into Engage occurs regardless of energy advantage
- Pursuit is unconditional lead-pursuit, with no lag/pure selector

Every premise of the §6 rewrite is empirically confirmed. Proceeding with that rewrite is correct — *after* the Recover safety net is hardened.

### Requires Round 5 Commit 7 before the DoEngage rewrite

The Recover stall-failure is a safety-net bug. The DoEngage rewrite will produce an AI that disengages strategically, but edge cases (player baits AI into a low-altitude trap, multi-cycle engagement bleeds energy reserves) will still drive Recover entries. The current Recover implementation cannot reliably arrest a low-speed steep dive.

Fix: scale the commanded pitch setpoint by airspeed/desired-airspeed in FlightStabilizer.Stabilize, analogous to the existing cos(bank) gate. See Round 5 Commit 7.

### Bonus finding — pilot incapacitation works correctly

Playtest 1 demonstrated that the existing damage subsystem (`DroneComponent`, `AircraftEntity.DamageComponent`, the `pilot` hitbox at body-frame (-0.7, 0.20, -0.40) → (0.5, 0.95, 0.40) with 80 HP) is wired end-to-end and produces the documented historical behaviour (research note §2.1 head-wound-severe): control inputs zeroed, throttle forced to zero, constant nose-down torque plus small random roll perturbation. This is the "slumped over the stick" behaviour from the Hawker death (23 November 1916).

The AIController is not pilot-aware — it continues to emit setpoints after the pilot is incapacitated, which the AircraftEntity layer silently drops. No gameplay impact (the commands are ignored), but log noise and CPU waste during the death sequence. Belongs in the §7 wound-state slice eventually; not a priority now.

### Climb-state long-stall observation

Playtest 2 cycle 6's 30.42s Climb dwell, during which airspeed dropped to 25 m/s but vy stayed mildly positive, is the same root cause as the Recover failure (commanded pitch exceeds available aerodynamic authority at low speed). It is non-catastrophic — Climb still climbs, just slowly and at the edge of stall — but it should benefit automatically from the Round 5 Commit 7 stall guard (which applies universally in FlightStabilizer, not just to Recover). Worth re-measuring after Commit 7 lands.

---

## Files in this directory

- `playtest-1-with-fire.log` — raw Unity console output, playtest 1 (with player fire).
- `playtest-2-no-fire.log` — raw Unity console output, playtest 2 (no fire, controlled experiment).
- `2026-05-16-baseline-pre-doengage-rewrite.md` — this document.

When the §6 DoEngage rewrite lands, run a comparable playtest and add `2026-MM-DD-post-doengage.md` alongside to document the before/after delta against the McCudden cycle benchmark.

# AI State of Play — Stage 6 Round 5 closeout

**Date:** 2026-05-16
**Branch / HEAD:** `main` / `06b85f6`
**Unity:** 6000.4.6f1
**Status:** AI dogfight loop functional end-to-end. Ready for the `DoEngage()` energy-state rewrite (next slice).

This document is the design contract for the next slice. It is not a retrospective. Read it before touching `AIController.cs` or `FlightStabilizer.cs`.

---

## 1. Snapshot

The Fokker D.VII (AI) detects the Sopwith Camel (player) at any bearing within visual range, transitions Patrol → Engage, computes a lead-angle solution from muzzle velocity and target kinematics, drives bank and pitch setpoints into the FlightStabilizer, and fires through three deflection-gated windows. Tactical safeties (Climb, Disengage, Recover) operate above the engagement loop.

Source layout:
- `Assets/Scripts/AI/AIController.cs` — tactical FSM (Patrol, Search, Engage, Evade, Disengage, RTB, Climb, Recover)
- `Assets/Scripts/AI/FlightStabilizer.cs` — inner-loop attitude controller (three cos(bank)-gated PIDs)
- `Assets/Scripts/AI/FlightSetpoint.cs` — DTO from FSM to stabilizer (bank, pitch, airspeed, fire)
- `Assets/Scripts/Flight/ControlInput.cs` — DTO from stabilizer to AircraftController (aileron, elevator, throttle)
- `Assets/Tests/EditMode/FlightStabilizerTests.cs` — attitude-extraction unit tests + the killer recovery test

Latest commits:
- `06b85f6` Verification artefact: Sopwith Camel render post-LFS migration
- `13fd471` Housekeeping: ignore Unity auto-recovery scratch
- `41c002b` Stage 6 Round 5 Commit 6: dogfight-cadence tuning

---

## 2. Architecture

**Two-tier control.** Tier 1 (`AIController.Decide()`) is a state machine that emits a `FlightSetpoint` per state. Tier 2 (`FlightStabilizer.Stabilize(setpoint, bank, pitch, speed, dt)`) converts the setpoint into a `ControlInput` (aileron, elevator, throttle) via three PIDs. The roll PID drives bank, the pitch PID drives world-frame pitch (gated by `cos(bank)` — this is the key mathematical insight), the throttle PID drives airspeed. The `AircraftController` applies the resulting `ControlInput` in FixedUpdate.

`FlightSetpoint` is deliberately minimal — four fields:

```csharp
public struct FlightSetpoint {
    public double DesiredBankRad;     // +ve = right wing down
    public double DesiredPitchRad;    // +ve = nose up
    public double DesiredAirspeedMs;
    public bool   Fire;
}
```

The FSM never touches control surfaces directly. Adding any new tactical state means writing a new `Do<State>()` method that returns a `FlightSetpoint` — nothing else.

**Recover is a hard interrupt** from any other state. Triggers: bank > 60° OR pitch < -45° OR (vy < -30 m/s AND altitude < 500 m). Exits: bank < 15° AND vy > -5 m/s AND altitude > 300 m. The recover-state setpoint (pitch +0.3 rad, airspeed 70 m/s, wings level) is what the killer regression test `FlightStabilizerTests.InvertedDescentRecoversInSixSeconds` is wired against — that test pins the recover contract.

---

## 3. Decision log

**Two-tier control replacing eight ad-hoc overrides (rounds 4a–4h).** The pre-refactor FSM emitted `ControlInput` directly and patched failure modes (graveyard spiral, stall, altitude floor) with per-tick body-frame overrides that fought each other and required round-after-round retuning. The cos(bank) gate in the stabilizer solves the underlying issue mathematically: world-frame pitch rate = body-frame pitch rate × cos(bank), and the stabilizer's output projection respects that. With the gate in place the eight overrides become unnecessary by construction.

**Patrol → Engage gate widened to 360° (`frontHemisphereDeg = 180f`).** An AI without a meatspace cockpit has no rear-quarter blind spot that needs modelling, and the prior front-hemisphere check let players approach from behind unmolested.

**Bank-dependent Patrol pitch.** `pitch = base / cos(bank)` with `patrolPitchRad = 0.15` at full bank and `patrolCruisePitchRad = 0.05` at wings level keeps altitude stable through coordinated turns rather than bleeding into the floor.

**Recover triggers must have headroom from Engage clamps.** `engageBankClampRad = 0.7` (~40°) sits below `recoverBankTriggerRad = 1.05` (~60°); `engagePitchClampRad = 0.50` (~28.6°) sits below `recoverPitchTriggerRad = -0.785` (~-45°). Widening Engage clamps into Recover trigger range produces a documented FSM bounce.

**MonoBehaviour, not ECS.** Solo side project with strong Inspector-tuning habits; ECS adds friction for no scale gain at one-AI one-player. Revisit only if multi-AI squadron mechanics become a goal.

**Multi-window firing is fixed.** Three windows (far 300m × 25°, close 100m × 15°, snap 50m × 60°) cover all dogfight geometries that the lead-pursuit algorithm produces. The next slice does NOT change this — pursuit-mode selection is upstream of firing. Note that the `farFireRangeM = 300` window is generous by historical standards (Mannock's rule was 100 yards / 90m, Ball's 50 yards / 45m, Boelcke fired only "when I can see the goggle strap on my opponent's crash helmet") and is a candidate for a future doctrine-grounded narrowing — but not in this slice.

**Camel turn-rate asymmetry is a roll-onset effect, not a steady-state turn-rate effect.** Modern flight tests of an original Camel (FLYING Magazine, "Calculated Sopwith Camel") found similar steady-state turn rates left and right; the gyroscopic asymmetry produces a faster roll *entry* into a right turn (nose-down) and slower entry to the left (nose-up), but sustained turn rate is broadly symmetric. The popular "Camel turns 270° right rather than 90° left" claim is folk myth. Relevant context if a Camel-piloted AI is ever added; the current AI flies the D.VII and so is unaffected.

---

## 4. Current tunable surface

All values are on disk in `AIController.cs` `[SerializeField]` defaults. Inspector overrides on the Fokker D.VII prefab take precedence (verify before each playtest).

| Domain | Constant | Value | Note |
|---|---|---|---|
| Decision | `decisionRateHz` | 5 | FSM tick rate |
| Detection | `visualRangeM` | 1000 | engagement radius |
| Detection | `frontHemisphereDeg` | 180 | 360° awareness |
| Engage | `engageBankClampRad` | 0.7 | ±40° |
| Engage | `engagePitchClampRad` | 0.50 | ±28.6° |
| Engage | `engageAirspeedMs` | 55 | |
| Engage | `engageBreakOffRangeM` | 60 | collision avoidance |
| Engage abandon | `engageAbandonAltitudeDropM` | 500 | Engage → Climb trigger |
| Engage abandon | `engageAbandonDescentRateMs` | 45 | |
| Patrol | `patrolBankClampRad` | 0.45 | ±25.8° |
| Patrol | `patrolPitchRad` / `patrolCruisePitchRad` | 0.15 / 0.05 | bank-dependent |
| Patrol | `patrolAirspeedMs` | 55 | matches engage |
| Climb | `climbPitchRad` / `climbAirspeedMs` | 0.3 / 45 | |
| Climb | `climbExitAltitudeM` | 700 | back to Engage at this alt |
| Fire | `farFireRangeM` / `farFireDeflectionDeg` | 300 / 25 | |
| Fire | `closeFireRangeM` / `closeFireDeflectionDeg` | 100 / 15 | |
| Fire | `snapFireRangeM` / `snapFireDeflectionDeg` | 50 / 60 | |
| Burst | `burstOnS` / `burstOffS` | 0.3 / 0.5 | |
| Energy | `energyLowSpeedMs` | 25 | Disengage trigger |
| Energy | `energyLowAltitudeM` | 200 | Disengage trigger |
| Disengage | `disengageDurationS` / `disengageAltitudeFloorM` | 5 / 1000 | |
| Evade | `evadeDurationS` | 3 | |
| Recover | `recoverBankTriggerRad` | 1.05 | ~60° |
| Recover | `recoverPitchTriggerRad` | -0.785 | ~-45° |
| Recover | `recoverDescentTriggerMs` / `recoverDescentTriggerAltM` | -30 / 500 | combined trigger |
| Recover | `recoverBankExitRad` / `recoverVyExitMs` / `recoverExitAltitudeM` | 0.26 / -5 / 300 | exit AND-gated |
| Recover | `recoverAirspeedMs` / `recoverPitchRad` | 70 / 0.3 | killer-test setpoint |

---

## 5. Known foot-guns

**Inspector ↔ code-default drift.** `[SerializeField]` defaults in code do not propagate to baked scene-YAML. Every default change requires both a code edit and an Inspector update on the AIController component. Ali handles the Inspector edits.

**Engage clamps ↔ Recover triggers headroom.** See §3. Any future tuning that widens Engage clamps must verify headroom still exists, otherwise the FSM bounces out of Engage via spurious Recover triggers.

**Engage / Climb / Recover all want pitch.** The FSM's transition logic resolves precedence (Recover wins, then Climb, then Engage). Do not add a fourth state that emits pitch without an explicit handover rule.

**LFS smudge appears as `git status` modification noise on `.glb`.** Resolved as of `13fd471`. If it recurs, the diagnostic is in §LFS resolution of the prior Cowork session: compare working-tree SHA256 to HEAD LFS pointer oid; identical means churn, different means real model update.

---

## 6. Design contract: `DoEngage()` rewrite (next slice)

`DoEngage()` today commits to lead pursuit unconditionally and clamps the resulting setpoint to engage limits. This works against a passive or weakly maneuvering target. Against a Camel that turns inside the D.VII at low altitude, it produces stalled gunshots and energy bleed.

The rewrite adds two things, in order: an energy-state model, and a pursuit-mode selector that reads from it.

### 6.1 Energy state

Specific energy is the working metric:

```
E_s = h + v² / (2g)        [units: m, altitude-equivalent]
```

where `h` is altitude in metres, `v` is true airspeed in m/s, `g = 9.81`. This collapses altitude and speed into a single comparable scalar.

Energy advantage:

```
ΔE = E_s(self) − E_s(target)
```

Positive ΔE means the AI has the option to dictate engagement geometry — climb away and re-engage, or convert altitude into speed and close. Negative ΔE means the AI is on the defensive: it cannot out-climb the target without disengaging.

Computed each decision tick (`decisionRateHz = 5`). Target velocity and altitude are accessible via `_targetRb` and `target.position.y`. Add a method `ComputeEnergyState(Rigidbody rb)` to `AIController` returning a `double` in metres.

This formalises arithmetically what late-war pilots understood intuitively. McCudden, Mannock, and Boelcke wrote about altitude as a savings account: spend it on a diving attack, refuse engagement when bankrupt. The energy-state model is the doctrinal substrate of the pursuit-mode selector that follows, not just a tuning trick.

### 6.2 Pursuit-mode selection

Three modes, selected per decision tick:

- **Lead pursuit** (current behaviour). Nose ahead of target on its predicted track. Gun solution. Used when range × deflection falls within any firing window AND ΔE ≥ 0.
- **Lag pursuit**. Nose behind target's predicted track. Preserves energy, controls range, sets up the next pass. Used when ΔE < 0, when aspect > 90° at range > closeFireRangeM, or when aspect > 120° regardless of energy (target is effectively facing the AI — no gun solution available).
- **Pure pursuit**. Nose on target's current position. Closes range fast but bleeds energy and ends in overshoot. Used as a transient — never a default — when range > 0.4 × visualRangeM AND ΔE ≥ 50 m.

Decision tree (evaluated in order, first match wins):

```
if (aspect > 120°)                                 → lag       (doctrinal floor: no gun solution from front)
else if (ΔE < 0)                                   → lag       (preserve energy)
else if (aspect > 90° AND range > closeFireRangeM) → lag       (re-position)
else if (range > 0.4 × visualRangeM AND ΔE > 50)   → pure      (close)
else                                               → lead      (gun)
```

**Aspect angle** is `Vector3.Angle(target.forward, (self.position − target.position))` — the off-tail angle measured from the target's own velocity vector. 0° is directly astern (best gun position); 180° is head-on. The 90° threshold is the perpendicular bearing to the target's velocity; lag pursuit is preferred for any approach geometry from beam-on or wider, which preserves more energy than committing to a low-deflection shot from a marginal angle and keeps the AI engageable rather than constantly re-positioning. (A stricter 60° threshold — more faithful to Boelcke Dictum #5 and McCudden/Mannock practice — was considered but rejected on gameplay grounds: it produces an AI that disengages too readily and reduces dogfight density. The doctrinal floor at 120° handles the geometric edge case that 60° would have addressed.) The 120° floor encodes Boelcke's Dictum #6 by deferral — when the target is facing the AI, the appropriate response is Evade-mode head-on convergence (a future slice), not an Engage-mode head-on commitment. The geometric break-off at `engageBreakOffRangeM = 60m` already protects against collision; this doctrinal floor prevents the AI from wasting decision ticks even setting up a head-on attack.

### 6.3 Setpoint contract (unchanged)

The rewrite does NOT touch the `FlightSetpoint` struct or the stabilizer interface. All three pursuit modes emit the same shape. Only the computation of bank and pitch changes per mode:

- **Lead** — existing `ComputeLeadPoint()` math. Unchanged.
- **Lag** — lead point computed with a negative time-to-bullet, projecting the target's past track. The nose points behind the target's predicted position.
- **Pure** — lead point = `target.position` directly. No projection.

Airspeed setpoint per mode:
- Lead: `engageAirspeedMs` (= 55)
- Lag: `engageAirspeedMs × 0.85` — bleed off speed to tighten lag turn and preserve energy (the WW1 doctrine of "altitude as savings account")
- Pure: `engageAirspeedMs × 1.15`, capped at airframe limit — close fast

`Fire` decision (`ShouldFireMultiWindow`) is unchanged and independent of pursuit mode. The firing-window deflection caps (25° / 15° / 60°) geometrically filter shots that lag-mode produces, so no explicit `Fire = false when mode == Lag` gate is needed — the existing logic naturally yields zero firing opportunities during lag-mode flight.

### 6.4 Test acceptance criteria

- Existing `FlightStabilizerTests.InvertedDescentRecoversInSixSeconds` must still pass — the stabilizer interface is untouched.
- New EditMode test `AIControllerTests.PursuitModeSelection` — given synthetic `(ΔE, aspect, range)` triples covering each branch of §6.2's decision tree (including the doctrinal floor at aspect > 120° and the rear-quarter threshold at aspect > 60°), assert the decision tree returns the expected mode. Pure unit test, no Unity scene.
- New EditMode test `AIControllerTests.EnergyStateMonotonicInClimbingTarget` — verify ΔE decreases when the target out-climbs the AI. Validates the math.
- Playtest acceptance (the **McCudden cycle benchmark**) — in a **2 minute 30 second** engagement against a turning-Camel target at 1000 m starting altitude, the AI should complete at least one full historical *cycle*: engage in lead pursuit until ΔE drops below zero (or aspect deteriorates past 90°), break off via lag pursuit or transition Engage → Climb, recover altitude, then re-engage from regained energy advantage. Hit rate is a secondary metric. The qualitative benchmark from the research note is "Veteran disengages 60–70% of fights it does not start from advantage" — translated into mechanics, the AI should *demonstrably* refuse to fight on into negative ΔE rather than burning energy chasing a manoeuvring target. If it never disengages, the lag-mode triggers are too lax; if it never engages, lead-mode threshold is too strict. The 2:30 window (rather than a more relaxed 5 minutes) reflects gameplay-engagement priority — a player should not have to wait minutes between observable AI tactical events. If the AI cannot demonstrate the cycle inside 2:30, either the energy-loss rate per engage pass is too slow (tuning issue) or the lag-mode triggers are too strict (design issue).

### 6.5 Out of scope for this slice

Scissoring, rolling scissors, vertical reposition (the historical Immelmann — a zoom-climb + stall-turn, **not** the modern half-loop-half-roll, which is folk myth per the research note), split-S, high-yo-yo / low-yo-yo, squadron coordination, damage-aware decision modulation, the D.VII "hung on the prop" near-stall trick (would require new physics work in the stabilizer). These are future slices and must not bleed into this one.

### 6.6 Doctrinal grounding

Explicit mapping between the next-slice code contract and its historical source, so the design rationale survives future refactors:

| Code element | Historical source | Note |
|---|---|---|
| `ΔE ≥ 0` precondition for lead pursuit | Boelcke Dictum #1 ("secure advantages before attacking") | Energy advantage is the modern formalisation of "advantage" |
| `aspect > 90°` triggers lag pursuit | Boelcke Dictum #5 ("assail your opponent from behind") — partially | 90° is the perpendicular bearing to target velocity; chosen over a stricter 60° rear-quarter threshold to preserve dogfight density in gameplay. A stricter threshold is a future tuning option |
| `aspect > 120°` doctrinal floor | Boelcke Dictum #6 ("if your opponent dives on you, fly to meet it") — by deferral | Head-on convergence is Evade-mode behaviour, not Engage-mode |
| Multi-window firing (`farFireRangeM = 300m`, `closeFireRangeM = 100m`, `snapFireRangeM = 50m`) | Boelcke Dictum #3 ("fire only at close range, and only when your opponent is properly in your sights") | The 300m window is generous by Mannock-Ball-Boelcke standards; flagged for future review |
| Engage → Climb on energy bleed (`engageAbandonAltitudeDropM = 500m`) | McCudden cycle: study, stalk, bounce, zoom-out | Already in code; energy-state model formalises the trigger |
| `lostGeometrySeconds = 4` (existing) | Research note §3.5: "Geometry lost (cannot regain a firing position within ~3 manoeuvres)" | At `decisionRateHz = 5`, 4 seconds ≈ 20 decision ticks; broadly aligned |
| `energyLowSpeedMs = 25`, `energyLowAltitudeM = 200` (existing) | Research note §3.5: hard floors below which the AI must disengage | Already encoded; the new energy-state model sits above these absolute floors as a *relative* metric |

Aspects of historical doctrine NOT encoded in this slice: sun positioning (visual-domain modelling not yet built), formation tactics (one AI), wingman protection (one AI), per-pilot skill tier / morale / physiology, fire reactions, wound states. All filed in §7.

---

## 7. Deferred

The research note `docs/research/AI Enemy Behaviour Modelling Research Note.docx` (or wherever it ends up filed) is the canonical reference for the items below. Each is its own slice; none should bleed into the §6 contract.

**Skill tiers (Rookie / Regular / Veteran / Ace).** Per research note §5.3. Fixed at instantiation, gates which behaviours are available, parameterises scan radius, reaction lag, fire accuracy, manoeuvre repertoire, disengage threshold, and spin-recovery probability. Replaces the single-AI assumption with a tier-parameterised one. The current Fokker D.VII implementation is implicitly "Veteran" in research-note terms.

**Morale / nerve states (Fresh / Steady / Wind-Up / Broken).** Drifts over a sortie and across a campaign. Modulates aggression and disengagement thresholds. Per research note §1, §5.1, §5.5.

**Physiological state (Healthy / Cold / Hypoxic / Wounded-light / Wounded-serious / Incapacitating).** Degrades control inputs and reaction time. Per research note §2.3. Hypoxia kicks in above 10,000 ft, becomes serious above 15,000 ft — currently irrelevant because the AI patrols at 1000 m (~3,300 ft).

**Wound and damage state taxonomy.** Per research note §2 — graduated probabilistic model: leg/foot wound (rudder degraded), arm/hand wound (no gun-jam clearance), torso wound (Voss-style 30–90s degradation), head wound (Richthofen-grazing vs Hawker-immediate). Requires a damage-modelling layer that doesn't yet exist.

**Fire reactions.** Per research note §2.2 — weighted options by nationality and skill: side-slip dive (Lufbery method), jump without parachute (rare, mostly pre-mid-1918 German), parachute (German post-mid-1918, ~⅔ success rate per Heinecke records), service-pistol suicide (Mannock's stated intention), ride-it-down. Requires a fire-state representation and a parachute system.

**Formation tactics.** Per research note §3.2 — Kette (3 aircraft), Jasta (8–12), Jagdgeschwader (4 Jastas), RFC patrol (5–7). Each implies wingman-aware behaviour and the Lufbery-circle defensive formation. Out of scope until multi-AI is on the roadmap.

**Per-ace personality overlays.** Per research note §5.3. Voss (+Aggression, +Risk, flat-skid snapshot), Mannock (+Formation discipline, +Wingman protection), McCudden (+Patience, +Altitude, stalker), Richthofen (+Conservatism, +Positional), Ball (+Solo, +Closing range), Udet (+Energy efficiency, sun-stalker), Bishop (+Solo, +Aggression). Best implemented as a "personality" ScriptableObject that overlays the base skill-tier parameters.

**D.VII "hung on the prop" near-stall manoeuvre.** Per research note §4.2 and Smithsonian. The D.VII retained aileron and elevator authority near the stall, allowing the pilot to "hang" the aircraft on its propeller and fire upward into the bellies of higher aircraft. Requires extending FlightStabilizer to model near-stall handling rather than treating stall as a failure mode. Distinctive signature manoeuvre — worth implementing properly when AI behavioural fidelity becomes a goal.

**Sun positioning and "Hun in the sun" detection bias.** Per research note §3.4 and §1.1. Requires a visual-domain model with sun-quadrant detection penalties. Currently the AI has uniform 360° detection out to `visualRangeM = 1000m`; reality was strongly directional.

**Gun jams.** Per research note §6. Vickers and Spandau jammed frequently; clearance required the cockpit hammer described in Lee's *Open Cockpit*. Modelled as ~5–15% chance per long burst, 5–20s clearance with two hands free, impossible with one hand wounded. Would extend the WeaponSystem with a jam state.

**AI difficulty surface.** Per the current code: single-value-per-tunable today. A "skill" slider would scale clamps, decision rate, and firing thresholds together — partially superseded by the skill-tier work above but useful as a player-facing difficulty selector orthogonal to the AI's internal "skill level."

**Composite-manoeuvre repertoire.** Scissoring, rolling scissors, historical Immelmann (zoom + stall-turn), split-S, high/low yo-yo, chandelle. Per §6.5. Each requires the FSM to express temporally-extended manoeuvres rather than per-tick setpoints — a non-trivial architectural extension.

**Multi-target arbitration.** Currently one target, hard-wired. Real WW1 dogfights involved 5–20 aircraft per side; threat-selection logic (closest? highest threat? least defended?) is its own slice.

**Disengagement criteria refinement.** Per research note §3.5: fuel <35–40%, out of one gun + second gun low, outnumbered ≥2:1 without altitude advantage, any wound, damage to flight controls/engine, geometry lost. Several of these are partially encoded (`ammoLowFraction`, `componentLowFraction`); fuel modelling is absent; outnumbered-reasoning is absent (single AI).

---

## 8. Source materials

- **`docs/research/AI Enemy Behaviour Modelling Research Note.docx`** (or wherever filed) — comprehensive 250-paragraph historical research note covering Dicta Boelcke, named-pilot styles (Boelcke, Richthofen, Voss, Ball, Mannock, McCudden, Udet, Bishop, Rickenbacker, Grinnell-Milne), wound/damage state taxonomy, formation tactics, hypoxia/cold, parachute statistics, aircraft-specific behaviour, and a programming roadmap. Canonical reference for all §7 deferred items.
- **Boelcke's Dicta (1916)** — eight tactical rules, full text in research note §Key Findings #1. The single most important text for AI behaviour modelling.
- **McCudden, *Flying Fury* (1918)** — primary source for the "study, stalk, bounce, zoom-out" pattern that the McCudden cycle benchmark in §6.4 is named after.
- **Mannock's diary and Jones, *King of Air Fighters* (1934)** — primary sources for formation discipline and wingman protection doctrine.
- **Richthofen, *Der rote Kampfflieger / The Red Battle Flyer*** — primary source for positional / conservative pursuit doctrine and the Hawker engagement (test case for pursuit-to-the-lines behaviour, §7 research-note §7).
- **Hart, *Bloody April*** — operational context for April 1917 and the "11-day rookie life expectancy" datum.
- **FlightStabilizer regression guard** — `Assets/Tests/EditMode/FlightStabilizerTests.cs::InvertedDescentRecoversInSixSeconds` pins the inner-loop architecture across all future changes to the FSM.

## Appendix A — Glossary

- **Aspect angle.** Angle between the target's velocity vector and the line from target to attacker. 0° = directly astern (best gun position), 180° = head-on.
- **Boelcke Dicta.** Eight tactical rules codified by Oswald Boelcke in 1916 at the request of Oberst Hermann von der Lieth-Thomsen, distributed throughout the Fliegertruppe and quietly adopted by the British (Mannock's "Rules of Combat" are a near-paraphrase). The doctrinal substrate of late-war fighter combat.
- **Bounce.** The diving attack from up-sun, at high speed, with a single firing pass and zoom-climb away. The classic Boelcke / McCudden / Udet / Richthofen attack. Currently subsumed into the Engage state; a future slice may extract it.
- **Cos(bank) gate.** The mathematical relationship `world-frame pitch rate = body-frame pitch rate × cos(bank)` that the stabilizer's PID output respects, making the eight rounds-4a–4h ad-hoc overrides unnecessary.
- **Lag pursuit.** Pointing the nose behind the target's predicted track. Preserves energy and controls closure rate. The historical default for re-positioning after a failed pass.
- **Lead point.** Position the attacker aims at to put bullets where the target will be when the bullets arrive. Function of muzzle velocity, range, and target velocity.
- **McCudden cycle.** Study → stalk → bounce → zoom-out → re-engage from regained energy advantage. The disciplined-engagement pattern that distinguished James McCudden's 57-victory record and is the qualitative target of the §6.4 playtest acceptance criterion.
- **Pure pursuit.** Pointing the nose directly at the target's current position. Closes range fastest but bleeds energy and ends in overshoot. Transient mode in the §6.2 decision tree.
- **Specific energy (E_s).** Sum of potential and kinetic energy per unit mass, expressed in altitude units. `E_s = h + v²/(2g)`. The arithmetic formalisation of what WW1 pilots intuitively understood as "altitude as a savings account."
- **Stalk.** Patience-based phase between detection and engagement: the attacker shadows the target from a position upsun and above, matching speed, refusing to be drawn down, until either the target descends or a textbook 6 o'clock high position becomes achievable. McCudden's signature behaviour. Currently subsumed into Patrol/Search; a future slice may extract it.

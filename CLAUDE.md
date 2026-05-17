# CLAUDE.md

Project-level notes that span more than one source file. Add entries here when
something is non-obvious from reading the code and would surprise a future
developer (human or AI) modifying the affected area.

---

## Playtest harness reflection targets

`Assets/Tests/PlaytestHarness/PlaytestRunner.cs` reaches into private members
of production classes via reflection. Caching `MethodInfo` / `FieldInfo` handles
once at first scenario run; lookups throw `InvalidOperationException` on the
first scenario invocation if a name has changed.

| Class                 | Member                | Kind                | Use                                                 |
|-----------------------|-----------------------|---------------------|-----------------------------------------------------|
| `AircraftController`  | `Awake`               | private method      | Manual lifecycle (EditMode skips MonoBehaviour Awake)|
| `AircraftController`  | `FixedUpdate`         | private method      | Per-tick simulation step                            |
| `AircraftController`  | `initialSpeedMs`      | private SerializeField | Set per scenario before Awake                    |
| `AircraftController`  | `_entity`             | private field       | Reach the AircraftEntity to reseed its RNG          |
| `AircraftEntity`      | `_rng`                | private readonly field | Reseed from `scenario.Seed` (see RNG note below) |
| `AIController`        | `Awake`               | private method      | Manual lifecycle (EditMode skips MonoBehaviour Awake)|
| `AIController`        | `_diagPursuitMode`    | private field       | Telemetry sampling (pursuit mode per tick)          |
| `AIController`        | `_diagDeltaE`         | private field       | Telemetry sampling (ΔE per tick)                    |

**Renaming any of these will break the playtest harness with a runtime error
on first scenario invocation, not at compile time. If you need to rename,
search `Assets/Tests/PlaytestHarness/` for the old name and update
`PlaytestRunner.cs`'s reflection calls. Production-code consumers do not
depend on any of these names.**

`AIController.StateEnteredTime` is exposed `internal` via
`InternalsVisibleTo("AcesOverTheLines.PlaytestHarness")` rather than
reflection because it has a dedicated unit test in
`AIControllerTests.NowSecondsSource_WhenOverridden_DrivesEngageDwell`.

## AircraftEntity unseeded `System.Random`

`AircraftEntity` instantiates an unseeded `System.Random` in its default
constructor path (`Assets/Scripts/Flight/AircraftEntity.cs`, the
`_rng = rng ?? new System.Random()` line). The constructor accepts an optional
seeded `rng` parameter but `AircraftController.Awake` does not pass one.

The playtest harness papers over this via reflection seeding from
`scenario.Seed` after `Awake` returns. The RNG is latent under normal pursuit
scenarios — it only fires under spin / pilot-out / fuel-fire branches — but
reseeding is necessary for determinism robustness in scenarios that provoke
those states (HardEvader and LevelOrbit are highest risk).

**If you fold seed-passing into AircraftEntity's constructor properly in a
future cleanup (e.g., adding a serialisable seed field to AircraftController
and forwarding it to the entity constructor), the harness's reflection seeding
becomes redundant and can be removed.** Search `Assets/Tests/PlaytestHarness/
PlaytestRunner.cs` for `_entityRngField` / `_rng` and delete the relevant
block.

## Phase 1 Engage doctrine — load-bearing thresholds

The Phase 1 boom-and-zoom rewrite of `AIController.DoEngage` and
`UpdateStateTransitions` introduced one new SerializeField (`perchAdvantageM`,
default 60m) and several hardcoded constants. The hardcodes are not arbitrary —
each was tuned through 8 iterations against the five playtest-harness scenarios
and would degrade scenario outcomes if changed without re-running the harness.

### Energy-discipline gate requires range > 800m

`UpdateStateTransitions` Engage branch fires `TransitionIfChanged(Climb)` when
`deltaE < -100m`. **Without the `range > 800m` condition on that gate,
scenarios with significant vertical separation at start (e.g. Scenario 4,
target 500m above AI) cycle Engage→Climb→Patrol every ~500ms and never execute
a pursuit.** The energy gate triggers immediately because the altitude
difference produces deltaE ≈ -500m; the Climb→Patrol exit (alt > 700 AND
vy > 0) fires immediately because the AI is already at altitude. Endless loop,
zero pursuit.

Discovered during Phase 1 iteration rev-#8 (final iteration). The range gate
says "below 800m, commit to the merge despite energy deficit — the dive
recovers kinetic energy". This is load-bearing for Scenario 4.

### Scenario 1 (co-altitude start) is a known Phase 1 limit

`StraightLevel_Decoy` (AI and player both at 1000m, 700m apart, player slower)
is unresolved under Phase 1's pure-pursuit doctrine. Telemetry at firing
range shows the AI achieves:

- Altitude parity (0.002m diff)
- Yaw alignment (signedAngleOffDeg ~ 0°)
- Range < 250m

…but **inherited vertical velocity from the climb-to-perch leaves the body
pitch ~17° off the line of sight at firing range**. The 2° entry cone tests
3D angle including pitch; AI's pitched-up body forward never aligns with the
horizontal line to target within the 0.5s firing window. The flight model's
PID + rate limits cannot damp the inherited vy fast enough. Iter rev-#5 (pitch
override to 0) and rev-#6 (vy-proportional damping) both induced P-only
oscillation rather than damping.

Phase 2 (angles fighting) should resolve this via sustained banked-turn
pursuit where attitude reaches a trim state matching the line of sight, not
a stepped pitch transition.

### Phase 1 hardcoded constants

| Constant | Value | Used by | Tuning note |
|---|---|---|---|
| `perchAdvantageM` | 60m (SerializeField) | DoEngage perch | The only Phase 1 SerializeField. 100→60 in iter rev-#2 |
| Perch tolerance band | 20m | DoEngage pitch logic | If below perch by >20m, climb |
| Above-target dive trigger | 5m | DoEngage pitch logic | Lowered from 30m → 5m; airframe ceiling above target is ~16m |
| Dive aggressiveness range | 200m | DoEngage pitch logic | Lowered from 400m → 200m; keeps -0.20 dive longer |
| Post-pass extension hold | 1.5s | UpdateStateTransitions | Range must open continuously this long before Climb |
| Energy-gate range condition | 800m | UpdateStateTransitions | See "Energy-discipline gate requires range > 800m" above |
| Engage stalemate timeout | 60s (SerializeField) | UpdateStateTransitions | Pre-existing SerializeField; was 25s, bumped in iter 3 |

Parameterise these as SerializeFields only if Phase 2 or later needs to vary
them per-aircraft or per-doctrine. Until then, the hardcodes carry their tuning
in source where it can be read alongside the surrounding control flow.

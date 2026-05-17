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

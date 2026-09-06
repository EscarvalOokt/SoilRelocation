# Soil Relocation architecture

This document defines the accepted product architecture and implementation boundaries of Soil Relocation.

It is an architecture specification, not a RoadMap, research log, or implementation guide.

## Sources of truth

1. The installed RimWorld 1.6 implementation defines vanilla runtime behavior.
2. The current production implementation defines behavior actually available in the built mod.
3. This document records accepted Soil Relocation architecture decisions and stable project contracts.
4. `docs/testing.md` defines automated testing and runtime acceptance boundaries.

## Runtime baseline

- RimWorld 1.6.
- .NET Framework 4.7.2.
- No official DLC dependencies are required by the project baseline.
- Harmony is a required runtime dependency for project-owned RimWorld integration boundaries: native cancellation integration and language/play-data reinjection invalidation of cached final `TerrainDef` identities. The project compiles against `Lib.Harmony.Ref` and relies on the `brrainz.harmony` mod to provide the shared runtime assembly; Soil Relocation does not bundle its own `0Harmony.dll`.
- Allow Tool compatibility is optional. Soil Relocation has no compile-time or mandatory runtime dependency on Allow Tool and remains fully functional when it is absent.

## Product boundaries

- Soil Relocation is a standalone project with its own package, assembly, namespace, and Def identifiers.
- The project relocates a fixed set of soil-like terrain families rather than providing a general terrain-copying system.
- Source-family support, placement-destination eligibility, and associated-natural-stone reconstruction are separate contracts and must not be inferred from one another.
- Technical restrictions are introduced only where required for correctness and a stable relocation lifecycle; use of supported behavior remains a player choice.

## Relocation family model

The built-in relocation families are:

| Family | Canonical source/result | Built-in source membership |
| --- | --- | --- |
| Sand | `Sand` | `Sand` |
| Gravel | `Gravel` | `Gravel` |
| Soil | `Soil` | `Soil`, `MossyTerrain`, `GlowforestSoil`, `GrasslandSoil` |
| Rich Soil | `SoilRich` | `SoilRich`, `Riverbank` |

A terrain with `driesTo != null` is excluded from the relocatable source contract.

`RelocationRuleResolver` is the source-family authority. Placement destination rules do not make a terrain a relocatable source, and associated-rock reconstruction does not make a terrain a source family.

## Resource accounting

- Digging one valid source cell produces exactly one portable resource belonging to that relocation family.
- Placing one terrain cell consumes exactly one portable resource.
- Built-in families own their canonical resource and canonical placement terrain.
- The accounting contract is fixed at `1:1`; work amount, work speed, selected skills, XP, hauling, and placement mode do not change resource quantity.
- Resource creation occurs only after successful final Dig validation.
- Resource consumption occurs only after successful final Place validation and immediately before the terrain mutation is committed.

## Portable resource contract

Portable relocation resources use the following fixed item-level contract:

- `stackLimit = 1`;
- `useHitPoints = false`;
- `tradeability = None`.

These values are intentional architecture decisions. Physical and presentation balance values such as Mass, path cost, and Beauty are not part of this architecture contract.

## Third-party family membership

Third-party integration extends membership of an existing relocation family; it does not create arbitrary new families in the initial extension contract.

A third-party terrain can participate only through the established family-resolution contract. Broad terrain-category heuristics are not a substitute for explicit family membership.

## Digging result

Digging removes the relocatable source terrain and exposes the deterministic associated natural-stone result for that cell when the map supports that reconstruction contract.

This is not an attempt to recover the historical final terrain that happened to exist before map generation completed. It reconstructs the associated natural-rock substrate defined by the project-owned deterministic profile.

Terrain mutation uses the normal RimWorld `TerrainGrid` API rather than direct terrain-array mutation.

## Natural-rock reconstruction

Associated-natural-stone reconstruction is deterministic and map-owned.

- The reconstruction profile is derived from eligible natural rock definitions and deterministic map inputs.
- Runtime reconstruction does not rerun vanilla global `RockNoises`, does not consume global `Rand`, and does not persist a universal per-cell geological grid.
- The map component commits reconstruction state only after a successful Dig operation needs it.
- Save/load preserves the component-owned deterministic profile/state required by the implemented contract.
- Reconstruction identity is independent of placement-destination eligibility.

## Placement resolution

Place supports two result modes:

- `Canonical` — place the relocation family's canonical terrain.
- `Biome-native` — resolve the family-specific biome-native result when the accepted resolver can do so, otherwise use the established fallback behavior.

The resolved terrain is not trusted merely because it came from a resolver. Final completion validation must verify the resolved result before resource consumption and `TerrainGrid.SetTerrain`.

## Destination terrain eligibility

The destination policy supports natural-rock-derived terrain variants through a dedicated derived-identity contract.

The classification starts from eligible natural rock `ThingDef` definitions and follows their terrain relationships, including the accepted Rough, RoughHewn, and Smooth variants. This is intentionally narrower than generic terrain properties.

The derived identity cache is rebuilt after final Def data changes, including the supported language/play-data reinjection lifecycle, so cached `TerrainDef` identity cannot remain stale after Def replacement.

Natural-stone-derived eligibility must not be broadened through generic properties such as:

- `TerrainDef.natural`;
- `categoryType == Stone`;
- `SmoothableStone`;
- the mere presence of `smoothedTerrain`;
- appearance or naming similarity to natural rock.

Constructed stone floors remain outside the natural-stone-derived destination contract.

## Special placement destinations

Special placement destinations are an explicit contract separate from natural-stone-derived classification, relocation source-family membership, and associated-natural-stone reconstruction.

### Accepted identities

The placement contract explicitly accepts these final terrain identities as destinations:

- exact persistent `Ice`;
- exact `VolcanicRock`;
- exact `VolcanicRock_Smooth`.

These are explicit identities, not generalized classifications, and their support does not make them relocatable source families or natural-stone-derived members.

The Ice contract does **not** include:

- `ThinIce`;
- generic `TerrainDef.IsIce` matching;
- unrelated temporary or Ice-tagged terrain;
- Ice as a relocatable source family or natural-stone-derived lineage member.

The volcanic contract does **not** include:

- `CooledLava`;
- `LavaDeep`;
- `LavaShallow`;
- generic `natural`, `categoryType == Stone`, `SmoothableStone`, or `smoothedTerrain` matching;
- volcanic terrain as a relocatable source family or natural-stone-derived lineage member.

The volcanic contract is based on the researched Odyssey lifecycle, including vanilla `VolcanicRock -> VolcanicRock_Smooth` smoothing and the resulting loss of exact Lava Flow substrate eligibility. It is not inferred from the Ice contract or from a generic notion that volcanic terrain is stone.

## Dig / Place order backend

Dig and Place are represented by selectable relocation order Things rather than transient designations alone.

- Orders expose persistent selectable state required by the implemented UX and work lifecycle.
- Place has an `AwaitingMaterial` phase and a `ReadyForWork` phase.
- Material delivery belongs to Hauling; actual relocation work belongs to the dedicated `SoilRelocation` work type.
- A ready Place order owns the delivered canonical resource until successful placement or cancellation.
- Order state survives save/load according to the implemented Scribe contract.

## Validation semantics

Creation-time validation improves UX but is not the final invariant.

Relocation-specific validation distinguishes permanent from transient failure where the distinction affects whether an existing order should remain actionable.

- Permanently invalid relocation state cancels the affected order during project-owned revalidation.
- Transient fire and removable-plant prerequisites preserve the order until work can resume.
- Work discovery, job start, and especially successful completion revalidate mutable requirements.
- Resource creation/consumption and terrain mutation happen only after successful final validation.

Completion-time validation includes, where applicable:

- current source/destination terrain and source-family eligibility;
- structural blockers and conduit policy;
- plant prerequisites;
- fire;
- destination substrate policy;
- final resolved placement terrain;
- required portable resource for placement.

## UX and cancellation

Relocation orders are selectable and participate in their project-owned Inspect/gizmo UX.

- Ordinary cancellation integrates with RimWorld's native `Designator_Cancel` through Harmony.
- Optional Allow Tool bulk cancellation uses the same Soil Relocation cancellation backend when Allow Tool is present.
- Soil Relocation does not extend vanilla map `Designator_Forbid` / `Designator_Unforbid` to non-item orders. Selected relocation orders use their own Allow/Forbid state.
- Cancellation of a `ReadyForWork` Place order refunds its contained canonical resource exactly once.

## Cell blockers and transient prerequisites

Placement and digging use shared project-owned blocker semantics rather than ad-hoc checks in individual UI or JobDriver paths.

Structural blockers that make the operation permanently invalid are rejected consistently. Transient prerequisites such as fire or removable plants can delay work without destroying a still-valid order.

The final completion path is authoritative.

## Resource balance and terrain mutation

The project does not use hidden resource multipliers, terrain cloning, or direct grid-array mutation.

A successful Place operation consumes the owned resource and commits the resolved persistent terrain through RimWorld's terrain API. A failed final validation leaves the terrain unchanged and does not consume the resource.

## Work and scheduling

Actual relocation work is owned by the dedicated `SoilRelocation` work type.

The implemented work contract supports configurable work amount and selected skills while using vanilla work-speed Stats for effective speed where applicable.

JobDriver-local work progress and configuration snapshots are serialized with the active job so save/load does not silently reinterpret in-progress work using newly changed settings.

Adding the custom work type may shift positional `DefMap<WorkTypeDef, int>` priorities in an existing save when loaded WorkType definition order changes. Soil Relocation intentionally does not provide a migration layer for that case; affected existing saves require manual review in the Work tab.

## Relocation XP

Relocation XP uses one configurable raw budget shared by digging and placement:

```text
range   0.03..0.30
step    0.03
default 0.09
```

With no selected skill, relocation grants no skill XP. With one selected skill, the full configured raw budget is learned by that skill on each work tick. With multiple selected skills, the same total budget is split evenly across them.

The configured XP rate and selected skill set are snapshotted per active `JobDriver` and serialized with it. Raw XP is not multiplied by effective work speed.

## Existing-save removal boundary

Removing Soil Relocation from an existing save is supported only with explicit preparation; it is not a silent or unconditional compatibility path.

- A save must not persist a pawn with an active Soil Relocation Dig or Place `JobDriver` when the mod is disabled. Such active-job state is outside the supported removal boundary and can leave the affected pawn in an invalid load/spawn state with recurring runtime errors.
- Before removal, relocation work must be stopped and relocation orders should be cancelled while Soil Relocation is still present so project-owned job/order cleanup runs with the production types available. Remaining portable relocation resources should also be removed or consumed when practical because they cannot survive removal of their defining mod.
- Even after preparation, the first load without Soil Relocation can report missing mod-owned Def/type/Scribe state, including the serialized natural-rock map component. The supported procedure requires saving once without the mod after a successful load and then loading that new save.
- Soil Relocation does not provide automatic migration or repair for already corrupted post-removal saves. Automatic pre-uninstall cleanup tooling is not part of the current production contract.

The player-facing uninstall procedure is documented in `README.md`. Runtime acceptance responsibility for this boundary is defined in `docs/testing.md`.

## Localization boundary

- English localization is the source of truth for all user-facing mod terminology.
- User-facing English keys are added together with the production feature that introduces them; production behavior should not rely on hard-coded Russian or Ukrainian user-facing strings.
- The shipped English key set is the stabilized source for the current translations and user-facing terminology.
- Russian and Ukrainian localization are shipped from the stabilized English key set and must remain in key parity with it as user-facing functionality changes.
- Russian and Ukrainian translations follow the official terminology used by the corresponding vanilla RimWorld localization where an established vanilla term exists.
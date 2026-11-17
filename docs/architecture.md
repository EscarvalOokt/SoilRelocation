# Soil Relocation architecture

This document describes Soil Relocation's implementation and gameplay contracts. Player instructions and build commands are in the [README](../README.md). Verification procedures are in the [testing policy](testing.md).

## Runtime and package

The production project builds for RimWorld 1.7 on `net10.0`. The Windows game installation provides `Assembly-CSharp.dll` and `UnityStubs.dll`. `brrainz.harmony` supplies the shared `0Harmony.dll` used for native cancellation integration and cache invalidation after language and play-data reinjection. Both game and Harmony DLLs are compile references supplied by the local installation. Core plus Harmony is sufficient. Allow Tool integration is optional.

`About.xml` declares the supported game versions. `LoadFolders.xml` loads shared content from the package root in both versions and selects `1.6/` for RimWorld 1.6 or `Current/` for RimWorld 1.7. The 1.6 DLL is preserved in `mod/1.6/Assemblies/`. `HistoricalModVersion` records its SHA-256 in the production project, and deployment verifies that checksum. When a later game version needs a new DLL, preserve the last compatible DLL and checksum under its game version and update the build references, `CurrentRimWorldVersion`, `About.xml` and `LoadFolders.xml` together.

Ordinary builds write the current production DLL to `bin/` with `DeployToGame=false`. `DeployToGame=true` combines the shared source content, preserved DLL and current build output in an external mod directory. The complete package contains the two Soil Relocation DLLs. The `escarval.soilrelocation` package ID, assembly name and serialized Def and class names identify the same mod across game versions.

The deployment targets validate the destination, package identity, version mappings and historical DLL hash. A nonempty destination must identify Soil Relocation in `About/About.xml`. The deployment task stages the new package in a sibling directory, including a local `About/PublishedFileId.txt` when the source supplies no ID. It retains the previous installation while transferring unmanaged entries and publishes the prepared directory by renaming it. The journal records which entries to restore if the transfer is interrupted. A retry takes the destination lock and recovers the transaction before staging again. Top-level `Assemblies/` and `1.7/` belong to the managed contents and are removed during the update. [Deployment targets](../build/ModDeployment.targets) · [Deployment task](../build/DeployModPackage.cs) · [Build instructions](../README.md#deploying-a-development-build)

## Implementation map

| Location | Responsibility |
| --- | --- |
| [SoilRelocationMod.cs](../src/SoilRelocation/SoilRelocationMod.cs), [Settings](../src/SoilRelocation/Settings) | Mod startup, settings UI and persistence |
| [Domain](../src/SoilRelocation/Domain), [Substrate](../src/SoilRelocation/Substrate) | Relocation families, placement rules and natural-rock reconstruction |
| [Relocation](../src/SoilRelocation/Relocation), [Digging](../src/SoilRelocation/Digging), [Placement](../src/SoilRelocation/Placement) | Designators, orders, work givers, job drivers and resource handling |
| [Work](../src/SoilRelocation/Work), [Patches](../src/SoilRelocation/Patches), [Compatibility](../src/SoilRelocation/Compatibility) | Work and XP policy, RimWorld integration and optional Allow Tool integration |
| [About.xml](../mod/About/About.xml), [LoadFolders.xml](../mod/LoadFolders.xml), [SoilRelocation.csproj](../src/SoilRelocation/SoilRelocation.csproj), [deployment targets](../build/ModDeployment.targets) | Package metadata, game-version loading, build configuration and deployment |

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

[`RelocationRuleResolver`](../src/SoilRelocation/Domain/RelocationRuleResolver.cs) determines source-family membership.

## Resource accounting

Each successful Dig produces one portable resource for its family. Each successful Place consumes one corresponding resource per cell. Work amount, work speed, selected skills, XP, hauling and placement mode do not change this `1:1` ratio. Each built-in family defines its canonical resource and canonical placement terrain.

## Portable resource contract

Portable relocation resources use the following fixed item-level contract:

- `stackLimit = 1`;
- `useHitPoints = false`;
- `tradeability = None`.

These values are intentional architecture decisions. Physical and presentation balance values such as Mass, path cost, and Beauty are not part of this architecture contract.

## Third-party family membership

The supported third-party contract extends membership of an existing built-in family. An eligible terrain has one enabled `RelocationTerrainExtension` pointing to a family with both canonical Defs. The resolver rejects absent, duplicate, disabled or invalid membership and any terrain with `driesTo` set. On RimWorld 1.7 it also excludes terrain with `temporary` or `isFoundation` set from digging and biome-native placement choices. RimWorld stores these layers separately, so replacing the underlying surface would leave the same material available to dig again. The Place order catalog covers the four built-in families.

## Digging result

On a supported map, Dig exposes the `building.naturalTerrain` of the natural rock selected by the cell's reconstruction profile. For example, a profile that selects `SolidIce` for the cell yields `Ice`.

## Natural-rock reconstruction

Associated-natural-stone reconstruction is deterministic and map-owned.

- The reconstruction profile is derived from eligible natural rock definitions and deterministic map inputs.
- On RimWorld 1.7, capture and save repair require a persistent surface in `building.naturalTerrain`. This works for biome-forced rocks and for `SolidIce` with its `Ice` surface. An unusable slot takes another usable rock from the same profile while retaining its noise seed. If every saved slot needs replacement, repair uses eligible rocks from the current map environment.
- Runtime reconstruction does not rerun vanilla global `RockNoises`, does not consume global `Rand`, and does not persist a universal per-cell geological grid.
- The map component commits reconstruction state only after a successful Dig operation needs it.
- Save/load preserves the component-owned deterministic profile/state required by the implemented contract.
- Reconstruction identity is independent of placement-destination eligibility.

## Placement resolution

Place supports two result modes:

- `Canonical` — place the relocation family's canonical terrain.
- `Biome-native` — prefer the destination biome's gravel terrain if it belongs to the same family. Otherwise use the unique same-family terrain from the biome's fertility list. If there is no unique candidate, use the family's canonical terrain.

## Destination terrain eligibility

The destination policy accepts terrain with `TerrainDef.IsRock` directly. It also gathers derived terrain identities from eligible natural rock `ThingDef` definitions through their `naturalTerrain`, `leaveTerrain` and smoothed terrain references, covering Rough, RoughHewn and Smooth surfaces. The special destinations below use a separate exact-identity check.

A Harmony postfix invalidates the derived identity cache after `LoadedLanguage.InjectIntoData_AfterImpliedDefs`. The next lookup that needs the cache rebuilds it from the current Def references.

Natural-stone-derived eligibility must not be broadened through generic properties such as:

- `TerrainDef.natural`;
- `categoryType == Stone`;
- `SmoothableStone`;
- the mere presence of `smoothedTerrain`;
- appearance or naming similarity to natural rock.

Constructed stone floors remain outside the natural-stone-derived destination contract.

## Special placement destinations

### Accepted identities

The placement contract explicitly accepts these final terrain identities as destinations:

- exact persistent `Ice`;
- exact `VolcanicRock`;
- exact `VolcanicRock_Smooth`.

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

On a LavaField map with active Lava Flow, a completed Place job in **Standard** mode leaves the new terrain as the persistent surface on either accepted volcanic destination. The previous volcanic rock is removed from the terrain layers, and the same Lava Flow rejects the cell as a spread target. The [focused Odyssey checks](testing.md#focused-odyssey-runtime-responsibilities) verify this behavior.

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
- Fire and plants, including plants that cannot currently be removed, preserve an existing order until work can resume.
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
- Optional Allow Tool bulk cancellation uses the same Soil Relocation cancellation backend when the expected `Activate(Designator, Map)` and `ActivationResult` API resolves. The compatibility patches skip targets whose API does not match.
- Soil Relocation does not extend vanilla map `Designator_Forbid` / `Designator_Unforbid` to non-item orders. Selected relocation orders use their own Allow/Forbid state.
- Cancellation of a `ReadyForWork` Place order refunds its contained canonical resource exactly once.

## Cell blockers and transient prerequisites

Placement and digging use shared project-owned blocker semantics rather than ad-hoc checks in individual UI or JobDriver paths.

Structural blockers are permanent validation failures. Fire and plants that cannot currently be removed prevent new orders. A removable plant can become a prerequisite through RimWorld's harvest-wood or plant-cutting designation. During work, the shared validation rules keep the existing order until a transient blocker clears.

The final completion path is authoritative.

## Resource balance and terrain mutation

Dig produces the portable resource and commits the prepared reconstruction profile before exposing the substrate. Place consumes the delivered resource immediately before committing the resolved terrain. Both operations update the persistent surface through `TerrainGrid.SetTerrain` and clear snow and filth from the affected cell.

## Work and scheduling

Actual relocation work is owned by the dedicated `SoilRelocation` work type.

The implemented work contract supports configurable work amount and selected skills while using vanilla work-speed Stats for effective speed where applicable.

The JobDriver snapshots work amount, selected skills and raw XP rate when work starts. It serializes those values and progress with the active job. Conduit permission and placement resolution use the current settings at revalidation.

Adding the custom work type may shift positional `DefMap<WorkTypeDef, int>` priorities in an existing save when loaded WorkType definition order changes. Soil Relocation intentionally does not provide a migration layer for that case; affected existing saves require manual review in the Work tab.

## Relocation XP

Digging and placement share one configurable raw XP budget. The [player settings](../README.md#settings) list its range, step and default.

With no selected skill, relocation grants no skill XP. With one selected skill, the full configured raw budget is learned by that skill on each work tick. With multiple selected skills, the same total budget is split evenly across them.

Raw XP per tick is independent of effective work speed.

## Existing-save removal boundary

The [player-facing removal procedure](../README.md#removing-from-an-existing-save) explains how to prepare, save, and reload the colony.

- A save must not persist a pawn with an active Soil Relocation Dig or Place `JobDriver` when the mod is disabled. Such active-job state is outside the supported removal boundary and can leave the affected pawn in an invalid load/spawn state with recurring runtime errors.
- Before removal, relocation work must be stopped and relocation orders should be cancelled while Soil Relocation is still present so project-owned job/order cleanup runs with the production types available. Remaining portable relocation resources should also be removed or consumed when practical because they cannot survive removal of their defining mod.
- Even after preparation, the first load without Soil Relocation can report missing mod-owned Def/type/Scribe state, including the serialized natural-rock map component. The supported procedure requires saving once without the mod after a successful load and then loading that new save.
- Soil Relocation does not provide automatic migration or repair for already corrupted post-removal saves. Automatic pre-uninstall cleanup tooling is not part of the current production contract.

The [save/load and removal scenarios](testing.md#saveload-and-removal-compatibility) define how to verify this boundary for a candidate.

## Localization boundary

- Shared Defs contain the English text for items, orders and work so every game language has a fallback. Russian and Ukrainian override these fields through `DefInjected`.
- English, Russian and Ukrainian provide the same keys for strings translated from production code.
- Russian and Ukrainian translations follow the official terminology used by the corresponding vanilla RimWorld localization where an established vanilla term exists.
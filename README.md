# Soil Relocation

A RimWorld 1.6 mod that lets colonists dig up supported soil-like terrain, haul it as a resource, and place it somewhere else.

Status: `released`

## Features

- Dig up and relocate Sand, Gravel, Soil, and Rich Soil.
- Each successfully dug tile gives one matching haulable resource, and each successfully placed tile uses one resource.
- Supported local terrain such as Mossy Terrain, Glowforest Soil, Grassland Soil, and Riverbank is handled through the corresponding relocation family.
- Digging removes the supported soil-like terrain and restores the natural rock or ice expected at that location.
- Choose whether placed terrain uses its standard version or matches the local biome when possible.
- Colonists haul dug material to Place orders before actual relocation work begins.
- Dig and Place use the dedicated `SoilRelocation` work type in the Work tab.
- Work amount, affected skills, XP gain, and relocation under conduits are configurable.
- Ordinary cancellation integrates with RimWorld's native Cancel designator and standard Cancel hotkey.
- Optional Allow Tool compatibility extends its bulk cancellation actions to relocation orders without creating a required dependency.
- English, Russian, and Ukrainian localization is included.

## Supported terrain

The built-in relocation families are:

| Material | Built-in source terrain | Standard placed terrain |
| --- | --- | --- |
| Sand | `Sand` | `Sand` |
| Gravel | `Gravel` | `Gravel` |
| Soil | `Soil`, `MossyTerrain`, `GlowforestSoil`, `GrasslandSoil` | `Soil` |
| Rich Soil | `SoilRich`, `Riverbank` | `SoilRich` |

Terrain with `driesTo != null` is not directly relocatable even when it otherwise belongs to a supported family. Some such terrain can become relocatable after it dries into a supported terrain type.

Third-party terrain can use Soil Relocation's existing membership extension contract to opt into, override membership to, or explicitly opt out of one of the four supported families. The extension contract does not create new relocation families or independent portable-resource semantics.

## Placement destinations

Relocated terrain can be placed on natural-rock-derived Rough, RoughHewn, and Smooth terrain variants, such as the corresponding Granite, Marble, Slate, and other natural-rock surfaces.

Persistent `Ice`, `VolcanicRock`, and `VolcanicRock_Smooth` are also valid placement destinations. This does not make rock, ice, or volcanic terrain relocatable source families.

Constructed stone floors such as tile and flagstone remain ineligible. `ThinIce`, `CooledLava`, `LavaDeep`, `LavaShallow`, and unrelated terrain that merely shares Ice- or stone-like properties are not supported by association.

## Settings

| Setting | Supported values | Default |
| --- | --- | --- |
| Placed terrain type | `Standard`, `Match local biome` | `Standard` |
| Allow relocation under conduits | On / Off | Off |
| Digging work required | `50..5000`, step `10` | `1000` |
| Placement work required | `50..5000`, step `10` | `1000` |
| Digging skills | Any selection of Mining, Construction, Plants, or none | None |
| Placement skills | Any selection of Mining, Construction, Plants, or none | None |
| Skill XP rate | `0.03..0.30`, step `0.03` | `0.09` |

If no skill is selected, the operation uses general work speed and grants no selected-skill XP. If several skills are selected, their effective work speeds are averaged and the available XP is divided between them.

Changes to work amount, selected skills, and XP settings affect newly started relocation work rather than changing work already in progress.

## Relocation workflow

Dig creates a persistent selectable relocation order. Once the work completes successfully, the dug tile produces one matching portable resource.

Place uses a two-phase workflow:

```text
Place order
→ haul one matching resource to the destination
→ Relocate work
→ final validation
→ consume the resource
→ place the terrain
```

Permanent placement failures cancel the affected order during relocation-specific revalidation. Transient fire and removable-plant prerequisites preserve the order until work can resume. Cancelling a Place order after its resource has been delivered refunds that resource exactly once.

## Requirements and compatibility

- RimWorld 1.6.
- Harmony (`brrainz.harmony`) is required at runtime.
- No official DLC is required.
- Allow Tool is optional; Soil Relocation remains fully functional when it is absent.
- Development builds target .NET Framework 4.7.2.

Representative Core, Odyssey, all-official-DLC, Allow Tool, save/load, localization, and synthetic third-party extension scenarios are covered by the acceptance policy in `docs/testing.md`.

## Existing saves and uninstalling

### Adding to an existing save

Soil Relocation adds a custom `SoilRelocation` work type. RimWorld stores Work-tab priorities positionally, so enabling the mod in an existing save can shift previously stored priorities when the loaded WorkType order changes. Review the Work tab after adding the mod and correct any affected priorities manually.

### Removing from an existing save

Removing Soil Relocation from an existing save is supported **with preparation**. Do not remove the mod while a pawn is actively performing Soil Relocation Dig or Place work; doing so can leave that pawn in an invalid load/spawn state and produce recurring runtime errors.

Before disabling the mod:

1. Make a backup of the save.
2. Keep Soil Relocation enabled and cancel all remaining Soil Relocation Dig and Place orders.
3. Confirm that no pawn is actively performing Soil Relocation work.
4. When practical, remove or consume remaining portable relocation resources; they cannot survive removal of the mod that defines them.
5. Save the game, then exit RimWorld completely.
6. Disable Soil Relocation and start RimWorld again.
7. Load the prepared save. The first load may report missing Soil Relocation Def/type/Scribe state, including the serialized natural-rock map component.
8. If the map loads normally, save again with Soil Relocation still disabled, exit, and reload that new save. This post-removal save/load cycle is part of the supported removal procedure.

Soil Relocation does not provide automatic migration or repair for an already corrupted post-removal save.

## Known limitations

- Soil Relocation does not migrate positional custom WorkType priorities in existing saves. Adding the custom `SoilRelocation` work type can shift stored Work-tab priorities when loaded WorkType ordering changes; affected saves should be reviewed and corrected manually in the Work tab.
- RimWorld's map `Designator_Forbid` and `Designator_Unforbid` target `ThingCategory.Item` only. Relocation orders are non-item order Things, so use their selected-order Allow/Forbid toggle instead.
- Natural stone, water, ice, constructed floors, and other non-soil-like terrain are not independent relocatable source families.
- Backward compatibility and save migration from previous unrelated soil-relocation packages are outside the project scope.

## Development

### Local configuration

Copy:

```text
docs/SoilRelocation.Local.props.example
```

to:

```text
src/SoilRelocation.Local.props
```

Then configure:

- `RimWorldManagedDir` — RimWorld's `RimWorldWin64_Data/Managed` directory.
- `RimWorldModAssembliesDir` — the `Assemblies` directory of the local development installation of this mod.

The local props file contains machine-specific paths and is intentionally excluded from Git.

### Build

```bash
dotnet build src/SoilRelocation.sln -c Release
```

The production project copies `SoilRelocation.dll` to the configured `RimWorldModAssembliesDir` after a successful build. Harmony is referenced for compilation through `Lib.Harmony.Ref`; the project does not bundle its own `0Harmony.dll`.

### Tests

```bash
dotnet test tests/SoilRelocation.Tests/SoilRelocation.Tests.csproj -c Release
```

Keep deterministic project-owned behavior in automated tests. Validate behavior that depends on the complete RimWorld, Verse, Unity, Harmony, or optional third-party-mod runtime separately in game.

## Documentation

Public project documentation:

- `docs/architecture.md` — accepted architecture and gameplay contracts.
- `docs/testing.md` — automated testing and runtime acceptance policy.

## Repository structure

```text
assets/   Source assets
mod/      RimWorld package content
src/      Production source and solution
tests/    Automated and runtime test projects
docs/     Public documentation and configuration examples
```
# Soil Relocation testing policy

This document defines the automated testing and runtime acceptance boundaries for Soil Relocation. Build, test and deployment commands are in the [README](../README.md#development).

## Testing principle

Automated tests should protect meaningful project-owned contracts and realistic regressions. Test count and broad method-level coverage are not goals by themselves.

## Automated test boundary

The NUnit-based `SoilRelocation.Tests` project contains deterministic tests for behavior owned by Soil Relocation.

The current suite covers these deterministic contracts.

- relocation-family mapping and third-party membership/exclusion rules;
- placement resolution and validation-result classification;
- placement-destination classification owned by Soil Relocation, including the implemented exact special-destination boundaries;
- settings defaults, normalization, and reset behavior;
- deterministic cell-content and blocker policy where RimWorld runtime state is not required;
- natural-rock reconstruction profile and repair rules that can be exercised deterministically;
- relocation skill-selection and work/XP arithmetic;
- package metadata, load-folder mappings, the project's historical-version declaration and DLL checksum, required assets, and the shared source package layout;
- XML parsing, keyed translation parity, and Russian and Ukrainian Def translations matched to English fields in the shared Defs.

Keep the 1:1 terrain-resource cycle and other behavior owned by RimWorld, Harmony or an optional mod in the runtime scenarios below. Exercise those systems in game to establish their integration behavior.

## Build and package checks

Use a clean external deployment directory and a disposable copy of any prior installation. Run the following checks before in-game acceptance.

1. Build and run the NUnit suite for RimWorld 1.7 with the default `DeployToGame=false` and no destination configured. Confirm that the current DLL appears in `bin/`, the solution stages each probe DLL under its test mod in `tests/`, and the installed mod and historical DLL are unchanged.
2. Deploy into a clean destination. Check the shared assets and verify that `About.xml` declares 1.6 and 1.7 and `LoadFolders.xml` maps them to `/` plus `1.6` or `/` plus `Current`, respectively. Verify the installed 1.6 DLL against the project's historical SHA-256 and the installed 1.7 DLL against the build output. The complete package contains exactly these two DLLs. Game, Harmony, and probe assemblies come from their own installations or test fixtures.
3. Redeploy into a disposable copy containing top-level `Assemblies/` and `1.7/` directories, a stale managed asset, a local `About/PublishedFileId.txt`, and a file outside managed paths. Confirm that the two directories and the stale asset are removed. Confirm the local ID, when the source has no ID, and the file outside managed paths survive.
4. Check that unsafe or mismatched destinations, inconsistent version metadata or mappings, and missing or changed historical DLLs fail before destination changes. Set `RimWorldModAssembliesDir` as described in the [deployment instructions](../README.md#deploying-a-development-build). To run the [validation target](../build/ModDeployment.targets) alone, use the command below. Use this target for negative checks against real game or repository paths and use isolated fixtures for junction and symbolic-link checks.

   ```powershell
   dotnet msbuild src/SoilRelocation/SoilRelocation.csproj -t:ValidateModDeployment -p:DeployToGame=true
   ```

After the Release build, run `./tests/SoilRelocation.Tests/Project/Test-ModDeployment.ps1` in PowerShell to check empty and unrelated destinations, redeployment, and recovery from an interrupted update. The script uses a temporary directory. An external writable parent can be supplied with `-Parent`.

## Runtime acceptance setup

Record the date, source revision, candidate package and installed DLL hashes, game version and revision, platform, Harmony build, ordered DLC and mod list, language, relevant settings and save context. Record whether the run used the game UI or headless mode. Use a compatible Harmony build for each supported game version and enable one copy of Soil Relocation. Run save/load and removal scenarios with disposable saves or backups.

Record failures, skipped or unrun scenarios, and explicit support exclusions separately. For each skip, give the missing conditions needed for a repeat run.

For 1.7, build the solution and copy the relevant test mod directory from `tests/` into the game's `Mods/` directory. Enable it alongside the candidate for its matching scenarios. Check that the candidate also loads with the standard mod configuration.

- [Extensibility](../tests/SoilRelocation.Extensibility.TestMod) checks terrain-family extensions at startup. Run it separately from profiles A–D because its fixture applies synthetic Def patches. Read the `[SoilRelocation.ExtensibilityTests]` results in the game log. The duplicate-membership diagnostic is expected for the deliberately duplicated fixture.
- [Placement](../tests/SoilRelocation.Placement.TestMod) exposes map actions in the **Soil Relocation** category of the Dev Mode debug actions menu. Start with **Validate release acceptance profile** for the current mod and DLC configuration.
- [Removal](../tests/SoilRelocation.Removal.TestMod) exposes **Removal: prepare …** actions in the same category. Use **Removal: validate prepared fixture** before saving. Restart with Soil Relocation disabled and the Removal test mod enabled. Use **Removal: verify first load without mod** and **Removal: verify post-removal resave** on the corresponding loads.

## Runtime scenarios

- **Package selection in 1.6 and 1.7.** Load the same complete package in each version. Confirm exactly the expected DLL is loaded from the path for that game version, the shared Defs and localization load, Harmony startup succeeds, and the Architect dropdown works. In 1.6, verify a basic Dig, Place and Cancel sequence against the preserved DLL.
- **Shared placement text in 1.6 and 1.7.** In English, Russian and Ukrainian, check the four Place designator tooltips under **Architect → Floors** and the rejection shown on an unsupported surface. Confirm that each string resolves and describes the supported surfaces, including the distinction between ice and thin ice. Check both versions because they share the same language files.
- **Core in 1.7.** Forbid a Dig and a supplied Place order after work has started. Confirm that work stops, the terrain stays intact, and the Place order still holds its material. Allow the orders and check that each completes once. Also verify invalid destinations and native Cancel. For the placement save/load probe, run Verify immediately after Prepare and confirm it reports the missing reload and keeps both orders. Save, load that save in the same process, and run Verify again. Repeat the save/load checks with active relocation jobs on a disposable save.
- **Settings behavior in 1.7.** Change work amount, selected skills and XP with a job already in progress. Check that the active job keeps its initial values and a newly started job uses the changes. Check that changing conduit permission and placement mode takes effect on the next relevant revalidation.
- **DLC and integrations in 1.7.** Use the profiles below for the focused Odyssey terrain lifecycle, language reinjection, third-party Def extensions and Allow Tool cancellation when a compatible Allow Tool build is available.
- **Existing saves in 1.7.** Check the [Work-tab priorities](../README.md#adding-to-an-existing-save) after adding the candidate to an existing colony. Separately, start 1.7 with Harmony and the candidate enabled and load a copy of a representative save made in 1.6 with Soil Relocation installed. Verify that the mod recognizes persisted settings, Defs, orders and natural-rock state. Run [prepared removal](#saveload-and-removal-compatibility) on a separate copy.

Behavior that depends on the RimWorld, Verse, Harmony, or supported optional-mod runtime must be validated in game.

This includes:

- Def initialization, XML patch application, and in-process language/play-data reinjection when cached final-Def identity participates in behavior;
- final processed Def state for representative third-party integration;
- map generation and `TerrainGrid` lifecycle;
- Scribe/save-load behavior;
- order, WorkGiver, JobDriver, hauling, reservation, enroute, and toil lifecycles;
- selection, Inspect, gizmo, forbid/allow state, grouped actions, cancellation, and progress-bar UI behavior;
- Harmony integration with native RimWorld behavior at project-owned integration boundaries;
- optional Allow Tool integration and soft-dependency behavior when Allow Tool is absent;
- terrain mutation and completion-time side effects;
- settings and order UI presentation through RimWorld and Verse;
- final asset paths and localization layout;
- DLC-specific or special-map-generator behavior.

## Representative release matrix

Use these representative environments for the corresponding 1.7 scenarios.

- **Profile A — Core baseline:** Core only among official content, without Allow Tool.
- **Profile B — Odyssey focused:** Core + Odyssey, without Allow Tool. Focused current-map coverage includes the forced-Vacstone path represented by an effective single-Vacstone biome such as Orbit, a SolidIce-capable GlacialPlain path, and LavaField coverage for the accepted volcanic destination boundary.
- **Profile C — All official:** Core + Royalty + Ideology + Biotech + Anomaly + Odyssey, without Allow Tool. Switch between English, Russian and Ukrainian within one game process to exercise play-data reinjection. In each language, check the Architect dropdown, designator and Inspect labels, settings, rejection messages, progress bars, and how Allow, Forbid and Cancel appear. Check the log for missing translations or textures.
- **Profile D — All official + Allow Tool:** Use the official content from profile C and a game-compatible Allow Tool build. After running **Validate release acceptance profile**, right-click the native reverse Cancel gizmo on a selected relocation order. Confirm **Cancel selected** cancels all orders on the current map that match the exact Dig or Place order types in the selection and leaves other Place families intact. Confirm **Cancel designations** cancels relocation orders and ordinary cancelable designations. Repeat through the Allow Tool context-action hotkey if enabled. Record this profile as pending when a compatible build is unavailable.

## Placement destination acceptance responsibilities

### Natural-stone-derived boundary

Regression coverage must preserve the implemented natural-stone-derived contract:

- representative Rough natural rock is accepted;
- corresponding RoughHewn and Smooth variants are accepted through the derived-identity contract;
- Granite and Odyssey Vacstone remain representative positive families;
- constructed stone floors and unrelated stone-like terrain remain negative cases;
- language/play-data reinjection must not leave a stale destination identity cache.

### Special placement destination boundary

The implemented architecture boundary is narrow and explicit. Release acceptance must permanently protect these identities and exclusions:

Positive cases:

- exact persistent `TerrainDefOf.Ice`;
- exact `VolcanicRock`;
- exact `VolcanicRock_Smooth`.

Negative boundary cases:

- `ThinIce` and unrelated Ice-tagged or temporary terrain;
- `CooledLava`, `LavaDeep`, and `LavaShallow`;
- unrelated terrain that merely shares `natural`, `categoryType == Stone`, `SmoothableStone`, `smoothedTerrain`, or similar properties;
- constructed stone floors.

This responsibility does not make any of these special destinations relocatable source families or natural-stone-derived members.


## Focused Odyssey runtime responsibilities

On a disposable LavaField map with active Lava Flow, run **Probe volcanic smoothing against Lava Flow** from the Placement test mod's **Soil Relocation** Dev Mode category. Confirm that the same Lava Flow condition accepts the test cell as a potential spread target before smoothing and rejects it once the surface is `VolcanicRock_Smooth`. The smoothing check also verifies that the game keeps no `VolcanicRock` in the terrain layers underneath. If Lava Flow is inactive, record the missing condition and repeat with an active flow.

Use equivalent cells to compare actual placement with that vanilla control. In **Standard** mode, complete a Place job on each allowed volcanic surface. Inspect `TerrainGrid` to confirm the placed terrain persists and the previous volcanic rock is absent from the terrain layers. Compare the active condition's `CanLavaPotentiallySpreadInto` result for the originally unsmoothed cell before and after placement. Like the vanilla control, it should change from eligible to ineligible. Verify that `CooledLava`, `LavaDeep`, and `LavaShallow` reject Place orders.

## Save/load and removal compatibility

Normal save/load behavior for Soil Relocation-owned state remains a release responsibility, including active orders, delivered Place resources, active jobs, deterministic natural-rock reconstruction state, and settings snapshots where applicable.

Prepared removal tests recoverable persisted state with no active Dig or Place job. See the [player-facing preparation steps](../README.md#removing-from-an-existing-save). Use separate saves for the prepared cases below and the active-job failure cases.

Removal acceptance must cover these prepared non-active persisted states:

- a save without active relocation state;
- initialized natural-rock reconstruction profile state;
- spawned portable terrain resources;
- a persisted Dig order;
- a Place order in `AwaitingMaterial`;
- a Place order in `ReadyForWork` with its delivered resource.

For these non-active states, the first load without Soil Relocation may emit missing Def/class/type and Scribe errors for removed mod-owned data. Acceptance requires that the map still reaches a playable state, surviving vanilla terrain and representative pawn state remain usable, captured Work priorities remain consistent by `defName`, a short runtime smoke does not produce recurring failures, and a save created after removal can be loaded again without recurring stale Soil Relocation state.

Persisted active Soil Relocation jobs are an unsafe removal boundary and are not an accepted prepared-removal state. Runtime acceptance must treat both active Dig and active Place `JobDriver` cases as unsafe and public documentation must require that no pawn is actively performing Soil Relocation work before the mod is disabled.

The current production contract does not include automatic migration or repair for an already corrupted post-removal save. Any future claim that active-job removal is safe, or any future pre-uninstall cleanup implementation, requires new targeted runtime evidence before the player-facing compatibility wording can be strengthened.

## Release candidate validation

For each candidate, verify the current build, automated suite, package checks and DLL selection in both supported game versions. Identify the source revision, build output, packaged DLLs and installed copy in the results. Confirm publication separately from build, staging and local deployment.

Rerun the runtime scenarios and DLC or mod profiles affected by a production change or by a change of game version. Changes to serialized relocation Things, active jobs, cancellation or cleanup, and natural-rock persistence require the relevant save/load and removal cases.

## Adding tests

Keep deterministic regressions in `SoilRelocation.Tests`. Use a focused in-game scenario or the applicable runtime probe when the behavior depends on RimWorld, Harmony, DLC, or an optional mod. Record the inputs and expected observable outcome so the scenario can be reproduced.
# Soil Relocation testing policy

This document defines the automated testing and runtime acceptance boundaries for Soil Relocation. It describes what must be verified; it does not record historical test-run results.

## Testing principle

Automated tests should protect meaningful project-owned contracts and realistic regressions. Test count and broad method-level coverage are not goals by themselves.

Historical PASS/FAIL checkpoints are not part of project documentation. Concrete run results belong to the development workflow and repository history, while this document remains stable as a statement of testing responsibility and acceptance criteria.

## Automated test boundary

The NUnit-based `SoilRelocation.Tests` project contains deterministic tests for behavior owned by Soil Relocation.

Suitable automated-test targets include:

- relocation-family mapping and third-party membership/exclusion rules;
- placement resolution and validation-result classification;
- placement-destination classification owned by Soil Relocation, including the implemented exact special-destination boundaries;
- settings defaults, normalization, and reset behavior;
- deterministic cell-content and blocker policy where RimWorld runtime state is not required;
- natural-rock reconstruction profile and repair rules that can be exercised deterministically;
- relocation skill-selection and work/XP arithmetic;
- project-owned accounting and rule-resolution helpers.

Automated tests should not reproduce complex Verse, Unity, Harmony, or third-party-mod runtime systems merely to increase coverage.

## Runtime acceptance boundary

Behavior whose semantics belong to the complete RimWorld, Verse, Unity, Harmony, or supported optional-mod runtime must be validated in game.

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
- Unity/IMGUI presentation;
- final asset paths and localization layout;
- DLC-specific or special-map-generator behavior.

Runtime probes may be used as focused diagnostic and acceptance tools, but their past numeric results, specific fixture coordinates, and individual historical PASS/FAIL summaries are not documentation state.

## Representative release matrix

The representative RimWorld 1.6 acceptance matrix is fixed as follows:

- **Profile A — Core baseline:** Core only among official content, without Allow Tool.
- **Profile B — Odyssey focused:** Core + Odyssey, without Allow Tool. Focused current-map coverage includes the forced-Vacstone path represented by an effective single-Vacstone biome such as Orbit, a SolidIce-capable GlacialPlain path, and LavaField coverage for the accepted volcanic destination boundary.
- **Profile C — All official:** Core + Royalty + Ideology + Biotech + Anomaly + Odyssey, without Allow Tool. This profile also carries the representative English/Russian/Ukrainian localization smoke boundary, including in-process language switching so play-data reinjection is exercised within the same RimWorld process.
- **Profile D — All official + Allow Tool:** Profile C official content with Allow Tool enabled for the optional compatibility boundary.

The matrix defines representative environments, not a requirement to rerun every historical scenario after every change. Targeted revalidation should follow the changed boundary unless a change materially affects a broader profile.

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

Odyssey-focused runtime acceptance must cover the runtime semantics that cannot be proven by deterministic project tests alone when they are relevant to a changed boundary.

For the implemented volcanic placement boundary, focused acceptance must verify that:

- the exact `VolcanicRock` and `VolcanicRock_Smooth` identities are the only volcanic placement additions;
- replacement through Soil Relocation uses normal `TerrainGrid` mutation semantics;
- Lava Flow-related runtime behavior does not require hidden preservation of exact `VolcanicRock` beyond the already accepted vanilla lifecycle contract;
- `CooledLava`, `LavaDeep`, and `LavaShallow` do not become supported by association.

This section defines ongoing acceptance responsibility. It does not preserve the historical diagnostic probe output that originally established the research conclusion.

## Save/load and removal compatibility

Normal save/load behavior for Soil Relocation-owned state remains a release responsibility, including active orders, delivered Place resources, active jobs, deterministic natural-rock reconstruction state, and settings snapshots where applicable.

Existing-save removal has a separate accepted compatibility boundary: **supported with preparation**. Runtime acceptance must preserve the distinction between recoverable non-active persisted state and unsafe active custom-job state.

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

## Release revalidation policy

The broad baseline acceptance matrix, focused Ice/volcanic destination responsibilities, and existing-save removal matrix together form the current runtime acceptance foundation.

Future production changes should use targeted revalidation for affected contracts rather than mechanically repeating unrelated scenarios. Existing destination, source-family, `1:1` resource-accounting, cancellation, material-order, natural-stone reconstruction, normal save/load, and removal-preparation boundaries remain regression responsibilities, but they should be rerun only when the changed implementation crosses those boundaries.

A broader representative profile should be rerun only when the changed implementation crosses that profile's environment boundary. Any production change that affects serialized relocation Things, active jobs, cancellation/cleanup lifecycle, natural-rock component persistence, or other removal-relevant state must include the corresponding removal compatibility cases in its targeted runtime revalidation.
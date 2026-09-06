using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace SoilRelocation.Removal.RuntimeTests
{
    public static class RemovalCompatibilityRuntimeProbe
    {
        private const string LogPrefix = "[SoilRelocation.RemovalTests]";

        private const string HarmonyPackageId = "brrainz.harmony";
        private const string SoilRelocationPackageId = "escarval.soilrelocation";
        private const string RemovalTestsPackageId = "escarval.soilrelocation.removaltests";
        private const string PlacementTestsPackageId = "escarval.soilrelocation.placementtests";
        private const string ExtensibilityTestsPackageId = "escarval.soilrelocation.extensibilitytests";

        private const string DigOrderDefName = "SoilRelocation_DigOrder";
        private const string PlaceSoilOrderDefName = "SoilRelocation_PlaceSoilOrder";
        private const string SoilResourceDefName = "SoilRelocation_Soil";
        private const string SoilTerrainDefName = "Soil";
        private const string DigJobDefName = "SoilRelocation_DigTerrain";
        private const string PlaceJobDefName = "SoilRelocation_PlaceTerrain";
        private const string DigWorkGiverDefName = "SoilRelocation_DigTerrain";
        private const string PlaceWorkGiverDefName = "SoilRelocation_PlaceTerrain";
        private const string SoilRelocationWorkTypeDefName = "SoilRelocation";

        private const string NaturalRockComponentTypeName = "SoilRelocation.Substrate.NaturalRockMapComponent";
        private const string NaturalRockProfileFieldName = "_profile";
        private const string ProductionAssemblyName = "SoilRelocation";

        private const string ScenarioR0 = "R0_Clean";
        private const string ScenarioR1 = "R1_InitializedProfile";
        private const string ScenarioR2 = "R2_PortableResource";
        private const string ScenarioR3 = "R3_DigOrder";
        private const string ScenarioR4 = "R4_PlaceAwaiting";
        private const string ScenarioR5 = "R5_PlaceReady";
        private const string ScenarioR6 = "R6_ActiveDigJob";
        private const string ScenarioR7 = "R7_ActivePlaceJob";

        private const int MinimumFixtureRadius = 4;
        private const int MaximumFixtureRadius = 35;

        private static readonly string[] _knownOrderDefNames =
        {
            DigOrderDefName,
            "SoilRelocation_PlaceSandOrder",
            "SoilRelocation_PlaceGravelOrder",
            PlaceSoilOrderDefName,
            "SoilRelocation_PlaceRichSoilOrder"
        };

        private static readonly string[] _knownResourceDefNames =
        {
            "SoilRelocation_Sand",
            "SoilRelocation_Gravel",
            SoilResourceDefName,
            "SoilRelocation_RichSoil"
        };

        private static readonly string[] _knownJobDefNames =
        {
            DigJobDefName,
            PlaceJobDefName
        };

        [DebugAction(
            "Soil Relocation",
            "Removal: prepare R0 clean",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareR0Clean()
        {
            RunAction("Removal R0 clean setup", PrepareCleanScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: prepare R1 initialized profile",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareR1InitializedProfile()
        {
            RunAction("Removal R1 initialized-profile setup", PrepareInitializedProfileScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: capture R1 initialized profile",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void CaptureR1InitializedProfile()
        {
            RunAction("Removal R1 initialized-profile capture", CaptureInitializedProfileScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: prepare R2 portable resource",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareR2PortableResource()
        {
            RunAction("Removal R2 portable-resource setup", PreparePortableResourceScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: prepare R3 Dig order",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareR3DigOrder()
        {
            RunAction("Removal R3 Dig-order setup", PrepareDigOrderScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: prepare R4 Place awaiting",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareR4PlaceAwaiting()
        {
            RunAction("Removal R4 Place-awaiting setup", PreparePlaceAwaitingScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: prepare R5 Place ready",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareR5PlaceReady()
        {
            RunAction("Removal R5 Place-ready setup", PreparePlaceReadyScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: prepare R6 active Dig job",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareR6ActiveDigJob()
        {
            RunAction("Removal R6 active-Dig setup", PrepareActiveDigScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: prepare R7 active Place job",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareR7ActivePlaceJob()
        {
            RunAction("Removal R7 active-Place setup", PrepareActivePlaceScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: capture active job fixture",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void CaptureActiveJobFixture()
        {
            RunAction("Removal active-job capture", CaptureActiveJobScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: validate prepared fixture",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ValidatePreparedFixture()
        {
            RunAction("Removal prepared-fixture validation", ValidatePreparedScenario);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: verify first load without mod",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyFirstLoadWithoutMod()
        {
            RunAction("Removal first-load verification", VerifyFirstRemovalLoad);
        }

        [DebugAction(
            "Soil Relocation",
            "Removal: verify post-removal resave",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyPostRemovalResave()
        {
            RunAction("Removal post-resave verification", VerifySecondRemovalLoad);
        }

        private static void PrepareCleanScenario(ProbeReport report)
        {
            if (!TryPrepareBaseline(
                    report,
                    out Map map,
                    out Pawn priorityPawn,
                    out RemovalProbeMapComponent component,
                    out List<string> workTypeNames,
                    out List<int> workPriorities))
            {
                return;
            }

            TerrainDef terrain = map.terrainGrid.TerrainAt(priorityPawn.Position);

            if (terrain == null)
            {
                report.Fail("R0 setup", "Could not resolve terrain under the priority-snapshot pawn.");
                return;
            }

            component.BeginScenario(
                ScenarioR0,
                RemovalProbeMapComponent.PhasePrepared,
                priorityPawn.Position,
                terrain.defName,
                terrain.defName,
                null,
                null,
                priorityPawn.thingIDNumber,
                workTypeNames,
                workPriorities);

            report.Pass("R0 setup", "Prepared a clean save with no Soil Relocation-owned persisted gameplay state.");
            ReportPreparedSaveInstructions(component, report);
        }

        private static void PrepareInitializedProfileScenario(ProbeReport report)
        {
            if (!TryPrepareBaseline(
                    report,
                    out Map map,
                    out Pawn priorityPawn,
                    out RemovalProbeMapComponent component,
                    out List<string> workTypeNames,
                    out List<int> workPriorities))
            {
                return;
            }

            if (!TryCreateValidDigOrder(
                    map,
                    priorityPawn,
                    out _,
                    out Pawn capablePawn,
                    out IntVec3 cell,
                    out TerrainDef originalTerrain,
                    report))
            {
                return;
            }

            component.BeginScenario(
                ScenarioR1,
                RemovalProbeMapComponent.PhasePendingProfileCompletion,
                cell,
                originalTerrain.defName,
                SoilTerrainDefName,
                DigOrderDefName,
                DigJobDefName,
                priorityPawn.thingIDNumber,
                workTypeNames,
                workPriorities);

            report.Pass(
                "R1 setup",
                "Prepared one valid Dig operation at " + cell +
                " for initializing the production natural-rock profile.");
            report.Info(
                "R1 operator steps",
                "Unpause and let a colonist complete only this Dig operation. Pause immediately after completion, " +
                "then run 'Removal: capture R1 initialized profile'. Suggested capable pawn: " +
                capablePawn.LabelShortCap + ". Do not save the removal fixture before the capture action succeeds.");
        }

        private static void CaptureInitializedProfileScenario(ProbeReport report)
        {
            if (!TryGetSingleScenario(
                    ScenarioR1,
                    RemovalProbeMapComponent.PhasePendingProfileCompletion,
                    requireSoilRelocation: true,
                    report,
                    out Map map,
                    out RemovalProbeMapComponent component))
            {
                return;
            }

            IntVec3 cell = component.FixtureCell;
            Thing order = FindSpawnedThingAt(map, cell, DigOrderDefName);

            if (order != null)
            {
                report.Skip(
                    "R1 capture",
                    "The Dig order still exists at " + cell +
                    ". Let the operation finish, pause, and run capture again.");
                return;
            }

            if (!TryGetNaturalRockProfileState(
                    map,
                    out bool componentPresent,
                    out bool hasProfile,
                    out string diagnostic))
            {
                report.Fail("R1 capture", diagnostic);
                return;
            }

            if (!componentPresent || !hasProfile)
            {
                report.Fail(
                    "R1 capture",
                    "The Dig order is gone but no committed natural-rock reconstruction profile is present.");
                return;
            }

            DestroySpawnedThingsByDef(map, SoilResourceDefName);
            DestroyCarriedThingsByDef(map, SoilResourceDefName);

            TerrainDef originalTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail(component.OriginalTerrainDefName);

            if (originalTerrain == null)
            {
                report.Fail(
                    "R1 capture",
                    "Original fixture terrain '" + component.OriginalTerrainDefName + "' is unavailable.");
                return;
            }

            map.terrainGrid.SetTerrain(cell, originalTerrain);
            component.SetExpectedTerrain(originalTerrain.defName);

            CustomStateSnapshot state = CaptureCustomState();

            if (state.SpawnedOrderCount != 0 || state.SpawnedResourceCount != 0 || state.ActiveCustomJobCount != 0)
            {
                report.Fail(
                    "R1 capture",
                    "Could not isolate the committed profile after Dig completion. Remaining state: " +
                    state.Describe() + ". Reload the clean baseline and prepare R1 again.");
                return;
            }

            component.MarkPrepared();
            report.Pass(
                "R1 capture",
                "Committed profile is present; the Dig order/resource were removed and the fixture terrain was " +
                "restored.");
            ReportPreparedSaveInstructions(component, report);
        }

        private static void PreparePortableResourceScenario(ProbeReport report)
        {
            if (!TryPrepareBaseline(
                    report,
                    out Map map,
                    out Pawn priorityPawn,
                    out RemovalProbeMapComponent component,
                    out List<string> workTypeNames,
                    out List<int> workPriorities))
            {
                return;
            }

            ThingDef resourceDef = DefDatabase<ThingDef>.GetNamedSilentFail(SoilResourceDefName);

            if (resourceDef == null || !TryFindGeneralFixtureCell(map, priorityPawn.Position, out IntVec3 cell))
            {
                report.Fail("R2 setup", "Soil portable resource Def or a stable fixture cell is unavailable.");
                return;
            }

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            Thing resource = null;

            try
            {
                resource = ThingMaker.MakeThing(resourceDef);
                resource.stackCount = 1;
                GenSpawn.Spawn(resource, cell, map);

                if (resource.TryGetComp<CompForbiddable>() != null)
                {
                    resource.SetForbidden(true, warnOnFail: false);
                }

                component.BeginScenario(
                    ScenarioR2,
                    RemovalProbeMapComponent.PhasePrepared,
                    cell,
                    terrain?.defName,
                    terrain?.defName,
                    SoilResourceDefName,
                    null,
                    priorityPawn.thingIDNumber,
                    workTypeNames,
                    workPriorities);

                report.Pass("R2 setup", "Prepared one spawned Soil portable resource at " + cell + ".");
                ReportPreparedSaveInstructions(component, report);
                resource = null;
            }
            finally
            {
                if (resource != null && !resource.Destroyed)
                {
                    resource.Destroy();
                }
            }
        }

        private static void PrepareDigOrderScenario(ProbeReport report)
        {
            if (!TryPrepareBaseline(
                    report,
                    out Map map,
                    out Pawn priorityPawn,
                    out RemovalProbeMapComponent component,
                    out List<string> workTypeNames,
                    out List<int> workPriorities))
            {
                return;
            }

            if (!TryCreateValidDigOrder(
                    map,
                    priorityPawn,
                    out Thing order,
                    out _,
                    out IntVec3 cell,
                    out TerrainDef originalTerrain,
                    report))
            {
                return;
            }

            order.SetForbidden(true, warnOnFail: false);

            component.BeginScenario(
                ScenarioR3,
                RemovalProbeMapComponent.PhasePrepared,
                cell,
                originalTerrain.defName,
                SoilTerrainDefName,
                DigOrderDefName,
                null,
                priorityPawn.thingIDNumber,
                workTypeNames,
                workPriorities);

            report.Pass(
                "R3 setup",
                "Prepared one valid forbidden Dig order at " + cell +
                "; forbidding stabilizes the persisted order before save.");
            ReportPreparedSaveInstructions(component, report);
        }

        private static void PreparePlaceAwaitingScenario(ProbeReport report)
        {
            if (!TryPrepareBaseline(
                    report,
                    out Map map,
                    out Pawn priorityPawn,
                    out RemovalProbeMapComponent component,
                    out List<string> workTypeNames,
                    out List<int> workPriorities))
            {
                return;
            }

            if (!TryCreateValidReadyPlaceOrder(
                    map,
                    priorityPawn,
                    out Thing validationOrder,
                    out _,
                    out IntVec3 cell,
                    out TerrainDef terrain,
                    report))
            {
                return;
            }

            DestroyThingAndContents(validationOrder);

            ThingDef orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(PlaceSoilOrderDefName);
            Thing awaitingOrder = ThingMaker.MakeThing(orderDef);
            GenSpawn.Spawn(awaitingOrder, cell, map);
            awaitingOrder.SetForbidden(true, warnOnFail: false);

            component.BeginScenario(
                ScenarioR4,
                RemovalProbeMapComponent.PhasePrepared,
                cell,
                terrain.defName,
                terrain.defName,
                PlaceSoilOrderDefName,
                null,
                priorityPawn.thingIDNumber,
                workTypeNames,
                workPriorities);

            report.Pass(
                "R4 setup",
                "Prepared one forbidden Place order in AwaitingMaterial at " + cell + " with an empty ThingOwner.");
            ReportPreparedSaveInstructions(component, report);
        }

        private static void PreparePlaceReadyScenario(ProbeReport report)
        {
            if (!TryPrepareBaseline(
                    report,
                    out Map map,
                    out Pawn priorityPawn,
                    out RemovalProbeMapComponent component,
                    out List<string> workTypeNames,
                    out List<int> workPriorities))
            {
                return;
            }

            if (!TryCreateValidReadyPlaceOrder(
                    map,
                    priorityPawn,
                    out Thing order,
                    out _,
                    out IntVec3 cell,
                    out TerrainDef terrain,
                    report))
            {
                return;
            }

            order.SetForbidden(true, warnOnFail: false);

            component.BeginScenario(
                ScenarioR5,
                RemovalProbeMapComponent.PhasePrepared,
                cell,
                terrain.defName,
                terrain.defName,
                PlaceSoilOrderDefName,
                null,
                priorityPawn.thingIDNumber,
                workTypeNames,
                workPriorities);

            report.Pass(
                "R5 setup",
                "Prepared one forbidden ReadyForWork Place order at " + cell +
                " containing exactly one Soil resource.");
            ReportPreparedSaveInstructions(component, report);
        }

        private static void PrepareActiveDigScenario(ProbeReport report)
        {
            if (!TryPrepareBaseline(
                    report,
                    out Map map,
                    out Pawn priorityPawn,
                    out RemovalProbeMapComponent component,
                    out List<string> workTypeNames,
                    out List<int> workPriorities))
            {
                return;
            }

            if (!TryCreateValidDigOrder(
                    map,
                    priorityPawn,
                    out _,
                    out Pawn capablePawn,
                    out IntVec3 cell,
                    out TerrainDef originalTerrain,
                    report))
            {
                return;
            }

            component.BeginScenario(
                ScenarioR6,
                RemovalProbeMapComponent.PhasePendingActiveJobCapture,
                cell,
                originalTerrain.defName,
                SoilTerrainDefName,
                DigOrderDefName,
                DigJobDefName,
                priorityPawn.thingIDNumber,
                workTypeNames,
                workPriorities);

            report.Pass("R6 setup", "Prepared one valid Dig order for an active-job removal fixture at " + cell + ".");
            report.Info(
                "R6 operator steps",
                "Unpause and let a colonist begin actual Dig work. When the progress bar is strictly between 0% and " +
                "100%, pause and run 'Removal: capture active job fixture'. Suggested capable pawn: " +
                capablePawn.LabelShortCap + ". Do not save before capture succeeds.");
        }

        private static void PrepareActivePlaceScenario(ProbeReport report)
        {
            if (!TryPrepareBaseline(
                    report,
                    out Map map,
                    out Pawn priorityPawn,
                    out RemovalProbeMapComponent component,
                    out List<string> workTypeNames,
                    out List<int> workPriorities))
            {
                return;
            }

            if (!TryCreateValidReadyPlaceOrder(
                    map,
                    priorityPawn,
                    out _,
                    out Pawn capablePawn,
                    out IntVec3 cell,
                    out TerrainDef terrain,
                    report))
            {
                return;
            }

            component.BeginScenario(
                ScenarioR7,
                RemovalProbeMapComponent.PhasePendingActiveJobCapture,
                cell,
                terrain.defName,
                terrain.defName,
                PlaceSoilOrderDefName,
                PlaceJobDefName,
                priorityPawn.thingIDNumber,
                workTypeNames,
                workPriorities);

            report.Pass(
                "R7 setup",
                "Prepared one ReadyForWork Place order for an active-job removal fixture at " + cell + ".");
            report.Info(
                "R7 operator steps",
                "Unpause and let a colonist begin actual Place work. When the progress bar is strictly between 0% " +
                "and 100%, pause and run 'Removal: capture active job fixture'. Suggested capable pawn: " +
                capablePawn.LabelShortCap + ". Do not save before capture succeeds.");
        }

        private static void CaptureActiveJobScenario(ProbeReport report)
        {
            if (!TryGetSinglePendingActiveScenario(report, out Map map, out RemovalProbeMapComponent component))
            {
                return;
            }

            Thing order = FindSpawnedThingAt(map, component.FixtureCell, component.FixtureThingDefName);

            if (order == null)
            {
                report.Fail(
                    "Active-job capture",
                    "The prepared order no longer exists at " + component.FixtureCell +
                    ". Reload the clean baseline and retry.");
                return;
            }

            if (!TryFindPartialActiveJob(
                    map,
                    order,
                    component.ExpectedActiveJobDefName,
                    out Pawn pawn,
                    out string progressDescription))
            {
                report.Skip(
                    "Active-job capture",
                    "The expected job is not in partial actual work yet. Keep the order alive, let work progress, " +
                    "pause, and run capture again.");
                return;
            }

            component.MarkPrepared(pawn.thingIDNumber);
            report.Pass(
                "Active-job capture",
                "Captured active " + component.ExpectedActiveJobDefName + " on pawn " + pawn.LabelShortCap + ": " +
                progressDescription + ".");
            ReportPreparedSaveInstructions(component, report);
        }

        private static void ValidatePreparedScenario(ProbeReport report)
        {
            if (!TryGetSinglePreparedScenario(
                    requireSoilRelocation: true,
                    report,
                    out Map map,
                    out RemovalProbeMapComponent component))
            {
                return;
            }

            ValidatePreparedScenarioCore(map, component, report);

            if (report.FailureCount == 0)
            {
                report.Pass(
                    "Prepared fixture",
                    "Scenario " + component.Scenario + " is stable and ready to save before removal.");
                ReportPreparedSaveInstructions(component, report);
            }
        }

        private static void VerifyFirstRemovalLoad(ProbeReport report)
        {
            if (!TryGetSinglePreparedScenario(
                    requireSoilRelocation: false,
                    report,
                    out Map map,
                    out RemovalProbeMapComponent component))
            {
                return;
            }

            if (IsRunningPackage(SoilRelocationPackageId))
            {
                report.Fail("Removal environment", "Soil Relocation is still active. Disable it and restart RimWorld.");
                return;
            }

            VerifyRemovalEnvironment(report);
            VerifyRemovalScenarioCore(map, component, report);

            component.MarkFirstLoadVerified();

            report.Info(
                "Manual log review",
                "Review the full load/runtime log. Record missing-mod warnings separately from missing " +
                "Def/class/type " +
                "errors, Scribe exceptions, recurring stack traces, or errors after unpausing. Do not classify a " +
                "normal missing-mod warning as a failure by itself.");
            report.Info(
                "Tick smoke",
                "Unpause briefly and confirm the map continues ticking and the fixture pawn can return to normal " +
                "vanilla job selection without recurring errors. Then pause again.");
            report.Info(
                "Next",
                "Save this game again with Soil Relocation still disabled (suggested suffix: _After), exit RimWorld, " +
                "start it again with the same mod list, load the new save, and run " +
                "'Removal: verify post-removal resave'.");
        }

        private static void VerifySecondRemovalLoad(ProbeReport report)
        {
            if (!TryGetSingleScenario(
                    null,
                    RemovalProbeMapComponent.PhaseFirstLoadVerified,
                    requireSoilRelocation: false,
                    report,
                    out Map map,
                    out RemovalProbeMapComponent component))
            {
                return;
            }

            if (IsRunningPackage(SoilRelocationPackageId))
            {
                report.Fail("Removal environment", "Soil Relocation is active during the post-removal resave check.");
                return;
            }

            if (component.ObservedLoadCount <= component.FirstVerificationLoadCount)
            {
                report.Fail(
                    "Post-removal reload",
                    "The test-owned manifest has not passed through another save/load cycle since the first " +
                    "verification. Save without Soil Relocation, exit, reload that new save, then run this action.");
                return;
            }

            report.Pass(
                "Post-removal reload",
                "The independent test manifest confirms that the save created without Soil Relocation was written " +
                "and loaded in a distinct second cycle.");

            VerifyRemovalEnvironment(report);
            VerifyRemovalScenarioCore(map, component, report);

            component.MarkComplete();

            report.Info(
                "Manual log review",
                "Review this second load for new or recurring Scribe/Def/type errors. The second load is the " +
                "stability " + "gate for the save produced after Soil Relocation was already absent.");
            report.Info(
                "Result recording",
                "Record this scenario in the R0-R7 matrix with first-load behavior, surviving vanilla state, Work " +
                "priorities, resave result, second-load result, and log observations. Do not infer the overall " +
                "player-facing removal classification from this scenario alone.");
        }

        private static bool TryPrepareBaseline(
            ProbeReport report,
            out Map map,
            out Pawn priorityPawn,
            out RemovalProbeMapComponent component,
            out List<string> workTypeNames,
            out List<int> workPriorities)
        {
            map = Find.CurrentMap;
            priorityPawn = null;
            component = null;
            workTypeNames = null;
            workPriorities = null;

            if (map == null)
            {
                report.Fail("Preparation environment", "No current map is available.");
                return false;
            }

            if (!ValidatePreparationPackages(report))
            {
                return false;
            }

            var activeComponents = Find.Maps.Select(candidate => candidate?.GetComponent<RemovalProbeMapComponent>())
                .Where(candidate => candidate != null && candidate.HasScenario).ToList();

            if (activeComponents.Count > 0)
            {
                report.Fail(
                    "Preparation environment",
                    "A removal fixture already exists in this save. Prepare each R0-R7 case from a clean baseline " +
                    "save rather than chaining scenarios in one save.");
                return false;
            }

            CustomStateSnapshot state = CaptureCustomState();

            if (state.SpawnedOrderCount != 0 || state.SpawnedResourceCount != 0 || state.ActiveCustomJobCount != 0)
            {
                report.Fail(
                    "Clean baseline",
                    "Existing Soil Relocation gameplay state would make this scenario ambiguous: " + state.Describe() +
                    ". Use a clean baseline save.");
                return false;
            }

            if (!TryValidateNoCommittedProfiles(report))
            {
                return false;
            }

            priorityPawn = ResolvePrioritySnapshotPawn(map);

            if (priorityPawn == null)
            {
                report.Fail(
                    "Work priority snapshot",
                    "No spawned free colonist with initialized work settings is available on the current map.");
                return false;
            }

            if (!TrySnapshotOfficialWorkPriorities(
                    priorityPawn,
                    out workTypeNames,
                    out workPriorities,
                    out string error))
            {
                report.Fail("Work priority snapshot", error);
                return false;
            }

            component = map.GetComponent<RemovalProbeMapComponent>();

            if (component == null)
            {
                report.Fail(
                    "Removal manifest",
                    "The independent RemovalProbeMapComponent is unavailable. Confirm the removal runtime test mod " +
                    "assembly is loaded.");
                return false;
            }

            report.Pass(
                "Clean baseline",
                "No spawned Soil Relocation orders/resources, active relocation jobs, or committed natural-rock " +
                "profiles were detected in the save.");
            report.Pass(
                "Work priority snapshot",
                "Captured " + workTypeNames.Count + " official WorkType priorities from " + priorityPawn.LabelShortCap +
                " for defName-based comparison after removal.");
            report.Info(
                "Controlled environment",
                "Keep Harmony and the Removal Runtime Tests enabled when Soil Relocation is disabled. Placement and " +
                "Extensibility runtime test mods must remain disabled for this matrix.");

            return true;
        }

        private static bool ValidatePreparationPackages(ProbeReport report)
        {
            var valid = true;

            if (!IsRunningPackage(SoilRelocationPackageId))
            {
                report.Fail("Preparation environment", "Soil Relocation is not active.");
                valid = false;
            }

            if (!IsRunningPackage(RemovalTestsPackageId))
            {
                report.Fail("Preparation environment", "The independent Removal Runtime Tests mod is not active.");
                valid = false;
            }

            if (!IsRunningPackage(HarmonyPackageId))
            {
                report.Fail(
                    "Preparation environment",
                    "Harmony is not active. Keep the required production dependency enabled so the removal pass " +
                    "changes only Soil Relocation itself.");
                valid = false;
            }

            if (IsRunningPackage(PlacementTestsPackageId) || IsRunningPackage(ExtensibilityTestsPackageId))
            {
                report.Fail(
                    "Preparation environment",
                    "Placement or Extensibility runtime test mods are active. Disable them before preparing removal " +
                    "fixtures because they depend on Soil Relocation and cannot remain in the post-removal mod list.");
                valid = false;
            }

            return valid;
        }

        private static void VerifyRemovalEnvironment(ProbeReport report)
        {
            if (IsRunningPackage(RemovalTestsPackageId))
            {
                report.Pass("Removal test mod", "Independent Removal Runtime Tests mod is still active.");
            }
            else
            {
                report.Fail("Removal test mod", "Removal Runtime Tests mod is not active.");
            }

            if (IsRunningPackage(HarmonyPackageId))
            {
                report.Pass("Harmony environment", "Harmony remains active, isolating Soil Relocation removal.");
            }
            else
            {
                report.Fail(
                    "Harmony environment",
                    "Harmony was also disabled. This no longer isolates removal of Soil Relocation from a dependency " +
                    "change.");
            }

            if (IsRunningPackage(PlacementTestsPackageId) || IsRunningPackage(ExtensibilityTestsPackageId))
            {
                report.Fail(
                    "Auxiliary test mods",
                    "Placement or Extensibility runtime test mods remain active after removal.");
            }
            else
            {
                report.Pass("Auxiliary test mods", "Soil Relocation-dependent runtime test mods are absent.");
            }

            bool assemblyLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                string.Equals(assembly.GetName().Name, ProductionAssemblyName, StringComparison.Ordinal));

            if (assemblyLoaded)
            {
                report.Fail(
                    "Production assembly",
                    "Assembly '" + ProductionAssemblyName +
                    "' is still loaded even though the production mod should be absent.");
            }
            else
            {
                report.Pass("Production assembly", "Production SoilRelocation assembly is absent from the process.");
            }

            VerifyProductionDefsAbsent(report);
        }

        private static void VerifyProductionDefsAbsent(ProbeReport report)
        {
            var unexpected = new List<string>();

            foreach (string defName in _knownOrderDefNames.Concat(_knownResourceDefNames))
            {
                if (DefDatabase<ThingDef>.GetNamedSilentFail(defName) != null)
                {
                    unexpected.Add("ThingDef:" + defName);
                }
            }

            foreach (string defName in _knownJobDefNames)
            {
                if (DefDatabase<JobDef>.GetNamedSilentFail(defName) != null)
                {
                    unexpected.Add("JobDef:" + defName);
                }
            }

            if (DefDatabase<WorkTypeDef>.GetNamedSilentFail(SoilRelocationWorkTypeDefName) != null)
            {
                unexpected.Add("WorkTypeDef:" + SoilRelocationWorkTypeDefName);
            }

            if (unexpected.Count > 0)
            {
                report.Fail(
                    "Production Def removal",
                    "Soil Relocation Defs are unexpectedly still present: " + string.Join(", ", unexpected) + ".");
            }
            else
            {
                report.Pass("Production Def removal", "Known Soil Relocation Thing/Job/WorkType Defs are absent.");
            }
        }

        private static void VerifyRemovalScenarioCore(Map map, RemovalProbeMapComponent component, ProbeReport report)
        {
            report.Pass("Save load", "The game reached the runtime verifier with the persisted fixture map available.");

            if (component.SchemaVersion != RemovalProbeMapComponent.CurrentSchemaVersion)
            {
                report.Fail(
                    "Removal manifest",
                    "Unsupported test manifest schema " + component.SchemaVersion + ". Expected " +
                    RemovalProbeMapComponent.CurrentSchemaVersion + ".");
            }
            else
            {
                report.Pass(
                    "Removal manifest",
                    "Independent manifest survived load for scenario " + component.Scenario + ", loadCount=" +
                    component.ObservedLoadCount + ".");
            }

            if ((map.Parent?.ID ?? -1) != component.MapParentId)
            {
                report.Fail(
                    "Map identity",
                    "The manifest loaded on an unexpected map parent. expected=" + component.MapParentId + ", actual=" +
                    (map.Parent?.ID ?? -1) + ".");
            }
            else
            {
                report.Pass("Map identity", "The fixture map survived removal and load.");
            }

            VerifyExpectedTerrain(map, component, report);
            VerifyWorkPriorities(map, component, report);
            VerifyActivePawnState(map, component, report);

            report.Info(
                "Removed custom state",
                DescribeRemovedScenarioState(component) +
                " The loader's treatment of removed mod-owned XML nodes must be interpreted together with the " +
                "manual log review; disappearance of mod-owned content alone is not treated as corruption.");
        }

        private static void VerifyExpectedTerrain(Map map, RemovalProbeMapComponent component, ProbeReport report)
        {
            if (!component.HasFixtureCell || string.IsNullOrEmpty(component.ExpectedTerrainDefName))
            {
                report.Fail(
                    "Terrain state",
                    "The persisted manifest does not contain a valid expected terrain cell/state.");
                return;
            }

            IntVec3 cell = component.FixtureCell;

            if (!cell.InBounds(map))
            {
                report.Fail("Terrain state", "Fixture cell " + cell + " is out of bounds after load.");
                return;
            }

            TerrainDef actual = map.terrainGrid.TerrainAt(cell);

            if (actual == null || !string.Equals(
                    actual.defName,
                    component.ExpectedTerrainDefName,
                    StringComparison.Ordinal))
            {
                report.Fail(
                    "Terrain state",
                    "Surviving vanilla terrain changed unexpectedly at " + cell + ". expected='" +
                    component.ExpectedTerrainDefName + "', actual='" + (actual?.defName ?? "<null>") + "'.");
            }
            else
            {
                report.Pass("Terrain state", "Surviving terrain at " + cell + " remains '" + actual.defName + "'.");
            }
        }

        private static void VerifyWorkPriorities(Map map, RemovalProbeMapComponent component, ProbeReport report)
        {
            Pawn pawn = FindPawnById(map, component.PriorityPawnId);

            if (pawn == null)
            {
                report.Fail(
                    "Work priorities",
                    "The pawn used for the pre-removal WorkType snapshot is missing after load (id=" +
                    component.PriorityPawnId + ").");
                return;
            }

            if (pawn.workSettings == null)
            {
                report.Fail("Work priorities", "The surviving snapshot pawn has no workSettings after load.");
                return;
            }

            if (!component.HasValidWorkPrioritySnapshot)
            {
                report.Fail("Work priorities", "The persisted official WorkType snapshot is missing or malformed.");
                return;
            }

            var mismatches = new List<string>();
            var missing = new List<string>();

            for (var i = 0; i < component.WorkTypeDefNames.Count; i++)
            {
                string defName = component.WorkTypeDefNames[i];
                int expected = component.WorkPriorities[i];
                WorkTypeDef workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);

                if (workType == null)
                {
                    missing.Add(defName);
                    continue;
                }

                int actual;

                try
                {
                    actual = pawn.workSettings.GetPriority(workType);
                }
                catch (Exception exception)
                {
                    mismatches.Add(defName + " threw " + exception.GetBaseException().GetType().Name);
                    continue;
                }

                if (actual != expected)
                {
                    mismatches.Add(defName + " " + expected + "->" + actual);
                }
            }

            if (missing.Count > 0)
            {
                report.Fail(
                    "Work priorities",
                    "Official WorkType Defs disappeared: " + string.Join(", ", missing) + ".");
            }

            if (mismatches.Count > 0)
            {
                report.Fail(
                    "Work priorities",
                    "Surviving official priorities changed by defName: " + string.Join("; ", mismatches) +
                    ". Record this as a player-facing removal consequence even if the save otherwise loads.");
            }
            else if (missing.Count == 0)
            {
                report.Pass(
                    "Work priorities",
                    "All " + component.WorkTypeDefNames.Count +
                    " captured official WorkType priorities match by defName.");
            }
        }

        private static void VerifyActivePawnState(Map map, RemovalProbeMapComponent component, ProbeReport report)
        {
            if (component.ActiveJobPawnId < 0)
            {
                Pawn priorityPawn = FindPawnById(map, component.PriorityPawnId);

                if (priorityPawn == null || priorityPawn.Dead || !priorityPawn.Spawned || priorityPawn.jobs == null)
                {
                    report.Fail(
                        "Pawn state",
                        "The representative surviving pawn is not in a usable spawned job state.");
                }
                else
                {
                    report.Pass(
                        "Pawn state",
                        "Representative pawn remains spawned/alive with a valid job tracker after removal.");
                }

                return;
            }

            Pawn activePawn = FindPawnById(map, component.ActiveJobPawnId);

            if (activePawn == null)
            {
                report.Fail(
                    "Active-job pawn",
                    "The pawn that owned the partial relocation job is missing after removal (id=" +
                    component.ActiveJobPawnId + ").");
                return;
            }

            if (activePawn.Dead || !activePawn.Spawned || activePawn.jobs == null)
            {
                report.Fail(
                    "Active-job pawn",
                    "The former relocation worker did not survive in a normal spawned/job-tracker state.");
                return;
            }

            string currentJob = activePawn.CurJob?.def?.defName ?? "<none>";

            if (string.Equals(currentJob, component.ExpectedActiveJobDefName, StringComparison.Ordinal))
            {
                report.Fail(
                    "Active-job pawn",
                    "The removed custom job unexpectedly remains current: " + currentJob + ".");
                return;
            }

            report.Pass(
                "Active-job pawn",
                "Former relocation worker remains usable; current job after removal is '" + currentJob + "'.");
        }

        private static void ValidatePreparedScenarioCore(
            Map map,
            RemovalProbeMapComponent component,
            ProbeReport report)
        {
            VerifyExpectedTerrain(map, component, report);
            VerifyPreRemovalWorkPriorities(map, component, report);

            CustomStateSnapshot state = CaptureCustomState();

            switch (component.Scenario)
            {
                case ScenarioR0:
                    ValidateExpectedState(
                        "R0 prepared state",
                        state,
                        expectedSpawnedOrders: 0,
                        expectedSpawnedResources: 0,
                        expectedActiveJobs: 0,
                        report);
                    ValidateNaturalProfileExpectation(map, expectedHasProfile: false, report);
                    break;

                case ScenarioR1:
                    ValidateExpectedState(
                        "R1 prepared state",
                        state,
                        expectedSpawnedOrders: 0,
                        expectedSpawnedResources: 0,
                        expectedActiveJobs: 0,
                        report);
                    ValidateNaturalProfileExpectation(map, expectedHasProfile: true, report);
                    break;

                case ScenarioR2:
                    ValidateExpectedState(
                        "R2 prepared state",
                        state,
                        expectedSpawnedOrders: 0,
                        expectedSpawnedResources: 1,
                        expectedActiveJobs: 0,
                        report);
                    ValidateFixtureThing(map, component, SoilResourceDefName, requireForbidden: false, report);
                    ValidateNaturalProfileExpectation(map, expectedHasProfile: false, report);
                    break;

                case ScenarioR3:
                    ValidateExpectedState(
                        "R3 prepared state",
                        state,
                        expectedSpawnedOrders: 1,
                        expectedSpawnedResources: 0,
                        expectedActiveJobs: 0,
                        report);
                    ValidateFixtureThing(map, component, DigOrderDefName, requireForbidden: true, report);
                    ValidateNaturalProfileExpectation(map, expectedHasProfile: false, report);
                    break;

                case ScenarioR4:
                    ValidateExpectedState(
                        "R4 prepared state",
                        state,
                        expectedSpawnedOrders: 1,
                        expectedSpawnedResources: 0,
                        expectedActiveJobs: 0,
                        report);
                    ValidatePlaceOrderContents(
                        map,
                        component,
                        expectedContainedCount: 0,
                        requireForbidden: true,
                        report);
                    ValidateNaturalProfileExpectation(map, expectedHasProfile: false, report);
                    break;

                case ScenarioR5:
                    ValidateExpectedState(
                        "R5 prepared state",
                        state,
                        expectedSpawnedOrders: 1,
                        expectedSpawnedResources: 0,
                        expectedActiveJobs: 0,
                        report);
                    ValidatePlaceOrderContents(
                        map,
                        component,
                        expectedContainedCount: 1,
                        requireForbidden: true,
                        report);
                    ValidateNaturalProfileExpectation(map, expectedHasProfile: false, report);
                    break;

                case ScenarioR6:
                    ValidateExpectedState(
                        "R6 prepared state",
                        state,
                        expectedSpawnedOrders: 1,
                        expectedSpawnedResources: 0,
                        expectedActiveJobs: 1,
                        report);
                    ValidateFixtureThing(map, component, DigOrderDefName, requireForbidden: false, report);
                    ValidatePartialActiveJob(map, component, report);
                    ValidateNaturalProfileExpectation(map, expectedHasProfile: false, report);
                    break;

                case ScenarioR7:
                    ValidateExpectedState(
                        "R7 prepared state",
                        state,
                        expectedSpawnedOrders: 1,
                        expectedSpawnedResources: 0,
                        expectedActiveJobs: 1,
                        report);
                    ValidatePlaceOrderContents(
                        map,
                        component,
                        expectedContainedCount: 1,
                        requireForbidden: false,
                        report);
                    ValidatePartialActiveJob(map, component, report);
                    ValidateNaturalProfileExpectation(map, expectedHasProfile: false, report);
                    break;

                default:
                    report.Fail("Prepared fixture", "Unknown scenario '" + component.Scenario + "'.");
                    break;
            }
        }

        private static void VerifyPreRemovalWorkPriorities(
            Map map,
            RemovalProbeMapComponent component,
            ProbeReport report)
        {
            Pawn pawn = FindPawnById(map, component.PriorityPawnId);

            if (pawn == null || pawn.workSettings == null || !component.HasValidWorkPrioritySnapshot)
            {
                report.Fail("Pre-removal Work priorities", "The persisted priority snapshot cannot be validated.");
                return;
            }

            var mismatches = new List<string>();

            for (var i = 0; i < component.WorkTypeDefNames.Count; i++)
            {
                WorkTypeDef workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(component.WorkTypeDefNames[i]);

                if (workType == null)
                {
                    mismatches.Add(component.WorkTypeDefNames[i] + "=<missing>");
                    continue;
                }

                int actual = pawn.workSettings.GetPriority(workType);

                if (actual != component.WorkPriorities[i])
                {
                    mismatches.Add(workType.defName + " " + component.WorkPriorities[i] + "->" + actual);
                }
            }

            if (mismatches.Count > 0)
            {
                report.Fail(
                    "Pre-removal Work priorities",
                    "Priorities changed after fixture preparation: " + string.Join("; ", mismatches) + ".");
            }
            else
            {
                report.Pass("Pre-removal Work priorities", "Priority snapshot is still unchanged before save.");
            }
        }

        private static void ValidateExpectedState(
            string scenario,
            CustomStateSnapshot state,
            int expectedSpawnedOrders,
            int expectedSpawnedResources,
            int expectedActiveJobs,
            ProbeReport report)
        {
            if (state.SpawnedOrderCount != expectedSpawnedOrders ||
                state.SpawnedResourceCount != expectedSpawnedResources ||
                state.ActiveCustomJobCount != expectedActiveJobs)
            {
                report.Fail(
                    scenario,
                    "Expected orders/resources/jobs=" + expectedSpawnedOrders + "/" + expectedSpawnedResources + "/" +
                    expectedActiveJobs + ", actual=" + state.SpawnedOrderCount + "/" + state.SpawnedResourceCount +
                    "/" + state.ActiveCustomJobCount + ". " + state.Describe());
            }
            else
            {
                report.Pass(scenario, "Persisted-state counts match the isolated scenario contract.");
            }
        }

        private static void ValidateFixtureThing(
            Map map,
            RemovalProbeMapComponent component,
            string expectedDefName,
            bool requireForbidden,
            ProbeReport report)
        {
            Thing thing = FindSpawnedThingAt(map, component.FixtureCell, expectedDefName);

            if (thing == null)
            {
                report.Fail(
                    "Fixture Thing",
                    "Expected '" + expectedDefName + "' is missing at " + component.FixtureCell + ".");
                return;
            }

            if (requireForbidden)
            {
                CompForbiddable forbiddable = thing.TryGetComp<CompForbiddable>();

                if (forbiddable == null || !forbiddable.Forbidden)
                {
                    report.Fail("Fixture Thing", "The stable fixture Thing is no longer forbidden.");
                    return;
                }
            }

            report.Pass(
                "Fixture Thing",
                "Expected '" + expectedDefName + "' is present at " + component.FixtureCell + ".");
        }

        private static void ValidatePlaceOrderContents(
            Map map,
            RemovalProbeMapComponent component,
            int expectedContainedCount,
            bool requireForbidden,
            ProbeReport report)
        {
            Thing order = FindSpawnedThingAt(map, component.FixtureCell, PlaceSoilOrderDefName);

            if (order == null)
            {
                report.Fail("Place order state", "Place order is missing at " + component.FixtureCell + ".");
                return;
            }

            if (!(order is IThingHolder holder))
            {
                report.Fail("Place order state", "Place order no longer implements vanilla IThingHolder.");
                return;
            }

            ThingOwner owner = holder.GetDirectlyHeldThings();

            if (owner == null)
            {
                report.Fail("Place order state", "Place order returned no directly held ThingOwner.");
                return;
            }

            int actualCount = owner.Count;

            if (actualCount != expectedContainedCount)
            {
                report.Fail(
                    "Place order state",
                    "Expected contained count=" + expectedContainedCount + ", actual=" + actualCount + ".");
                return;
            }

            if (expectedContainedCount == 1)
            {
                Thing material = owner[0];

                if (material == null || material.def?.defName != SoilResourceDefName || material.stackCount != 1)
                {
                    report.Fail(
                        "Place order state",
                        "Contained resource is not exactly one '" + SoilResourceDefName + "'.");
                    return;
                }
            }

            if (requireForbidden)
            {
                CompForbiddable forbiddable = order.TryGetComp<CompForbiddable>();

                if (forbiddable == null || !forbiddable.Forbidden)
                {
                    report.Fail("Place order state", "The stable Place order is no longer forbidden.");
                    return;
                }
            }

            report.Pass(
                "Place order state",
                "Place order persisted-state fixture is intact with contained count=" + actualCount + ".");
        }

        private static void ValidatePartialActiveJob(Map map, RemovalProbeMapComponent component, ProbeReport report)
        {
            Thing order = FindSpawnedThingAt(map, component.FixtureCell, component.FixtureThingDefName);
            Pawn pawn = FindPawnById(map, component.ActiveJobPawnId);

            if (order == null || pawn == null)
            {
                report.Fail("Active job state", "Captured order or active pawn is missing before save.");
                return;
            }

            Job currentJob = pawn.CurJob;

            if (currentJob == null || currentJob.def?.defName != component.ExpectedActiveJobDefName ||
                currentJob.targetA.Thing != order)
            {
                report.Fail(
                    "Active job state",
                    "Captured pawn is no longer running the expected relocation job against the fixture order.");
                return;
            }

            if (!TryReadPartialProgress(pawn.jobs?.curDriver, out string progressDescription))
            {
                report.Fail("Active job state", "Captured relocation driver is no longer at partial work progress.");
                return;
            }

            report.Pass(
                "Active job state",
                "Partial custom JobDriver remains active before save: " + progressDescription + ".");
        }

        private static void ValidateNaturalProfileExpectation(Map map, bool expectedHasProfile, ProbeReport report)
        {
            if (!TryGetNaturalRockProfileState(
                    map,
                    out bool componentPresent,
                    out bool hasProfile,
                    out string diagnostic))
            {
                report.Fail("Natural-rock profile", diagnostic);
                return;
            }

            if (!componentPresent)
            {
                report.Fail(
                    "Natural-rock profile",
                    "Production NaturalRockMapComponent is missing while the mod is active.");
                return;
            }

            if (hasProfile != expectedHasProfile)
            {
                report.Fail(
                    "Natural-rock profile",
                    "Expected committed profile=" + expectedHasProfile + ", actual=" + hasProfile + ".");
                return;
            }

            report.Pass("Natural-rock profile", "Committed profile state matches expected=" + expectedHasProfile + ".");
        }

        private static bool TryValidateNoCommittedProfiles(ProbeReport report)
        {
            foreach (Map map in Find.Maps)
            {
                if (!TryGetNaturalRockProfileState(
                        map,
                        out bool componentPresent,
                        out bool hasProfile,
                        out string diagnostic))
                {
                    report.Fail("Clean baseline / natural-rock profile", diagnostic);
                    return false;
                }

                if (!componentPresent)
                {
                    report.Fail(
                        "Clean baseline / natural-rock profile",
                        "NaturalRockMapComponent is missing from map " + map.uniqueID +
                        " while Soil Relocation is active.");
                    return false;
                }

                if (hasProfile)
                {
                    report.Fail(
                        "Clean baseline / natural-rock profile",
                        "Map " + map.uniqueID +
                        " already has a committed reconstruction profile. Use a clean baseline save.");
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetNaturalRockProfileState(
            Map map,
            out bool componentPresent,
            out bool hasProfile,
            out string diagnostic)
        {
            componentPresent = false;
            hasProfile = false;
            diagnostic = null;

            Type componentType = FindLoadedType(NaturalRockComponentTypeName);

            if (componentType == null)
            {
                diagnostic = "Production type '" + NaturalRockComponentTypeName + "' is not loaded.";
                return false;
            }

            MethodInfo getComponentMethod = typeof(Map).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method => method.Name == "GetComponent" && method.IsGenericMethodDefinition &&
                                          method.GetGenericArguments().Length == 1 &&
                                          method.GetParameters().Length == 0);

            if (getComponentMethod == null)
            {
                diagnostic = "Could not resolve Map.GetComponent<T>() for production component inspection.";
                return false;
            }

            object component;

            try
            {
                component = getComponentMethod.MakeGenericMethod(componentType).Invoke(map, null);
            }
            catch (Exception exception)
            {
                diagnostic = "Map.GetComponent reflection failed: " + exception.GetBaseException().Message;
                return false;
            }

            if (component == null)
            {
                componentPresent = false;
                return true;
            }

            componentPresent = true;
            FieldInfo profileField = componentType.GetField(
                NaturalRockProfileFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (profileField == null)
            {
                diagnostic = "Could not resolve production NaturalRockMapComponent._profile field.";
                return false;
            }

            hasProfile = profileField.GetValue(component) != null;
            return true;
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, throwOnError: false);

                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool TryCreateValidDigOrder(
            Map map,
            Pawn anchor,
            out Thing order,
            out Pawn capablePawn,
            out IntVec3 cell,
            out TerrainDef originalTerrain,
            ProbeReport report)
        {
            order = null;
            capablePawn = null;
            cell = IntVec3.Invalid;
            originalTerrain = null;

            ThingDef orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(DigOrderDefName);
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail(SoilTerrainDefName);
            WorkGiver_Scanner worker = ResolveWorkGiverScanner(DigWorkGiverDefName);

            if (orderDef == null || soil == null || worker == null)
            {
                report.Fail("Dig fixture", "Dig order, Soil terrain, or Dig WorkGiver is unavailable.");
                return false;
            }

            List<Pawn> pawns = ResolveCandidatePawns(map);

            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(
                         anchor.Position,
                         MaximumFixtureRadius,
                         useCenter: true))
            {
                if (!IsGeneralFixtureCell(map, anchor.Position, candidate))
                {
                    continue;
                }

                TerrainDef previous = map.terrainGrid.TerrainAt(candidate);

                if (!ReferenceEquals(previous, soil))
                {
                    continue;
                }

                Thing candidateOrder = null;

                try
                {
                    candidateOrder = ThingMaker.MakeThing(orderDef);
                    GenSpawn.Spawn(candidateOrder, candidate, map);

                    Pawn candidatePawn = FindPawnWithJob(worker, pawns, candidateOrder);

                    if (candidatePawn == null)
                    {
                        DestroyThingAndContents(candidateOrder);
                        candidateOrder = null;
                        continue;
                    }

                    order = candidateOrder;
                    capablePawn = candidatePawn;
                    cell = candidate;
                    originalTerrain = previous;
                    return true;
                }
                catch
                {
                    DestroyThingAndContents(candidateOrder);
                    throw;
                }
            }

            report.Fail(
                "Dig fixture",
                "Could not find a clear nearby Soil cell/pawn combination for which the production Dig WorkGiver " +
                "creates a job.");
            return false;
        }

        private static bool TryCreateValidReadyPlaceOrder(
            Map map,
            Pawn anchor,
            out Thing order,
            out Pawn capablePawn,
            out IntVec3 cell,
            out TerrainDef terrain,
            ProbeReport report)
        {
            order = null;
            capablePawn = null;
            cell = IntVec3.Invalid;
            terrain = null;

            ThingDef orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(PlaceSoilOrderDefName);
            ThingDef resourceDef = DefDatabase<ThingDef>.GetNamedSilentFail(SoilResourceDefName);
            WorkGiver_Scanner worker = ResolveWorkGiverScanner(PlaceWorkGiverDefName);

            if (orderDef == null || resourceDef == null || worker == null)
            {
                report.Fail("Place fixture", "Place order/resource Def or Place WorkGiver is unavailable.");
                return false;
            }

            List<Pawn> pawns = ResolveCandidatePawns(map);

            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(
                         anchor.Position,
                         MaximumFixtureRadius,
                         useCenter: true))
            {
                if (!IsGeneralFixtureCell(map, anchor.Position, candidate))
                {
                    continue;
                }

                TerrainDef candidateTerrain = map.terrainGrid.TerrainAt(candidate);

                if (candidateTerrain == null || !candidateTerrain.IsRock)
                {
                    continue;
                }

                Thing candidateOrder = null;

                try
                {
                    candidateOrder = SpawnReadyPlaceOrder(orderDef, resourceDef, candidate, map);
                    Pawn candidatePawn = FindPawnWithJob(worker, pawns, candidateOrder);

                    if (candidatePawn == null)
                    {
                        DestroyThingAndContents(candidateOrder);
                        DestroySpawnedThingsByDef(map, SoilResourceDefName);
                        candidateOrder = null;
                        continue;
                    }

                    order = candidateOrder;
                    capablePawn = candidatePawn;
                    cell = candidate;
                    terrain = candidateTerrain;
                    return true;
                }
                catch
                {
                    DestroyThingAndContents(candidateOrder);
                    DestroySpawnedThingsByDef(map, SoilResourceDefName);
                    throw;
                }
            }

            report.Fail(
                "Place fixture",
                "Could not find a nearby natural-rock destination/pawn combination accepted by the production " +
                "Place WorkGiver.");
            return false;
        }

        private static Thing SpawnReadyPlaceOrder(ThingDef orderDef, ThingDef resourceDef, IntVec3 cell, Map map)
        {
            Thing order = ThingMaker.MakeThing(orderDef);

            if (!(order is IThingHolder holder))
            {
                throw new InvalidOperationException("Place order ThingDef did not create an IThingHolder.");
            }

            GenSpawn.Spawn(order, cell, map);

            Thing resource = ThingMaker.MakeThing(resourceDef);
            resource.stackCount = 1;
            ThingOwner owner = holder.GetDirectlyHeldThings();

            if (owner == null || !owner.TryAdd(resource))
            {
                resource.Destroy();
                DestroyThingAndContents(order);
                throw new InvalidOperationException("Place order rejected its canonical Soil resource.");
            }

            return order;
        }

        private static Pawn FindPawnWithJob(WorkGiver_Scanner worker, IEnumerable<Pawn> pawns, Thing target)
        {
            foreach (Pawn pawn in pawns)
            {
                Job job;

                try
                {
                    job = worker.JobOnThing(pawn, target, forced: true);
                }
                catch
                {
                    continue;
                }

                if (job != null && job.targetA.Thing == target)
                {
                    return pawn;
                }
            }

            return null;
        }

        private static WorkGiver_Scanner ResolveWorkGiverScanner(string defName)
        {
            WorkGiverDef def = DefDatabase<WorkGiverDef>.GetNamedSilentFail(defName);
            return def?.Worker as WorkGiver_Scanner;
        }

        private static bool TryFindGeneralFixtureCell(Map map, IntVec3 center, out IntVec3 result)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, MaximumFixtureRadius, useCenter: true))
            {
                if (IsGeneralFixtureCell(map, center, cell))
                {
                    result = cell;
                    return true;
                }
            }

            result = IntVec3.Invalid;
            return false;
        }

        private static bool IsGeneralFixtureCell(Map map, IntVec3 center, IntVec3 cell)
        {
            if (!cell.InBounds(map) || cell.DistanceToSquared(center) < MinimumFixtureRadius * MinimumFixtureRadius ||
                cell.Fogged(map) || !cell.Standable(map))
            {
                return false;
            }

            List<Thing> things = cell.GetThingList(map);

            for (var i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];

                if (thing is Plant || thing is Fire || thing is Blueprint || thing is Frame || thing is Building)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<Pawn> ResolveCandidatePawns(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned.Where(pawn =>
                pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.jobs != null).ToList();
        }

        private static Pawn ResolvePrioritySnapshotPawn(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn =>
                pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.workSettings != null);
        }

        private static Pawn FindPawnById(Map map, int thingId)
        {
            if (map == null || thingId < 0)
            {
                return null;
            }

            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn =>
                pawn != null && pawn.thingIDNumber == thingId);
        }

        private static bool TrySnapshotOfficialWorkPriorities(
            Pawn pawn,
            out List<string> defNames,
            out List<int> priorities,
            out string error)
        {
            defNames = new List<string>();
            priorities = new List<int>();
            error = null;

            if (pawn?.workSettings == null)
            {
                error = "Pawn workSettings are unavailable.";
                return false;
            }

            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(def =>
                    def != null && def.modContentPack != null && def.modContentPack.IsOfficialMod)
                .OrderBy(def => def.defName, StringComparer.Ordinal).ToList();

            if (workTypes.Count == 0)
            {
                error = "No official WorkTypeDefs were found.";
                return false;
            }

            foreach (WorkTypeDef workType in workTypes)
            {
                defNames.Add(workType.defName);
                priorities.Add(pawn.workSettings.GetPriority(workType));
            }

            return true;
        }

        private static bool TryFindPartialActiveJob(
            Map map,
            Thing order,
            string expectedJobDefName,
            out Pawn pawn,
            out string progressDescription)
        {
            pawn = null;
            progressDescription = null;

            foreach (Pawn candidate in map.mapPawns.FreeColonistsSpawned)
            {
                Job currentJob = candidate?.CurJob;

                if (currentJob == null || currentJob.def?.defName != expectedJobDefName ||
                    currentJob.targetA.Thing != order)
                {
                    continue;
                }

                if (!TryReadPartialProgress(candidate.jobs?.curDriver, out progressDescription))
                {
                    continue;
                }

                pawn = candidate;
                return true;
            }

            return false;
        }

        private static bool TryReadPartialProgress(JobDriver driver, out string description)
        {
            description = null;

            if (driver == null)
            {
                return false;
            }

            Type type = driver.GetType();
            FieldInfo workLeftField = type.GetField("_workLeft", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo totalWorkField = type.GetField("_totalWork", BindingFlags.Instance | BindingFlags.NonPublic);

            if (workLeftField == null || totalWorkField == null || workLeftField.FieldType != typeof(float) ||
                totalWorkField.FieldType != typeof(float))
            {
                return false;
            }

            var workLeft = (float)workLeftField.GetValue(driver);
            var totalWork = (float)totalWorkField.GetValue(driver);
            bool partial = totalWork > 0f && workLeft > 0f && workLeft < totalWork;

            description = "driver=" + type.FullName + ", workLeft=" + workLeft.ToString("0.###") + ", totalWork=" +
                          totalWork.ToString("0.###") + ".";

            return partial;
        }

        private static Thing FindSpawnedThingAt(Map map, IntVec3 cell, string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

            if (map == null || def == null || !cell.InBounds(map))
            {
                return null;
            }

            return map.listerThings.ThingsOfDef(def).FirstOrDefault(thing =>
                thing != null && thing.Spawned && !thing.Destroyed && thing.Position == cell);
        }

        private static void DestroySpawnedThingsByDef(Map map, string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

            if (map == null || def == null)
            {
                return;
            }

            foreach (Thing thing in map.listerThings.ThingsOfDef(def).ToList())
            {
                if (thing != null && !thing.Destroyed)
                {
                    thing.Destroy();
                }
            }
        }

        private static void DestroyCarriedThingsByDef(Map map, string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

            if (map == null || def == null)
            {
                return;
            }

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                Thing carried = pawn?.carryTracker?.CarriedThing;

                if (carried != null && carried.def == def && !carried.Destroyed)
                {
                    carried.Destroy();
                }
            }
        }

        private static void DestroyThingAndContents(Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return;
            }

            if (thing is IThingHolder holder)
            {
                ThingOwner owner = holder.GetDirectlyHeldThings();

                while (owner != null && owner.Count > 0)
                {
                    Thing contained = owner[0];

                    if (contained == null)
                    {
                        break;
                    }

                    contained.Destroy();
                }
            }

            if (!thing.Destroyed)
            {
                thing.Destroy();
            }
        }

        private static CustomStateSnapshot CaptureCustomState()
        {
            var snapshot = new CustomStateSnapshot();

            foreach (Map map in Find.Maps)
            {
                foreach (string defName in _knownOrderDefNames)
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

                    if (def != null)
                    {
                        snapshot.SpawnedOrderCount += map.listerThings.ThingsOfDef(def).Count(thing =>
                            thing != null && thing.Spawned && !thing.Destroyed);
                    }
                }

                foreach (string defName in _knownResourceDefNames)
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

                    if (def != null)
                    {
                        snapshot.SpawnedResourceCount += map.listerThings.ThingsOfDef(def).Count(thing =>
                            thing != null && thing.Spawned && !thing.Destroyed);
                    }
                }

                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    string jobDefName = pawn?.CurJob?.def?.defName;

                    if (!string.IsNullOrEmpty(jobDefName) && _knownJobDefNames.Contains(jobDefName))
                    {
                        snapshot.ActiveCustomJobCount++;
                    }
                }
            }

            return snapshot;
        }

        private static bool TryGetSinglePendingActiveScenario(
            ProbeReport report,
            out Map map,
            out RemovalProbeMapComponent component)
        {
            if (!TryGetSingleScenario(
                    null,
                    RemovalProbeMapComponent.PhasePendingActiveJobCapture,
                    requireSoilRelocation: true,
                    report,
                    out map,
                    out component))
            {
                return false;
            }

            if (component.Scenario != ScenarioR6 && component.Scenario != ScenarioR7)
            {
                report.Fail(
                    "Active-job capture",
                    "Pending scenario '" + component.Scenario + "' is not an active-job fixture.");
                return false;
            }

            return true;
        }

        private static bool TryGetSinglePreparedScenario(
            bool requireSoilRelocation,
            ProbeReport report,
            out Map map,
            out RemovalProbeMapComponent component)
        {
            return TryGetSingleScenario(
                null,
                RemovalProbeMapComponent.PhasePrepared,
                requireSoilRelocation,
                report,
                out map,
                out component);
        }

        private static bool TryGetSingleScenario(
            string expectedScenario,
            string expectedPhase,
            bool requireSoilRelocation,
            ProbeReport report,
            out Map map,
            out RemovalProbeMapComponent component)
        {
            map = null;
            component = null;

            var components = Find.Maps.Select(candidate => candidate?.GetComponent<RemovalProbeMapComponent>())
                .Where(candidate => candidate != null && candidate.HasScenario).ToList();

            if (components.Count != 1)
            {
                report.Fail(
                    "Removal manifest",
                    "Expected exactly one active removal manifest in the loaded game, found " + components.Count + ".");
                return false;
            }

            component = components[0];
            map = component.OwnerMap;

            if (!string.IsNullOrEmpty(expectedScenario) && component.Scenario != expectedScenario)
            {
                report.Fail(
                    "Removal manifest",
                    "Expected scenario '" + expectedScenario + "', actual='" + component.Scenario + "'.");
                return false;
            }

            if (!string.IsNullOrEmpty(expectedPhase) && component.Phase != expectedPhase)
            {
                report.Fail(
                    "Removal manifest",
                    "Expected phase '" + expectedPhase + "', actual='" + component.Phase + "'.");
                return false;
            }

            bool soilRelocationActive = IsRunningPackage(SoilRelocationPackageId);

            if (soilRelocationActive != requireSoilRelocation)
            {
                report.Fail(
                    "Removal environment",
                    requireSoilRelocation
                        ? "Soil Relocation must be active for this action."
                        : "Soil Relocation must be disabled for this action.");
                return false;
            }

            if (!IsRunningPackage(RemovalTestsPackageId))
            {
                report.Fail("Removal environment", "Removal Runtime Tests mod is not active.");
                return false;
            }

            return true;
        }

        private static bool IsRunningPackage(string packageId)
        {
            return LoadedModManager.RunningModsListForReading.Any(mod =>
                mod != null && string.Equals(mod.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
        }

        private static string DescribeRemovedScenarioState(RemovalProbeMapComponent component)
        {
            switch (component.Scenario)
            {
                case ScenarioR0:
                    return "R0 contained no active mod-owned gameplay state before removal.";
                case ScenarioR1:
                    return "R1 contained a committed production NaturalRockMapComponent profile before removal.";
                case ScenarioR2:
                    return "R2 contained one spawned Soil portable resource before removal.";
                case ScenarioR3:
                    return "R3 contained one Dig relocation order before removal.";
                case ScenarioR4:
                    return "R4 contained one AwaitingMaterial Place order before removal.";
                case ScenarioR5:
                    return "R5 contained one ReadyForWork Place order owning one resource before removal.";
                case ScenarioR6:
                    return "R6 contained one partial active Dig JobDriver plus its order before removal.";
                case ScenarioR7:
                    return "R7 contained one partial active Place JobDriver plus its ready order/resource before " +
                           "removal.";
                default:
                    return "Unknown pre-removal scenario '" + component.Scenario + "'.";
            }
        }

        private static void ReportPreparedSaveInstructions(RemovalProbeMapComponent component, ProbeReport report)
        {
            report.Info(
                "Save protocol",
                "Run 'Removal: validate prepared fixture' while paused. If it passes, save as " +
                GetSuggestedBeforeSaveName(component.Scenario) + ", exit RimWorld completely, disable only Soil " +
                "Relocation (keep Harmony and Removal Runtime Tests enabled), restart, load that save, and run " +
                "'Removal: verify first load without mod'. If the save cannot reach a playable map, record the load " +
                "failure and its log as the first-load result for this scenario.");
        }

        private static string GetSuggestedBeforeSaveName(string scenario)
        {
            return "SR_Removal_" + scenario + "_Before";
        }

        private static void RunAction(string title, Action<ProbeReport> action)
        {
            var report = new ProbeReport();

            try
            {
                action(report);
            }
            catch (Exception exception)
            {
                report.Fail("Probe execution", "Unhandled exception: " + exception);
            }

            WriteReport(title, report);
        }

        private static void WriteReport(string title, ProbeReport report)
        {
            var builder = new StringBuilder();

            builder.AppendLine(LogPrefix + " " + title + " finished.");

            foreach (string line in report.Lines)
            {
                builder.AppendLine(LogPrefix + " " + line);
            }

            builder.Append(
                LogPrefix + " Summary: PASS=" + report.PassCount + ", FAIL=" + report.FailureCount + ", SKIP=" +
                report.SkipCount + ".");

            if (report.FailureCount > 0)
            {
                Log.Error(builder.ToString());
            }
            else
            {
                Log.Message(builder.ToString());
            }
        }

        private sealed class CustomStateSnapshot
        {
            public int SpawnedOrderCount { get; set; }

            public int SpawnedResourceCount { get; set; }

            public int ActiveCustomJobCount { get; set; }

            public string Describe()
            {
                return "spawnedOrders=" + SpawnedOrderCount + ", spawnedResources=" + SpawnedResourceCount +
                       ", activeJobs=" + ActiveCustomJobCount;
            }
        }

        private sealed class ProbeReport
        {
            private readonly List<string> _lines = new List<string>();

            public int PassCount { get; private set; }

            public int FailureCount { get; private set; }

            public int SkipCount { get; private set; }

            public IReadOnlyList<string> Lines => _lines;

            public void Info(string scenario, string message)
            {
                _lines.Add("[INFO] " + scenario + ": " + message);
            }

            public void Pass(string scenario, string message)
            {
                PassCount++;
                _lines.Add("[PASS] " + scenario + ": " + message);
            }

            public void Fail(string scenario, string message)
            {
                FailureCount++;
                _lines.Add("[FAIL] " + scenario + ": " + message);
            }

            public void Skip(string scenario, string message)
            {
                SkipCount++;
                _lines.Add("[SKIP] " + scenario + ": " + message);
            }
        }
    }
}
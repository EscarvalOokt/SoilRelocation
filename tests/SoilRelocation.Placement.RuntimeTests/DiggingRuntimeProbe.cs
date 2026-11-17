using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using RimWorld;
using SoilRelocation.Digging;
using SoilRelocation.Domain;
using SoilRelocation.Settings;
using SoilRelocation.Substrate;
using SoilRelocation.Work;
using Verse;
using Verse.AI;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        private const string MissingRockDefName = "SoilRelocation_RuntimeTest_MissingNaturalRock";
        private const int DigSaveLoadWorkAmount = 1370;
        private const float DigSaveLoadRawXpPerTick = 0.18f;

        private static readonly DigFamilyCase[] _digFamilyCases =
        [
            new DigFamilyCase("Sand", "Sand", "SoilRelocation_Sand"),
            new DigFamilyCase("Gravel", "Gravel", "SoilRelocation_Gravel"),
            new DigFamilyCase("Soil", "Soil", "SoilRelocation_Soil"),
            new DigFamilyCase("Rich Soil", "SoilRich", "SoilRelocation_RichSoil"),
            new DigFamilyCase("MossyTerrain", "MossyTerrain", "SoilRelocation_Soil"),
            new DigFamilyCase("Riverbank", "Riverbank", "SoilRelocation_RichSoil")
        ];

        private static DigSaveLoadProbeState _digSaveLoadState;

        [DebugAction(
            "Soil Relocation",
            "Validate digging lifecycle",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ValidateDiggingLifecycle()
        {
            var report = new ProbeReport();

            try
            {
                RunDiggingRuntimeProbe(report);
            }
            catch (Exception exception)
            {
                report.Fail("Digging runtime probe", "Unhandled exception: " + exception);
            }

            WriteReport("Digging lifecycle probe", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Prepare digging save-load cases",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareDiggingSaveLoadCases()
        {
            var report = new ProbeReport();

            try
            {
                PrepareDigSaveLoadCases(report);
            }
            catch (Exception exception)
            {
                RestorePreparedDigSettings();
                report.Fail("Digging save-load setup", "Unhandled exception: " + exception);
            }

            WriteReport("Digging save-load setup", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Capture digging save-load progress",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void CaptureDiggingSaveLoadProgress()
        {
            var report = new ProbeReport();

            try
            {
                CaptureDigSaveLoadProgress(report);
            }
            catch (Exception exception)
            {
                RestorePreparedDigSettings();
                report.Fail("Digging progress capture", "Unhandled exception: " + exception);
            }

            WriteReport("Digging save-load progress capture", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Verify digging save-load cases",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyDiggingSaveLoadCases()
        {
            var report = new ProbeReport();

            try
            {
                VerifyDigSaveLoadCases(report);
            }
            catch (Exception exception)
            {
                report.Fail("Digging save-load verification", "Unhandled exception: " + exception);
            }
            finally
            {
                RestorePreparedDigSettings();
            }

            WriteReport("Digging save-load verification", report);
        }

        private static void RunDiggingRuntimeProbe(ProbeReport report)
        {
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail("Environment", "No current map is available.");
                return;
            }

            if (!TryResolveUxDefinitions(out UxDefinitions definitions, report))
            {
                return;
            }

            Pawn pawn = ResolvePawn(map, requireEmptyCarry: false);

            if (pawn == null)
            {
                report.Fail("Environment", "No spawned free colonist is available for Dig scheduling checks.");
                return;
            }

            report.Info("Environment", "Pawn=" + pawn.LabelShortCap + ", map=" + map.uniqueID + ".");

            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;
            RelocationSkillSelection originalSkills = settings?.DiggingSkills ?? RelocationSkillSelection.None;

            try
            {
                if (settings != null)
                {
                    settings.DiggingSkills = RelocationSkillSelection.None;
                }

                ProbeLazyNaturalRockProfileLifecycle(map, pawn, definitions, report);
                ProbeDigFamilyMatrix(map, pawn, definitions, report);
                ProbeDigPermanentRevalidation(map, pawn, definitions, report);
                ProbeDigTransientBlockers(map, pawn, definitions, report);
                ProbeDigCompletionSideEffects(map, definitions, report);
                ProbeNaturalRockProfileRepairIntegration(map, pawn, report);
            }
            finally
            {
                if (settings != null)
                {
                    settings.DiggingSkills = originalSkills;
                }
            }

            ProbeDigWorkContracts(map, pawn, definitions, report);

            report.Info(
                "Dig work / manual completion smoke",
                "During final acceptance, prioritize one normal Dig order and let its real JobDriver complete. " +
                "Confirm " +
                "the visible progress bar reaches completion, exactly one portable resource appears, and the exposed " +
                "associated natural terrain matches the map reconstruction profile.");
        }

        private static void ProbeLazyNaturalRockProfileLifecycle(
            Map map,
            Pawn pawn,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string scenario = "Natural-rock profile / lazy lifecycle";
            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();

            if (component == null)
            {
                report.Fail(scenario, "NaturalRockMapComponent is unavailable on the current map.");
                return;
            }

            if (!TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile originalProfile))
            {
                report.Fail(scenario, "Could not inspect NaturalRockMapComponent._profile.");
                return;
            }

            if (originalProfile != null)
            {
                report.Skip(
                    scenario,
                    "The current map already has a committed natural-rock profile. Run this case on a fresh map to " +
                    "exercise first-use capture; the remaining Dig probes will still run.");
                return;
            }

            if (!TryFindClearRockCells(map, pawn.Position, 4, 35, 2, out List<IntVec3> cells))
            {
                report.Skip(scenario, "Could not find two clear natural-rock cells for the lazy-profile fixture.");
                return;
            }

            TerrainDef firstOriginal = map.terrainGrid.TerrainAt(cells[0]);
            TerrainDef secondOriginal = map.terrainGrid.TerrainAt(cells[1]);
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            TerrainDef unsupported = ResolveUnsupportedTerrain();
            var spawnedResources = new List<Thing>();
            DigRelocationOrder order = null;

            if (soil == null || unsupported == null)
            {
                report.Skip(scenario, "Required Soil or an unsupported TerrainDef is unavailable.");
                return;
            }

            try
            {
                if (!component.TryPrepareResolution(cells[0], out NaturalRockResolution prepared) ||
                    !prepared.RequiresProfileCommit ||
                    component.HasProfile)
                {
                    report.Fail(
                        scenario,
                        "Preparing the first reconstruction did not produce an uncommitted candidate profile.");
                    return;
                }

                map.terrainGrid.SetTerrain(cells[0], soil);
                order = SpawnDigOrder(definitions.DigOrderDef, cells[0], map);
                order.TryCancel();
                order = null;

                if (component.HasProfile)
                {
                    report.Fail(scenario, "Creating and cancelling a Dig order committed a reconstruction profile.");
                    return;
                }

                order = SpawnDigOrder(definitions.DigOrderDef, cells[0], map);
                map.terrainGrid.SetTerrain(cells[0], unsupported);
                Job invalidatedJob = definitions.DigWorker.JobOnThing(pawn, order, forced: true);

                if (invalidatedJob != null || !order.Destroyed || component.HasProfile)
                {
                    report.Fail(
                        scenario,
                        "Permanently invalidated Dig work either survived, scheduled work, or committed a profile.");
                    return;
                }

                order = null;
                map.terrainGrid.SetTerrain(cells[0], soil);
                order = SpawnDigOrder(definitions.DigOrderDef, cells[0], map);
                DiggingValidationResult validation = DiggingValidator.Validate(map, cells[0]);

                if (!validation.IsValid ||
                    !DigResultResolver.TryPrepare(map, cells[0], validation, out DigResultPreparation preparation))
                {
                    report.Fail(scenario, "Could not prepare the first successful Dig result.");
                    return;
                }

                List<Thing> before = CaptureSpawnedThings(map, preparation.PortableResourceDef);

                if (!DigResultApplier.TryApply(map, cells[0], preparation))
                {
                    report.Fail(scenario, "The first valid Dig result could not be applied.");
                    return;
                }

                spawnedResources.AddRange(FindNewSpawnedThings(map, preparation.PortableResourceDef, before));
                order.TryCancel();
                order = null;

                if (!component.HasProfile)
                {
                    report.Fail(scenario, "Successful first Dig did not commit the candidate profile.");
                    return;
                }

                if (!component.TryPrepareResolution(cells[1], out NaturalRockResolution committedResolution) ||
                    committedResolution.RequiresProfileCommit)
                {
                    report.Fail(scenario, "A later reconstruction did not reuse the committed profile.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Profile capture is lazy: preparation/cancellation/invalidation do not commit it; the first " +
                    "successful Dig commits it, and later resolutions reuse the committed profile.");
            }
            finally
            {
                DestroyUxOrder(order);
                DestroyThings(spawnedResources);
                RestoreTerrain(map, cells[0], firstOriginal);
                RestoreTerrain(map, cells[1], secondOriginal);
                TrySetNaturalRockProfile(component, null);
            }
        }

        private static void ProbeDigFamilyMatrix(Map map, Pawn pawn, UxDefinitions definitions, ProbeReport report)
        {
            foreach (DigFamilyCase familyCase in _digFamilyCases)
            {
                string scenario = "Dig family / " + familyCase.Name;
                TerrainDef sourceTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail(familyCase.TerrainDefName);
                ThingDef expectedResource = DefDatabase<ThingDef>.GetNamedSilentFail(familyCase.ResourceDefName);

                if (sourceTerrain == null || expectedResource == null)
                {
                    report.Fail(scenario, "Expected source terrain or canonical portable resource Def is unavailable.");
                    continue;
                }

                if (!TryFindClearRockCell(map, pawn.Position, 4, 35, out IntVec3 cell))
                {
                    report.Skip(scenario, "Could not find a clear natural-rock fixture cell.");
                    continue;
                }

                TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
                NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();
                TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile originalProfile);
                DigRelocationOrder order = null;
                var spawnedResources = new List<Thing>();

                try
                {
                    map.terrainGrid.SetTerrain(cell, sourceTerrain);
                    DiggingValidationResult validation = DiggingValidator.Validate(map, cell);

                    if (!validation.IsValid ||
                        validation.Family == null ||
                        !ReferenceEquals(validation.Family.CanonicalPortableResource, expectedResource))
                    {
                        report.Fail(
                            scenario,
                            "Final loaded terrain membership did not resolve to the expected canonical resource.");
                        continue;
                    }

                    order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                    Job job = definitions.DigWorker.JobOnThing(pawn, order, forced: true);

                    if (!IsExpectedDigJob(job, order))
                    {
                        report.Fail(scenario, "The production Dig WorkGiver did not produce the expected job.");
                        continue;
                    }

                    if (!DigResultResolver.TryPrepare(map, cell, validation, out DigResultPreparation preparation) ||
                        !ReferenceEquals(preparation.PortableResourceDef, expectedResource) ||
                        preparation.ExposedTerrain == null)
                    {
                        report.Fail(scenario, "DigResultResolver did not prepare the expected family result.");
                        continue;
                    }

                    List<Thing> before = CaptureSpawnedThings(map, expectedResource);

                    if (!DigResultApplier.TryApply(map, cell, preparation))
                    {
                        report.Fail(scenario, "DigResultApplier rejected the valid prepared result.");
                        continue;
                    }

                    List<Thing> created = FindNewSpawnedThings(map, expectedResource, before);
                    spawnedResources.AddRange(created);
                    order.TryCancel();
                    order = null;

                    bool exactResource = created.Count == 1 && created[0].stackCount == 1;
                    bool terrainApplied = ReferenceEquals(map.terrainGrid.TerrainAt(cell), preparation.ExposedTerrain);

                    if (!exactResource || !terrainApplied)
                    {
                        report.Fail(
                            scenario,
                            "Completion did not create exactly one canonical resource and expose the resolved " +
                            "natural " +
                            "terrain.");
                        continue;
                    }

                    report.Pass(
                        scenario,
                        "WorkGiver scheduling and Dig completion preserve the 1 cell -> 1 canonical resource family " +
                        "contract and expose the associated natural terrain.");
                }
                finally
                {
                    DestroyUxOrder(order);
                    DestroyThings(spawnedResources);
                    RestoreTerrain(map, cell, originalTerrain);
                    TrySetNaturalRockProfile(component, originalProfile);
                }
            }
        }

        private static void ProbeDigPermanentRevalidation(
            Map map,
            Pawn pawn,
            UxDefinitions definitions,
            ProbeReport report)
        {
            ProbeDigUnsupportedSourceInvalidation(map, pawn, definitions, report);
            ProbeDigStructuralBlockerInvalidation(map, pawn, definitions, report);
        }

        private static void ProbeDigUnsupportedSourceInvalidation(
            Map map,
            Pawn pawn,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string scenario = "Dig revalidation / unsupported source";
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            TerrainDef unsupported = ResolveUnsupportedTerrain();

            if (soil == null ||
                unsupported == null ||
                !TryFindClearRockCell(map, pawn.Position, 4, 35, out IntVec3 cell))
            {
                report.Skip(scenario, "Could not prepare Soil/unsupported terrain fixture data.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            DigRelocationOrder order = null;
            ThingDef resourceDef = DefDatabase<ThingDef>.GetNamedSilentFail("SoilRelocation_Soil");
            List<Thing> before = CaptureSpawnedThings(map, resourceDef);

            try
            {
                map.terrainGrid.SetTerrain(cell, soil);
                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);

                if (!IsExpectedDigJob(definitions.DigWorker.JobOnThing(pawn, order, forced: true), order))
                {
                    report.Skip(scenario, "The initial valid Dig fixture could not be scheduled.");
                    return;
                }

                map.terrainGrid.SetTerrain(cell, unsupported);
                Job staleJob = definitions.DigWorker.JobOnThing(pawn, order, forced: true);
                bool noResource = FindNewSpawnedThings(map, resourceDef, before).Count == 0;

                if (staleJob != null ||
                    !order.Destroyed ||
                    !noResource ||
                    !ReferenceEquals(map.terrainGrid.TerrainAt(cell), unsupported))
                {
                    report.Fail(
                        scenario,
                        "Permanent source invalidation scheduled work, preserved the order, created material, or " +
                        "overwrote the externally changed terrain.");
                    return;
                }

                order = null;
                report.Pass(
                    scenario,
                    "A source terrain that becomes unsupported is rejected on revalidation without material creation " +
                    "or terrain overwrite.");
            }
            finally
            {
                DestroyUxOrder(order);
                DestroyThings(FindNewSpawnedThings(map, resourceDef, before));
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeDigStructuralBlockerInvalidation(
            Map map,
            Pawn pawn,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string scenario = "Dig revalidation / structural blocker";
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            ThingDef conduitDef = DefDatabase<ThingDef>.GetNamedSilentFail("PowerConduit");
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            if (soil == null ||
                conduitDef == null ||
                settings == null ||
                !TryFindClearRockCell(map, pawn.Position, 4, 35, out IntVec3 cell))
            {
                report.Skip(scenario, "Could not prepare the structural-blocker fixture.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            bool originalAllowConduits = settings.AllowRelocationUnderConduits;
            DigRelocationOrder order = null;
            Thing conduit = null;

            try
            {
                settings.AllowRelocationUnderConduits = false;
                map.terrainGrid.SetTerrain(cell, soil);
                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                conduit = ThingMaker.MakeThing(conduitDef);
                GenSpawn.Spawn(conduit, cell, map);

                Job blockedJob = definitions.DigWorker.JobOnThing(pawn, order, forced: true);

                if (blockedJob != null || !order.Destroyed)
                {
                    report.Fail(scenario, "A new structural blocker did not permanently cancel the Dig order.");
                    return;
                }

                order = null;
                report.Pass(
                    scenario,
                    "A structural blocker introduced after order creation is detected on work revalidation and " +
                    "cancels the order.");
            }
            finally
            {
                settings.AllowRelocationUnderConduits = originalAllowConduits;
                DestroyUxOrder(order);
                DestroyLooseThing(conduit);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeDigTransientBlockers(Map map, Pawn pawn, UxDefinitions definitions, ProbeReport report)
        {
            ProbeDigFireBlocker(map, pawn, definitions, report);
            ProbeDigPlantPrerequisite(map, pawn, definitions, report, "Dig transient blocker / plant prerequisite");

            const string scenario = "Dig transient blocker / plant on another loaded map";
            foreach (Map otherMap in Find.Maps)
            {
                if (otherMap != map && ResolvePawn(otherMap, requireEmptyCarry: false) is { } otherPawn)
                {
                    ProbeDigPlantPrerequisite(otherMap, otherPawn, definitions, report, scenario);
                    return;
                }
            }

            report.Skip(scenario, "Requires a second loaded map with a spawned free colonist.");
        }

        private static void ProbeDigFireBlocker(Map map, Pawn pawn, UxDefinitions definitions, ProbeReport report)
        {
            const string scenario = "Dig transient blocker / fire";
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            ThingDef fireDef = DefDatabase<ThingDef>.GetNamedSilentFail("Fire");

            if (soil == null || fireDef == null || !TryFindClearRockCell(map, pawn.Position, 4, 35, out IntVec3 cell))
            {
                report.Skip(scenario, "Could not prepare the fire fixture.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            DigRelocationOrder order = null;
            Thing fire = null;

            try
            {
                map.terrainGrid.SetTerrain(cell, soil);
                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                fire = ThingMaker.MakeThing(fireDef);
                GenSpawn.Spawn(fire, cell, map);

                Job blockedJob = definitions.DigWorker.JobOnThing(pawn, order, forced: true);

                if (blockedJob != null || order.Destroyed)
                {
                    report.Fail(scenario, "Fire did not preserve the Dig order as a transient blocker.");
                    return;
                }

                fire.Destroy();
                fire = null;

                if (!IsExpectedDigJob(definitions.DigWorker.JobOnThing(pawn, order, forced: true), order))
                {
                    report.Fail(scenario, "Dig work did not become available after the fire was removed.");
                    return;
                }

                report.Pass(scenario, "Fire pauses Dig work without cancelling the order; work resumes afterwards.");
            }
            finally
            {
                DestroyLooseThing(fire);
                DestroyUxOrder(order);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeDigPlantPrerequisite(
            Map map,
            Pawn pawn,
            UxDefinitions definitions,
            ProbeReport report,
            string scenario)
        {
            Map viewedMap = Find.CurrentMap;
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            ThingDef plantDef = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeOak");

            if (soil == null || plantDef == null || !TryFindClearRockCell(map, pawn.Position, 4, 35, out IntVec3 cell))
            {
                report.Skip(scenario, "Could not prepare the plant fixture.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            DigRelocationOrder order = null;
            Plant plant = null;

            try
            {
                map.terrainGrid.SetTerrain(cell, soil);
                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                plant = ThingMaker.MakeThing(plantDef) as Plant;

                if (plant == null)
                {
                    report.Skip(scenario, "Plant_TreeOak did not create a Plant instance.");
                    return;
                }

                GenSpawn.Spawn(plant, cell, map);
                Job blockedJob = definitions.DigWorker.JobOnThing(pawn, order, forced: true);
                Designation cut = map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant);
                Designation harvest = map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant);

                bool foreignDesignation = viewedMap != map && viewedMap.designationManager.DesignationOn(plant) != null;
                if (blockedJob != null || order.Destroyed || (cut == null && harvest == null) || foreignDesignation)
                {
                    report.Fail(
                        scenario,
                        "Plant prerequisite did not preserve the order and request vanilla plant removal.");
                    return;
                }

                report.Pass(
                    scenario,
                    "A removable plant remains a transient blocker and reuses a vanilla Cut/Harvest designation.");
            }
            finally
            {
                if (viewedMap != map && plant != null)
                {
                    viewedMap.designationManager.RemoveAllDesignationsOn(plant);
                }

                DestroyUxOrder(order);
                DestroyLooseThing(plant);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeDigCompletionSideEffects(Map map, UxDefinitions definitions, ProbeReport report)
        {
            const string successScenario = "Dig completion side effects / success";
            const string cancelScenario = "Dig completion side effects / cancellation";
            Pawn pawn = ResolvePawn(map, requireEmptyCarry: false);
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            ThingDef filthDef = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Dirt");

            if (pawn == null ||
                soil == null ||
                filthDef == null ||
                !TryFindFilthFreeRockCells(map, pawn.Position, 2, out List<IntVec3> cells))
            {
                report.Skip(successScenario, "Could not prepare two clear filth-free cells for side-effect checks.");
                report.Skip(cancelScenario, "Side-effect cancellation fixture was not available.");
                return;
            }

            ProbeSuccessfulDigSideEffects(map, definitions, cells[0], soil, filthDef, report);
            ProbeCancelledDigSideEffects(map, definitions, cells[1], soil, filthDef, report);
        }

        private static void ProbeSuccessfulDigSideEffects(
            Map map,
            UxDefinitions definitions,
            IntVec3 cell,
            TerrainDef soil,
            ThingDef filthDef,
            ProbeReport report)
        {
            const string scenario = "Dig completion side effects / success";
            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            float originalSnow = ReadSnowDepth(map, cell);
            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();
            TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile originalProfile);
            DigRelocationOrder order = null;
            Thing filth = null;
            var spawnedResources = new List<Thing>();

            try
            {
                map.terrainGrid.SetTerrain(cell, soil);
                map.snowGrid.SetDepth(cell, 0.5f);
                filth = ThingMaker.MakeThing(filthDef);
                GenSpawn.Spawn(filth, cell, map);

                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                DiggingValidationResult validation = DiggingValidator.Validate(map, cell);

                if (!validation.IsValid ||
                    !DigResultResolver.TryPrepare(map, cell, validation, out DigResultPreparation preparation))
                {
                    report.Fail(scenario, "Could not prepare a valid Dig completion.");
                    return;
                }

                List<Thing> before = CaptureSpawnedThings(map, preparation.PortableResourceDef);

                if (!DigResultApplier.TryApply(map, cell, preparation))
                {
                    report.Fail(scenario, "Valid Dig completion could not be applied.");
                    return;
                }

                spawnedResources.AddRange(FindNewSpawnedThings(map, preparation.PortableResourceDef, before));
                order.TryCancel();
                order = null;

                bool snowCleared = ReadSnowDepth(map, cell) <= 0.0001f;
                bool filthCleared = filth.Destroyed && !cell.GetThingList(map).Any(thing => thing is Filth);

                if (!snowCleared || !filthCleared || spawnedResources.Count != 1)
                {
                    report.Fail(
                        scenario,
                        "Successful completion did not clear snow/filth and create exactly one portable resource.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Successful Dig completion clears snow and filth while producing exactly one portable resource.");
            }
            finally
            {
                DestroyUxOrder(order);
                DestroyThings(spawnedResources);
                DestroyLooseThing(filth);
                map.snowGrid.SetDepth(cell, originalSnow);
                RestoreTerrain(map, cell, originalTerrain);
                TrySetNaturalRockProfile(component, originalProfile);
            }
        }

        private static void ProbeCancelledDigSideEffects(
            Map map,
            UxDefinitions definitions,
            IntVec3 cell,
            TerrainDef soil,
            ThingDef filthDef,
            ProbeReport report)
        {
            const string scenario = "Dig completion side effects / cancellation";
            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            float originalSnow = ReadSnowDepth(map, cell);
            DigRelocationOrder order = null;
            Thing filth = null;

            try
            {
                map.terrainGrid.SetTerrain(cell, soil);
                map.snowGrid.SetDepth(cell, 0.5f);
                filth = ThingMaker.MakeThing(filthDef);
                GenSpawn.Spawn(filth, cell, map);

                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                order.TryCancel();
                order = null;

                bool snowPreserved = Math.Abs(ReadSnowDepth(map, cell) - 0.5f) <= 0.0001f;
                bool filthPreserved = filth.Spawned &&
                                      !filth.Destroyed &&
                                      cell.GetThingList(map).Any(thing => ReferenceEquals(thing, filth));
                bool terrainPreserved = ReferenceEquals(map.terrainGrid.TerrainAt(cell), soil);

                if (!snowPreserved || !filthPreserved || !terrainPreserved)
                {
                    report.Fail(
                        scenario,
                        "Cancelling an unfinished Dig order applied one or more completion-only side effects.");
                    return;
                }

                report.Pass(scenario, "Cancelling an unfinished Dig order leaves terrain, snow, and filth untouched.");
            }
            finally
            {
                DestroyUxOrder(order);
                DestroyLooseThing(filth);
                map.snowGrid.SetDepth(cell, originalSnow);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeDigWorkContracts(Map map, Pawn pawn, UxDefinitions definitions, ProbeReport report)
        {
            const string scenario = "Dig work / settings and eligibility";
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");

            if (settings == null || soil == null || !TryFindClearRockCell(map, pawn.Position, 4, 35, out IntVec3 cell))
            {
                report.Skip(scenario, "Required settings or Dig fixture cell is unavailable.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            RelocationSkillSelection originalSkills = settings.DiggingSkills;
            int originalWorkAmount = settings.DigWorkAmount;
            float originalXp = settings.RawXpPerTick;
            DigRelocationOrder order = null;

            try
            {
                map.terrainGrid.SetTerrain(cell, soil);
                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);

                RelocationSkillSelection[] representativeSelections =
                [
                    RelocationSkillSelection.None,
                    RelocationSkillSelection.Mining,
                    RelocationSkillSelection.Mining | RelocationSkillSelection.Construction
                ];

                foreach (RelocationSkillSelection selection in representativeSelections)
                {
                    settings.DiggingSkills = selection;
                    Job job = definitions.DigWorker.JobOnThing(pawn, order, forced: true);
                    bool expected = RelocationWorkUtility.CanPerform(pawn, selection);

                    if ((job != null) != expected)
                    {
                        report.Fail(
                            scenario,
                            "WorkGiver eligibility diverged from RelocationWorkUtility for selection " +
                            selection +
                            ".");
                        return;
                    }
                }

                settings.DiggingSkills = RelocationSkillSelection.None;
                settings.DigWorkAmount = DigSaveLoadWorkAmount;
                settings.RawXpPerTick = DigSaveLoadRawXpPerTick;

                if (SoilRelocationSettings.NormalizeWorkAmount(settings.DigWorkAmount) != DigSaveLoadWorkAmount ||
                    Math.Abs(
                        SoilRelocationSettings.NormalizeRawXpPerTick(settings.RawXpPerTick) - DigSaveLoadRawXpPerTick) >
                    0.0001f)
                {
                    report.Fail(
                        scenario,
                        "Representative non-default work/XP settings were not stable normalized values.");
                    return;
                }

                Job firstAttempt = definitions.DigWorker.JobOnThing(pawn, order, forced: true);
                Job secondAttempt = definitions.DigWorker.JobOnThing(pawn, order, forced: true);

                if (firstAttempt == null ||
                    secondAttempt == null ||
                    order.Destroyed ||
                    !ReferenceEquals(map.terrainGrid.TerrainAt(cell), soil))
                {
                    report.Fail(
                        scenario,
                        "Creating/abandoning scheduling attempts consumed the order or mutated terrain prematurely.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Dig scheduling delegates None/single/multi-skill eligibility to RelocationWorkUtility; " +
                    "representative work/XP settings normalize correctly and scheduling attempts do not own progress.");

                ProbeDisabledDigSkillScheduling(map, definitions, order, report);
            }
            finally
            {
                settings.DiggingSkills = originalSkills;
                settings.DigWorkAmount = originalWorkAmount;
                settings.RawXpPerTick = originalXp;
                DestroyUxOrder(order);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeDisabledDigSkillScheduling(
            Map map,
            UxDefinitions definitions,
            DigRelocationOrder order,
            ProbeReport report)
        {
            const string scenario = "Dig work / disabled selected skill";
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            if (settings == null)
            {
                report.Skip(scenario, "Soil Relocation settings are unavailable.");
                return;
            }

            (SkillDef Def, RelocationSkillSelection Selection)[] skills =
            [
                (SkillDefOf.Plants, RelocationSkillSelection.Plants),
                (SkillDefOf.Construction, RelocationSkillSelection.Construction),
                (SkillDefOf.Mining, RelocationSkillSelection.Mining)
            ];

            var tested = false;
            foreach (Pawn candidate in map.mapPawns.FreeColonistsSpawned)
            {
                if (candidate?.skills == null || candidate.Dead || candidate.Downed)
                {
                    continue;
                }

                foreach ((SkillDef skill, RelocationSkillSelection selection) in skills)
                {
                    if (!candidate.skills.GetSkill(skill).TotallyDisabled)
                    {
                        continue;
                    }

                    settings.DiggingSkills = selection;
                    Job job = definitions.DigWorker.JobOnThing(candidate, order, forced: true);

                    if (job != null || RelocationWorkUtility.CanPerform(candidate, selection))
                    {
                        report.Fail(
                            scenario,
                            "Dig accepted a pawn whose selected skill is disabled: " + selection + ".");
                        return;
                    }

                    tested = true;
                }
            }

            if (tested)
            {
                report.Pass(scenario, "Dig excludes colonists whose selected skill is disabled.");
            }
            else
            {
                report.Skip(scenario, "No spawned free colonist has a disabled Mining, Construction or Plants skill.");
            }
        }

        private static void ProbeNaturalRockProfileRepairIntegration(Map map, Pawn pawn, ProbeReport report)
        {
            const string scenario = "Natural-rock profile / runtime repair integration";
            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();

            if (component == null ||
                Find.World == null ||
                !TryFindClearRockCell(map, pawn.Position, 4, 35, out IntVec3 cell))
            {
                report.Skip(scenario, "Required map component/world/fixture cell is unavailable.");
                return;
            }

            List<ThingDef> environmentRocks =
                Find.World.NaturalRockTypesIn(map.Tile)?.Where(rock => rock != null).ToList() ?? [];

            if (environmentRocks.Count == 0)
            {
                report.Skip(scenario, "The current map environment exposes no reconstructible natural rocks.");
                return;
            }

            if (!TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile originalProfile))
            {
                report.Fail(scenario, "Could not inspect NaturalRockMapComponent._profile.");
                return;
            }

            var synthetic = new NaturalRockReconstructionProfile(
                NaturalRockReconstructionProfile.CurrentVersion,
                [environmentRocks[0].defName, MissingRockDefName]);

            try
            {
                if (!TrySetNaturalRockProfile(component, synthetic))
                {
                    report.Fail(scenario, "Could not install the controlled persisted-profile fixture.");
                    return;
                }

                if (!component.TryPrepareResolution(cell, out _))
                {
                    report.Fail(scenario, "Normal component resolution failed instead of repairing the missing slot.");
                    return;
                }

                if (!TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile repaired) ||
                    repaired == null ||
                    repaired.SlotCount != 2 ||
                    repaired.GetRockDefName(0) != environmentRocks[0].defName ||
                    repaired.GetRockDefName(1) != environmentRocks[0].defName)
                {
                    report.Fail(
                        scenario,
                        "The repaired committed profile did not preserve slot structure and reuse the saved valid " +
                        "rock.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Normal component resolution repairs a missing persisted slot from surviving saved rock identity " +
                    "and commits the repaired profile without changing slot count/order structure.");
            }
            finally
            {
                TrySetNaturalRockProfile(component, originalProfile);
            }
        }

        private static void PrepareDigSaveLoadCases(ProbeReport report)
        {
            const string scenario = "Digging save-load setup";
            Map map = Find.CurrentMap;

            if (map == null || !TryResolveUxDefinitions(out UxDefinitions definitions, report))
            {
                return;
            }

            if (_digSaveLoadState != null)
            {
                report.Fail(scenario, "A prepared Dig save-load fixture already exists.");
                return;
            }

            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");

            if (component == null || settings == null || soil == null)
            {
                report.Fail(scenario, "Required component, settings, or Soil Def is unavailable.");
                return;
            }

            Pawn anchor = ResolvePawn(map, requireEmptyCarry: false);

            if (anchor == null || !TryFindClearRockCells(map, anchor.Position, 4, 35, 2, out List<IntVec3> cells))
            {
                report.Fail(scenario, "Could not resolve a pawn and two clear natural-rock fixture cells.");
                return;
            }

            if (!EnsureCommittedNaturalRockProfile(map, cells[0], soil, definitions, component, report))
            {
                return;
            }

            if (!TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile committedProfile) ||
                committedProfile == null)
            {
                report.Fail(scenario, "A committed reconstruction profile is unavailable after setup.");
                return;
            }

            List<string> profileSlots = ReadProfileSlots(committedProfile);
            TerrainDef workOriginal = map.terrainGrid.TerrainAt(cells[1]);
            DigRelocationOrder workOrder = null;
            Pawn workPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(candidate =>
                candidate is { Spawned: true, Dead: false, Downed: false } &&
                RelocationWorkUtility.CanPerform(
                    candidate,
                    RelocationSkillSelection.Mining | RelocationSkillSelection.Construction));

            bool canCaptureProgress = workPawn != null;
            int originalWorkAmount = settings.DigWorkAmount;
            RelocationSkillSelection originalSkills = settings.DiggingSkills;
            float originalXp = settings.RawXpPerTick;

            try
            {
                map.terrainGrid.SetTerrain(cells[1], soil);
                workOrder = SpawnDigOrder(definitions.DigOrderDef, cells[1], map);

                if (canCaptureProgress)
                {
                    settings.DigWorkAmount = DigSaveLoadWorkAmount;
                    settings.DiggingSkills = RelocationSkillSelection.Mining | RelocationSkillSelection.Construction;
                    settings.RawXpPerTick = DigSaveLoadRawXpPerTick;
                }
                else
                {
                    workOrder.SetForbidden(true, warnOnFail: false);
                }

                _digSaveLoadState = new DigSaveLoadProbeState(
                    map,
                    cells[1],
                    definitions.DigOrderDef.defName,
                    workOriginal,
                    profileSlots,
                    canCaptureProgress,
                    originalWorkAmount,
                    originalSkills,
                    originalXp);

                workOrder = null;

                report.Pass(
                    scenario,
                    "Prepared a committed natural-rock profile and a separate Dig work order for save-load.");

                if (canCaptureProgress)
                {
                    report.Info(
                        "Digging save-load / operator steps",
                        "Let a capable colonist start the Dig order at " +
                        cells[1] +
                        ". When its progress bar is " +
                        "between 0% and 100%, pause and run 'Capture digging save-load progress'. That action " +
                        "restores " +
                        "your original settings. Then save, load that save, pause immediately, and run 'Verify " +
                        "digging save-load cases'.");
                }
                else
                {
                    report.Skip(
                        "Digging progress save-load",
                        "No colonist can currently perform Mining+Construction work; only committed-profile " +
                        "persistence " +
                        "is prepared. Keep the game paused, save/load, then run the verifier.");
                }
            }
            catch
            {
                settings.DigWorkAmount = originalWorkAmount;
                settings.DiggingSkills = originalSkills;
                settings.RawXpPerTick = originalXp;
                DestroyUxOrder(workOrder);
                RestoreTerrain(map, cells[1], workOriginal);
                _digSaveLoadState = null;
                throw;
            }
        }

        private static void CaptureDigSaveLoadProgress(ProbeReport report)
        {
            const string scenario = "Digging progress capture";
            Map map = Find.CurrentMap;

            if (_digSaveLoadState == null || !_digSaveLoadState.FixtureMap.Matches(map))
            {
                report.Fail(scenario, "No matching prepared Dig save-load fixture exists on this map.");
                return;
            }

            DigSaveLoadProbeState state = _digSaveLoadState;

            if (!state.CanCaptureProgress)
            {
                RestorePreparedDigSettings();
                report.Skip(scenario, "Progress capture was disabled when the fixture was prepared.");
                return;
            }

            ThingDef orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.SavedDigOrderDefName);
            DigRelocationOrder order = FindDigOrderAt(map, state.WorkCell, orderDef);
            Pawn pawn = FindPawnWorkingOnOrder<JobDriver_DigRelocatableTerrain>(map, order);

            if (order == null || pawn == null)
            {
                report.Skip(
                    scenario,
                    "The prepared Dig job is not in its active work phase. Let a colonist work until the bar is " +
                    "partially filled, pause, and run this action again.");
                return;
            }

            if (!TryReadDigDriverState(pawn.jobs.curDriver, out DigDriverStateSnapshot snapshot) ||
                !IsPartialProgress(snapshot.Progress))
            {
                report.Skip(
                    scenario,
                    "The active driver is not at partial progress yet: " + DescribeProgress(snapshot.Progress) + ".");
                return;
            }

            state.DriverSnapshot = snapshot;
            state.ProgressCaptured = true;
            RestorePreparedDigSettings();

            if (Math.Abs(snapshot.Progress.TotalWork - DigSaveLoadWorkAmount) > 0.001f ||
                snapshot.SkillSelection != (RelocationSkillSelection.Mining | RelocationSkillSelection.Construction) ||
                Math.Abs(snapshot.RawXpPerTick - DigSaveLoadRawXpPerTick) > 0.0001f)
            {
                report.Fail(
                    scenario,
                    "The newly started JobDriver did not snapshot the prepared non-default work/skill/XP settings.");
                return;
            }

            report.Pass(
                scenario,
                "Captured partial JobDriver-local progress with the prepared non-default work/skill/XP snapshot: " +
                DescribeProgress(snapshot.Progress) +
                ".");
            report.Info(
                "Digging progress capture / next",
                "Original Soil Relocation settings have been restored. Keep the game paused, save now, load that " +
                "save, pause immediately, and run 'Verify digging save-load cases'.");
        }

        private static void VerifyDigSaveLoadCases(ProbeReport report)
        {
            const string scenario = "Digging save-load lifecycle";
            Map map = Find.CurrentMap;

            if (_digSaveLoadState == null || map == null)
            {
                report.Fail(scenario, "No prepared Dig save-load state exists in this game process.");
                return;
            }

            DigSaveLoadProbeState state = _digSaveLoadState;

            if (!state.FixtureMap.VerifyReload(map, report, scenario))
            {
                return;
            }

            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();

            if (!TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile profile) ||
                profile == null ||
                !ProfileMatches(profile, state.ProfileSlots))
            {
                report.Fail(
                    scenario,
                    "The committed ordered natural-rock reconstruction profile did not survive save-load.");
            }
            else
            {
                report.Pass(
                    scenario,
                    "The committed ordered natural-rock reconstruction profile survived save-load unchanged.");
            }

            ThingDef orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.SavedDigOrderDefName);
            DigRelocationOrder order = FindDigOrderAt(map, state.WorkCell, orderDef);

            try
            {
                if (!state.CanCaptureProgress)
                {
                    report.Skip("Digging progress save-load", "Progress capture was disabled during fixture setup.");
                    return;
                }

                if (!state.ProgressCaptured)
                {
                    report.Skip(
                        "Digging progress save-load",
                        "No pre-save active-driver snapshot was captured; profile persistence was still verified.");
                    return;
                }

                Pawn pawn = FindPawnWorkingOnOrder<JobDriver_DigRelocatableTerrain>(map, order);

                if (order == null ||
                    pawn == null ||
                    !TryReadDigDriverState(pawn.jobs.curDriver, out DigDriverStateSnapshot loaded))
                {
                    report.Fail(
                        "Digging progress save-load",
                        "The prepared partial Dig order/JobDriver was not restored. Pause immediately after loading.");
                    return;
                }

                DigDriverStateSnapshot before = state.DriverSnapshot;
                bool progressRestored = IsRestoredProgress(before.Progress, loaded.Progress);
                bool snapshotRestored = loaded.SkillSelection == before.SkillSelection &&
                                        Math.Abs(loaded.RawXpPerTick - before.RawXpPerTick) <= 0.0001f;

                if (!progressRestored || !snapshotRestored)
                {
                    report.Fail(
                        "Digging progress save-load",
                        "JobDriver-local work or its skill/XP snapshot did not survive save-load. before=" +
                        DescribeProgress(before.Progress) +
                        ", loaded=" +
                        DescribeProgress(loaded.Progress) +
                        ".");
                    return;
                }

                report.Pass(
                    "Digging progress save-load",
                    "Partial Dig progress and the JobDriver-local skill/XP snapshot survive save-load without moving " +
                    "progress ownership onto the order.");
            }
            finally
            {
                DestroyUxOrder(order);
                RestoreTerrain(map, state.WorkCell, state.WorkOriginalTerrain);
                _digSaveLoadState = null;
            }
        }

        private static bool EnsureCommittedNaturalRockProfile(
            Map map,
            IntVec3 cell,
            TerrainDef soil,
            UxDefinitions definitions,
            NaturalRockMapComponent component,
            ProbeReport report)
        {
            const string scenario = "Digging save-load / committed profile setup";

            if (component.HasProfile)
            {
                return true;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            DigRelocationOrder order = null;
            var spawnedResources = new List<Thing>();

            try
            {
                map.terrainGrid.SetTerrain(cell, soil);
                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                DiggingValidationResult validation = DiggingValidator.Validate(map, cell);

                if (!validation.IsValid ||
                    !DigResultResolver.TryPrepare(map, cell, validation, out DigResultPreparation preparation))
                {
                    report.Fail(scenario, "Could not prepare the first successful Dig used to commit the profile.");
                    return false;
                }

                List<Thing> before = CaptureSpawnedThings(map, preparation.PortableResourceDef);

                if (!DigResultApplier.TryApply(map, cell, preparation))
                {
                    report.Fail(scenario, "Could not apply the first successful Dig used to commit the profile.");
                    return false;
                }

                spawnedResources.AddRange(FindNewSpawnedThings(map, preparation.PortableResourceDef, before));
                order.TryCancel();
                order = null;

                if (!component.HasProfile)
                {
                    report.Fail(scenario, "Successful setup Dig did not commit the natural-rock profile.");
                    return false;
                }

                report.Pass(scenario, "Committed the map's natural-rock profile before preparing save-load.");
                return true;
            }
            finally
            {
                DestroyUxOrder(order);
                DestroyThings(spawnedResources);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static bool TryReadDigDriverState(JobDriver driver, out DigDriverStateSnapshot snapshot)
        {
            snapshot = default(DigDriverStateSnapshot);

            if (!(driver is JobDriver_DigRelocatableTerrain) ||
                !TryReadProgressSnapshot(driver, out WorkProgressSnapshot progress))
            {
                return false;
            }

            Type type = driver.GetType();
            FieldInfo skillField = type.GetField("_skillSelection", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo xpField = type.GetField("_rawXpPerTick", BindingFlags.Instance | BindingFlags.NonPublic);

            if (skillField == null ||
                xpField == null ||
                skillField.FieldType != typeof(RelocationSkillSelection) ||
                xpField.FieldType != typeof(float))
            {
                return false;
            }

            snapshot = new DigDriverStateSnapshot(
                progress,
                (RelocationSkillSelection)skillField.GetValue(driver)!,
                (float)xpField.GetValue(driver)!);
            return true;
        }

        private static void RestorePreparedDigSettings()
        {
            if (_digSaveLoadState == null || _digSaveLoadState.SettingsRestored)
            {
                return;
            }

            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            if (settings == null)
            {
                return;
            }

            settings.DigWorkAmount = _digSaveLoadState.OriginalWorkAmount;
            settings.DiggingSkills = _digSaveLoadState.OriginalSkills;
            settings.RawXpPerTick = _digSaveLoadState.OriginalRawXpPerTick;
            _digSaveLoadState.SettingsRestored = true;
        }

        private static bool TryGetNaturalRockProfile(
            NaturalRockMapComponent component,
            out NaturalRockReconstructionProfile profile)
        {
            profile = null;

            if (component == null)
            {
                return false;
            }

            FieldInfo field = typeof(NaturalRockMapComponent).GetField(
                "_profile",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null || field.FieldType != typeof(NaturalRockReconstructionProfile))
            {
                return false;
            }

            profile = (NaturalRockReconstructionProfile)field.GetValue(component);
            return true;
        }

        private static bool TrySetNaturalRockProfile(
            NaturalRockMapComponent component,
            NaturalRockReconstructionProfile profile)
        {
            if (component == null)
            {
                return false;
            }

            FieldInfo field = typeof(NaturalRockMapComponent).GetField(
                "_profile",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null || field.FieldType != typeof(NaturalRockReconstructionProfile))
            {
                return false;
            }

            field.SetValue(component, profile);
            return true;
        }

        private static TerrainDef ResolveUnsupportedTerrain()
        {
            TerrainDef concrete = DefDatabase<TerrainDef>.GetNamedSilentFail("Concrete");

            if (concrete != null && !RelocationRuleResolver.Resolve(concrete).IsSupported)
            {
                return concrete;
            }

            return DefDatabase<TerrainDef>.AllDefsListForReading.FirstOrDefault(terrain =>
                terrain != null && !RelocationRuleResolver.Resolve(terrain).IsSupported);
        }

        private static List<Thing> CaptureSpawnedThings(Map map, ThingDef def)
        {
            if (map == null || def == null)
            {
                return [];
            }

            return map.listerThings.ThingsOfDef(def)
                .Where(thing => thing is { Spawned: true, Destroyed: false })
                .ToList();
        }

        private static List<Thing> FindNewSpawnedThings(Map map, ThingDef def, IEnumerable<Thing> before)
        {
            var known = new HashSet<Thing>(before ?? []);
            return CaptureSpawnedThings(map, def).Where(thing => !known.Contains(thing)).ToList();
        }

        private static void DestroyThings(IEnumerable<Thing> things)
        {
            if (things == null)
            {
                return;
            }

            foreach (Thing thing in things.ToList())
            {
                DestroyLooseThing(thing);
            }
        }

        private static float ReadSnowDepth(Map map, IntVec3 cell)
        {
            return map?.snowGrid?.GetDepth(cell) ?? 0f;
        }

        private static bool TryFindFilthFreeRockCells(Map map, IntVec3 center, int count, out List<IntVec3> cells)
        {
            cells = [];

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 35, useCenter: true))
            {
                if (!IsClearRockCell(map, center, cell, 4) || cell.GetThingList(map).Any(thing => thing is Filth))
                {
                    continue;
                }

                cells.Add(cell);

                if (cells.Count == count)
                {
                    return true;
                }
            }

            cells.Clear();
            return false;
        }

        private static List<string> ReadProfileSlots(NaturalRockReconstructionProfile profile)
        {
            var slots = new List<string>();

            if (profile == null)
            {
                return slots;
            }

            for (var i = 0; i < profile.SlotCount; i++)
            {
                slots.Add(profile.GetRockDefName(i));
            }

            return slots;
        }

        private static bool ProfileMatches(NaturalRockReconstructionProfile profile, IReadOnlyList<string> slots)
        {
            if (profile == null || slots == null || profile.SlotCount != slots.Count)
            {
                return false;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                if (profile.GetRockDefName(i) != slots[i])
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class DigFamilyCase(string name, string terrainDefName, string resourceDefName)
        {
            public string Name { get; } = name;

            public string TerrainDefName { get; } = terrainDefName;

            public string ResourceDefName { get; } = resourceDefName;
        }

        private readonly struct DigDriverStateSnapshot(
            WorkProgressSnapshot progress,
            RelocationSkillSelection skillSelection,
            float rawXpPerTick)
        {
            public WorkProgressSnapshot Progress { get; } = progress;

            public RelocationSkillSelection SkillSelection { get; } = skillSelection;

            public float RawXpPerTick { get; } = rawXpPerTick;
        }

        private sealed class DigSaveLoadProbeState(
            Map map,
            IntVec3 workCell,
            string digOrderDefName,
            TerrainDef workOriginalTerrain,
            IReadOnlyList<string> profileSlots,
            bool canCaptureProgress,
            int originalWorkAmount,
            RelocationSkillSelection originalSkills,
            float originalRawXpPerTick)
        {
            public SaveLoadProbeMap FixtureMap { get; } = new(map);

            public IntVec3 WorkCell { get; } = workCell;

            public string SavedDigOrderDefName { get; } = digOrderDefName;

            public TerrainDef WorkOriginalTerrain { get; } = workOriginalTerrain;

            public IReadOnlyList<string> ProfileSlots { get; } = profileSlots;

            public bool CanCaptureProgress { get; } = canCaptureProgress;

            public int OriginalWorkAmount { get; } = originalWorkAmount;

            public RelocationSkillSelection OriginalSkills { get; } = originalSkills;

            public float OriginalRawXpPerTick { get; } = originalRawXpPerTick;

            public bool SettingsRestored { get; set; }

            public bool ProgressCaptured { get; set; }

            public DigDriverStateSnapshot DriverSnapshot { get; set; }
        }
    }
}
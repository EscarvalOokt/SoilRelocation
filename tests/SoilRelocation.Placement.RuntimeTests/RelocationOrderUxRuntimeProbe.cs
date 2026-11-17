using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using RimWorld;
using SoilRelocation.Digging;
using SoilRelocation.Domain;
using SoilRelocation.Relocation;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        private const string DigOrderDefName = "SoilRelocation_DigOrder";
        private const string DigWorkGiverDefName = "SoilRelocation_DigTerrain";
        private const string DigWorldGraphicPath = "Designations/ExtractTree";
        private const string PlaceWorldGraphicPath = "Things/Special/TerrainBlueprint";
        private const int PlaceWorldGraphicRenderQueue = 2950;
        private const int UxFixtureMaximumRadius = 45;

        private static SelectionUxProbeState _uxSelectionState;
        private static GroupedControlProbeState _uxGroupedControlState;
        private static UxSaveLoadProbeState _uxSaveLoadState;

        [DebugAction(
            "Soil Relocation",
            "Validate relocation order UX contracts",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ValidateRelocationOrderUxContracts()
        {
            var report = new ProbeReport();

            try
            {
                RunRelocationOrderUxContracts(report);
            }
            catch (Exception exception)
            {
                report.Fail("UX contract probe", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation-order UX contract probe", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Prepare relocation order UX fixture",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareRelocationOrderUxFixture()
        {
            var report = new ProbeReport();

            try
            {
                PrepareSelectionUxFixture(report);
            }
            catch (Exception exception)
            {
                report.Fail("Selection fixture setup", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation-order selection fixture", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Verify relocation order selection",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyRelocationOrderSelection()
        {
            var report = new ProbeReport();

            try
            {
                VerifySelectionUxFixture(report);
            }
            catch (Exception exception)
            {
                report.Fail("Selection verification", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation-order selection verification", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Prepare relocation grouped controls",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareRelocationGroupedControls()
        {
            var report = new ProbeReport();

            try
            {
                PrepareGroupedControlFixture(report);
            }
            catch (Exception exception)
            {
                report.Fail("Grouped-control setup", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation grouped-controls fixture", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Verify relocation grouped controls",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyRelocationGroupedControls()
        {
            var report = new ProbeReport();

            try
            {
                VerifyGroupedControlFixture(report);
            }
            catch (Exception exception)
            {
                report.Fail("Grouped-control verification", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation grouped-controls verification", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Prepare relocation UX save-load",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareRelocationUxSaveLoad()
        {
            var report = new ProbeReport();

            try
            {
                PrepareUxSaveLoadFixture(report);
            }
            catch (Exception exception)
            {
                report.Fail("UX save-load setup", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation UX save-load setup", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Capture relocation UX progress",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void CaptureRelocationUxProgress()
        {
            var report = new ProbeReport();

            try
            {
                CaptureUxProgress(report);
            }
            catch (Exception exception)
            {
                report.Fail("Progress capture", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation UX progress capture", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Verify relocation UX save-load",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyRelocationUxSaveLoad()
        {
            var report = new ProbeReport();

            try
            {
                VerifyUxSaveLoadFixture(report);
            }
            catch (Exception exception)
            {
                report.Fail("UX save-load verification", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation UX save-load verification", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Cleanup relocation order UX fixtures",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void CleanupRelocationOrderUxFixtures()
        {
            var report = new ProbeReport();

            try
            {
                CleanupSelectionUxState();
                CleanupGroupedControlState();
                CleanupUxSaveLoadState(Find.CurrentMap);
                Find.Selector?.ClearSelection();

                if (_uxSaveLoadState == null)
                {
                    report.Pass("Cleanup", "Removed all prepared relocation-order UX fixtures owned by this probe.");
                }
                else
                {
                    report.Skip(
                        "Cleanup / save-load",
                        "The UX save-load fixture belongs to another map. Switch to the prepared map and run cleanup " +
                        "again.");
                }
            }
            catch (Exception exception)
            {
                report.Fail("Cleanup", "Unhandled exception: " + exception);
            }

            WriteReport("Relocation-order UX cleanup", report);
        }

        private static void RunRelocationOrderUxContracts(ProbeReport report)
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

            ProbeOrderDefinitionContracts(definitions, report);
            ProbeWorldGraphicContracts(definitions, report);
            ProbePlaceDesignatorVisualContracts(report);
            ProbeOrderSchedulingContracts(map, definitions, report);
            report.Info(
                "Progress presentation / manual",
                "Use the existing 'Prepare full placement lifecycle' runtime fixture as the delivery-to-work smoke " +
                "path: no relocation-work progress bar should appear during HaulToContainer delivery, and the bar " +
                "should appear only after the order reaches actual Place work.");
        }

        private static bool TryResolveUxDefinitions(out UxDefinitions definitions, ProbeReport report)
        {
            ThingDef digOrderDef = DefDatabase<ThingDef>.GetNamedSilentFail(DigOrderDefName);
            WorkGiverDef digWorkGiverDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail(DigWorkGiverDefName);
            WorkGiverDef placeWorkGiverDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail(ActualWorkGiverDefName);
            WorkGiverDef deliveryWorkGiverDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail(DeliveryWorkGiverDefName);

            FamilyCase soilCase = ResolveFamilyCase("Soil");

            if (digOrderDef == null ||
                digWorkGiverDef == null ||
                placeWorkGiverDef == null ||
                deliveryWorkGiverDef == null ||
                soilCase == null ||
                !soilCase.TryResolve(out ThingDef soilOrderDef, out ThingDef soilResourceDef))
            {
                definitions = null;
                report.Fail("Environment", "Required Dig/Place order, resource, or WorkGiver Defs are unavailable.");
                return false;
            }

            var digWorker = digWorkGiverDef.Worker as WorkGiver_DigRelocatableTerrain;
            var placeWorker = placeWorkGiverDef.Worker as WorkGiver_PlaceRelocatableTerrain;
            var deliveryWorker = deliveryWorkGiverDef.Worker as WorkGiver_DeliverRelocationMaterial;

            if (digWorker == null || placeWorker == null || deliveryWorker == null)
            {
                definitions = null;
                report.Fail(
                    "Environment",
                    "One or more relocation WorkGiverDefs did not instantiate their expected production workers.");
                return false;
            }

            definitions = new UxDefinitions(
                digOrderDef,
                soilOrderDef,
                soilResourceDef,
                digWorker,
                placeWorker,
                deliveryWorker);
            return true;
        }

        private static void ProbeOrderDefinitionContracts(UxDefinitions definitions, ProbeReport report)
        {
            const string sharedScenario = "Order identity / shared base";
            var orderDefs = new List<ThingDef> { definitions.DigOrderDef };

            foreach (FamilyCase familyCase in _familyCases)
            {
                if (!familyCase.TryResolve(out ThingDef orderDef, out _))
                {
                    report.Fail("Order identity / " + familyCase.Name, "Expected Place order Def is unavailable.");
                    continue;
                }

                orderDefs.Add(orderDef);
            }

            bool exactDefsDistinct = orderDefs.Count == 5 && orderDefs.Distinct().Count() == 5;

            if (exactDefsDistinct)
            {
                report.Pass(
                    sharedScenario,
                    "Dig and all four family-specific Place orders retain distinct ThingDef identity.");
            }
            else
            {
                report.Fail(sharedScenario, "Expected five distinct Dig/Place order ThingDefs.");
            }

            ProbeSingleOrderDefinition(
                "Order identity / Dig",
                definitions.DigOrderDef,
                typeof(DigRelocationOrder),
                report);

            foreach (FamilyCase familyCase in _familyCases)
            {
                if (!familyCase.TryResolve(out ThingDef orderDef, out _))
                {
                    continue;
                }

                ProbeSingleOrderDefinition(
                    "Order identity / " + familyCase.Name,
                    orderDef,
                    typeof(RelocationMaterialOrder),
                    report);
            }
        }

        private static void ProbeSingleOrderDefinition(
            string scenario,
            ThingDef orderDef,
            Type expectedConcreteType,
            ProbeReport report)
        {
            Thing order = null;

            try
            {
                order = ThingMaker.MakeThing(orderDef);

                bool concreteTypeValid = order != null && order.GetType() == expectedConcreteType;
                bool sharedTypeValid = order is RelocationOrder;
                bool forbiddableValid = order?.TryGetComp<CompForbiddable>() != null;
                bool directInfoCardIdentityValid = !(order is IConstructible) && orderDef.entityDefToBuild == null;
                List<Gizmo> gizmos = order?.GetGizmos().ToList() ?? [];
                bool forbidGizmoValid = gizmos.OfType<Command_Toggle>().Any();
                // ReSharper disable once SimplifyLinqExpressionUseAll
                bool customCancelGizmoAbsent = !gizmos.OfType<Command_Action>()
                    .Any(command => command.defaultLabel == "DesignatorCancel".Translate());

                if (!concreteTypeValid ||
                    !sharedTypeValid ||
                    !forbiddableValid ||
                    !directInfoCardIdentityValid ||
                    !forbidGizmoValid ||
                    !customCancelGizmoAbsent)
                {
                    report.Fail(
                        scenario,
                        "concreteType=" +
                        (order?.GetType().FullName ?? "<null>") +
                        ", sharedRelocationOrder=" +
                        sharedTypeValid +
                        ", CompForbiddable=" +
                        forbiddableValid +
                        ", forbidGizmo=" +
                        forbidGizmoValid +
                        ", customCancelGizmoAbsent=" +
                        customCancelGizmoAbsent +
                        ", directThingInfoCard=" +
                        directInfoCardIdentityValid +
                        ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Creates exact " +
                    expectedConcreteType.Name +
                    " with vanilla Allow/Forbid, no project-owned Cancel gizmo, and direct Thing InfoCard identity.");
            }
            finally
            {
                if (order is { Destroyed: false })
                {
                    order.Destroy();
                }
            }
        }

        private static void ProbeWorldGraphicContracts(UxDefinitions definitions, ProbeReport report)
        {
            const string placeScenario = "Visual identity / Place world graphic";
            GraphicData referenceGraphic = definitions.SoilOrderDef.graphicData;

            if (!IsExpectedPlaceWorldGraphic(referenceGraphic))
            {
                report.Fail(
                    placeScenario,
                    "The reference Soil Place order does not use the expected terrain-blueprint graphic.");
                return;
            }

            foreach (FamilyCase familyCase in _familyCases)
            {
                if (!familyCase.TryResolve(out ThingDef orderDef, out _))
                {
                    report.Fail(placeScenario, familyCase.Name + " order Def is unavailable.");
                    return;
                }

                if (!HasSameWorldGraphic(referenceGraphic, orderDef.graphicData) ||
                    !IsExpectedPlaceWorldGraphic(orderDef.graphicData))
                {
                    report.Fail(
                        placeScenario,
                        familyCase.Name + " does not share the exact Place terrain-blueprint graphic metadata.");
                    return;
                }
            }

            report.Pass(
                placeScenario,
                "All four family-specific Place orders share one terrain-blueprint world graphic while retaining " +
                "exact " +
                "Def identity.");

            const string digScenario = "Visual identity / Dig world graphic";
            GraphicData digGraphic = definitions.DigOrderDef.graphicData;
            bool digValid = digGraphic is { texPath: DigWorldGraphicPath } &&
                            referenceGraphic != null &&
                            digGraphic.texPath != referenceGraphic.texPath;

            if (digValid)
            {
                report.Pass(digScenario, "Dig retains its separate ExtractTree world-visual identity.");
            }
            else
            {
                report.Fail(
                    digScenario,
                    "Dig graphic path was " +
                    (digGraphic?.texPath ?? "<null>") +
                    ", expected " +
                    DigWorldGraphicPath +
                    " and a value distinct from Place.");
            }
        }

        private static bool IsExpectedPlaceWorldGraphic(GraphicData graphicData)
        {
            return graphicData is { texPath: PlaceWorldGraphicPath } &&
                   graphicData.graphicClass == typeof(Graphic_Single) &&
                   graphicData.shaderType is { defName: "MetaOverlay" } &&
                   graphicData.renderQueue == PlaceWorldGraphicRenderQueue;
        }

        private static bool HasSameWorldGraphic(GraphicData first, GraphicData second)
        {
            return first != null &&
                   second != null &&
                   first.texPath == second.texPath &&
                   first.graphicClass == second.graphicClass &&
                   first.shaderType == second.shaderType &&
                   first.renderQueue == second.renderQueue;
        }

        private static void ProbePlaceDesignatorVisualContracts(ProbeReport report)
        {
            foreach (FamilyCase familyCase in _familyCases)
            {
                string scenario = "Visual identity / designator / " + familyCase.Name;

                if (!familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef) ||
                    !RelocationMaterialOrderUtility.TryResolveFamily(orderDef, out RelocationFamilyDef family) ||
                    !ReferenceEquals(family.CanonicalPortableResource, resourceDef))
                {
                    report.Fail(scenario, "Could not resolve the production family/resource identity.");
                    continue;
                }

                GraphicData graphicData = family.CanonicalPortableResource?.graphicData;

                if (graphicData == null || graphicData.texPath.NullOrEmpty())
                {
                    report.Fail(scenario, "Canonical portable resource has no usable GraphicData.");
                    continue;
                }

                Designator_PlaceRelocatableTerrain designator = familyCase.CreateDesignator();
                Texture2D expectedIcon = ContentFinder<Texture2D>.Get(graphicData.texPath);
                bool iconValid = ReferenceEquals(designator.icon, expectedIcon);
                bool tintValid = designator.IconDrawColor == graphicData.color;

                if (!iconValid || !tintValid)
                {
                    report.Fail(
                        scenario,
                        "iconMatch=" +
                        iconValid +
                        ", tintMatch=" +
                        tintValid +
                        ", resourceTexPath=" +
                        graphicData.texPath +
                        ".");
                    continue;
                }

                report.Pass(
                    scenario,
                    "Place designator icon and tint are sourced from canonical portable-resource GraphicData.");
            }
        }

        private static void ProbeOrderSchedulingContracts(Map map, UxDefinitions definitions, ProbeReport report)
        {
            Pawn anchorPawn = ResolvePawn(map, requireEmptyCarry: false);

            if (anchorPawn == null)
            {
                report.Skip("Scheduling", "No spawned free colonist is available for WorkGiver acceptance checks.");
                return;
            }

            ProbeDigForbidScheduling(map, anchorPawn, definitions, report);
            ProbeDeliveryForbidScheduling(map, anchorPawn, definitions, report);
            ProbePlaceForbidScheduling(map, anchorPawn, definitions, report);
        }

        private static void ProbeDigForbidScheduling(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string scenario = "Scheduling / Dig Allow-Forbid";

            if (!TryFindClearDigCell(map, anchorPawn.Position, 4, UxFixtureMaximumRadius, out IntVec3 cell))
            {
                report.Skip(scenario, "Could not find a clear production-valid Dig cell on the current map.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            DigRelocationOrder order = null;

            try
            {
                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                Pawn pawn = FindPawnForDigOrder(map, definitions.DigWorker, order);

                if (pawn == null)
                {
                    report.Skip(scenario, "No free colonist can currently schedule the prepared Dig order.");
                    return;
                }

                Job allowedJob = definitions.DigWorker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedDigJob(allowedJob, order))
                {
                    report.Fail(
                        scenario,
                        "Allowed Dig order did not produce the expected job: " + DescribeJob(allowedJob) + ".");
                    return;
                }

                order.SetForbidden(true, warnOnFail: false);
                Job forbiddenNormal = definitions.DigWorker.JobOnThing(pawn, order, forced: false);
                Job forbiddenForced = definitions.DigWorker.JobOnThing(pawn, order, forced: true);

                if (forbiddenNormal != null || forbiddenForced != null)
                {
                    report.Fail(scenario, "Forbidden Dig order produced a new job, including forced scheduling.");
                    return;
                }

                order.SetForbidden(false, warnOnFail: false);
                Job allowedAgain = definitions.DigWorker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedDigJob(allowedAgain, order))
                {
                    report.Fail(scenario, "Allowed Dig order did not become schedulable again.");
                    return;
                }

                report.Pass(scenario, "Forbid blocks new normal/forced Dig jobs and Allow restores scheduling.");
            }
            finally
            {
                DestroyUxOrder(order);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeDeliveryForbidScheduling(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string scenario = "Scheduling / Place delivery Allow-Forbid";

            if (!TryFindClearRockCell(map, anchorPawn.Position, 4, UxFixtureMaximumRadius, out IntVec3 cell))
            {
                report.Skip(scenario, "Could not find a clear natural-rock Place destination.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                map,
                definitions.SoilResourceDef,
                anchorPawn);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(definitions.SoilOrderDef, cell, map, tracked);
                Thing resource = SpawnResource(definitions.SoilResourceDef, anchorPawn.Position, map, tracked);
                resource.SetForbidden(false, warnOnFail: false);

                Pawn pawn = FindPawnForDeliveryOrder(map, definitions.DeliveryWorker, order);

                if (pawn == null)
                {
                    report.Skip(
                        scenario,
                        "No free colonist can currently schedule delivery to the prepared Place order.");
                    return;
                }

                Job allowedJob = definitions.DeliveryWorker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedDeliveryJobForOrder(allowedJob, order))
                {
                    report.Fail(
                        scenario,
                        "Allowed awaiting Place order did not produce HaulToContainer: " +
                        DescribeJob(allowedJob) +
                        ".");
                    return;
                }

                order.SetForbidden(true, warnOnFail: false);
                Job forbiddenNormal = definitions.DeliveryWorker.JobOnThing(pawn, order, forced: false);
                Job forbiddenForced = definitions.DeliveryWorker.JobOnThing(pawn, order, forced: true);

                if (forbiddenNormal != null || forbiddenForced != null)
                {
                    report.Fail(scenario, "Forbidden awaiting Place order produced a new delivery job.");
                    return;
                }

                order.SetForbidden(false, warnOnFail: false);
                Job allowedAgain = definitions.DeliveryWorker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedDeliveryJobForOrder(allowedAgain, order))
                {
                    report.Fail(scenario, "Allow did not restore Place material-delivery scheduling.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Forbid blocks new normal/forced HaulToContainer delivery and Allow restores delivery scheduling.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbePlaceForbidScheduling(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string scenario = "Scheduling / Place work Allow-Forbid";

            if (!TryFindClearRockCell(map, anchorPawn.Position, 4, UxFixtureMaximumRadius, out IntVec3 cell))
            {
                report.Skip(scenario, "Could not find a clear natural-rock Place destination.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(
                    definitions.SoilOrderDef,
                    definitions.SoilResourceDef,
                    cell,
                    map,
                    tracked);

                Pawn pawn = FindPawnForPlaceOrder(map, definitions.PlaceWorker, order);

                if (pawn == null)
                {
                    report.Skip(scenario, "No free colonist can currently schedule actual Place work.");
                    return;
                }

                Job allowedJob = definitions.PlaceWorker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedWorkJob(allowedJob, order))
                {
                    report.Fail(
                        scenario,
                        "Allowed ready Place order did not produce expected work: " + DescribeJob(allowedJob) + ".");
                    return;
                }

                order.SetForbidden(true, warnOnFail: false);
                Job forbiddenNormal = definitions.PlaceWorker.JobOnThing(pawn, order, forced: false);
                Job forbiddenForced = definitions.PlaceWorker.JobOnThing(pawn, order, forced: true);

                if (forbiddenNormal != null || forbiddenForced != null)
                {
                    report.Fail(scenario, "Forbidden ready Place order produced a new actual-work job.");
                    return;
                }

                order.SetForbidden(false, warnOnFail: false);
                Job allowedAgain = definitions.PlaceWorker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedWorkJob(allowedAgain, order))
                {
                    report.Fail(scenario, "Allow did not restore actual Place work scheduling.");
                    return;
                }

                report.Pass(scenario, "Forbid blocks new normal/forced Place work and Allow restores scheduling.");
            }
            finally
            {
                DestroyTracked(tracked);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void PrepareSelectionUxFixture(ProbeReport report)
        {
            const string scenario = "Selection fixture setup";
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail(scenario, "No current map is available.");
                return;
            }

            if (_uxSelectionState != null)
            {
                report.Fail(scenario, "A selection fixture is already prepared. Clean it up before creating another.");
                return;
            }

            Pawn pawn = ResolvePawn(map, requireEmptyCarry: false);

            if (pawn == null ||
                !TryFindClearDigCells(map, pawn.Position, 4, UxFixtureMaximumRadius, 2, out List<IntVec3> digCells) ||
                !TryFindClearRockCells(map, pawn.Position, 4, UxFixtureMaximumRadius, 5, out List<IntVec3> placeCells))
            {
                report.Fail(scenario, "Could not resolve a colonist, two Dig cells, and five Place cells.");
                return;
            }

            ThingDef digDef = DefDatabase<ThingDef>.GetNamedSilentFail(DigOrderDefName);

            if (digDef == null)
            {
                report.Fail(scenario, "Dig order Def is unavailable.");
                return;
            }

            var orders = new List<RelocationOrder>();
            var originalTerrains = new List<CellTerrainState>();

            try
            {
                for (var i = 0; i < digCells.Count; i++)
                {
                    originalTerrains.Add(new CellTerrainState(digCells[i], map.terrainGrid.TerrainAt(digCells[i])));
                    DigRelocationOrder order = SpawnDigOrder(digDef, digCells[i], map);
                    order.SetForbidden(true, warnOnFail: false);
                    orders.Add(order);
                }

                FamilyCase[] placeCases =
                [
                    ResolveFamilyCase("Sand"),
                    ResolveFamilyCase("Sand"),
                    ResolveFamilyCase("Gravel"),
                    ResolveFamilyCase("Soil"),
                    ResolveFamilyCase("Rich Soil")
                ];

                for (var i = 0; i < placeCases.Length; i++)
                {
                    FamilyCase familyCase = placeCases[i];

                    if (familyCase == null || !familyCase.TryResolve(out ThingDef orderDef, out _))
                    {
                        throw new InvalidOperationException(
                            "Could not resolve a Place family for the selection fixture.");
                    }

                    originalTerrains.Add(new CellTerrainState(placeCells[i], map.terrainGrid.TerrainAt(placeCells[i])));
                    RelocationMaterialOrder order = SpawnOrder(orderDef, placeCells[i], map, null);
                    order.SetForbidden(true, warnOnFail: false);
                    orders.Add(order);
                }

                _uxSelectionState = new SelectionUxProbeState(map, orders, originalTerrains);

                report.Pass(
                    scenario,
                    "Prepared 2 Dig, 2 Sand, 1 Gravel, 1 Soil, and 1 Rich Soil order; all are forbidden to keep the " +
                    "fixture stable.");
                report.Info("Selection fixture / positions", DescribeOrderPositions(orders));
                report.Info(
                    "Selection fixture / operator steps",
                    "Keep the game paused. Select Dig, Sand, Gravel, Soil, and Rich Soil orders one at a time and " +
                    "verify the vanilla Inspect/InfoCard shows each exact order identity. Double-click a Dig and run " +
                    "'Verify relocation order selection'; double-click a Sand Place order and verify again; then " +
                    "vanilla multi-select all five Place orders and verify. Finally add/select both Dig orders so all " +
                    "seven fixture orders are selected and verify once more. Visually confirm Dig uses its separate " +
                    "marker and all Place families share the terrain-blueprint world graphic.");
            }
            catch
            {
                DestroyUxOrders(orders);
                RestoreCellTerrains(map, originalTerrains);
                _uxSelectionState = null;
                throw;
            }
        }

        private static void VerifySelectionUxFixture(ProbeReport report)
        {
            const string scenario = "Selection verification";

            if (_uxSelectionState == null)
            {
                report.Fail(scenario, "No relocation-order selection fixture is prepared.");
                return;
            }

            SelectionUxProbeState state = _uxSelectionState;

            if (Find.CurrentMap != state.Map)
            {
                report.Fail(scenario, "The current map is not the map containing the prepared selection fixture.");
                return;
            }

            List<object> selectedObjects = Find.Selector?.SelectedObjectsListForReading;

            if (selectedObjects == null || selectedObjects.Count == 0)
            {
                report.Fail(scenario, "Nothing is currently selected.");
                return;
            }

            var selectedOrders = selectedObjects.OfType<RelocationOrder>().ToList();
            bool containsForeignSelection = selectedOrders.Count != selectedObjects.Count ||
                                            selectedOrders.Any(order => !state.Orders.Contains(order));

            if (containsForeignSelection)
            {
                report.Fail(
                    scenario,
                    "Selection contains non-fixture objects: " + DescribeSelectedObjects(selectedObjects) + ".");
                return;
            }

            var digOrders = state.Orders.Where(order => order is DigRelocationOrder).ToList();
            var placeOrders = state.Orders.Where(order => order is RelocationMaterialOrder).ToList();
            var sandOrders = state.Orders.Where(order =>
                    order is RelocationMaterialOrder && order.def.defName == "SoilRelocation_PlaceSandOrder")
                .ToList();

            if (selectedOrders.Count == 1)
            {
                RelocationOrder selected = selectedOrders[0];
                bool implementsConstructible = typeof(IConstructible).IsAssignableFrom(selected.GetType());
                bool directIdentity = !implementsConstructible && selected.def.entityDefToBuild == null;

                if (!directIdentity)
                {
                    report.Fail(scenario, "Single selected order does not retain direct Thing InfoCard identity.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Single selection is exact " + selected.def.defName + " (label='" + selected.LabelCap + "').");
                report.Info(
                    "Inspect smoke test",
                    "Open the vanilla Inspect/InfoCard for this selected Thing and confirm it displays this exact " +
                    "order " +
                    "identity.");
                return;
            }

            if (SameReferenceSet(selectedOrders, digOrders))
            {
                report.Pass(
                    scenario,
                    "Real selection contains exactly the two Dig ThingDefs and no Place order; Dig double-click " +
                    "matching " +
                    "is isolated.");
                return;
            }

            if (SameReferenceSet(selectedOrders, sandOrders))
            {
                report.Pass(
                    scenario,
                    "Real selection contains exactly the two Sand Place ThingDefs; other Place families and Dig are " +
                    "excluded.");
                return;
            }

            if (SameReferenceSet(selectedOrders, placeOrders))
            {
                report.Pass(
                    scenario,
                    "Real vanilla multi-selection contains all five Place fixture orders across the four exact " +
                    "family-specific ThingDefs.");
                report.Info(
                    "Selection verification / next",
                    "For the mixed Dig/Place selection check, add/select both Dig fixture orders so all seven orders " +
                    "are selected, then run the verifier again.");
                return;
            }

            if (SameReferenceSet(selectedOrders, state.Orders))
            {
                report.Pass(
                    scenario,
                    "Real vanilla multi-selection contains all seven mixed Dig/Place fixture orders.");
                return;
            }

            report.Fail(
                scenario,
                "Selection did not match an expected fixture state. Selected=" +
                DescribeSelectedObjects(selectedObjects) +
                ".");
        }

        private static void PrepareGroupedControlFixture(ProbeReport report)
        {
            const string scenario = "Grouped-control setup";
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail(scenario, "No current map is available.");
                return;
            }

            if (_uxGroupedControlState != null)
            {
                report.Fail(scenario, "A grouped-control fixture is already prepared.");
                return;
            }

            if (!TryResolveUxDefinitions(out UxDefinitions definitions, report))
            {
                return;
            }

            Pawn anchorPawn = ResolvePawn(map, requireEmptyCarry: false);

            if (anchorPawn == null ||
                !TryFindClearDigCell(map, anchorPawn.Position, 4, UxFixtureMaximumRadius, out IntVec3 digCell) ||
                !TryFindSeparatedRockCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    2,
                    7,
                    out List<IntVec3> placeCells))
            {
                report.Fail(scenario, "Could not resolve one Dig cell and two separated Place cells.");
                return;
            }

            TerrainDef digOriginalTerrain = map.terrainGrid.TerrainAt(digCell);
            TerrainDef awaitingOriginalTerrain = map.terrainGrid.TerrainAt(placeCells[0]);
            TerrainDef readyOriginalTerrain = map.terrainGrid.TerrainAt(placeCells[1]);
            DigRelocationOrder digOrder = null;
            RelocationMaterialOrder awaitingOrder = null;
            RelocationMaterialOrder readyOrder = null;
            Thing awaitingResource = null;
            Thing readyResource = null;
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                map,
                definitions.SoilResourceDef,
                anchorPawn);

            try
            {
                digOrder = SpawnDigOrder(definitions.DigOrderDef, digCell, map);
                awaitingOrder = SpawnOrder(definitions.SoilOrderDef, placeCells[0], map, null);
                readyOrder = SpawnOrder(definitions.SoilOrderDef, placeCells[1], map, null);

                readyResource = ThingMaker.MakeThing(definitions.SoilResourceDef);
                readyResource.stackCount = 1;

                if (!readyOrder.GetDirectlyHeldThings().TryAdd(readyResource))
                {
                    throw new InvalidOperationException("Ready Place order rejected its canonical fixture resource.");
                }

                awaitingResource = ThingMaker.MakeThing(definitions.SoilResourceDef);
                awaitingResource.stackCount = 1;
                GenSpawn.Spawn(awaitingResource, anchorPawn.Position, map);
                awaitingResource.SetForbidden(false, warnOnFail: false);

                Pawn digPawn = FindPawnForDigOrder(map, definitions.DigWorker, digOrder);
                Pawn deliveryPawn = FindPawnForDeliveryOrder(map, definitions.DeliveryWorker, awaitingOrder);
                Pawn placePawn = FindPawnForPlaceOrder(map, definitions.PlaceWorker, readyOrder);

                if (digPawn == null || deliveryPawn == null || placePawn == null)
                {
                    report.Fail(
                        scenario,
                        "Prepared orders are not all schedulable while allowed; the current colony cannot validate " +
                        "grouped scheduling.");
                    return;
                }

                _uxGroupedControlState = new GroupedControlProbeState(
                    map,
                    definitions,
                    digOrder,
                    awaitingOrder,
                    readyOrder,
                    awaitingResource,
                    readyResource,
                    digPawn,
                    deliveryPawn,
                    placePawn,
                    digCell,
                    placeCells[0],
                    placeCells[1],
                    digOriginalTerrain,
                    awaitingOriginalTerrain,
                    readyOriginalTerrain,
                    resourceStates);

                resourceStates = null;

                report.Pass(
                    scenario,
                    "Prepared one Dig, one awaiting Place, and one ready Place order with live scheduling baselines.");
                report.Info(
                    "Grouped-control / positions",
                    "Dig=" + digCell + "; awaiting Place=" + placeCells[0] + "; ready Place=" + placeCells[1] + ".");
                report.Info(
                    "Grouped-control / operator steps",
                    "Keep the game paused. Vanilla multi-select the three fixture orders, click the grouped Forbid " +
                    "command, then run 'Verify relocation grouped controls'. Next click grouped Allow and verify " +
                    "again. Finally keep all three selected, confirm the native reverse Cancel gizmo is present, " +
                    "click it, and verify a third time.");
            }
            catch
            {
                _uxGroupedControlState = null;
                throw;
            }
            finally
            {
                if (_uxGroupedControlState == null)
                {
                    RestoreResourceStates(resourceStates);
                    DestroyUxOrder(digOrder);
                    DestroyOrderAndContents(awaitingOrder);
                    DestroyOrderAndContents(readyOrder);
                    DestroyLooseThing(awaitingResource);
                    DestroyLooseThing(readyResource);
                    RestoreTerrain(map, digCell, digOriginalTerrain);
                    RestoreTerrain(map, placeCells[0], awaitingOriginalTerrain);
                    RestoreTerrain(map, placeCells[1], readyOriginalTerrain);
                }
            }
        }

        private static void VerifyGroupedControlFixture(ProbeReport report)
        {
            const string scenario = "Grouped-control verification";

            if (_uxGroupedControlState == null)
            {
                report.Fail(scenario, "No grouped-control fixture is prepared.");
                return;
            }

            GroupedControlProbeState state = _uxGroupedControlState;

            if (Find.CurrentMap != state.Map)
            {
                report.Fail(scenario, "The current map is not the map containing the grouped-control fixture.");
                return;
            }

            switch (state.Phase)
            {
                case GroupedControlPhase.AwaitingForbid:
                    VerifyGroupedForbid(state, report);
                    break;

                case GroupedControlPhase.AwaitingAllow:
                    VerifyGroupedAllow(state, report);
                    break;

                case GroupedControlPhase.AwaitingCancel:
                    VerifyGroupedCancel(state, report);
                    break;

                default:
                    report.Fail(scenario, "Unknown grouped-control phase.");
                    break;
            }
        }

        private static void VerifyGroupedForbid(GroupedControlProbeState state, ProbeReport report)
        {
            const string scenario = "Grouped controls / Forbid";

            if (!AllOrdersAlive(state) || !AllOrdersForbidden(state))
            {
                report.Fail(
                    scenario,
                    "All three prepared orders must still exist and be forbidden after the grouped command.");
                return;
            }

            Job digNormal = state.Definitions.DigWorker.JobOnThing(state.DigPawn, state.DigOrder, forced: false);
            Job digForced = state.Definitions.DigWorker.JobOnThing(state.DigPawn, state.DigOrder, forced: true);
            Job deliveryNormal = state.Definitions.DeliveryWorker.JobOnThing(
                state.DeliveryPawn,
                state.AwaitingOrder,
                forced: false);
            Job deliveryForced = state.Definitions.DeliveryWorker.JobOnThing(
                state.DeliveryPawn,
                state.AwaitingOrder,
                forced: true);
            Job placeNormal = state.Definitions.PlaceWorker.JobOnThing(
                state.PlacePawn,
                state.ReadyOrder,
                forced: false);
            Job placeForced = state.Definitions.PlaceWorker.JobOnThing(state.PlacePawn, state.ReadyOrder, forced: true);

            if (digNormal != null ||
                digForced != null ||
                deliveryNormal != null ||
                deliveryForced != null ||
                placeNormal != null ||
                placeForced != null)
            {
                report.Fail(scenario, "At least one forbidden fixture order remained schedulable.");
                return;
            }

            if (!GroupedFixtureStateUnchanged(state, requireReadyMaterialContained: true))
            {
                report.Fail(scenario, "Forbid changed terrain or material accounting in the prepared fixture.");
                return;
            }

            state.Phase = GroupedControlPhase.AwaitingAllow;
            report.Pass(
                scenario,
                "Grouped Forbid reached Dig, awaiting Place, and ready Place; all new normal/forced scheduling is " +
                "blocked.");
            report.Info(
                "Grouped controls / next",
                "With the same three orders selected, click grouped Allow and run the verifier again.");
        }

        private static void VerifyGroupedAllow(GroupedControlProbeState state, ProbeReport report)
        {
            const string scenario = "Grouped controls / Allow";

            if (!AllOrdersAlive(state) ||
                AllOrdersForbidden(state) ||
                state.DigOrder.IsForbidden(state.DigPawn) ||
                state.AwaitingOrder.IsForbidden(state.DeliveryPawn) ||
                state.ReadyOrder.IsForbidden(state.PlacePawn))
            {
                report.Fail(
                    scenario,
                    "All three prepared orders must still exist and be allowed after the grouped command.");
                return;
            }

            Job digJob = state.Definitions.DigWorker.JobOnThing(state.DigPawn, state.DigOrder, forced: true);
            Job deliveryJob = state.Definitions.DeliveryWorker.JobOnThing(
                state.DeliveryPawn,
                state.AwaitingOrder,
                forced: true);
            Job placeJob = state.Definitions.PlaceWorker.JobOnThing(state.PlacePawn, state.ReadyOrder, forced: true);

            if (!IsExpectedDigJob(digJob, state.DigOrder) ||
                !IsExpectedDeliveryJobForOrder(deliveryJob, state.AwaitingOrder) ||
                !IsExpectedWorkJob(placeJob, state.ReadyOrder))
            {
                report.Fail(
                    scenario,
                    "Allow did not restore all three scheduling paths. Dig=" +
                    DescribeJob(digJob) +
                    "; delivery=" +
                    DescribeJob(deliveryJob) +
                    "; Place=" +
                    DescribeJob(placeJob) +
                    ".");
                return;
            }

            if (!GroupedFixtureStateUnchanged(state, requireReadyMaterialContained: true))
            {
                report.Fail(scenario, "Allow changed terrain or material accounting in the prepared fixture.");
                return;
            }

            state.Phase = GroupedControlPhase.AwaitingCancel;
            report.Pass(
                scenario,
                "Grouped Allow restores Dig, delivery, and actual Place scheduling without mutating state.");
            report.Info(
                "Grouped controls / next",
                "Keep the game paused, keep/recreate a mixed selection containing the three fixture orders, confirm " +
                "the native reverse Cancel gizmo uses the standard Cancel hotkey, click it, then run the verifier " +
                "again.");
        }

        private static void VerifyGroupedCancel(GroupedControlProbeState state, ProbeReport report)
        {
            const string scenario = "Grouped controls / Cancel";

            if (!state.DigOrder.Destroyed || !state.AwaitingOrder.Destroyed || !state.ReadyOrder.Destroyed)
            {
                report.Fail(scenario, "Native reverse Cancel did not destroy all three tracked relocation orders.");
                return;
            }

            bool readyRefundValid = state.ReadyResource is { Destroyed: false, stackCount: 1, Spawned: true } &&
                                    state.ReadyResource.Map == state.Map;
            bool awaitingResourceAccounted = state.AwaitingResource is { Destroyed: false };
            bool terrainUnchanged =
                ReferenceEquals(state.Map.terrainGrid.TerrainAt(state.DigCell), state.DigOriginalTerrain) &&
                ReferenceEquals(state.Map.terrainGrid.TerrainAt(state.AwaitingCell), state.AwaitingOriginalTerrain) &&
                ReferenceEquals(state.Map.terrainGrid.TerrainAt(state.ReadyCell), state.ReadyOriginalTerrain);
            bool staleJobs = state.Map.mapPawns.FreeColonistsSpawned.Any(pawn => JobTargetsAny(
                pawn.CurJob,
                state.DigOrder,
                state.AwaitingOrder,
                state.ReadyOrder));

            if (!readyRefundValid || !awaitingResourceAccounted || !terrainUnchanged || staleJobs)
            {
                report.Fail(
                    scenario,
                    "readyRefund=" +
                    readyRefundValid +
                    ", awaitingResourceAccounted=" +
                    awaitingResourceAccounted +
                    ", terrainUnchanged=" +
                    terrainUnchanged +
                    ", staleCurrentJob=" +
                    staleJobs +
                    ".");
                return;
            }

            report.Pass(
                scenario,
                "Native reverse Cancel removed mixed Dig/Place orders, refunded the exact contained Place " +
                "resource once, " +
                "preserved the " +
                "awaiting world resource and terrain, and left no current pawn job targeting the destroyed orders.");
            report.Info(
                "Grouped controls / enroute",
                "The probe validates observable job/resource state after cancellation; existing placement runtime " +
                "coverage " +
                "remains " +
                "the authoritative detailed IHaulEnroute lifecycle check.");

            CleanupGroupedControlState();
        }

        private static void PrepareUxSaveLoadFixture(ProbeReport report)
        {
            const string scenario = "UX save-load setup";
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail(scenario, "No current map is available.");
                return;
            }

            if (_uxSaveLoadState != null)
            {
                report.Fail(scenario, "A relocation UX save-load fixture already exists.");
                return;
            }

            if (!TryResolveUxDefinitions(out UxDefinitions definitions, report))
            {
                return;
            }

            List<Pawn> pawns = ResolvePawns(map, 2, requireEmptyCarry: false);
            Pawn anchorPawn = pawns.FirstOrDefault() ?? ResolvePawn(map, requireEmptyCarry: false);

            if (anchorPawn == null ||
                !TryFindClearDigCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    2,
                    out List<IntVec3> digCells) ||
                !TryFindClearRockCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    2,
                    out List<IntVec3> placeCells))
            {
                report.Fail(scenario, "Could not resolve two Dig cells and two Place cells for the save-load fixture.");
                return;
            }

            TerrainDef forbiddenDigOriginal = map.terrainGrid.TerrainAt(digCells[0]);
            TerrainDef workDigOriginal = map.terrainGrid.TerrainAt(digCells[1]);
            TerrainDef forbiddenPlaceOriginal = map.terrainGrid.TerrainAt(placeCells[0]);
            TerrainDef workPlaceOriginal = map.terrainGrid.TerrainAt(placeCells[1]);

            DigRelocationOrder forbiddenDig = null;
            DigRelocationOrder workDig = null;
            RelocationMaterialOrder forbiddenPlace = null;
            RelocationMaterialOrder workPlace = null;

            try
            {
                forbiddenDig = SpawnDigOrder(definitions.DigOrderDef, digCells[0], map);
                forbiddenDig.SetForbidden(true, warnOnFail: false);

                workDig = SpawnDigOrder(definitions.DigOrderDef, digCells[1], map);

                forbiddenPlace = SpawnOrder(definitions.SoilOrderDef, placeCells[0], map, null);
                forbiddenPlace.SetForbidden(true, warnOnFail: false);

                workPlace = SpawnReadyOrder(
                    definitions.SoilOrderDef,
                    definitions.SoilResourceDef,
                    placeCells[1],
                    map,
                    null);

                Pawn digPawn = FindPawnForDigOrder(map, definitions.DigWorker, workDig);
                Pawn placePawn = FindPawnForPlaceOrder(map, definitions.PlaceWorker, workPlace, digPawn);
                bool canCaptureConcurrentProgress =
                    digPawn != null && placePawn != null && !ReferenceEquals(digPawn, placePawn);

                if (!canCaptureConcurrentProgress)
                {
                    workDig.SetForbidden(true, warnOnFail: false);
                    workPlace.SetForbidden(true, warnOnFail: false);
                }

                _uxSaveLoadState = new UxSaveLoadProbeState(
                    map,
                    digCells[0],
                    digCells[1],
                    placeCells[0],
                    placeCells[1],
                    definitions.DigOrderDef.defName,
                    definitions.SoilOrderDef.defName,
                    definitions.SoilResourceDef.defName,
                    forbiddenDigOriginal,
                    workDigOriginal,
                    forbiddenPlaceOriginal,
                    workPlaceOriginal,
                    canCaptureConcurrentProgress);

                report.Pass(
                    scenario,
                    "Prepared forbidden Dig/Place persistence cases and separate Dig/Place actual-work fixtures.");

                if (canCaptureConcurrentProgress)
                {
                    report.Info(
                        "Progress save-load / operator steps",
                        "Let the two allowed work fixtures be taken by colonists (prioritize them if needed). When " +
                        "both Dig and Place progress bars are visibly between 0% and 100%, pause and run 'Capture " +
                        "relocation UX progress'. " +
                        "Then save, load that save, pause immediately, and run 'Verify relocation UX save-load'.");
                    report.Info(
                        "Progress save-load / positions",
                        "forbidden Dig=" +
                        digCells[0] +
                        "; work Dig=" +
                        digCells[1] +
                        "; forbidden Place=" +
                        placeCells[0] +
                        "; work Place=" +
                        placeCells[1] +
                        ".");
                }
                else
                {
                    report.Skip(
                        "Progress save-load",
                        "Two distinct currently-capable colonists are not available; progress capture is disabled " +
                        "for this fixture, but forbidden-state save-load can still be verified. Work fixtures were " +
                        "forbidden to keep them stable.");
                    report.Info(
                        "Forbidden save-load / operator steps",
                        "Keep the game paused, save, load that save, then run 'Verify relocation UX save-load'.");
                }
            }
            catch
            {
                DestroyUxOrder(forbiddenDig);
                DestroyUxOrder(workDig);
                DestroyOrderAndContents(forbiddenPlace);
                DestroyOrderAndContents(workPlace);
                RestoreTerrain(map, digCells[0], forbiddenDigOriginal);
                RestoreTerrain(map, digCells[1], workDigOriginal);
                RestoreTerrain(map, placeCells[0], forbiddenPlaceOriginal);
                RestoreTerrain(map, placeCells[1], workPlaceOriginal);
                _uxSaveLoadState = null;
                throw;
            }
        }

        private static void CaptureUxProgress(ProbeReport report)
        {
            const string scenario = "Progress capture";
            Map map = Find.CurrentMap;

            if (_uxSaveLoadState == null || !_uxSaveLoadState.FixtureMap.Matches(map))
            {
                report.Fail(scenario, "No matching relocation UX save-load fixture exists on the current map.");
                return;
            }

            UxSaveLoadProbeState state = _uxSaveLoadState;

            if (!state.CanCaptureProgress)
            {
                report.Skip(scenario, "This fixture was prepared without two distinct capable colonists.");
                return;
            }

            ThingDef digDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.SavedDigOrderDefName);
            ThingDef placeDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.PlaceOrderDefName);
            DigRelocationOrder digOrder = FindDigOrderAt(map, state.WorkDigCell, digDef);
            RelocationMaterialOrder placeOrder = FindOrderAt(map, state.WorkPlaceCell, placeDef);

            Pawn digPawn = FindPawnWorkingOnOrder<JobDriver_DigRelocatableTerrain>(map, digOrder);
            Pawn placePawn = FindPawnWorkingOnOrder<JobDriver_PlaceRelocatableTerrain>(map, placeOrder);

            if (digPawn == null || placePawn == null)
            {
                report.Skip(
                    scenario,
                    "Both actual-work jobs are not active simultaneously. Keep/restore the fixture, let both jobs " +
                    "reach " +
                    "their " +
                    "visible work phase, pause, and run this action again.");
                return;
            }

            if (!TryReadProgressSnapshot(digPawn.jobs.curDriver, out WorkProgressSnapshot digSnapshot) ||
                !TryReadProgressSnapshot(placePawn.jobs.curDriver, out WorkProgressSnapshot placeSnapshot))
            {
                report.Fail(scenario, "Could not read the expected JobDriver-local _workLeft/_totalWork fields.");
                return;
            }

            if (!IsPartialProgress(digSnapshot) || !IsPartialProgress(placeSnapshot))
            {
                report.Skip(
                    scenario,
                    "Both jobs must be inside partial actual work. Dig=" +
                    DescribeProgress(digSnapshot) +
                    "; Place=" +
                    DescribeProgress(placeSnapshot) +
                    ".");
                return;
            }

            state.DigProgress = digSnapshot;
            state.PlaceProgress = placeSnapshot;
            state.ProgressCaptured = true;

            report.Pass(
                scenario,
                "Captured JobDriver-local partial progress. Dig=" +
                DescribeProgress(digSnapshot) +
                "; Place=" +
                DescribeProgress(placeSnapshot) +
                ".");
            report.Info(
                "Progress capture / next",
                "Keep the game paused and save now. In the unsaved timeline you may then interrupt an active " +
                "Dig/Place job to confirm its bar disappears while the order survives, and let a fresh job start to " +
                "confirm progress restarts under the existing interruption semantics. Do not overwrite the captured " +
                "save. Load that save, pause immediately, and run 'Verify relocation UX save-load'.");
        }

        private static void VerifyUxSaveLoadFixture(ProbeReport report)
        {
            const string scenario = "UX save-load lifecycle";
            Map map = Find.CurrentMap;

            if (_uxSaveLoadState == null || map == null)
            {
                report.Fail(scenario, "No prepared relocation UX save-load state exists in this game process.");
                return;
            }

            UxSaveLoadProbeState state = _uxSaveLoadState;

            if (!state.FixtureMap.VerifyReload(map, report, scenario))
            {
                return;
            }

            ThingDef digDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.SavedDigOrderDefName);
            ThingDef placeDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.PlaceOrderDefName);
            ThingDef resourceDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.PlaceResourceDefName);

            if (digDef == null ||
                placeDef == null ||
                resourceDef == null ||
                !TryResolveUxDefinitions(out UxDefinitions definitions, report))
            {
                report.Fail(scenario, "Prepared production Defs are unavailable after load.");
                return;
            }

            DigRelocationOrder forbiddenDig = FindDigOrderAt(map, state.ForbiddenDigCell, digDef);
            RelocationMaterialOrder forbiddenPlace = FindOrderAt(map, state.ForbiddenPlaceCell, placeDef);

            bool forbiddenPersisted = forbiddenDig != null &&
                                      forbiddenPlace != null &&
                                      forbiddenDig.TryGetComp<CompForbiddable>()?.Forbidden == true &&
                                      forbiddenPlace.TryGetComp<CompForbiddable>()?.Forbidden == true;

            if (!forbiddenPersisted)
            {
                report.Fail(scenario, "Forbidden Dig/Place state did not survive save-load.");
            }
            else
            {
                report.Pass(scenario, "CompForbiddable state survived save-load for both Dig and Place orders.");
            }

            ProbeNativeCancellationAfterLoad(forbiddenDig, forbiddenPlace, report);
            ProbeForbiddenSchedulingAfterLoad(forbiddenDig, forbiddenPlace, definitions, report);
            ProbeOrderOwnedProgressBoundary(report);

            if (!state.CanCaptureProgress)
            {
                report.Skip("Progress save-load", "Progress capture was disabled when this fixture was prepared.");
                CleanupUxSaveLoadState(map);
                return;
            }

            if (!state.ProgressCaptured)
            {
                report.Skip(
                    "Progress save-load",
                    "No pre-save progress snapshot was captured. Forbidden-state persistence was still checked.");
                CleanupUxSaveLoadState(map);
                return;
            }

            DigRelocationOrder workDig = FindDigOrderAt(map, state.WorkDigCell, digDef);
            RelocationMaterialOrder workPlace = FindOrderAt(map, state.WorkPlaceCell, placeDef);
            Pawn digPawn = FindPawnWorkingOnOrder<JobDriver_DigRelocatableTerrain>(map, workDig);
            Pawn placePawn = FindPawnWorkingOnOrder<JobDriver_PlaceRelocatableTerrain>(map, workPlace);

            if (workDig == null || workPlace == null || digPawn == null || placePawn == null)
            {
                report.Fail(
                    "Progress save-load",
                    "One or both partial actual-work jobs/orders were not restored. Pause immediately after loading " +
                    "the " +
                    "prepared save.");
                CleanupUxSaveLoadState(map);
                return;
            }

            if (!TryReadProgressSnapshot(digPawn.jobs.curDriver, out WorkProgressSnapshot loadedDig) ||
                !TryReadProgressSnapshot(placePawn.jobs.curDriver, out WorkProgressSnapshot loadedPlace))
            {
                report.Fail("Progress save-load", "Could not read restored JobDriver-local progress fields.");
                CleanupUxSaveLoadState(map);
                return;
            }

            bool digValid = IsRestoredProgress(state.DigProgress, loadedDig);
            bool placeValid = IsRestoredProgress(state.PlaceProgress, loadedPlace);

            if (!digValid || !placeValid)
            {
                report.Fail(
                    "Progress save-load",
                    "Restored progress is inconsistent with the pre-save snapshots. preDig=" +
                    DescribeProgress(state.DigProgress) +
                    ", loadedDig=" +
                    DescribeProgress(loadedDig) +
                    "; prePlace=" +
                    DescribeProgress(state.PlaceProgress) +
                    ", loadedPlace=" +
                    DescribeProgress(loadedPlace) +
                    ".");
            }
            else
            {
                report.Pass(
                    "Progress save-load",
                    "Dig and Place restore positive partial JobDriver-local work without resetting total work or " +
                    "progress.");
                report.Info(
                    "Progress bar smoke test",
                    "With the game running, visually confirm the vanilla progress bar reappears over each restored " +
                    "active " +
                    "order.");
            }

            CleanupUxSaveLoadState(map);
        }

        private static void ProbeForbiddenSchedulingAfterLoad(
            DigRelocationOrder forbiddenDig,
            RelocationMaterialOrder forbiddenPlace,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string digScenario = "Save-load / forbidden Dig scheduling";
            const string deliveryScenario = "Save-load / forbidden Place delivery scheduling";

            if (forbiddenDig == null)
            {
                report.Fail(digScenario, "Forbidden Dig fixture order is missing after load.");
            }

            if (forbiddenPlace == null)
            {
                report.Fail(deliveryScenario, "Forbidden Place fixture order is missing after load.");
            }

            if (forbiddenDig == null || forbiddenPlace == null)
            {
                return;
            }

            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail(digScenario, "No current map is available after load.");
                report.Fail(deliveryScenario, "No current map is available after load.");
                return;
            }

            ProbeForbiddenDigSchedulingAfterLoad(map, forbiddenDig, definitions, report);
            ProbeForbiddenDeliverySchedulingAfterLoad(map, forbiddenPlace, definitions, report);
        }

        private static void ProbeForbiddenDigSchedulingAfterLoad(
            Map map,
            DigRelocationOrder forbiddenDig,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string scenario = "Save-load / forbidden Dig scheduling";
            Pawn pawn;

            try
            {
                forbiddenDig.SetForbidden(false, warnOnFail: false);
                pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(candidate =>
                    IsPostLoadSchedulingProbePawn(candidate) &&
                    IsExpectedDigJob(
                        definitions.DigWorker.JobOnThing(candidate, forbiddenDig, forced: true),
                        forbiddenDig));
            }
            finally
            {
                forbiddenDig.SetForbidden(true, warnOnFail: false);
            }

            if (pawn == null)
            {
                report.Skip(
                    scenario,
                    "No colonist outside the restored relocation-progress jobs can establish an allowed Dig " +
                    "scheduling baseline after load.");
                return;
            }

            if (!forbiddenDig.IsForbidden(pawn))
            {
                report.Skip(
                    scenario,
                    "The selected baseline pawn's current runtime context bypasses Thing.IsForbidden for the " +
                    "restored Dig order (" +
                    DescribeSchedulingProbePawn(pawn) +
                    "). This pawn is not a valid probe " +
                    "for new " +
                    "forbidden scheduling.");
                return;
            }

            Job normalJob = definitions.DigWorker.JobOnThing(pawn, forbiddenDig, forced: false);
            Job forcedJob = definitions.DigWorker.JobOnThing(pawn, forbiddenDig, forced: true);

            if (normalJob != null || forcedJob != null)
            {
                report.Fail(
                    scenario,
                    "Restored forbidden Dig order produced scheduling for a pawn that currently sees it as " +
                    "forbidden. normal=" +
                    DescribeJob(normalJob) +
                    ", forced=" +
                    DescribeJob(forcedJob) +
                    "; " +
                    DescribeSchedulingProbePawn(pawn) +
                    ".");
                return;
            }

            report.Pass(
                scenario,
                "Restored forbidden Dig order remains excluded from new normal/forced scheduling for a verified " +
                "post-load baseline pawn.");
        }

        private static void ProbeForbiddenDeliverySchedulingAfterLoad(
            Map map,
            RelocationMaterialOrder forbiddenPlace,
            UxDefinitions definitions,
            ProbeReport report)
        {
            const string scenario = "Save-load / forbidden Place delivery scheduling";
            Pawn anchorPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(IsPostLoadSchedulingProbePawn);

            if (anchorPawn == null)
            {
                report.Skip(
                    scenario,
                    "No colonist outside the restored relocation-progress jobs is available for the delivery " +
                    "scheduling baseline after load.");
                return;
            }

            Thing resource = null;

            try
            {
                resource = SpawnResource(definitions.SoilResourceDef, anchorPawn.Position, map, null);
                resource.SetForbidden(false, warnOnFail: false);

                forbiddenPlace.SetForbidden(false, warnOnFail: false);
                Pawn pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(candidate =>
                    IsPostLoadSchedulingProbePawn(candidate) &&
                    IsExpectedDeliveryJobForOrder(
                        definitions.DeliveryWorker.JobOnThing(candidate, forbiddenPlace, forced: true),
                        forbiddenPlace));
                forbiddenPlace.SetForbidden(true, warnOnFail: false);

                if (pawn == null)
                {
                    report.Skip(
                        scenario,
                        "No colonist outside the restored relocation-progress jobs can establish an allowed Place " +
                        "delivery scheduling baseline after load.");
                    return;
                }

                if (!forbiddenPlace.IsForbidden(pawn))
                {
                    report.Skip(
                        scenario,
                        "The selected baseline pawn's current runtime context bypasses Thing.IsForbidden for the " +
                        "restored Place order (" +
                        DescribeSchedulingProbePawn(pawn) +
                        "). This pawn is not a valid " +
                        "probe for new forbidden scheduling.");
                    return;
                }

                Job normalJob = definitions.DeliveryWorker.JobOnThing(pawn, forbiddenPlace, forced: false);
                Job forcedJob = definitions.DeliveryWorker.JobOnThing(pawn, forbiddenPlace, forced: true);

                if (normalJob != null || forcedJob != null)
                {
                    report.Fail(
                        scenario,
                        "Restored forbidden awaiting Place order produced delivery scheduling for a pawn that " +
                        "currently sees it as forbidden. normal=" +
                        DescribeJob(normalJob) +
                        ", forced=" +
                        DescribeJob(forcedJob) +
                        "; " +
                        DescribeSchedulingProbePawn(pawn) +
                        ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Restored forbidden awaiting Place order remains excluded from new normal/forced delivery " +
                    "scheduling with an available canonical resource.");
            }
            finally
            {
                forbiddenPlace.SetForbidden(true, warnOnFail: false);
                DestroyLooseThing(resource);
            }
        }

        private static bool IsPostLoadSchedulingProbePawn(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed)
            {
                return false;
            }

            JobDriver driver = pawn.jobs?.curDriver;
            return !(driver is JobDriver_DigRelocatableTerrain) && !(driver is JobDriver_PlaceRelocatableTerrain);
        }

        private static string DescribeSchedulingProbePawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return "pawn=<null>";
            }

            string jobDefName = pawn.CurJob?.def?.defName ?? "<none>";
            string driverName = pawn.jobs?.curDriver?.GetType().Name ?? "<none>";
            return "curJob=" + jobDefName + ", driver=" + driverName;
        }

        private static void ProbeOrderOwnedProgressBoundary(ProbeReport report)
        {
            const string scenario = "Progress ownership";
            Type[] orderTypes =
            [
                typeof(RelocationOrder),
                typeof(DigRelocationOrder),
                typeof(RelocationMaterialOrder)
            ];

            foreach (Type type in orderTypes)
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (fields.Any(field => field.Name == "_workLeft" || field.Name == "_totalWork"))
                {
                    report.Fail(scenario, type.Name + " unexpectedly owns JobDriver work-progress fields.");
                    return;
                }
            }

            report.Pass(
                scenario,
                "Relocation orders do not own _workLeft/_totalWork; active work progress remains JobDriver-local.");
        }

        private static bool TryReadProgressSnapshot(JobDriver driver, out WorkProgressSnapshot snapshot)
        {
            snapshot = default(WorkProgressSnapshot);

            if (driver == null)
            {
                return false;
            }

            Type type = driver.GetType();
            FieldInfo workLeftField = type.GetField("_workLeft", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo totalWorkField = type.GetField("_totalWork", BindingFlags.Instance | BindingFlags.NonPublic);

            if (workLeftField == null ||
                totalWorkField == null ||
                workLeftField.FieldType != typeof(float) ||
                totalWorkField.FieldType != typeof(float))
            {
                return false;
            }

            snapshot = new WorkProgressSnapshot(
                (float)workLeftField.GetValue(driver)!,
                (float)totalWorkField.GetValue(driver)!);
            return true;
        }

        private static bool IsPartialProgress(WorkProgressSnapshot snapshot)
        {
            return snapshot is { TotalWork: > 0f, WorkLeft: > 0f } && snapshot.WorkLeft < snapshot.TotalWork;
        }

        private static bool IsRestoredProgress(WorkProgressSnapshot beforeSave, WorkProgressSnapshot afterLoad)
        {
            if (!IsPartialProgress(beforeSave) ||
                afterLoad.TotalWork <= 0f ||
                afterLoad.WorkLeft <= 0f ||
                afterLoad.WorkLeft >= afterLoad.TotalWork)
            {
                return false;
            }

            float totalTolerance = Math.Max(0.001f, beforeSave.TotalWork * 0.0001f);
            float progressTolerance = Math.Max(2f, beforeSave.TotalWork * 0.02f);

            return Math.Abs(afterLoad.TotalWork - beforeSave.TotalWork) <= totalTolerance &&
                   afterLoad.WorkLeft <= beforeSave.WorkLeft + progressTolerance;
        }

        private static string DescribeProgress(WorkProgressSnapshot snapshot)
        {
            float progress = snapshot.TotalWork <= 0f ? 0f : 1f - snapshot.WorkLeft / snapshot.TotalWork;
            return "workLeft=" +
                   snapshot.WorkLeft.ToString("0.###") +
                   ", totalWork=" +
                   snapshot.TotalWork.ToString("0.###") +
                   ", progress=" +
                   progress.ToString("P1");
        }

        private static Pawn FindPawnWorkingOnOrder<TDriver>(Map map, Thing order) where TDriver : JobDriver
        {
            if (map == null || order == null)
            {
                return null;
            }

            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn =>
                pawn?.CurJob != null &&
                ReferenceEquals(pawn.CurJob.targetA.Thing, order) &&
                pawn.jobs.curDriver is TDriver);
        }

        private static Pawn FindPawnForDigOrder(
            Map map,
            WorkGiver_DigRelocatableTerrain worker,
            DigRelocationOrder order,
            Pawn excludedPawn = null)
        {
            if (map == null || worker == null || order == null)
            {
                return null;
            }

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (!IsProbePawnAvailable(pawn, excludedPawn))
                {
                    continue;
                }

                Job job = worker.JobOnThing(pawn, order, forced: true);

                if (IsExpectedDigJob(job, order))
                {
                    return pawn;
                }
            }

            return null;
        }

        private static Pawn FindPawnForDeliveryOrder(
            Map map,
            WorkGiver_DeliverRelocationMaterial worker,
            RelocationMaterialOrder order,
            Pawn excludedPawn = null)
        {
            if (map == null || worker == null || order == null)
            {
                return null;
            }

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (!IsProbePawnAvailable(pawn, excludedPawn))
                {
                    continue;
                }

                Job job = worker.JobOnThing(pawn, order, forced: true);

                if (IsExpectedDeliveryJobForOrder(job, order))
                {
                    return pawn;
                }
            }

            return null;
        }

        private static Pawn FindPawnForPlaceOrder(
            Map map,
            WorkGiver_PlaceRelocatableTerrain worker,
            RelocationMaterialOrder order,
            Pawn excludedPawn = null)
        {
            if (map == null || worker == null || order == null)
            {
                return null;
            }

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (!IsProbePawnAvailable(pawn, excludedPawn))
                {
                    continue;
                }

                Job job = worker.JobOnThing(pawn, order, forced: true);

                if (IsExpectedWorkJob(job, order))
                {
                    return pawn;
                }
            }

            return null;
        }

        private static bool IsProbePawnAvailable(Pawn pawn, Pawn excludedPawn)
        {
            return pawn is { Spawned: true, Dead: false, Downed: false } && !ReferenceEquals(pawn, excludedPawn);
        }

        private static bool IsExpectedDigJob(Job job, DigRelocationOrder order)
        {
            return job != null &&
                   job.def == SoilRelocationJobDefOf.SoilRelocation_DigTerrain &&
                   ReferenceEquals(job.targetA.Thing, order) &&
                   !job.targetB.IsValid &&
                   !job.targetC.IsValid;
        }

        private static bool TryFindClearDigCells(
            Map map,
            IntVec3 center,
            int minimumRadius,
            int maximumRadius,
            int count,
            out List<IntVec3> result)
        {
            result = [];

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, maximumRadius, useCenter: true))
            {
                if (!IsClearDigCell(map, center, cell, minimumRadius) || result.Contains(cell))
                {
                    continue;
                }

                result.Add(cell);

                if (result.Count == count)
                {
                    return true;
                }
            }

            result.Clear();
            return false;
        }

        private static bool TryFindClearDigCell(
            Map map,
            IntVec3 center,
            int minimumRadius,
            int maximumRadius,
            out IntVec3 result)
        {
            if (TryFindClearDigCells(map, center, minimumRadius, maximumRadius, 1, out List<IntVec3> cells))
            {
                result = cells[0];
                return true;
            }

            result = IntVec3.Invalid;
            return false;
        }

        private static bool IsClearDigCell(Map map, IntVec3 center, IntVec3 cell, int minimumRadius)
        {
            if (!cell.InBounds(map) ||
                cell.DistanceToSquared(center) < minimumRadius * minimumRadius ||
                !cell.Standable(map) ||
                cell.Fogged(map))
            {
                return false;
            }

            DiggingValidationResult validation = DiggingValidator.Validate(map, cell);

            if (!validation.IsValid)
            {
                return false;
            }

            ThingDef digOrderDef = SoilRelocationDigOrderDefOf.SoilRelocation_DigOrder;

            if (digOrderDef == null)
            {
                return false;
            }

            return !map.listerThings.ThingsOfDef(digOrderDef)
                .Any(thing => thing is { Spawned: true, Destroyed: false } && thing.Position == cell);
        }

        private static bool TryFindSeparatedRockCells(
            Map map,
            IntVec3 center,
            int minimumRadius,
            int maximumRadius,
            int count,
            int minimumSeparation,
            out List<IntVec3> result)
        {
            result = [];
            int minimumSeparationSquared = minimumSeparation * minimumSeparation;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, maximumRadius, useCenter: true))
            {
                if (!IsClearRockCell(map, center, cell, minimumRadius) ||
                    result.Any(existing => existing.DistanceToSquared(cell) < minimumSeparationSquared))
                {
                    continue;
                }

                result.Add(cell);

                if (result.Count == count)
                {
                    return true;
                }
            }

            result.Clear();
            return false;
        }

        private static DigRelocationOrder SpawnDigOrder(ThingDef orderDef, IntVec3 cell, Map map)
        {
            var order = ThingMaker.MakeThing(orderDef) as DigRelocationOrder;

            if (order == null)
            {
                throw new InvalidOperationException(
                    "ThingDef '" + orderDef?.defName + "' did not create DigRelocationOrder.");
            }

            GenSpawn.Spawn(order, cell, map);
            return order;
        }

        private static DigRelocationOrder FindDigOrderAt(Map map, IntVec3 cell, ThingDef orderDef)
        {
            if (map == null || orderDef == null || !cell.InBounds(map))
            {
                return null;
            }

            return map.listerThings.ThingsOfDef(orderDef)
                .OfType<DigRelocationOrder>()
                .FirstOrDefault(order => order.Spawned && !order.Destroyed && order.Position == cell);
        }

        private static bool SameReferenceSet<T>(IReadOnlyCollection<T> first, IReadOnlyCollection<T> second)
            where T : class
        {
            return first != null && second != null && first.Count == second.Count && first.All(second.Contains);
        }

        private static string DescribeOrderPositions(IEnumerable<RelocationOrder> orders)
        {
            return string.Join(
                "; ",
                orders.Where(order => order != null).Select(order => order.def.defName + "@" + order.Position));
        }

        private static string DescribeSelectedObjects(IEnumerable<object> selectedObjects)
        {
            return string.Join(
                ", ",
                selectedObjects.Select(selected =>
                {
                    if (selected is Thing thing)
                    {
                        return thing.def?.defName ?? "<Thing without Def>";
                    }

                    return selected?.GetType().Name ?? "<null>";
                }));
        }

        private static bool AllOrdersAlive(GroupedControlProbeState state)
        {
            return state.DigOrder is { Destroyed: false } &&
                   state.AwaitingOrder is { Destroyed: false } &&
                   state.ReadyOrder is { Destroyed: false };
        }

        private static bool AllOrdersForbidden(GroupedControlProbeState state)
        {
            return AllOrdersAlive(state) &&
                   state.DigOrder.TryGetComp<CompForbiddable>()?.Forbidden == true &&
                   state.AwaitingOrder.TryGetComp<CompForbiddable>()?.Forbidden == true &&
                   state.ReadyOrder.TryGetComp<CompForbiddable>()?.Forbidden == true;
        }

        private static bool GroupedFixtureStateUnchanged(
            GroupedControlProbeState state,
            bool requireReadyMaterialContained)
        {
            bool terrainUnchanged =
                ReferenceEquals(state.Map.terrainGrid.TerrainAt(state.DigCell), state.DigOriginalTerrain) &&
                ReferenceEquals(state.Map.terrainGrid.TerrainAt(state.AwaitingCell), state.AwaitingOriginalTerrain) &&
                ReferenceEquals(state.Map.terrainGrid.TerrainAt(state.ReadyCell), state.ReadyOriginalTerrain);
            bool awaitingUnchanged = state.AwaitingOrder.GetDirectlyHeldThings().Count == 0 &&
                                     state.AwaitingResource is { Destroyed: false };
            bool readyUnchanged = !requireReadyMaterialContained ||
                                  (state.ReadyOrder.TryGetContainedMaterial(out Thing contained) &&
                                   ReferenceEquals(contained, state.ReadyResource) &&
                                   contained.stackCount == 1);

            return terrainUnchanged && awaitingUnchanged && readyUnchanged;
        }

        private static bool JobTargetsAny(Job job, params Thing[] targets)
        {
            if (job == null || targets == null)
            {
                return false;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                Thing target = targets[i];

                if (target == null)
                {
                    continue;
                }

                if (ReferenceEquals(job.targetA.Thing, target) ||
                    ReferenceEquals(job.targetB.Thing, target) ||
                    ReferenceEquals(job.targetC.Thing, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static void DestroyUxOrders(IEnumerable<RelocationOrder> orders)
        {
            if (orders == null)
            {
                return;
            }

            foreach (RelocationOrder order in orders.ToList())
            {
                DestroyUxOrder(order);
            }
        }

        private static void DestroyUxOrder(RelocationOrder order)
        {
            if (order == null || order.Destroyed)
            {
                return;
            }

            if (order is RelocationMaterialOrder materialOrder)
            {
                DestroyOrderAndContents(materialOrder);
                return;
            }

            order.Destroy();
        }

        private static void DestroyLooseThing(Thing thing)
        {
            if (thing is { Destroyed: false })
            {
                thing.Destroy();
            }
        }

        private static void RestoreCellTerrains(Map map, IEnumerable<CellTerrainState> states)
        {
            if (states == null)
            {
                return;
            }

            foreach (CellTerrainState state in states)
            {
                RestoreTerrain(map, state.Cell, state.Terrain);
            }
        }

        private static void CleanupSelectionUxState()
        {
            if (_uxSelectionState == null)
            {
                return;
            }

            SelectionUxProbeState state = _uxSelectionState;
            DestroyUxOrders(state.Orders);
            RestoreCellTerrains(state.Map, state.OriginalTerrains);
            _uxSelectionState = null;
        }

        private static void CleanupGroupedControlState()
        {
            if (_uxGroupedControlState == null)
            {
                return;
            }

            GroupedControlProbeState state = _uxGroupedControlState;
            RestoreResourceStates(state.PreexistingResourceStates);
            DestroyUxOrder(state.DigOrder);
            DestroyOrderAndContents(state.AwaitingOrder);
            DestroyOrderAndContents(state.ReadyOrder);
            DestroyLooseThing(state.AwaitingResource);
            DestroyLooseThing(state.ReadyResource);
            RestoreTerrain(state.Map, state.DigCell, state.DigOriginalTerrain);
            RestoreTerrain(state.Map, state.AwaitingCell, state.AwaitingOriginalTerrain);
            RestoreTerrain(state.Map, state.ReadyCell, state.ReadyOriginalTerrain);
            _uxGroupedControlState = null;
        }

        private static void CleanupUxSaveLoadState(Map map)
        {
            if (_uxSaveLoadState == null)
            {
                return;
            }

            UxSaveLoadProbeState state = _uxSaveLoadState;

            if (!state.FixtureMap.Matches(map))
            {
                return;
            }

            ThingDef digDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.SavedDigOrderDefName);
            ThingDef placeDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.PlaceOrderDefName);

            DestroyUxOrder(FindDigOrderAt(map, state.ForbiddenDigCell, digDef));
            DestroyUxOrder(FindDigOrderAt(map, state.WorkDigCell, digDef));
            DestroyOrderAndContents(FindOrderAt(map, state.ForbiddenPlaceCell, placeDef));
            DestroyOrderAndContents(FindOrderAt(map, state.WorkPlaceCell, placeDef));

            RestoreTerrain(map, state.ForbiddenDigCell, state.ForbiddenDigOriginalTerrain);
            RestoreTerrain(map, state.WorkDigCell, state.WorkDigOriginalTerrain);
            RestoreTerrain(map, state.ForbiddenPlaceCell, state.ForbiddenPlaceOriginalTerrain);
            RestoreTerrain(map, state.WorkPlaceCell, state.WorkPlaceOriginalTerrain);

            _uxSaveLoadState = null;
        }

        private sealed class UxDefinitions(
            ThingDef digOrderDef,
            ThingDef soilOrderDef,
            ThingDef soilResourceDef,
            WorkGiver_DigRelocatableTerrain digWorker,
            WorkGiver_PlaceRelocatableTerrain placeWorker,
            WorkGiver_DeliverRelocationMaterial deliveryWorker)
        {
            public ThingDef DigOrderDef { get; } = digOrderDef;

            public ThingDef SoilOrderDef { get; } = soilOrderDef;

            public ThingDef SoilResourceDef { get; } = soilResourceDef;

            public WorkGiver_DigRelocatableTerrain DigWorker { get; } = digWorker;

            public WorkGiver_PlaceRelocatableTerrain PlaceWorker { get; } = placeWorker;

            public WorkGiver_DeliverRelocationMaterial DeliveryWorker { get; } = deliveryWorker;
        }

        private sealed class SelectionUxProbeState(
            Map map,
            List<RelocationOrder> orders,
            List<CellTerrainState> originalTerrains)
        {
            public Map Map { get; } = map;

            public IReadOnlyList<RelocationOrder> Orders { get; } = orders;

            public IReadOnlyList<CellTerrainState> OriginalTerrains { get; } = originalTerrains;
        }

        private sealed class CellTerrainState(IntVec3 cell, TerrainDef terrain)
        {
            public IntVec3 Cell { get; } = cell;

            public TerrainDef Terrain { get; } = terrain;
        }

        private enum GroupedControlPhase
        {
            AwaitingForbid,
            AwaitingAllow,
            AwaitingCancel
        }

        private sealed class GroupedControlProbeState(
            Map map,
            UxDefinitions definitions,
            DigRelocationOrder digOrder,
            RelocationMaterialOrder awaitingOrder,
            RelocationMaterialOrder readyOrder,
            Thing awaitingResource,
            Thing readyResource,
            Pawn digPawn,
            Pawn deliveryPawn,
            Pawn placePawn,
            IntVec3 digCell,
            IntVec3 awaitingCell,
            IntVec3 readyCell,
            TerrainDef digOriginalTerrain,
            TerrainDef awaitingOriginalTerrain,
            TerrainDef readyOriginalTerrain,
            List<ResourceForbiddenState> preexistingResourceStates)
        {
            public Map Map { get; } = map;

            public UxDefinitions Definitions { get; } = definitions;

            public DigRelocationOrder DigOrder { get; } = digOrder;

            public RelocationMaterialOrder AwaitingOrder { get; } = awaitingOrder;

            public RelocationMaterialOrder ReadyOrder { get; } = readyOrder;

            public Thing AwaitingResource { get; } = awaitingResource;

            public Thing ReadyResource { get; } = readyResource;

            public Pawn DigPawn { get; } = digPawn;

            public Pawn DeliveryPawn { get; } = deliveryPawn;

            public Pawn PlacePawn { get; } = placePawn;

            public IntVec3 DigCell { get; } = digCell;

            public IntVec3 AwaitingCell { get; } = awaitingCell;

            public IntVec3 ReadyCell { get; } = readyCell;

            public TerrainDef DigOriginalTerrain { get; } = digOriginalTerrain;

            public TerrainDef AwaitingOriginalTerrain { get; } = awaitingOriginalTerrain;

            public TerrainDef ReadyOriginalTerrain { get; } = readyOriginalTerrain;

            public List<ResourceForbiddenState> PreexistingResourceStates { get; } = preexistingResourceStates;

            public GroupedControlPhase Phase { get; set; } = GroupedControlPhase.AwaitingForbid;
        }

        private readonly struct WorkProgressSnapshot(float workLeft, float totalWork)
        {
            public float WorkLeft { get; } = workLeft;

            public float TotalWork { get; } = totalWork;
        }

        private sealed class UxSaveLoadProbeState(
            Map map,
            IntVec3 forbiddenDigCell,
            IntVec3 workDigCell,
            IntVec3 forbiddenPlaceCell,
            IntVec3 workPlaceCell,
            string digOrderDefName,
            string placeOrderDefName,
            string placeResourceDefName,
            TerrainDef forbiddenDigOriginalTerrain,
            TerrainDef workDigOriginalTerrain,
            TerrainDef forbiddenPlaceOriginalTerrain,
            TerrainDef workPlaceOriginalTerrain,
            bool canCaptureProgress)
        {
            public SaveLoadProbeMap FixtureMap { get; } = new(map);

            public IntVec3 ForbiddenDigCell { get; } = forbiddenDigCell;

            public IntVec3 WorkDigCell { get; } = workDigCell;

            public IntVec3 ForbiddenPlaceCell { get; } = forbiddenPlaceCell;

            public IntVec3 WorkPlaceCell { get; } = workPlaceCell;

            public string SavedDigOrderDefName { get; } = digOrderDefName;

            public string PlaceOrderDefName { get; } = placeOrderDefName;

            public string PlaceResourceDefName { get; } = placeResourceDefName;

            public TerrainDef ForbiddenDigOriginalTerrain { get; } = forbiddenDigOriginalTerrain;

            public TerrainDef WorkDigOriginalTerrain { get; } = workDigOriginalTerrain;

            public TerrainDef ForbiddenPlaceOriginalTerrain { get; } = forbiddenPlaceOriginalTerrain;

            public TerrainDef WorkPlaceOriginalTerrain { get; } = workPlaceOriginalTerrain;

            public bool CanCaptureProgress { get; } = canCaptureProgress;

            public bool ProgressCaptured { get; set; }

            public WorkProgressSnapshot DigProgress { get; set; }

            public WorkProgressSnapshot PlaceProgress { get; set; }
        }
    }
}
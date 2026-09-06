using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using SoilRelocation.Domain;
using SoilRelocation.Settings;
using SoilRelocation.Work;
using Verse;
using Verse.AI;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        private const string LogPrefix = "[SoilRelocation.PlacementTests]";
        private const string ActualWorkGiverDefName = "SoilRelocation_PlaceTerrain";
        private const string DeliveryWorkGiverDefName = "SoilRelocation_DeliverPlacementMaterial";
        private const string WorkTypeDefName = "SoilRelocation";
        private const string HaulingWorkTypeDefName = "Hauling";
        private const int CleanupRadiusSquared = 25;

        private static readonly FamilyCase[] _familyCases =
        {
            new FamilyCase(
                "Sand",
                "SoilRelocation_PlaceSandOrder",
                "SoilRelocation_Sand",
                () => new Designator_PlaceSand()),
            new FamilyCase(
                "Gravel",
                "SoilRelocation_PlaceGravelOrder",
                "SoilRelocation_Gravel",
                () => new Designator_PlaceGravel()),
            new FamilyCase(
                "Soil",
                "SoilRelocation_PlaceSoilOrder",
                "SoilRelocation_Soil",
                () => new Designator_PlaceSoil()),
            new FamilyCase(
                "Rich Soil",
                "SoilRelocation_PlaceRichSoilOrder",
                "SoilRelocation_RichSoil",
                () => new Designator_PlaceRichSoil())
        };

        private static SaveLoadProbeState _saveLoadState;
        private static ConcurrentDeliveryProbeState _concurrentDeliveryState;
        private static EndToEndProbeState _endToEndState;

        [DebugAction(
            "Soil Relocation",
            "Validate material order placement",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ValidateMaterialOrderPlacement()
        {
            var report = new ProbeReport();

            try
            {
                RunIntegratedProbe(report);
            }
            catch (Exception exception)
            {
                report.Fail("Probe execution", "Unhandled exception: " + exception);
            }

            WriteReport("Material-order placement probe", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Prepare placement save-load cases",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PreparePlacementSaveLoadCases()
        {
            var report = new ProbeReport();

            try
            {
                PrepareSaveLoadCases(report);
            }
            catch (Exception exception)
            {
                report.Fail("Save-load setup", "Unhandled exception: " + exception);
            }

            WriteReport("Placement save-load setup", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Verify placement save-load cases",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyPlacementSaveLoadCases()
        {
            var report = new ProbeReport();

            try
            {
                VerifySaveLoadCases(report);
            }
            catch (Exception exception)
            {
                report.Fail("Save-load verification", "Unhandled exception: " + exception);
            }

            WriteReport("Placement save-load verification", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Prepare concurrent placement delivery",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareConcurrentPlacementDelivery()
        {
            var report = new ProbeReport();

            try
            {
                PrepareConcurrentDelivery(report);
            }
            catch (Exception exception)
            {
                report.Fail("Concurrent delivery setup", "Unhandled exception: " + exception);
            }

            WriteReport("Concurrent placement delivery setup", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Verify concurrent placement delivery",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyConcurrentPlacementDelivery()
        {
            var report = new ProbeReport();

            try
            {
                VerifyConcurrentDelivery(report);
            }
            catch (Exception exception)
            {
                report.Fail("Concurrent delivery verification", "Unhandled exception: " + exception);
            }

            WriteReport("Concurrent placement delivery verification", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Prepare full placement lifecycle",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void PrepareFullPlacementLifecycle()
        {
            var report = new ProbeReport();

            try
            {
                PrepareEndToEndLifecycle(report);
            }
            catch (Exception exception)
            {
                report.Fail("End-to-end setup", "Unhandled exception: " + exception);
            }

            WriteReport("Full placement lifecycle setup", report);
        }

        [DebugAction(
            "Soil Relocation",
            "Verify full placement lifecycle",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void VerifyFullPlacementLifecycle()
        {
            var report = new ProbeReport();

            try
            {
                VerifyEndToEndLifecycle(report);
            }
            catch (Exception exception)
            {
                report.Fail("End-to-end verification", "Unhandled exception: " + exception);
            }

            WriteReport("Full placement lifecycle verification", report);
        }

        private static void RunIntegratedProbe(ProbeReport report)
        {
            if (!TryCreateContext(report, out ProbeContext context))
            {
                return;
            }

            report.Info("Environment", "Pawn=" + context.Pawn.LabelShortCap + ", target=" + context.TargetCell + ".");

            ProbeWorkGiverWiring(context, report);
            ProbeFamilyDefinitions(report);
            ProbeDesignatorOrderIdentity(context, report);
            ProbeFamilyDeliveryMatrix(context, report);
            ProbeFamilyCompletionMatrix(context, report);
            ProbeDeliveryJobs(context, report);
            ProbeCarriedResourceReuse(context, report);
            ProbeContainerContract(context, report);
            ProbeNearbyOrderIsolation(context, report);
            ProbeCancellation(context, report);
            ProbeResolutionModes(context, report);

            FamilyCase primaryCase = ResolveFamilyCase("Soil");

            if (primaryCase == null || !primaryCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
            {
                report.Fail("Environment", "Primary Soil family test Defs are not available.");
                return;
            }

            TerrainDef originalTerrain = context.Map.terrainGrid.TerrainAt(context.TargetCell);

            try
            {
                ProbeAwaitingOrder(
                    context.Map,
                    context.Pawn,
                    context.ActualWorker,
                    orderDef,
                    context.TargetCell,
                    report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbeReadyOrder(
                    context.Map,
                    context.Pawn,
                    context.ActualWorker,
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbeWorldResourceIndependence(
                    context.Map,
                    context.Pawn,
                    context.ActualWorker,
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbeSuccessfulCompletion(context.Map, orderDef, resourceDef, context.TargetCell, report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbeStaleDestination(context.Map, orderDef, resourceDef, context.TargetCell, report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbePermanentInvalidation(
                    context.Map,
                    context.Pawn,
                    context.ActualWorker,
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbeFireBlocker(
                    context.Map,
                    context.Pawn,
                    context.ActualWorker,
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbePlantPrerequisite(
                    context.Map,
                    context.Pawn,
                    context.ActualWorker,
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbeSkillEligibility(
                    context.Map,
                    context.Pawn,
                    context.ActualWorker,
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbeInterruptionInvariant(
                    context.Map,
                    context.Pawn,
                    context.ActualWorker,
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    report);
            }
            finally
            {
                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
            }

            report.Info(
                "Manual UI regression",
                "Select a Place order and open its InfoCard once during runtime acceptance; the exact order identity must be shown.");
        }

        private static bool TryCreateContext(ProbeReport report, out ProbeContext context)
        {
            context = null;

            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail("Environment", "No current map is available.");
                return false;
            }

            WorkGiverDef actualWorkGiverDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail(ActualWorkGiverDefName);
            WorkGiverDef deliveryWorkGiverDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail(DeliveryWorkGiverDefName);

            if (actualWorkGiverDef == null || deliveryWorkGiverDef == null)
            {
                report.Fail("Environment", "Required production WorkGiverDefs are not available.");
                return false;
            }

            var actualWorker = actualWorkGiverDef.Worker as WorkGiver_PlaceRelocatableTerrain;
            var deliveryWorker = deliveryWorkGiverDef.Worker as WorkGiver_DeliverRelocationMaterial;

            if (actualWorker == null || deliveryWorker == null)
            {
                report.Fail(
                    "Environment",
                    "Production placement WorkGivers did not instantiate with expected classes.");
                return false;
            }

            Pawn pawn = ResolvePawn(map, requireEmptyCarry: true);

            if (pawn == null)
            {
                report.Fail("Environment", "No spawned free colonist with an empty carry tracker is available.");
                return false;
            }

            if (!TryFindClearRockCell(map, pawn.Position, 4, 30, out IntVec3 targetCell))
            {
                report.Fail("Environment", "Could not find an empty standable natural-rock target cell.");
                return false;
            }

            context = new ProbeContext(
                map,
                pawn,
                targetCell,
                actualWorkGiverDef,
                deliveryWorkGiverDef,
                actualWorker,
                deliveryWorker);

            return true;
        }

        private static void ProbeWorkGiverWiring(ProbeContext context, ProbeReport report)
        {
            const string actualScenario = "Actual WorkGiver wiring";
            const string deliveryScenario = "Delivery WorkGiver wiring";

            WorkTypeDef expectedActualWorkType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(WorkTypeDefName);
            WorkTypeDef expectedDeliveryWorkType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(HaulingWorkTypeDefName);

            bool actualValid = expectedActualWorkType != null &&
                               context.ActualWorkGiverDef.giverClass == typeof(WorkGiver_PlaceRelocatableTerrain) &&
                               context.ActualWorkGiverDef.workType == expectedActualWorkType &&
                               context.ActualWorkGiverDef.scanThings && !context.ActualWorkGiverDef.scanCells;

            if (!actualValid)
            {
                report.Fail(
                    actualScenario,
                    "Expected SoilRelocation thing scanner. giverClass=" + context.ActualWorkGiverDef.giverClass +
                    ", workType=" + (context.ActualWorkGiverDef.workType?.defName ?? "<null>") + ", scanThings=" +
                    context.ActualWorkGiverDef.scanThings + ", scanCells=" + context.ActualWorkGiverDef.scanCells +
                    ".");
            }
            else
            {
                report.Pass(actualScenario, "Actual placement work is a SoilRelocation Thing scanner.");
            }

            bool deliveryValid = expectedDeliveryWorkType != null &&
                                 context.DeliveryWorkGiverDef.giverClass ==
                                 typeof(WorkGiver_DeliverRelocationMaterial) &&
                                 context.DeliveryWorkGiverDef.workType == expectedDeliveryWorkType &&
                                 context.DeliveryWorkGiverDef.scanThings && !context.DeliveryWorkGiverDef.scanCells;

            if (!deliveryValid)
            {
                report.Fail(
                    deliveryScenario,
                    "Expected Hauling thing scanner. giverClass=" + context.DeliveryWorkGiverDef.giverClass +
                    ", workType=" + (context.DeliveryWorkGiverDef.workType?.defName ?? "<null>") + ", scanThings=" +
                    context.DeliveryWorkGiverDef.scanThings + ", scanCells=" + context.DeliveryWorkGiverDef.scanCells +
                    ".");
            }
            else
            {
                report.Pass(deliveryScenario, "Material delivery is a Hauling Thing scanner.");
            }
        }

        private static void ProbeFamilyDefinitions(ProbeReport report)
        {
            foreach (FamilyCase familyCase in _familyCases)
            {
                string scenario = "Family identity / " + familyCase.Name;

                if (!familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
                {
                    report.Fail(scenario, "Expected production order/resource Defs are unavailable.");
                    continue;
                }

                if (!RelocationMaterialOrderUtility.IsBuiltInMaterialOrder(orderDef) ||
                    !RelocationMaterialOrderUtility.TryResolveFamily(orderDef, out RelocationFamilyDef family))
                {
                    report.Fail(
                        scenario,
                        "Order did not resolve through the production material-order family contract.");
                    continue;
                }

                if (!ReferenceEquals(family.CanonicalPortableResource, resourceDef))
                {
                    report.Fail(
                        scenario,
                        "Resolved canonical resource was " + (family.CanonicalPortableResource?.defName ?? "<null>") +
                        ", expected " + resourceDef.defName + ".");
                    continue;
                }

                report.Pass(
                    scenario,
                    "Order resolves to the expected family-owned canonical resource " + resourceDef.defName + ".");
            }
        }

        private static void ProbeDesignatorOrderIdentity(ProbeContext context, ProbeReport report)
        {
            TerrainDef originalTerrain = context.Map.terrainGrid.TerrainAt(context.TargetCell);

            try
            {
                foreach (FamilyCase familyCase in _familyCases)
                {
                    string scenario = "Designator order identity / " + familyCase.Name;

                    if (!familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
                    {
                        report.Fail(scenario, "Expected production order/resource Defs are unavailable.");
                        continue;
                    }

                    CleanupOrdersAt(context.Map, context.TargetCell);
                    RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                    Designator_PlaceRelocatableTerrain designator = familyCase.CreateDesignator();
                    AcceptanceReport acceptance = designator.CanDesignateCell(context.TargetCell);

                    if (!acceptance.Accepted)
                    {
                        report.Fail(
                            scenario,
                            "Production designator rejected the prepared rock cell: " + acceptance.Reason + ".");
                        continue;
                    }

                    designator.DesignateSingleCell(context.TargetCell);

                    RelocationMaterialOrder order = FindOrderAt(context.Map, context.TargetCell, orderDef);

                    if (order == null)
                    {
                        report.Fail(scenario, "Designator did not create the expected material-order ThingDef.");
                        continue;
                    }

                    try
                    {
                        if (!RelocationMaterialOrderUtility.TryResolveFamily(
                                order.def,
                                out RelocationFamilyDef family) || !ReferenceEquals(
                                family.CanonicalPortableResource,
                                resourceDef))
                        {
                            report.Fail(
                                scenario,
                                "Created order did not retain the expected family/resource identity.");
                            continue;
                        }

                        report.Pass(
                            scenario,
                            "Designator creates the exact family-specific order with the expected canonical resource.");
                    }
                    finally
                    {
                        DestroyOrderAndContents(order);
                    }
                }
            }
            finally
            {
                CleanupOrdersAt(context.Map, context.TargetCell);
                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
            }
        }

        private static void ProbeFamilyDeliveryMatrix(ProbeContext context, ProbeReport report)
        {
            TerrainDef originalTerrain = context.Map.terrainGrid.TerrainAt(context.TargetCell);

            try
            {
                foreach (FamilyCase familyCase in _familyCases)
                {
                    string scenario = "Delivery family / " + familyCase.Name;

                    if (!familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
                    {
                        report.Fail(scenario, "Expected production order/resource Defs are unavailable.");
                        continue;
                    }

                    RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
                    ProbeAvailableResource(context, orderDef, resourceDef, scenario, report);
                }
            }
            finally
            {
                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
            }
        }

        private static void ProbeFamilyCompletionMatrix(ProbeContext context, ProbeReport report)
        {
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            if (settings == null)
            {
                report.Skip("Family completion matrix", "Soil Relocation settings are unavailable.");
                return;
            }

            PlacementResolutionMode originalMode = settings.PlacementResolutionMode;
            TerrainDef originalTerrain = context.Map.terrainGrid.TerrainAt(context.TargetCell);

            try
            {
                settings.PlacementResolutionMode = PlacementResolutionMode.Canonical;

                foreach (FamilyCase familyCase in _familyCases)
                {
                    string scenario = "Completion family / " + familyCase.Name;

                    if (!familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
                    {
                        report.Fail(scenario, "Expected production order/resource Defs are unavailable.");
                        continue;
                    }

                    RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
                    var tracked = new List<Thing>();

                    try
                    {
                        RelocationMaterialOrder order = SpawnReadyOrder(
                            orderDef,
                            resourceDef,
                            context.TargetCell,
                            context.Map,
                            tracked);

                        if (!RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family))
                        {
                            report.Fail(scenario, "Order family could not be resolved.");
                            continue;
                        }

                        if (!order.TryGetContainedMaterial(out Thing material))
                        {
                            report.Fail(scenario, "Ready order did not expose its canonical material.");
                            continue;
                        }

                        PlacementValidationResult validation = PlacementValidator.Validate(
                            context.Map,
                            context.TargetCell,
                            family);

                        if (!validation.IsValid || !ReferenceEquals(
                                validation.ResolvedTerrain,
                                family.CanonicalTerrain))
                        {
                            report.Fail(
                                scenario,
                                "Canonical completion did not resolve to the family canonical terrain.");
                            continue;
                        }

                        bool applied = PlacementResultApplier.TryApply(order, validation);

                        if (!applied || !order.Destroyed || !material.Destroyed || !ReferenceEquals(
                                context.Map.terrainGrid.TerrainAt(context.TargetCell),
                                family.CanonicalTerrain))
                        {
                            report.Fail(
                                scenario,
                                "Production completion did not consume exactly one canonical resource and apply terrain.");
                            continue;
                        }

                        report.Pass(
                            scenario,
                            "Family-specific order completes with exact one-resource consumption and canonical terrain mutation.");
                    }
                    finally
                    {
                        DestroyTracked(tracked);
                        RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
                    }
                }
            }
            finally
            {
                settings.PlacementResolutionMode = originalMode;
                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
            }
        }

        private static void ProbeDeliveryJobs(ProbeContext context, ProbeReport report)
        {
            FamilyCase familyCase = ResolveFamilyCase("Soil");

            if (familyCase == null || !familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
            {
                report.Fail("Delivery matrix", "Primary Soil family Defs are unavailable.");
                return;
            }

            ProbeMissingResource(context, orderDef, resourceDef, report);
            ProbeForbiddenResource(context, orderDef, resourceDef, report);
            ProbeUnreachableResource(context, orderDef, resourceDef, report);
            ProbeWrongFamilyResource(context, orderDef, resourceDef, report);
            ProbeReadyOrderRejectsDelivery(context, orderDef, resourceDef, report);
            ProbeAwaitingPermanentInvalidDestination(context, orderDef, resourceDef, report);
            ProbeAwaitingStructuralBlocker(context, orderDef, resourceDef, report);
            ProbeAwaitingFireBlocker(context, orderDef, resourceDef, report);
            ProbeAwaitingPlantPrerequisite(context, orderDef, resourceDef, report);
        }

        private static void ProbeAvailableResource(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            string scenario,
            ProbeReport report)
        {
            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                resourceDef,
                context.Pawn);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);
                Thing resource = SpawnResource(resourceDef, context.Pawn.Position, context.Map, tracked);

                resource.SetForbidden(false, warnOnFail: false);

                Job job = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (!IsExpectedDeliveryJob(job, resource, order))
                {
                    report.Fail(scenario, "Expected controlled HaulToContainer job: " + DescribeJob(job) + ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Available canonical resource produces a one-unit HaulToContainer job targeting the exact order.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeMissingResource(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / missing resource";
            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                resourceDef,
                context.Pawn);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);
                Job job = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (job != null)
                {
                    report.Fail(scenario, "Order produced a delivery job without an available canonical resource.");
                    return;
                }

                report.Pass(scenario, "No delivery job is produced when canonical material is unavailable.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeForbiddenResource(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / forbidden resource";
            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                resourceDef,
                context.Pawn);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);
                Thing resource = SpawnResource(resourceDef, context.Pawn.Position, context.Map, tracked);

                resource.SetForbidden(true, warnOnFail: false);

                Job job = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (job != null)
                {
                    report.Fail(scenario, "Forbidden canonical resource incorrectly produced a delivery job.");
                    return;
                }

                report.Pass(scenario, "Forbidden canonical material is excluded from delivery scheduling.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeUnreachableResource(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / unreachable resource";
            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                resourceDef,
                context.Pawn);

            try
            {
                if (!TrySpawnUnreachableResource(context.Map, context.Pawn, resourceDef, tracked, out Thing resource))
                {
                    report.Skip(
                        scenario,
                        "Current map has no suitable spawned resource location that is unreachable by the probe pawn.");
                    return;
                }

                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);

                resource.SetForbidden(false, warnOnFail: false);

                Job job = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (job != null)
                {
                    report.Fail(scenario, "Unreachable canonical resource incorrectly produced a delivery job.");
                    return;
                }

                report.Pass(scenario, "Unreachable canonical material is excluded from delivery scheduling.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeWrongFamilyResource(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / wrong family resource";
            FamilyCase wrongFamilyCase = ResolveFamilyCase("Gravel");

            if (wrongFamilyCase == null || !wrongFamilyCase.TryResolve(out _, out ThingDef wrongResourceDef))
            {
                report.Skip(scenario, "Gravel portable resource Def is unavailable.");
                return;
            }

            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                resourceDef,
                context.Pawn);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);
                SpawnResource(wrongResourceDef, context.Pawn.Position, context.Map, tracked);

                Job job = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (job != null)
                {
                    report.Fail(
                        scenario,
                        "A different relocation family's resource satisfied the delivery requirement.");
                    return;
                }

                report.Pass(scenario, "Only the order family's canonical portable resource can satisfy delivery.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeReadyOrderRejectsDelivery(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / ready order";
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    context.Map,
                    tracked);

                Job job = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (job != null || order.GetSpaceRemainingWithEnroute(resourceDef, context.Pawn) != 0)
                {
                    report.Fail(scenario, "Ready order still exposed capacity or produced another delivery job.");
                    return;
                }

                report.Pass(scenario, "Ready order exposes zero material capacity and cannot schedule over-delivery.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeAwaitingPermanentInvalidDestination(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / awaiting permanent invalid destination";
            var tracked = new List<Thing>();
            TerrainDef originalTerrain = context.Map.terrainGrid.TerrainAt(context.TargetCell);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);

                if (!RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family) ||
                    family.CanonicalTerrain == null)
                {
                    report.Fail(scenario, "Order family or canonical terrain could not be resolved.");
                    return;
                }

                TerrainDef invalidTerrain = family.CanonicalTerrain;
                int before = CountSpawnedResourcesNear(context.Map, resourceDef, context.TargetCell);

                context.Map.terrainGrid.SetTerrain(context.TargetCell, invalidTerrain);

                PlacementValidationResult validation = PlacementValidator.Validate(
                    context.Map,
                    context.TargetCell,
                    family);

                if (!validation.IsPermanentFailure || validation.Status != PlacementValidationStatus.InvalidDestination)
                {
                    report.Fail(
                        scenario,
                        "Fixture did not produce the expected permanent InvalidDestination result: " +
                        validation.Status + ".");
                    return;
                }

                Job job = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);
                int after = CountSpawnedResourcesNear(context.Map, resourceDef, context.TargetCell);

                if (job != null || !order.Destroyed || after != before || !ReferenceEquals(
                        context.Map.terrainGrid.TerrainAt(context.TargetCell),
                        invalidTerrain))
                {
                    report.Fail(
                        scenario,
                        "Permanent invalidation scheduled delivery, preserved the awaiting order, changed resource " +
                        "accounting, or overwrote the externally changed terrain.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Permanent destination invalidation cancels an AwaitingMaterial order without creating or " +
                    "consuming material.");
            }
            finally
            {
                DestroyTracked(tracked);
                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
            }
        }

        private static void ProbeAwaitingStructuralBlocker(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / awaiting structural blocker";
            ThingDef conduitDef = DefDatabase<ThingDef>.GetNamedSilentFail("PowerConduit");
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            if (conduitDef == null || settings == null)
            {
                report.Skip(scenario, "PowerConduit or Soil Relocation settings are unavailable.");
                return;
            }

            var tracked = new List<Thing>();
            bool originalAllowConduits = settings.AllowRelocationUnderConduits;
            Thing conduit = null;

            try
            {
                settings.AllowRelocationUnderConduits = false;
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);

                if (!RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family))
                {
                    report.Fail(scenario, "Order family could not be resolved.");
                    return;
                }

                int before = CountSpawnedResourcesNear(context.Map, resourceDef, context.TargetCell);
                conduit = ThingMaker.MakeThing(conduitDef);
                GenSpawn.Spawn(conduit, context.TargetCell, context.Map);

                PlacementValidationResult validation = PlacementValidator.Validate(
                    context.Map,
                    context.TargetCell,
                    family);

                if (!validation.IsPermanentFailure || validation.Status != PlacementValidationStatus.StructuralBlocker)
                {
                    report.Fail(
                        scenario,
                        "Fixture did not produce the expected permanent StructuralBlocker result: " +
                        validation.Status + ".");
                    return;
                }

                Job job = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);
                int after = CountSpawnedResourcesNear(context.Map, resourceDef, context.TargetCell);

                if (job != null || !order.Destroyed || conduit.Destroyed || after != before)
                {
                    report.Fail(
                        scenario,
                        "Structural blocker did not cancel the awaiting order cleanly without changing blocker or " +
                        "resource state.");
                    return;
                }

                report.Pass(
                    scenario,
                    "A structural blocker permanently cancels an AwaitingMaterial order without creating or " +
                    "consuming material.");
            }
            finally
            {
                settings.AllowRelocationUnderConduits = originalAllowConduits;

                if (conduit != null && !conduit.Destroyed)
                {
                    conduit.Destroy();
                }

                DestroyTracked(tracked);
            }
        }

        private static void ProbeAwaitingFireBlocker(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / awaiting transient fire";
            ThingDef fireDef = DefDatabase<ThingDef>.GetNamedSilentFail("Fire");

            if (fireDef == null)
            {
                report.Skip(scenario, "Fire ThingDef is unavailable.");
                return;
            }

            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                resourceDef,
                context.Pawn);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);
                Thing resource = SpawnResource(resourceDef, context.Pawn.Position, context.Map, tracked);
                resource.SetForbidden(false, warnOnFail: false);

                Thing fire = ThingMaker.MakeThing(fireDef);
                GenSpawn.Spawn(fire, context.TargetCell, context.Map);
                tracked.Add(fire);

                Job blockedJob = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (blockedJob != null || order.Destroyed || order.IsReadyForWork)
                {
                    report.Fail(scenario, "Fire did not preserve the awaiting order as a transient blocker.");
                    return;
                }

                fire.Destroy();

                Job resumedJob = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (!IsExpectedDeliveryJob(resumedJob, resource, order))
                {
                    report.Fail(scenario, "Material delivery did not become available after the fire was removed.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Fire pauses delivery without cancelling the AwaitingMaterial order, and delivery resumes " +
                    "afterwards.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeAwaitingPlantPrerequisite(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Delivery / awaiting plant prerequisite";
            ThingDef plantDef = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeOak");

            if (plantDef == null)
            {
                report.Skip(scenario, "Plant_TreeOak is unavailable.");
                return;
            }

            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                resourceDef,
                context.Pawn);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);
                Thing resource = SpawnResource(resourceDef, context.Pawn.Position, context.Map, tracked);
                resource.SetForbidden(false, warnOnFail: false);

                Thing plantThing = ThingMaker.MakeThing(plantDef);

                if (!(plantThing is Plant plant))
                {
                    report.Skip(scenario, "Plant_TreeOak did not instantiate as Plant.");
                    plantThing.Destroy();
                    return;
                }

                GenSpawn.Spawn(plant, context.TargetCell, context.Map);
                tracked.Add(plant);
                plant.Growth = 1f;

                Job blockedJob = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);
                bool hasRemovalDesignation =
                    context.Map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) != null ||
                    context.Map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant) != null;

                if (blockedJob != null || order.Destroyed || order.IsReadyForWork || !hasRemovalDesignation)
                {
                    report.Fail(
                        scenario,
                        "Plant prerequisite did not preserve the awaiting order and request vanilla plant removal.");
                    return;
                }

                plant.Destroy();

                Job resumedJob = context.DeliveryWorker.JobOnThing(context.Pawn, order, forced: true);

                if (!IsExpectedDeliveryJob(resumedJob, resource, order))
                {
                    report.Fail(
                        scenario,
                        "Material delivery did not become available after the plant prerequisite was removed.");
                    return;
                }

                report.Pass(
                    scenario,
                    "A removable plant preserves the AwaitingMaterial order, requests vanilla removal, and allows " +
                    "delivery after the prerequisite is gone.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeCarriedResourceReuse(ProbeContext context, ProbeReport report)
        {
            const string scenario = "Delivery / carried resource";
            FamilyCase carriedCase = null;
            Pawn carrier = null;

            foreach (FamilyCase familyCase in _familyCases)
            {
                if (!familyCase.TryResolve(out _, out ThingDef candidateResourceDef))
                {
                    continue;
                }

                Pawn candidateCarrier = context.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn =>
                    pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && ReferenceEquals(
                        pawn.carryTracker?.CarriedThing?.def,
                        candidateResourceDef));

                if (candidateCarrier == null)
                {
                    continue;
                }

                carriedCase = familyCase;
                carrier = candidateCarrier;
                break;
            }

            if (carrier == null)
            {
                report.Skip(
                    scenario,
                    "No free colonist is currently carrying a canonical Soil Relocation resource. " +
                    "Repeat this probe while one is carrying such a resource to exercise this branch.");
                return;
            }

            Thing carriedThing = carrier.carryTracker.CarriedThing;

            if (!carriedCase.TryResolve(out ThingDef orderDef, out ThingDef carriedResourceDef))
            {
                report.Fail(scenario, "Carried resource family Defs became unavailable.");
                return;
            }

            if (!TryFindClearRockCell(context.Map, carrier.Position, 4, 30, out IntVec3 targetCell))
            {
                report.Skip(scenario, "Could not find a valid rock target reachable from the carrier.");
                return;
            }

            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                carriedResourceDef,
                carrier);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, targetCell, context.Map, tracked);
                Job job = context.DeliveryWorker.JobOnThing(carrier, order, forced: true);

                if (!IsExpectedDeliveryJob(job, carriedThing, order))
                {
                    report.Fail(scenario, "WorkGiver did not reuse the already-carried canonical resource.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Already-carried canonical material is reused as TargetA without acquiring another world resource.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeContainerContract(ProbeContext context, ProbeReport report)
        {
            const string scenario = "Material-order container contract";
            FamilyCase familyCase = ResolveFamilyCase("Soil");
            FamilyCase wrongFamilyCase = ResolveFamilyCase("Gravel");

            if (familyCase == null || wrongFamilyCase == null ||
                !familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef) ||
                !wrongFamilyCase.TryResolve(out _, out ThingDef wrongResourceDef))
            {
                report.Fail(scenario, "Required Soil/Gravel test Defs are unavailable.");
                return;
            }

            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);

                if (order.IsReadyForWork || order.SpaceRemainingFor(resourceDef) != 1 ||
                    order.SpaceRemainingFor(wrongResourceDef) != 0)
                {
                    report.Fail(
                        scenario,
                        "Empty order did not expose exactly one unit of canonical-resource capacity.");
                    return;
                }

                Thing resource = ThingMaker.MakeThing(resourceDef);
                resource.stackCount = 1;

                if (!order.GetDirectlyHeldThings().TryAdd(resource))
                {
                    resource.Destroy();
                    report.Fail(scenario, "Order holder rejected its canonical resource.");
                    return;
                }

                tracked.Add(resource);

                if (!order.IsReadyForWork || order.SpaceRemainingFor(resourceDef) != 0 ||
                    order.GetSpaceRemainingWithEnroute(resourceDef, context.Pawn) != 0 ||
                    !order.TryGetContainedMaterial(out Thing contained) || !ReferenceEquals(contained, resource))
                {
                    report.Fail(scenario, "Canonical deposit did not transition the order to the exact ready state.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Readiness is derived from one contained canonical resource and closes further delivery capacity.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeNearbyOrderIsolation(ProbeContext context, ProbeReport report)
        {
            const string scenario = "Delivery / nearby-order isolation";
            FamilyCase familyCase = ResolveFamilyCase("Soil");

            if (familyCase == null || !familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
            {
                report.Fail(scenario, "Primary Soil family Defs are unavailable.");
                return;
            }

            if (!TryFindClearRockCells(context.Map, context.Pawn.Position, 4, 30, 2, out List<IntVec3> cells))
            {
                report.Skip(scenario, "Could not find two suitable natural-rock target cells.");
                return;
            }

            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(
                context.Map,
                resourceDef,
                context.Pawn);

            try
            {
                RelocationMaterialOrder firstOrder = SpawnOrder(orderDef, cells[0], context.Map, tracked);
                RelocationMaterialOrder secondOrder = SpawnOrder(orderDef, cells[1], context.Map, tracked);
                Thing firstResource = SpawnResource(resourceDef, context.Pawn.Position, context.Map, tracked);
                Thing secondResource = SpawnResource(resourceDef, context.Pawn.Position, context.Map, tracked);

                Job firstJob = context.DeliveryWorker.JobOnThing(context.Pawn, firstOrder, forced: true);
                Job secondJob = context.DeliveryWorker.JobOnThing(context.Pawn, secondOrder, forced: true);

                if (!IsExpectedDeliveryJobForOrder(firstJob, firstOrder) ||
                    !IsExpectedDeliveryJobForOrder(secondJob, secondOrder))
                {
                    report.Fail(
                        scenario,
                        "One of the nearby orders did not receive an exact single-destination delivery job. first=" +
                        DescribeJob(firstJob) + ", second=" + DescribeJob(secondJob) + ".");
                    return;
                }

                if (firstJob.targetB.Thing == secondOrder || firstJob.targetC.Thing == secondOrder ||
                    secondJob.targetB.Thing == firstOrder || secondJob.targetC.Thing == firstOrder)
                {
                    report.Fail(scenario, "Delivery job leaked a second material order into its target set.");
                    return;
                }

                if (!ReferenceEquals(firstJob.targetA.Thing, firstResource) &&
                    !ReferenceEquals(firstJob.targetA.Thing, secondResource))
                {
                    report.Fail(scenario, "First delivery job did not use one of the prepared canonical resources.");
                    return;
                }

                if (!ReferenceEquals(secondJob.targetA.Thing, firstResource) &&
                    !ReferenceEquals(secondJob.targetA.Thing, secondResource))
                {
                    report.Fail(scenario, "Second delivery job did not use one of the prepared canonical resources.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Each HaulToContainer job targets exactly one material order and never carries a second destination.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeCancellation(ProbeContext context, ProbeReport report)
        {
            FamilyCase familyCase = ResolveFamilyCase("Soil");

            if (familyCase == null || !familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
            {
                report.Fail("Cancellation", "Primary Soil family Defs are unavailable.");
                return;
            }

            ProbeCancellationBeforeDelivery(context, orderDef, resourceDef, report);
            ProbeCancellationAfterDelivery(context, orderDef, resourceDef, report);
        }

        private static void ProbeCancellationBeforeDelivery(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Cancellation / before delivery";
            var tracked = new List<Thing>();
            int before = CountSpawnedResourcesNear(context.Map, resourceDef, context.TargetCell);

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, context.TargetCell, context.Map, tracked);

                bool cancelled = order.TryCancel();
                int after = CountSpawnedResourcesNear(context.Map, resourceDef, context.TargetCell);

                if (!cancelled || !order.Destroyed || after != before)
                {
                    report.Fail(
                        scenario,
                        "Empty order cancellation changed resource accounting or left the order alive.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Cancelling an awaiting order removes it without creating or consuming material.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeCancellationAfterDelivery(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            ProbeReport report)
        {
            const string scenario = "Cancellation / after delivery";
            var tracked = new List<Thing>();
            int before = CountSpawnedResourcesNear(context.Map, resourceDef, context.TargetCell);

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    context.Map,
                    tracked);

                if (!order.TryGetContainedMaterial(out Thing material))
                {
                    report.Fail(scenario, "Ready order did not expose contained material before cancellation.");
                    return;
                }

                bool cancelled = order.TryCancel();
                int after = CountSpawnedResourcesNear(context.Map, resourceDef, context.TargetCell);

                if (!cancelled || !order.Destroyed || material.Destroyed || !material.Spawned ||
                    material.stackCount != 1 || material.def != resourceDef ||
                    material.Position.DistanceToSquared(context.TargetCell) > CleanupRadiusSquared ||
                    after != before + 1)
                {
                    report.Fail(scenario, "Ready-order cancellation did not refund exactly one contained resource.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Cancelling a ready order drops its authoritative contained material exactly once.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeResolutionModes(ProbeContext context, ProbeReport report)
        {
            FamilyCase familyCase = ResolveFamilyCase("Soil");
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            if (familyCase == null || !familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef) ||
                settings == null)
            {
                report.Skip("Placement resolution modes", "Required Soil family Defs or settings are unavailable.");
                return;
            }

            PlacementResolutionMode originalMode = settings.PlacementResolutionMode;
            TerrainDef originalTerrain = context.Map.terrainGrid.TerrainAt(context.TargetCell);

            try
            {
                ProbeResolutionMode(context, orderDef, resourceDef, PlacementResolutionMode.Canonical, report);

                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);

                ProbeResolutionMode(context, orderDef, resourceDef, PlacementResolutionMode.BiomeNative, report);
            }
            finally
            {
                settings.PlacementResolutionMode = originalMode;
                RestoreTerrain(context.Map, context.TargetCell, originalTerrain);
            }
        }

        private static void ProbeResolutionMode(
            ProbeContext context,
            ThingDef orderDef,
            ThingDef resourceDef,
            PlacementResolutionMode mode,
            ProbeReport report)
        {
            string scenario = "Placement resolution / " + mode;
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;
            var tracked = new List<Thing>();

            if (settings == null)
            {
                report.Skip(scenario, "Soil Relocation settings are unavailable.");
                return;
            }

            try
            {
                settings.PlacementResolutionMode = mode;

                RelocationMaterialOrder order = SpawnReadyOrder(
                    orderDef,
                    resourceDef,
                    context.TargetCell,
                    context.Map,
                    tracked);

                if (!RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family))
                {
                    report.Fail(scenario, "Order family could not be resolved.");
                    return;
                }

                PlacementValidationResult validation = PlacementValidator.Validate(
                    context.Map,
                    context.TargetCell,
                    family);

                if (!validation.IsValid || validation.ResolvedTerrain == null)
                {
                    report.Fail(
                        scenario,
                        "Prepared order did not produce a valid resolved terrain: " + validation.Status + ".");
                    return;
                }

                if (mode == PlacementResolutionMode.Canonical && !ReferenceEquals(
                        validation.ResolvedTerrain,
                        family.CanonicalTerrain))
                {
                    report.Fail(scenario, "Canonical mode did not resolve to the family's canonical terrain.");
                    return;
                }

                bool applied = PlacementResultApplier.TryApply(order, validation);

                if (!applied || !order.Destroyed || !ReferenceEquals(
                        context.Map.terrainGrid.TerrainAt(context.TargetCell),
                        validation.ResolvedTerrain))
                {
                    report.Fail(scenario, "Resolved terrain was not applied through the production completion path.");
                    return;
                }

                bool usedCanonical = ReferenceEquals(validation.ResolvedTerrain, family.CanonicalTerrain);
                string resolution = usedCanonical
                    ? "canonical terrain"
                    : "distinct biome-native terrain " + validation.ResolvedTerrain.defName;

                report.Pass(
                    scenario,
                    mode + " mode completed successfully using " + resolution +
                    " with exact one-resource consumption.");

                if (mode == PlacementResolutionMode.BiomeNative && usedCanonical)
                {
                    report.Skip(
                        "Placement resolution / BiomeNative distinct candidate",
                        "The current biome has no unique distinct native Soil-family candidate; canonical fallback was verified.");
                }
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeAwaitingOrder(
            Map map,
            Pawn pawn,
            WorkGiver_PlaceRelocatableTerrain worker,
            ThingDef orderDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Awaiting material";
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnOrder(orderDef, targetCell, map, tracked);
                bool scannerSeesOrder = worker.PotentialWorkThingsGlobal(pawn).Contains(order);
                Job job = worker.JobOnThing(pawn, order, forced: true);

                if (!scannerSeesOrder)
                {
                    report.Fail(scenario, "The production scanner did not expose the material order.");
                    return;
                }

                if (order.IsReadyForWork || job != null)
                {
                    report.Fail(scenario, "An empty material order incorrectly produced actual relocation work.");
                    return;
                }

                report.Pass(
                    scenario,
                    "AwaitingMaterial order is visible to the scanner but does not produce actual work.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeReadyOrder(
            Map map,
            Pawn pawn,
            WorkGiver_PlaceRelocatableTerrain worker,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Ready for work";
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);
                Job job = worker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedWorkJob(job, order))
                {
                    report.Fail(
                        scenario,
                        "Ready order did not produce the expected single-target placement job: " + DescribeJob(job) +
                        ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "ReadyForWork order produces a placement job whose only material-order target is TargetA.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeWorldResourceIndependence(
            Map map,
            Pawn pawn,
            WorkGiver_PlaceRelocatableTerrain worker,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Delivery separated from work";
            var tracked = new List<Thing>();
            List<ResourceForbiddenState> resourceStates = CaptureAndForbidWorldResources(map, resourceDef, pawn);

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);
                Job job = worker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedWorkJob(job, order))
                {
                    report.Fail(
                        scenario,
                        "Actual work still depended on a separate world resource: " + DescribeJob(job) + ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Actual relocation work remains available without acquiring another world resource.");
            }
            finally
            {
                RestoreResourceStates(resourceStates);
                DestroyTracked(tracked);
            }
        }

        private static void ProbeSuccessfulCompletion(
            Map map,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Successful completion / exact once";
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);

                if (!RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family))
                {
                    report.Fail(scenario, "Order family could not be resolved.");
                    return;
                }

                TerrainDef terrainBefore = map.terrainGrid.TerrainAt(targetCell);
                PlacementValidationResult validation = PlacementValidator.Validate(map, targetCell, family);

                if (!validation.IsValid)
                {
                    report.Skip(scenario, "Prepared target is not currently valid: " + validation.Status + ".");
                    return;
                }

                order.TryGetContainedMaterial(out Thing containedBefore);

                bool firstApplied = PlacementResultApplier.TryApply(order, validation);
                bool secondApplied = PlacementResultApplier.TryApply(order, validation);

                if (!firstApplied || secondApplied || !order.Destroyed || containedBefore == null ||
                    !containedBefore.Destroyed ||
                    !ReferenceEquals(map.terrainGrid.TerrainAt(targetCell), validation.ResolvedTerrain) ||
                    ReferenceEquals(terrainBefore, map.terrainGrid.TerrainAt(targetCell)))
                {
                    report.Fail(
                        scenario,
                        "Completion did not mutate exactly the order target and consume its contained material once.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Completion applies the resolved terrain, consumes one contained resource, removes the order, and cannot apply twice.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeStaleDestination(
            Map map,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Completion / stale destination";
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);

                if (!RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family))
                {
                    report.Fail(scenario, "Order family could not be resolved.");
                    return;
                }

                PlacementValidationResult validation = PlacementValidator.Validate(map, targetCell, family);

                if (!validation.IsValid || validation.ResolvedTerrain == null)
                {
                    report.Skip(scenario, "Prepared target is not currently valid: " + validation.Status + ".");
                    return;
                }

                if (!order.TryGetContainedMaterial(out Thing material))
                {
                    report.Fail(scenario, "Ready order did not expose contained material.");
                    return;
                }

                map.terrainGrid.SetTerrain(targetCell, validation.ResolvedTerrain);

                bool applied = PlacementResultApplier.TryApply(order, validation);

                if (applied || order.Destroyed || !order.IsReadyForWork || material.Destroyed || !ReferenceEquals(
                        map.terrainGrid.TerrainAt(targetCell),
                        validation.ResolvedTerrain))
                {
                    report.Fail(
                        scenario,
                        "Stale validation consumed material or overwrote the externally changed destination.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Completion rejects stale destination state without consuming the contained resource or mutating terrain again.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbePermanentInvalidation(
            Map map,
            Pawn pawn,
            WorkGiver_PlaceRelocatableTerrain worker,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Permanent invalidation";
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);

                if (!RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family))
                {
                    report.Fail(scenario, "Order family could not be resolved.");
                    return;
                }

                TerrainDef invalidTerrain = family.CanonicalTerrain;

                if (invalidTerrain == null)
                {
                    report.Skip(scenario, "Family has no canonical terrain.");
                    return;
                }

                int before = CountSpawnedResourcesNear(map, resourceDef, targetCell);

                map.terrainGrid.SetTerrain(targetCell, invalidTerrain);

                Job job = worker.JobOnThing(pawn, order, forced: true);

                if (job != null)
                {
                    report.Fail(scenario, "Permanently invalid destination still produced actual work.");
                    return;
                }

                if (!order.Destroyed)
                {
                    report.Fail(scenario, "Permanently invalid order was not cancelled.");
                    return;
                }

                int after = CountSpawnedResourcesNear(map, resourceDef, targetCell);

                if (after != before + 1)
                {
                    report.Fail(scenario, "Cancelled ready order did not return exactly one contained resource.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Permanent destination failure cancels the order and returns exactly one authoritative contained resource.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeFireBlocker(
            Map map,
            Pawn pawn,
            WorkGiver_PlaceRelocatableTerrain worker,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Transient fire";
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);
                ThingDef fireDef = DefDatabase<ThingDef>.GetNamedSilentFail("Fire");

                if (fireDef == null)
                {
                    report.Skip(scenario, "Fire ThingDef is unavailable.");
                    return;
                }

                Thing fire = ThingMaker.MakeThing(fireDef);
                GenSpawn.Spawn(fire, targetCell, map);
                tracked.Add(fire);

                Job blockedJob = worker.JobOnThing(pawn, order, forced: true);

                if (blockedJob != null || order.Destroyed || !order.IsReadyForWork)
                {
                    report.Fail(scenario, "Fire did not preserve the ready order as a transient blocker.");
                    return;
                }

                fire.Destroy();

                Job resumedJob = worker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedWorkJob(resumedJob, order))
                {
                    report.Fail(scenario, "Work did not become available again after the fire was removed.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Fire pauses actual work without consuming or cancelling the ready order, and work resumes afterwards.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbePlantPrerequisite(
            Map map,
            Pawn pawn,
            WorkGiver_PlaceRelocatableTerrain worker,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Plant prerequisite";
            var tracked = new List<Thing>();

            try
            {
                ThingDef plantDef = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeOak");

                if (plantDef == null)
                {
                    report.Skip(scenario, "Plant_TreeOak is unavailable.");
                    return;
                }

                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);
                Thing plantThing = ThingMaker.MakeThing(plantDef);

                if (!(plantThing is Plant plant))
                {
                    report.Skip(scenario, "Plant_TreeOak did not instantiate as Plant.");
                    plantThing.Destroy();
                    return;
                }

                GenSpawn.Spawn(plant, targetCell, map);
                tracked.Add(plant);
                plant.Growth = 1f;

                Job job = worker.JobOnThing(pawn, order, forced: true);

                bool hasRemovalDesignation =
                    map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) != null ||
                    map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant) != null;

                if (job != null || order.Destroyed || !order.IsReadyForWork || !hasRemovalDesignation)
                {
                    report.Fail(
                        scenario,
                        "Plant prerequisite did not preserve the ready order and schedule vanilla plant removal.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Plant prerequisite blocks relocation work while preserving contained material and requesting plant removal.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void ProbeSkillEligibility(
            Map map,
            Pawn pawn,
            WorkGiver_PlaceRelocatableTerrain worker,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Skill eligibility";
            var tracked = new List<Thing>();
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            if (settings == null)
            {
                report.Skip(scenario, "Soil Relocation settings are unavailable.");
                return;
            }

            RelocationSkillSelection originalSelection = settings.PlacementSkills;

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);

                settings.PlacementSkills = RelocationSkillSelection.None;

                Job noneJob = worker.JobOnThing(pawn, order, forced: true);
                bool expectedNoneEligibility = RelocationWorkUtility.CanPerform(pawn, RelocationSkillSelection.None);

                if ((noneJob != null) != expectedNoneEligibility)
                {
                    report.Fail(
                        scenario,
                        "WorkGiver did not preserve the established None-skill eligibility contract.");
                    return;
                }

                settings.PlacementSkills = RelocationSkillSelection.Mining | RelocationSkillSelection.Construction;

                bool expectedMultiEligibility = RelocationWorkUtility.CanPerform(pawn, settings.PlacementSkills);
                Job multiJob = worker.JobOnThing(pawn, order, forced: true);

                if ((multiJob != null) != expectedMultiEligibility)
                {
                    report.Fail(
                        scenario,
                        "WorkGiver did not delegate multi-skill eligibility to RelocationWorkUtility.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Actual-work scheduling continues to use the existing RelocationWorkUtility eligibility contract.");
            }
            finally
            {
                settings.PlacementSkills = originalSelection;
                DestroyTracked(tracked);
            }
        }

        private static void ProbeInterruptionInvariant(
            Map map,
            Pawn pawn,
            WorkGiver_PlaceRelocatableTerrain worker,
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 targetCell,
            ProbeReport report)
        {
            const string scenario = "Interruption invariant";
            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder order = SpawnReadyOrder(orderDef, resourceDef, targetCell, map, tracked);

                if (!order.TryGetContainedMaterial(out Thing material))
                {
                    report.Fail(scenario, "Ready order did not expose contained material.");
                    return;
                }

                Job firstJob = worker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedWorkJob(firstJob, order))
                {
                    report.Fail(scenario, "Could not create the initial actual-work job.");
                    return;
                }

                if (order.Destroyed || !order.IsReadyForWork || material.Destroyed)
                {
                    report.Fail(scenario, "Creating an actual-work attempt prematurely consumed the order material.");
                    return;
                }

                Job secondJob = worker.JobOnThing(pawn, order, forced: true);

                if (!IsExpectedWorkJob(secondJob, order))
                {
                    report.Fail(scenario, "Ready order was not available for a later work attempt.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Starting or abandoning an actual-work attempt does not consume material; the ready order remains reusable.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void PrepareSaveLoadCases(ProbeReport report)
        {
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail("Save-load setup", "No current map is available.");
                return;
            }

            FamilyCase familyCase = ResolveFamilyCase("Soil");

            if (familyCase == null || !familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
            {
                report.Fail("Save-load setup", "Primary Soil family Defs are unavailable.");
                return;
            }

            Pawn pawn = ResolvePawn(map, requireEmptyCarry: false);

            if (pawn == null || !TryFindClearRockCells(map, pawn.Position, 4, 30, 2, out List<IntVec3> cells))
            {
                report.Fail("Save-load setup", "Could not resolve a pawn and two clear natural-rock cells.");
                return;
            }

            if (_saveLoadState != null)
            {
                report.Fail(
                    "Save-load setup",
                    "A prepared save-load probe already exists. Verify that probe before preparing another one.");
                return;
            }

            TerrainDef awaitingOriginalTerrain = map.terrainGrid.TerrainAt(cells[0]);
            TerrainDef readyOriginalTerrain = map.terrainGrid.TerrainAt(cells[1]);

            RelocationMaterialOrder awaitingOrder = null;
            RelocationMaterialOrder readyOrder = null;

            try
            {
                awaitingOrder = SpawnOrder(orderDef, cells[0], map, null);
                readyOrder = SpawnReadyOrder(orderDef, resourceDef, cells[1], map, null);

                _saveLoadState = new SaveLoadProbeState(
                    map.Parent?.ID ?? -1,
                    cells[0],
                    cells[1],
                    orderDef.defName,
                    resourceDef.defName,
                    awaitingOriginalTerrain,
                    readyOriginalTerrain);

                report.Pass(
                    "Save-load setup",
                    "Prepared one awaiting and one ready material order. Keep the game paused, save, load that save, then run " +
                    "'Verify placement save-load cases'.");
            }
            catch
            {
                DestroyOrderAndContents(awaitingOrder);
                DestroyOrderAndContents(readyOrder);
                RestoreTerrain(map, cells[0], awaitingOriginalTerrain);
                RestoreTerrain(map, cells[1], readyOriginalTerrain);
                _saveLoadState = null;
                throw;
            }
        }

        private static void VerifySaveLoadCases(ProbeReport report)
        {
            const string scenario = "Save-load lifecycle";
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail(scenario, "No current map is available.");
                return;
            }

            if (_saveLoadState == null)
            {
                report.Fail(scenario, "No prepared save-load state exists in this game process.");
                return;
            }

            SaveLoadProbeState state = _saveLoadState;

            if ((map.Parent?.ID ?? -1) != state.MapParentId)
            {
                report.Fail(scenario, "The current map is not the map on which the save-load probe was prepared.");
                return;
            }

            ThingDef orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.OrderDefName);
            ThingDef resourceDef = DefDatabase<ThingDef>.GetNamedSilentFail(state.ResourceDefName);

            if (orderDef == null || resourceDef == null)
            {
                report.Fail(scenario, "Prepared order/resource Defs are unavailable after load.");
                CleanupSaveLoadState(map);
                return;
            }

            RelocationMaterialOrder awaitingOrder = FindOrderAt(map, state.AwaitingCell, orderDef);
            RelocationMaterialOrder readyOrder = FindOrderAt(map, state.ReadyCell, orderDef);

            try
            {
                bool awaitingValid = awaitingOrder != null && !awaitingOrder.Destroyed &&
                                     !awaitingOrder.IsReadyForWork && awaitingOrder.GetDirectlyHeldThings().Count == 0;

                bool readyValid = readyOrder != null && !readyOrder.Destroyed && readyOrder.IsReadyForWork &&
                                  readyOrder.GetDirectlyHeldThings().Count == 1 &&
                                  readyOrder.TryGetContainedMaterial(out Thing material) && material != null &&
                                  material.def == resourceDef && material.stackCount == 1;

                if (!awaitingValid || !readyValid)
                {
                    report.Fail(
                        scenario,
                        "Awaiting/ready order state or the ready order's serialized ThingOwner did not survive save-load.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Awaiting and ready orders survive save-load; readiness is reconstructed from one serialized contained canonical resource.");
            }
            finally
            {
                CleanupSaveLoadState(map);
            }
        }

        private static void PrepareConcurrentDelivery(ProbeReport report)
        {
            const string scenario = "Concurrent delivery setup";
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail(scenario, "No current map is available.");
                return;
            }

            FamilyCase familyCase = ResolveFamilyCase("Soil");

            if (familyCase == null || !familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
            {
                report.Fail(scenario, "Primary Soil family Defs are unavailable.");
                return;
            }

            List<Pawn> pawns = ResolvePawns(map, 2, requireEmptyCarry: true);

            if (pawns.Count < 2 || !TryFindClearRockCells(map, pawns[0].Position, 4, 30, 2, out List<IntVec3> cells))
            {
                report.Fail(scenario, "Could not resolve two empty-handed pawns and two clear natural-rock cells.");
                return;
            }

            CleanupConcurrentDeliveryState();

            var tracked = new List<Thing>();

            try
            {
                RelocationMaterialOrder firstOrder = SpawnOrder(orderDef, cells[0], map, tracked);
                RelocationMaterialOrder secondOrder = SpawnOrder(orderDef, cells[1], map, tracked);
                Thing firstResource = SpawnResource(resourceDef, pawns[0].Position, map, tracked);
                Thing secondResource = SpawnResource(resourceDef, pawns[1].Position, map, tracked);

                if (!RelocationMaterialOrderUtility.TryResolveFamily(orderDef, out RelocationFamilyDef family))
                {
                    throw new InvalidOperationException("Prepared order family could not be resolved.");
                }

                PlacementValidationResult firstValidation = PlacementValidator.Validate(map, cells[0], family);
                PlacementValidationResult secondValidation = PlacementValidator.Validate(map, cells[1], family);

                if (!firstValidation.IsValid || !secondValidation.IsValid)
                {
                    throw new InvalidOperationException(
                        "Prepared concurrent targets are not valid: " + firstValidation.Status + ", " +
                        secondValidation.Status + ".");
                }

                _concurrentDeliveryState = new ConcurrentDeliveryProbeState(
                    map,
                    firstOrder,
                    secondOrder,
                    firstResource,
                    secondResource,
                    cells[0],
                    cells[1],
                    map.terrainGrid.TerrainAt(cells[0]),
                    map.terrainGrid.TerrainAt(cells[1]),
                    firstValidation.ResolvedTerrain,
                    secondValidation.ResolvedTerrain);

                tracked.Clear();

                report.Pass(
                    scenario,
                    "Prepared two orders and two canonical resources. Unpause and let normal Hauling/SoilRelocation AI run, " +
                    "then run 'Verify concurrent placement delivery'.");
            }
            finally
            {
                DestroyTracked(tracked);
            }
        }

        private static void VerifyConcurrentDelivery(ProbeReport report)
        {
            const string scenario = "Concurrent delivery lifecycle";

            if (_concurrentDeliveryState == null)
            {
                report.Fail(scenario, "No prepared concurrent-delivery state exists.");
                return;
            }

            ConcurrentDeliveryProbeState state = _concurrentDeliveryState;

            if (Find.CurrentMap != state.Map)
            {
                report.Fail(scenario, "Current map is not the map on which concurrent delivery was prepared.");
                return;
            }

            OperationRuntimeState firstState = EvaluateConcurrentOperation(
                state.FirstOrder,
                state.FirstCell,
                state.FirstExpectedTerrain,
                state.Map);

            OperationRuntimeState secondState = EvaluateConcurrentOperation(
                state.SecondOrder,
                state.SecondCell,
                state.SecondExpectedTerrain,
                state.Map);

            if (firstState == OperationRuntimeState.Pending || secondState == OperationRuntimeState.Pending)
            {
                report.Skip(
                    scenario,
                    "At least one prepared operation is still awaiting delivery/work. Let normal AI continue and run verification again.");
                return;
            }

            try
            {
                if (firstState == OperationRuntimeState.Invalid || secondState == OperationRuntimeState.Invalid)
                {
                    report.Fail(scenario, "At least one operation reached an invalid material/order/terrain state.");
                    return;
                }

                if (!ValidateConcurrentResourceAccounting(state, firstState, secondState))
                {
                    report.Fail(
                        scenario,
                        "Concurrent delivery violated exact two-resource accounting or left material outside terminal states.");
                    return;
                }

                if (!ValidateReadyOrderNoFurtherDelivery(state.FirstOrder, state.FirstResource, state.Map) ||
                    !ValidateReadyOrderNoFurtherDelivery(state.SecondOrder, state.SecondResource, state.Map))
                {
                    report.Fail(scenario, "A ready order still admitted a duplicate delivery job.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Both concurrent operations received at most one canonical resource and reached ReadyForWork or successful completion without cross-order delivery.");
            }
            finally
            {
                CleanupConcurrentDeliveryState();
            }
        }

        private static void PrepareEndToEndLifecycle(ProbeReport report)
        {
            const string scenario = "End-to-end setup";
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail(scenario, "No current map is available.");
                return;
            }

            Pawn pawn = ResolvePawn(map, requireEmptyCarry: true);

            if (pawn == null || !TryFindClearRockCells(
                    map,
                    pawn.Position,
                    4,
                    30,
                    _familyCases.Length,
                    out List<IntVec3> cells))
            {
                report.Fail(scenario, "Could not resolve an empty-handed pawn and four clear natural-rock targets.");
                return;
            }

            CleanupEndToEndState();

            var operations = new List<EndToEndOperationState>();
            var createdOrders = new List<RelocationMaterialOrder>();
            var createdResources = new List<Thing>();
            var originalTerrains = new List<TerrainDef>();

            try
            {
                for (var i = 0; i < _familyCases.Length; i++)
                {
                    FamilyCase familyCase = _familyCases[i];
                    IntVec3 targetCell = cells[i];

                    if (!familyCase.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
                    {
                        throw new InvalidOperationException(
                            "Required Defs are unavailable for family " + familyCase.Name + ".");
                    }

                    TerrainDef originalTerrain = map.terrainGrid.TerrainAt(targetCell);
                    originalTerrains.Add(originalTerrain);

                    Designator_PlaceRelocatableTerrain designator = familyCase.CreateDesignator();
                    AcceptanceReport acceptance = designator.CanDesignateCell(targetCell);

                    if (!acceptance.Accepted)
                    {
                        throw new InvalidOperationException(
                            "Production designator rejected " + familyCase.Name + " target: " + acceptance.Reason +
                            ".");
                    }

                    designator.DesignateSingleCell(targetCell);

                    RelocationMaterialOrder order = FindOrderAt(map, targetCell, orderDef);

                    if (order == null)
                    {
                        throw new InvalidOperationException(
                            "Production designator did not create the expected " + familyCase.Name + " order.");
                    }

                    createdOrders.Add(order);

                    Thing resource = SpawnResource(resourceDef, pawn.Position, map, null);
                    createdResources.Add(resource);

                    if (!RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family))
                    {
                        throw new InvalidOperationException(
                            "Prepared " + familyCase.Name + " order family could not be resolved.");
                    }

                    PlacementValidationResult validation = PlacementValidator.Validate(map, targetCell, family);

                    if (!validation.IsValid || validation.ResolvedTerrain == null)
                    {
                        throw new InvalidOperationException(
                            "Prepared " + familyCase.Name + " target became invalid: " + validation.Status + ".");
                    }

                    operations.Add(
                        new EndToEndOperationState(
                            familyCase.Name,
                            order,
                            resource,
                            targetCell,
                            originalTerrain,
                            validation.ResolvedTerrain));
                }

                _endToEndState = new EndToEndProbeState(map, operations);

                report.Pass(
                    scenario,
                    "Prepared all four production-designator orders plus their canonical world resources. " +
                    "Unpause and let normal Hauling and SoilRelocation work finish, then run " +
                    "'Verify full placement lifecycle'.");
            }
            catch
            {
                for (var i = 0; i < createdOrders.Count; i++)
                {
                    DestroyOrderAndContents(createdOrders[i]);
                }

                for (var i = 0; i < createdResources.Count; i++)
                {
                    Thing resource = createdResources[i];

                    if (resource != null && !resource.Destroyed)
                    {
                        resource.Destroy();
                    }
                }

                for (var i = 0; i < originalTerrains.Count; i++)
                {
                    RestoreTerrain(map, cells[i], originalTerrains[i]);
                }

                _endToEndState = null;
                throw;
            }
        }

        private static void VerifyEndToEndLifecycle(ProbeReport report)
        {
            const string scenario = "End-to-end lifecycle";

            if (_endToEndState == null)
            {
                report.Fail(scenario, "No prepared full-lifecycle state exists.");
                return;
            }

            EndToEndProbeState state = _endToEndState;

            if (Find.CurrentMap != state.Map)
            {
                report.Fail(scenario, "Current map is not the map on which the lifecycle probe was prepared.");
                return;
            }

            var active = state.Operations.Where(operation => operation.Order != null && !operation.Order.Destroyed)
                .ToList();

            if (active.Count > 0)
            {
                report.Skip(
                    scenario,
                    active.Count + " of " + state.Operations.Count +
                    " operations are still active. Let normal AI continue and run verification again.");
                return;
            }

            try
            {
                var failed = false;

                foreach (EndToEndOperationState operation in state.Operations)
                {
                    string familyScenario = scenario + " / " + operation.FamilyName;

                    if (operation.Resource == null || !operation.Resource.Destroyed || !ReferenceEquals(
                            state.Map.terrainGrid.TerrainAt(operation.TargetCell),
                            operation.ExpectedTerrain))
                    {
                        report.Fail(
                            familyScenario,
                            "Terminal state did not consume the canonical resource and apply the expected terrain.");
                        failed = true;
                        continue;
                    }

                    report.Pass(
                        familyScenario,
                        "Production designator, Hauling delivery, actual work, exact consumption, and terrain mutation completed.");
                }

                if (!failed)
                {
                    report.Pass(
                        scenario,
                        "All four families completed the production designator → Hauling → HaulToContainer → " +
                        "ReadyForWork → actual-work lifecycle without resource duplication or loss.");
                }
            }
            finally
            {
                CleanupEndToEndState();
            }
        }

        private static OperationRuntimeState EvaluateConcurrentOperation(
            RelocationMaterialOrder order,
            IntVec3 cell,
            TerrainDef expectedTerrain,
            Map map)
        {
            if (order == null)
            {
                return OperationRuntimeState.Invalid;
            }

            if (order.Destroyed)
            {
                return ReferenceEquals(map.terrainGrid.TerrainAt(cell), expectedTerrain)
                    ? OperationRuntimeState.Completed
                    : OperationRuntimeState.Invalid;
            }

            if (!order.Spawned || order.Map != map)
            {
                return OperationRuntimeState.Invalid;
            }

            return order.TryGetContainedMaterial(out _) && order.IsReadyForWork
                ? OperationRuntimeState.Ready
                : OperationRuntimeState.Pending;
        }

        private static bool ValidateConcurrentResourceAccounting(
            ConcurrentDeliveryProbeState state,
            OperationRuntimeState firstState,
            OperationRuntimeState secondState)
        {
            Thing[] resources = { state.FirstResource, state.SecondResource };
            RelocationMaterialOrder[] orders = { state.FirstOrder, state.SecondOrder };
            OperationRuntimeState[] states = { firstState, secondState };
            var contained = new HashSet<Thing>();
            var completedCount = 0;

            for (var i = 0; i < states.Length; i++)
            {
                if (states[i] == OperationRuntimeState.Completed)
                {
                    completedCount++;
                    continue;
                }

                if (states[i] != OperationRuntimeState.Ready ||
                    !orders[i].TryGetContainedMaterial(out Thing material) || material == null ||
                    (material != resources[0] && material != resources[1]) || !contained.Add(material))
                {
                    return false;
                }
            }

            int destroyedCount = resources.Count(resource => resource == null || resource.Destroyed);

            if (destroyedCount != completedCount)
            {
                return false;
            }

            foreach (Thing resource in resources)
            {
                if (resource == null)
                {
                    return false;
                }

                if (!resource.Destroyed && !contained.Contains(resource))
                {
                    return false;
                }
            }

            return contained.Count + completedCount == resources.Length;
        }

        private static bool ValidateReadyOrderNoFurtherDelivery(RelocationMaterialOrder order, Thing resource, Map map)
        {
            if (order == null || order.Destroyed || !order.IsReadyForWork)
            {
                return true;
            }

            Pawn pawn = ResolvePawn(map, requireEmptyCarry: true);

            if (pawn == null)
            {
                return true;
            }

            WorkGiverDef deliveryDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail(DeliveryWorkGiverDefName);
            var worker = deliveryDef?.Worker as WorkGiver_DeliverRelocationMaterial;

            if (worker == null)
            {
                return false;
            }

            ThingDef resourceDef = resource?.def;

            return resourceDef != null && order.GetSpaceRemainingWithEnroute(resourceDef, pawn) == 0 &&
                   worker.JobOnThing(pawn, order, forced: true) == null;
        }

        private static RelocationMaterialOrder SpawnReadyOrder(
            ThingDef orderDef,
            ThingDef resourceDef,
            IntVec3 cell,
            Map map,
            List<Thing> tracked)
        {
            RelocationMaterialOrder order = SpawnOrder(orderDef, cell, map, tracked);
            Thing resource = ThingMaker.MakeThing(resourceDef);
            resource.stackCount = 1;

            if (!order.GetDirectlyHeldThings().TryAdd(resource))
            {
                resource.Destroy();
                throw new InvalidOperationException("RelocationMaterialOrder rejected its canonical resource.");
            }

            tracked?.Add(resource);

            if (!order.IsReadyForWork)
            {
                throw new InvalidOperationException("RelocationMaterialOrder did not become ReadyForWork.");
            }

            return order;
        }

        private static RelocationMaterialOrder SpawnOrder(ThingDef orderDef, IntVec3 cell, Map map, List<Thing> tracked)
        {
            var order = ThingMaker.MakeThing(orderDef) as RelocationMaterialOrder;

            if (order == null)
            {
                throw new InvalidOperationException(
                    "ThingDef '" + orderDef.defName + "' did not create RelocationMaterialOrder.");
            }

            GenSpawn.Spawn(order, cell, map);
            tracked?.Add(order);
            return order;
        }

        private static Thing SpawnResource(ThingDef resourceDef, IntVec3 cell, Map map, List<Thing> tracked)
        {
            Thing resource = ThingMaker.MakeThing(resourceDef);
            resource.stackCount = 1;
            GenSpawn.Spawn(resource, cell, map);
            tracked?.Add(resource);
            return resource;
        }

        private static FamilyCase ResolveFamilyCase(string name)
        {
            return _familyCases.FirstOrDefault(familyCase => familyCase.Name == name);
        }

        private static Pawn ResolvePawn(Map map, bool requireEmptyCarry)
        {
            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn =>
                pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed &&
                (!requireEmptyCarry || pawn.carryTracker?.CarriedThing == null));
        }

        private static List<Pawn> ResolvePawns(Map map, int count, bool requireEmptyCarry)
        {
            return map.mapPawns.FreeColonistsSpawned.Where(pawn =>
                pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed &&
                (!requireEmptyCarry || pawn.carryTracker?.CarriedThing == null)).Take(count).ToList();
        }

        private static bool TryFindClearRockCells(
            Map map,
            IntVec3 center,
            int minimumRadius,
            int maximumRadius,
            int count,
            out List<IntVec3> result)
        {
            result = new List<IntVec3>();

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, maximumRadius, useCenter: true))
            {
                if (!IsClearRockCell(map, center, cell, minimumRadius))
                {
                    continue;
                }

                result.Add(cell);

                if (result.Count >= count)
                {
                    return true;
                }
            }

            result.Clear();
            return false;
        }

        private static bool TryFindClearRockCell(
            Map map,
            IntVec3 center,
            int minimumRadius,
            int maximumRadius,
            out IntVec3 result)
        {
            if (TryFindClearRockCells(map, center, minimumRadius, maximumRadius, 1, out List<IntVec3> cells))
            {
                result = cells[0];
                return true;
            }

            result = IntVec3.Invalid;
            return false;
        }

        private static bool IsClearRockCell(Map map, IntVec3 center, IntVec3 cell, int minimumRadius)
        {
            if (!cell.InBounds(map) || cell.DistanceToSquared(center) < minimumRadius * minimumRadius)
            {
                return false;
            }

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);

            if (terrain == null || !terrain.IsRock || !cell.Standable(map) || cell.Fogged(map) ||
                RelocationMaterialOrderUtility.HasBuiltInMaterialOrderAt(map, cell))
            {
                return false;
            }

            List<Thing> things = cell.GetThingList(map);

            for (var i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];

                if (thing is Building || thing is Plant || thing is Fire || thing is Blueprint)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TrySpawnUnreachableResource(
            Map map,
            Pawn pawn,
            ThingDef resourceDef,
            List<Thing> tracked,
            out Thing resource)
        {
            resource = null;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, 30, useCenter: false))
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map))
                {
                    continue;
                }

                Thing candidate = ThingMaker.MakeThing(resourceDef);
                candidate.stackCount = 1;
                GenSpawn.Spawn(candidate, cell, map);

                if (!pawn.CanReach(candidate, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    tracked?.Add(candidate);
                    resource = candidate;
                    return true;
                }

                candidate.Destroy();
            }

            return false;
        }

        private static RelocationMaterialOrder FindOrderAt(Map map, IntVec3 cell, ThingDef orderDef)
        {
            if (map == null || orderDef == null || !cell.InBounds(map))
            {
                return null;
            }

            return map.listerThings.ThingsOfDef(orderDef).OfType<RelocationMaterialOrder>().FirstOrDefault(order =>
                order.Spawned && !order.Destroyed && order.Position == cell);
        }

        private static bool IsExpectedWorkJob(Job job, RelocationMaterialOrder order)
        {
            return job != null && job.def == SoilRelocationPlacementJobDefOf.SoilRelocation_PlaceTerrain &&
                   job.targetA.Thing == order && !job.targetB.IsValid && !job.targetC.IsValid && job.count <= 0;
        }

        private static bool IsExpectedDeliveryJob(Job job, Thing resource, RelocationMaterialOrder order)
        {
            return IsExpectedDeliveryJobForOrder(job, order) && ReferenceEquals(job.targetA.Thing, resource);
        }

        private static bool IsExpectedDeliveryJobForOrder(Job job, RelocationMaterialOrder order)
        {
            return job != null && job.def == JobDefOf.HaulToContainer && job.targetA.HasThing &&
                   ReferenceEquals(job.targetB.Thing, order) && ReferenceEquals(job.targetC.Thing, order) &&
                   job.count == 1 && job.haulMode == HaulMode.ToContainer;
        }

        private static string DescribeJob(Job job)
        {
            if (job == null)
            {
                return "<null>";
            }

            return "def=" + (job.def?.defName ?? "<null>") + ", targetA=" + DescribeTarget(job.targetA) + ", targetB=" +
                   DescribeTarget(job.targetB) + ", targetC=" + DescribeTarget(job.targetC) + ", count=" + job.count +
                   ", haulMode=" + job.haulMode;
        }

        private static string DescribeTarget(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return "<invalid>";
            }

            if (target.HasThing)
            {
                return target.Thing?.def?.defName ?? "<null thing>";
            }

            return target.Cell.ToString();
        }

        private static List<ResourceForbiddenState> CaptureAndForbidWorldResources(
            Map map,
            ThingDef resourceDef,
            Pawn pawn)
        {
            var states = new List<ResourceForbiddenState>();

            if (map == null || resourceDef == null)
            {
                return states;
            }

            foreach (Thing thing in map.listerThings.ThingsOfDef(resourceDef).ToList())
            {
                if (!thing.Spawned)
                {
                    continue;
                }

                bool wasForbidden = thing.IsForbidden(pawn);
                states.Add(new ResourceForbiddenState(thing, wasForbidden));

                if (!wasForbidden)
                {
                    thing.SetForbidden(true, warnOnFail: false);
                }
            }

            return states;
        }

        private static void RestoreResourceStates(List<ResourceForbiddenState> states)
        {
            if (states == null)
            {
                return;
            }

            for (var i = 0; i < states.Count; i++)
            {
                ResourceForbiddenState state = states[i];

                if (state.Thing != null && !state.Thing.Destroyed && state.Thing.Spawned)
                {
                    state.Thing.SetForbidden(state.WasForbidden, warnOnFail: false);
                }
            }
        }

        private static int CountSpawnedResourcesNear(Map map, ThingDef resourceDef, IntVec3 targetCell)
        {
            if (map == null || resourceDef == null)
            {
                return 0;
            }

            return map.listerThings.ThingsOfDef(resourceDef).Where(thing =>
                    thing.Spawned && thing.Position.DistanceToSquared(targetCell) <= CleanupRadiusSquared)
                .Sum(thing => thing.stackCount);
        }

        private static void CleanupOrdersAt(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            foreach (FamilyCase familyCase in _familyCases)
            {
                ThingDef orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(familyCase.OrderDefName);

                if (orderDef == null)
                {
                    continue;
                }

                foreach (RelocationMaterialOrder order in map.listerThings.ThingsOfDef(orderDef)
                             .OfType<RelocationMaterialOrder>().Where(order => order.Spawned && order.Position == cell)
                             .ToList())
                {
                    DestroyOrderAndContents(order);
                }
            }
        }

        private static void DestroyOrderAndContents(RelocationMaterialOrder order)
        {
            if (order == null || order.Destroyed)
            {
                return;
            }

            ThingOwner holder = order.GetDirectlyHeldThings();

            while (holder != null && holder.Count > 0)
            {
                Thing thing = holder[0];

                if (thing == null)
                {
                    break;
                }

                thing.Destroy();
            }

            if (!order.Destroyed)
            {
                order.Destroy();
            }
        }

        private static void DestroyTracked(List<Thing> tracked)
        {
            if (tracked == null)
            {
                return;
            }

            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                Thing thing = tracked[i];

                if (thing != null && !thing.Destroyed)
                {
                    thing.Destroy();
                }
            }
        }

        private static void RestoreTerrain(Map map, IntVec3 cell, TerrainDef terrain)
        {
            if (map != null && terrain != null && cell.InBounds(map) &&
                !ReferenceEquals(map.terrainGrid.TerrainAt(cell), terrain))
            {
                map.terrainGrid.SetTerrain(cell, terrain);
            }
        }

        private static void CleanupSaveLoadState(Map map)
        {
            if (_saveLoadState == null || map == null || (map.Parent?.ID ?? -1) != _saveLoadState.MapParentId)
            {
                return;
            }

            ThingDef orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(_saveLoadState.OrderDefName);

            if (orderDef != null)
            {
                DestroyOrderAndContents(FindOrderAt(map, _saveLoadState.AwaitingCell, orderDef));
                DestroyOrderAndContents(FindOrderAt(map, _saveLoadState.ReadyCell, orderDef));
            }

            RestoreTerrain(map, _saveLoadState.AwaitingCell, _saveLoadState.AwaitingOriginalTerrain);
            RestoreTerrain(map, _saveLoadState.ReadyCell, _saveLoadState.ReadyOriginalTerrain);
            _saveLoadState = null;
        }

        private static void CleanupConcurrentDeliveryState()
        {
            if (_concurrentDeliveryState == null)
            {
                return;
            }

            ConcurrentDeliveryProbeState state = _concurrentDeliveryState;

            DestroyOrderAndContents(state.FirstOrder);
            DestroyOrderAndContents(state.SecondOrder);

            if (state.FirstResource != null && !state.FirstResource.Destroyed)
            {
                state.FirstResource.Destroy();
            }

            if (state.SecondResource != null && !state.SecondResource.Destroyed)
            {
                state.SecondResource.Destroy();
            }

            RestoreTerrain(state.Map, state.FirstCell, state.FirstOriginalTerrain);
            RestoreTerrain(state.Map, state.SecondCell, state.SecondOriginalTerrain);
            _concurrentDeliveryState = null;
        }

        private static void CleanupEndToEndState()
        {
            if (_endToEndState == null)
            {
                return;
            }

            EndToEndProbeState state = _endToEndState;

            foreach (EndToEndOperationState operation in state.Operations)
            {
                DestroyOrderAndContents(operation.Order);

                if (operation.Resource != null && !operation.Resource.Destroyed)
                {
                    operation.Resource.Destroy();
                }

                RestoreTerrain(state.Map, operation.TargetCell, operation.OriginalTerrain);
            }

            _endToEndState = null;
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

        private enum OperationRuntimeState
        {
            Pending,
            Ready,
            Completed,
            Invalid
        }

        private sealed class FamilyCase
        {
            private readonly Func<Designator_PlaceRelocatableTerrain> _createDesignator;

            public FamilyCase(
                string name,
                string orderDefName,
                string resourceDefName,
                Func<Designator_PlaceRelocatableTerrain> createDesignator)
            {
                Name = name;
                OrderDefName = orderDefName;
                ResourceDefName = resourceDefName;
                _createDesignator = createDesignator;
            }

            public string Name { get; }

            public string OrderDefName { get; }

            public string ResourceDefName { get; }

            public Designator_PlaceRelocatableTerrain CreateDesignator()
            {
                return _createDesignator();
            }

            public bool TryResolve(out ThingDef orderDef, out ThingDef resourceDef)
            {
                orderDef = DefDatabase<ThingDef>.GetNamedSilentFail(OrderDefName);
                resourceDef = DefDatabase<ThingDef>.GetNamedSilentFail(ResourceDefName);
                return orderDef != null && resourceDef != null;
            }
        }

        private sealed class ProbeContext
        {
            public ProbeContext(
                Map map,
                Pawn pawn,
                IntVec3 targetCell,
                WorkGiverDef actualWorkGiverDef,
                WorkGiverDef deliveryWorkGiverDef,
                WorkGiver_PlaceRelocatableTerrain actualWorker,
                WorkGiver_DeliverRelocationMaterial deliveryWorker)
            {
                Map = map;
                Pawn = pawn;
                TargetCell = targetCell;
                ActualWorkGiverDef = actualWorkGiverDef;
                DeliveryWorkGiverDef = deliveryWorkGiverDef;
                ActualWorker = actualWorker;
                DeliveryWorker = deliveryWorker;
            }

            public Map Map { get; }

            public Pawn Pawn { get; }

            public IntVec3 TargetCell { get; }

            public WorkGiverDef ActualWorkGiverDef { get; }

            public WorkGiverDef DeliveryWorkGiverDef { get; }

            public WorkGiver_PlaceRelocatableTerrain ActualWorker { get; }

            public WorkGiver_DeliverRelocationMaterial DeliveryWorker { get; }
        }

        private sealed class ResourceForbiddenState
        {
            public ResourceForbiddenState(Thing thing, bool wasForbidden)
            {
                Thing = thing;
                WasForbidden = wasForbidden;
            }

            public Thing Thing { get; }

            public bool WasForbidden { get; }
        }

        private sealed class SaveLoadProbeState
        {
            public SaveLoadProbeState(
                int mapParentId,
                IntVec3 awaitingCell,
                IntVec3 readyCell,
                string orderDefName,
                string resourceDefName,
                TerrainDef awaitingOriginalTerrain,
                TerrainDef readyOriginalTerrain)
            {
                MapParentId = mapParentId;
                AwaitingCell = awaitingCell;
                ReadyCell = readyCell;
                OrderDefName = orderDefName;
                ResourceDefName = resourceDefName;
                AwaitingOriginalTerrain = awaitingOriginalTerrain;
                ReadyOriginalTerrain = readyOriginalTerrain;
            }

            public int MapParentId { get; }

            public IntVec3 AwaitingCell { get; }

            public IntVec3 ReadyCell { get; }

            public string OrderDefName { get; }

            public string ResourceDefName { get; }

            public TerrainDef AwaitingOriginalTerrain { get; }

            public TerrainDef ReadyOriginalTerrain { get; }
        }

        private sealed class ConcurrentDeliveryProbeState
        {
            public ConcurrentDeliveryProbeState(
                Map map,
                RelocationMaterialOrder firstOrder,
                RelocationMaterialOrder secondOrder,
                Thing firstResource,
                Thing secondResource,
                IntVec3 firstCell,
                IntVec3 secondCell,
                TerrainDef firstOriginalTerrain,
                TerrainDef secondOriginalTerrain,
                TerrainDef firstExpectedTerrain,
                TerrainDef secondExpectedTerrain)
            {
                Map = map;
                FirstOrder = firstOrder;
                SecondOrder = secondOrder;
                FirstResource = firstResource;
                SecondResource = secondResource;
                FirstCell = firstCell;
                SecondCell = secondCell;
                FirstOriginalTerrain = firstOriginalTerrain;
                SecondOriginalTerrain = secondOriginalTerrain;
                FirstExpectedTerrain = firstExpectedTerrain;
                SecondExpectedTerrain = secondExpectedTerrain;
            }

            public Map Map { get; }

            public RelocationMaterialOrder FirstOrder { get; }

            public RelocationMaterialOrder SecondOrder { get; }

            public Thing FirstResource { get; }

            public Thing SecondResource { get; }

            public IntVec3 FirstCell { get; }

            public IntVec3 SecondCell { get; }

            public TerrainDef FirstOriginalTerrain { get; }

            public TerrainDef SecondOriginalTerrain { get; }

            public TerrainDef FirstExpectedTerrain { get; }

            public TerrainDef SecondExpectedTerrain { get; }
        }

        private sealed class EndToEndProbeState
        {
            public EndToEndProbeState(Map map, List<EndToEndOperationState> operations)
            {
                Map = map;
                Operations = operations;
            }

            public Map Map { get; }

            public IReadOnlyList<EndToEndOperationState> Operations { get; }
        }

        private sealed class EndToEndOperationState
        {
            public EndToEndOperationState(
                string familyName,
                RelocationMaterialOrder order,
                Thing resource,
                IntVec3 targetCell,
                TerrainDef originalTerrain,
                TerrainDef expectedTerrain)
            {
                FamilyName = familyName;
                Order = order;
                Resource = resource;
                TargetCell = targetCell;
                OriginalTerrain = originalTerrain;
                ExpectedTerrain = expectedTerrain;
            }

            public string FamilyName { get; }

            public RelocationMaterialOrder Order { get; }

            public Thing Resource { get; }

            public IntVec3 TargetCell { get; }

            public TerrainDef OriginalTerrain { get; }

            public TerrainDef ExpectedTerrain { get; }
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
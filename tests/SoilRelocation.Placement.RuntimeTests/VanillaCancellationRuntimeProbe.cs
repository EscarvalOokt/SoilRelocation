using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using SoilRelocation.Digging;
using SoilRelocation.Relocation;
using Verse;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        [DebugAction(
            "Soil Relocation",
            "Validate vanilla relocation cancellation",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ValidateVanillaRelocationCancellation()
        {
            var report = new ProbeReport();

            try
            {
                RunVanillaRelocationCancellationProbe(report);
            }
            catch (Exception exception)
            {
                report.Fail("Vanilla cancellation probe", "Unhandled exception: " + exception);
            }

            WriteReport("Vanilla relocation cancellation probe", report);
        }

        private static void RunVanillaRelocationCancellationProbe(ProbeReport report)
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

            Pawn anchorPawn = ResolvePawn(map, requireEmptyCarry: false);

            if (anchorPawn == null)
            {
                report.Fail("Environment", "No free colonist is available to anchor cancellation fixtures.");
                return;
            }

            Designator_Cancel cancelDesignator = Find.ReverseDesignatorDatabase.Get<Designator_Cancel>();

            if (cancelDesignator == null)
            {
                report.Fail("Environment", "Vanilla ReverseDesignatorDatabase did not provide Designator_Cancel.");
                return;
            }

            ProbeVanillaCancelEligibilityAndReverseGizmo(map, anchorPawn, definitions, cancelDesignator, report);
            ProbeVanillaCancelEntryPoints(map, anchorPawn, definitions, cancelDesignator, report);
            ProbeVanillaCancelMultiCell(map, anchorPawn, definitions, cancelDesignator, report);

            report.Info(
                "Native cancellation / manual smoke",
                "Use 'Prepare relocation grouped controls' for the mixed-selection UI check. After grouped Allow, " +
                "confirm the selected Dig/Place orders expose one native reverse Cancel gizmo with the standard " +
                "Cancel hotkey, then click it and run the grouped verifier. Also confirm the ordinary Cancel " +
                "designator still cancels a vanilla Blueprint/Frame or cancelable vanilla designation normally.");
        }

        private static void ProbeVanillaCancelEligibilityAndReverseGizmo(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            Designator_Cancel cancelDesignator,
            ProbeReport report)
        {
            const string scenario = "Native cancellation / eligibility and reverse gizmo";

            if (!TryFindVanillaCancelDigCell(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    out IntVec3 digCell) ||
                !TryFindVanillaCancelRockCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    1,
                    0,
                    out List<IntVec3> placeCells))
            {
                report.Skip(scenario, "Could not find clear Dig/Place cells without preexisting designations.");
                return;
            }

            TerrainDef digTerrain = map.terrainGrid.TerrainAt(digCell);
            TerrainDef placeTerrain = map.terrainGrid.TerrainAt(placeCells[0]);
            DigRelocationOrder digOrder = null;
            RelocationMaterialOrder placeOrder = null;
            DigRelocationOrder unspawnedDig = null;

            try
            {
                digOrder = SpawnDigOrder(definitions.DigOrderDef, digCell, map);
                placeOrder = SpawnOrder(definitions.SoilOrderDef, placeCells[0], map, null);
                unspawnedDig = ThingMaker.MakeThing(definitions.DigOrderDef) as DigRelocationOrder;

                bool digAccepted = cancelDesignator.CanDesignateThing(digOrder).Accepted;
                bool placeAccepted = cancelDesignator.CanDesignateThing(placeOrder).Accepted;
                bool unspawnedRejected = unspawnedDig != null &&
                                         !cancelDesignator.CanDesignateThing(unspawnedDig).Accepted;

                Command_Action digReverse = cancelDesignator.CreateReverseDesignationGizmo(digOrder);
                Command_Action placeReverse = cancelDesignator.CreateReverseDesignationGizmo(placeOrder);

                bool digMetadataValid = NativeCancelGizmoMetadataValid(cancelDesignator, digOrder, digReverse);
                bool placeMetadataValid = NativeCancelGizmoMetadataValid(cancelDesignator, placeOrder, placeReverse);
                bool projectCancelAbsent = !HasProjectOwnedCancelGizmo(digOrder) &&
                                           !HasProjectOwnedCancelGizmo(placeOrder);

                if (!digAccepted ||
                    !placeAccepted ||
                    !unspawnedRejected ||
                    !digMetadataValid ||
                    !placeMetadataValid ||
                    !projectCancelAbsent)
                {
                    report.Fail(
                        scenario,
                        "digAccepted=" +
                        digAccepted +
                        ", placeAccepted=" +
                        placeAccepted +
                        ", unspawnedRejected=" +
                        unspawnedRejected +
                        ", digReverseMetadata=" +
                        digMetadataValid +
                        ", placeReverseMetadata=" +
                        placeMetadataValid +
                        ", projectCancelAbsent=" +
                        projectCancelAbsent +
                        ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Vanilla Designator_Cancel accepts live Dig/Place orders on the current map, rejects an " +
                    "unspawned order, creates native reverse gizmos with the standard Cancel hotkey, and no " +
                    "project-owned Cancel gizmo remains.");
            }
            finally
            {
                DestroyUxOrder(digOrder);
                DestroyOrderAndContents(placeOrder);
                DestroyUxOrder(unspawnedDig);
                RestoreTerrain(map, digCell, digTerrain);
                RestoreTerrain(map, placeCells[0], placeTerrain);
            }
        }

        private static void ProbeVanillaCancelEntryPoints(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            Designator_Cancel cancelDesignator,
            ProbeReport report)
        {
            ProbeVanillaCancelDirectDig(map, anchorPawn, definitions, cancelDesignator, report);
            ProbeVanillaCancelSingleCellPlace(map, anchorPawn, definitions, cancelDesignator, report);
            ProbeVanillaCancelReverseGizmoReadyPlace(map, anchorPawn, definitions, cancelDesignator, report);
        }

        private static void ProbeVanillaCancelDirectDig(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            Designator_Cancel cancelDesignator,
            ProbeReport report)
        {
            const string scenario = "Native cancellation / DesignateThing Dig";

            if (!TryFindVanillaCancelDigCell(map, anchorPawn.Position, 4, UxFixtureMaximumRadius, out IntVec3 cell))
            {
                report.Skip(scenario, "Could not find a clear Dig cell without preexisting designations.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            DigRelocationOrder order = null;

            try
            {
                order = SpawnDigOrder(definitions.DigOrderDef, cell, map);
                cancelDesignator.DesignateThing(order);

                bool terrainUnchanged = ReferenceEquals(map.terrainGrid.TerrainAt(cell), originalTerrain);

                if (order == null || !order.Destroyed || !terrainUnchanged)
                {
                    report.Fail(
                        scenario,
                        "destroyed=" + (order?.Destroyed ?? false) + ", terrainUnchanged=" + terrainUnchanged + ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Vanilla Designator_Cancel.DesignateThing routes Dig cancellation through the relocation " +
                    "backend without mutating terrain.");
            }
            finally
            {
                DestroyUxOrder(order);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeVanillaCancelSingleCellPlace(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            Designator_Cancel cancelDesignator,
            ProbeReport report)
        {
            const string scenario = "Native cancellation / DesignateSingleCell Place";

            if (!TryFindVanillaCancelRockCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    1,
                    0,
                    out List<IntVec3> cells))
            {
                report.Skip(scenario, "Could not find a clear Place cell without preexisting designations.");
                return;
            }

            IntVec3 cell = cells[0];
            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            RelocationMaterialOrder order = null;
            int resourcesBefore = CountSpawnedResourceUnits(map, definitions.SoilResourceDef);

            try
            {
                order = SpawnOrder(definitions.SoilOrderDef, cell, map, null);
                cancelDesignator.DesignateSingleCell(cell);
                int resourcesAfter = CountSpawnedResourceUnits(map, definitions.SoilResourceDef);
                bool terrainUnchanged = ReferenceEquals(map.terrainGrid.TerrainAt(cell), originalTerrain);

                if (order == null || !order.Destroyed || resourcesAfter != resourcesBefore || !terrainUnchanged)
                {
                    report.Fail(
                        scenario,
                        "destroyed=" +
                        (order?.Destroyed ?? false) +
                        ", resourceDelta=" +
                        (resourcesAfter - resourcesBefore) +
                        ", terrainUnchanged=" +
                        terrainUnchanged +
                        ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Vanilla DesignateSingleCell cancels an awaiting Place order without creating material or " +
                    "mutating terrain.");
            }
            finally
            {
                DestroyOrderAndContents(order);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeVanillaCancelReverseGizmoReadyPlace(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            Designator_Cancel cancelDesignator,
            ProbeReport report)
        {
            const string scenario = "Native cancellation / reverse gizmo ready Place";

            if (!TryFindVanillaCancelRockCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    1,
                    0,
                    out List<IntVec3> cells))
            {
                report.Skip(scenario, "Could not find a clear Place cell without preexisting designations.");
                return;
            }

            IntVec3 cell = cells[0];
            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);
            RelocationMaterialOrder order = null;
            Thing containedMaterial = null;

            try
            {
                order = SpawnReadyOrder(definitions.SoilOrderDef, definitions.SoilResourceDef, cell, map, null);
                order.TryGetContainedMaterial(out containedMaterial);

                Command_Action reverseGizmo = cancelDesignator.CreateReverseDesignationGizmo(order);

                if (reverseGizmo?.action == null)
                {
                    report.Fail(scenario, "Vanilla Designator_Cancel did not provide an executable reverse gizmo.");
                    return;
                }

                reverseGizmo.action();

                bool refundValid = containedMaterial is { Destroyed: false, stackCount: 1, Spawned: true } &&
                                   containedMaterial.Map == map;
                bool terrainUnchanged = ReferenceEquals(map.terrainGrid.TerrainAt(cell), originalTerrain);

                if (!order.Destroyed || !refundValid || !terrainUnchanged)
                {
                    report.Fail(
                        scenario,
                        "destroyed=" +
                        order.Destroyed +
                        ", exactRefund=" +
                        refundValid +
                        ", terrainUnchanged=" +
                        terrainUnchanged +
                        ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "The native reverse Cancel gizmo executes the project-owned ready-Place cancellation path and " +
                    "refunds its exact contained material once.");
            }
            finally
            {
                DestroyOrderAndContents(order);
                DestroyLooseThing(containedMaterial);
                RestoreTerrain(map, cell, originalTerrain);
            }
        }

        private static void ProbeVanillaCancelMultiCell(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            Designator_Cancel cancelDesignator,
            ProbeReport report)
        {
            const string scenario = "Native cancellation / mixed multi-cell";

            if (!TryFindVanillaCancelDigCell(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    out IntVec3 digCell) ||
                !TryFindVanillaCancelRockCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    2,
                    7,
                    out List<IntVec3> placeCells))
            {
                report.Skip(scenario, "Could not find one Dig and two separated Place cells for the mixed fixture.");
                return;
            }

            TerrainDef digTerrain = map.terrainGrid.TerrainAt(digCell);
            TerrainDef awaitingTerrain = map.terrainGrid.TerrainAt(placeCells[0]);
            TerrainDef readyTerrain = map.terrainGrid.TerrainAt(placeCells[1]);
            DigRelocationOrder digOrder = null;
            RelocationMaterialOrder awaitingOrder = null;
            RelocationMaterialOrder readyOrder = null;
            Thing readyMaterial = null;
            int resourcesBefore = CountSpawnedResourceUnits(map, definitions.SoilResourceDef);

            try
            {
                digOrder = SpawnDigOrder(definitions.DigOrderDef, digCell, map);
                awaitingOrder = SpawnOrder(definitions.SoilOrderDef, placeCells[0], map, null);
                readyOrder = SpawnReadyOrder(
                    definitions.SoilOrderDef,
                    definitions.SoilResourceDef,
                    placeCells[1],
                    map,
                    null);
                readyOrder.TryGetContainedMaterial(out readyMaterial);

                cancelDesignator.DesignateMultiCell([digCell, placeCells[0], placeCells[1]]);

                int resourcesAfter = CountSpawnedResourceUnits(map, definitions.SoilResourceDef);
                bool allDestroyed = digOrder.Destroyed && awaitingOrder.Destroyed && readyOrder.Destroyed;
                bool exactReadyRefund = readyMaterial is { Destroyed: false, Spawned: true } &&
                                        readyMaterial.Map == map &&
                                        readyMaterial.stackCount == 1 &&
                                        resourcesAfter == resourcesBefore + 1;
                bool terrainUnchanged = ReferenceEquals(map.terrainGrid.TerrainAt(digCell), digTerrain) &&
                                        ReferenceEquals(map.terrainGrid.TerrainAt(placeCells[0]), awaitingTerrain) &&
                                        ReferenceEquals(map.terrainGrid.TerrainAt(placeCells[1]), readyTerrain);

                if (!allDestroyed || !exactReadyRefund || !terrainUnchanged)
                {
                    report.Fail(
                        scenario,
                        "allDestroyed=" +
                        allDestroyed +
                        ", exactReadyRefund=" +
                        exactReadyRefund +
                        ", resourceDelta=" +
                        (resourcesAfter - resourcesBefore) +
                        ", terrainUnchanged=" +
                        terrainUnchanged +
                        ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Vanilla DesignateMultiCell cancels mixed Dig/awaiting Place/ready Place orders, preserves " +
                    "terrain, creates no awaiting-order material, and refunds exactly one ready-order resource.");
            }
            finally
            {
                DestroyUxOrder(digOrder);
                DestroyOrderAndContents(awaitingOrder);
                DestroyOrderAndContents(readyOrder);
                DestroyLooseThing(readyMaterial);
                RestoreTerrain(map, digCell, digTerrain);
                RestoreTerrain(map, placeCells[0], awaitingTerrain);
                RestoreTerrain(map, placeCells[1], readyTerrain);
            }
        }

        private static void ProbeNativeCancellationAfterLoad(
            DigRelocationOrder digOrder,
            RelocationMaterialOrder placeOrder,
            ProbeReport report)
        {
            const string scenario = "Native cancellation / save-load availability";
            Designator_Cancel cancelDesignator = Find.ReverseDesignatorDatabase.Get<Designator_Cancel>();

            if (cancelDesignator == null || digOrder == null || placeOrder == null)
            {
                report.Fail(scenario, "Vanilla Cancel designator or one of the restored relocation orders is missing.");
                return;
            }

            bool digAccepted = cancelDesignator.CanDesignateThing(digOrder).Accepted;
            bool placeAccepted = cancelDesignator.CanDesignateThing(placeOrder).Accepted;
            bool digReverse = cancelDesignator.CreateReverseDesignationGizmo(digOrder) != null;
            bool placeReverse = cancelDesignator.CreateReverseDesignationGizmo(placeOrder) != null;

            if (!digAccepted || !placeAccepted || !digReverse || !placeReverse)
            {
                report.Fail(
                    scenario,
                    "digAccepted=" +
                    digAccepted +
                    ", placeAccepted=" +
                    placeAccepted +
                    ", digReverse=" +
                    digReverse +
                    ", placeReverse=" +
                    placeReverse +
                    ".");
                return;
            }

            report.Pass(
                scenario,
                "Restored forbidden Dig/Place orders remain recognized by vanilla Designator_Cancel and expose " +
                "native reverse Cancel gizmos after load.");
        }

        private static bool NativeCancelGizmoMetadataValid(
            Designator_Cancel cancelDesignator,
            RelocationOrder order,
            Command_Action gizmo)
        {
            return cancelDesignator != null &&
                   order != null &&
                   gizmo != null &&
                   gizmo.hotKey == KeyBindingDefOf.Designator_Cancel &&
                   // The vanilla reverse Cancel command uses an exact sort order.
                   // ReSharper disable once CompareOfFloatsByEqualityOperator
                   gizmo.Order == -20f &&
                   gizmo.defaultLabel == cancelDesignator.LabelCapReverseDesignating(order) &&
                   gizmo.defaultDesc == cancelDesignator.DescReverseDesignating(order);
        }

        private static bool HasProjectOwnedCancelGizmo(RelocationOrder order)
        {
            return order != null &&
                   order.GetGizmos()
                       .OfType<Command_Action>()
                       .Any(command => command.defaultLabel == "DesignatorCancel".Translate());
        }

        private static int CountSpawnedResourceUnits(Map map, ThingDef resourceDef)
        {
            if (map == null || resourceDef == null)
            {
                return 0;
            }

            return map.listerThings.ThingsOfDef(resourceDef)
                .Where(thing => thing is { Spawned: true, Destroyed: false })
                .Sum(thing => thing.stackCount);
        }

        private static bool TryFindVanillaCancelDigCell(
            Map map,
            IntVec3 center,
            int minimumRadius,
            int maximumRadius,
            out IntVec3 result)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, maximumRadius, useCenter: true))
            {
                if (IsClearDigCell(map, center, cell, minimumRadius) && !CellHasAnyDesignation(map, cell))
                {
                    result = cell;
                    return true;
                }
            }

            result = IntVec3.Invalid;
            return false;
        }

        private static bool TryFindVanillaCancelRockCells(
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
                    CellHasAnyDesignation(map, cell) ||
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

        private static bool CellHasAnyDesignation(Map map, IntVec3 cell)
        {
            return map?.designationManager != null && map.designationManager.AllDesignationsAt(cell).Any();
        }
    }
}
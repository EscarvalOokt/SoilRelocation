using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using RimWorld;
using SoilRelocation.Digging;
using SoilRelocation.Relocation;
using Verse;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        private const string AllowToolCancelSelectedTypeName = "AllowTool.Context.MenuEntry_CancelSelected";
        private const string AllowToolCancelDesignationsTypeName = "AllowTool.Context.MenuEntry_CancelDesignations";
        private const string AllowToolActivationResultTypeName = "AllowTool.Context.ActivationResult";
        private const string AllowToolBaseContextMenuEntryTypeName = "AllowTool.Context.BaseContextMenuEntry";

        [DebugAction(
            "Soil Relocation",
            "Validate Allow Tool relocation cancellation",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ValidateAllowToolRelocationCancellation()
        {
            var report = new ProbeReport();

            try
            {
                RunAllowToolRelocationCancellationProbe(report);
            }
            catch (Exception exception)
            {
                report.Fail("Allow Tool cancellation probe", "Unhandled exception: " + exception);
            }

            WriteReport("Allow Tool relocation cancellation probe", report);
        }

        private static void RunAllowToolRelocationCancellationProbe(ProbeReport report)
        {
            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail("Environment", "No current map is available.");
                return;
            }

            if (!TryResolveAllowToolApi(report, out AllowToolApi api))
            {
                return;
            }

            if (!TryResolveUxDefinitions(out UxDefinitions definitions, report))
            {
                return;
            }

            FamilyCase sandCase = ResolveFamilyCase("Sand");
            FamilyCase gravelCase = ResolveFamilyCase("Gravel");

            if (sandCase == null || gravelCase == null ||
                !sandCase.TryResolve(out ThingDef sandOrderDef, out ThingDef sandResourceDef) || !gravelCase.TryResolve(
                    out ThingDef gravelOrderDef,
                    out ThingDef gravelResourceDef))
            {
                report.Fail("Environment", "Sand/Gravel Place order or resource Defs are unavailable.");
                return;
            }

            Pawn anchorPawn = ResolvePawn(map, requireEmptyCarry: false);

            if (anchorPawn == null)
            {
                report.Fail("Environment", "No free colonist is available to anchor Allow Tool fixtures.");
                return;
            }

            Designator_Cancel cancelDesignator = Find.ReverseDesignatorDatabase.Get<Designator_Cancel>();

            if (cancelDesignator == null)
            {
                report.Fail("Environment", "Vanilla ReverseDesignatorDatabase did not provide Designator_Cancel.");
                return;
            }

            report.Pass(
                "Allow Tool API",
                "Resolved MenuEntry_CancelSelected, MenuEntry_CancelDesignations, and ActivationResult without a " +
                "compile-time Allow Tool reference.");

            ProbeAllowToolNoRelocationRegression(map, cancelDesignator, api, report);
            ProbeAllowToolCancelSelected(
                map,
                anchorPawn,
                definitions,
                sandOrderDef,
                sandResourceDef,
                gravelOrderDef,
                cancelDesignator,
                api,
                report);
            ProbeAllowToolCancelDesignations(
                map,
                anchorPawn,
                definitions,
                sandOrderDef,
                sandResourceDef,
                gravelOrderDef,
                gravelResourceDef,
                cancelDesignator,
                api,
                report);

            report.Info(
                "Allow Tool / manual smoke",
                "Right-click a native reverse Cancel gizmo and confirm Allow Tool's Cancel selected action " +
                "cancels all " +
                "map orders of the selected exact Dig/Place ThingDef types while leaving unrelated Place families " +
                "alone. Also confirm Cancel designations removes all relocation orders together with ordinary " +
                "cancelable vanilla designations. Test the Allow Tool context-action hotkey through the same menu " +
                "path if it is enabled in Allow Tool settings.");
        }


        private static void ProbeAllowToolNoRelocationRegression(
            Map map,
            Designator_Cancel cancelDesignator,
            AllowToolApi api,
            ProbeReport report)
        {
            const string scenario = "Allow Tool / original result without relocation targets";

            if (GetSpawnedRelocationOrdersForAllowTool(map).Count != 0 || HasCancelableNonPlanDesignation(map))
            {
                report.Skip(
                    scenario,
                    "The current map contains relocation orders or cancelable vanilla designations; skipping the " +
                    "non-destructive original-result regression check.");
                return;
            }

            Find.Selector?.ClearSelection();
            object selectedResult = api.InvokeCancelSelected(cancelDesignator, map);
            object designationsResult = api.InvokeCancelDesignations(cancelDesignator, map);
            var expectedSelectedMessage = "Designator_context_cancel_selected_fail".Translate().ToString();
            var expectedDesignationsMessage = "Designator_context_cancel_desig_msg".Translate(0, 0).ToString();
            bool selectedResultValid = api.ResultMatches(
                selectedResult,
                expectedSelectedMessage,
                MessageTypeDefOf.RejectInput,
                out string selectedDescription);
            bool designationsResultValid = api.ResultMatches(
                designationsResult,
                expectedDesignationsMessage,
                MessageTypeDefOf.TaskCompletion,
                out string designationsDescription);

            if (!selectedResultValid || !designationsResultValid)
            {
                report.Fail(
                    scenario,
                    "CancelSelected=" + selectedDescription + "; CancelDesignations=" + designationsDescription + ".");
                return;
            }

            report.Pass(
                scenario,
                "When no relocation targets exist, both patched Allow Tool actions preserve their original result " +
                "messages and message types.");
        }

        private static void ProbeAllowToolCancelSelected(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            ThingDef sandOrderDef,
            ThingDef sandResourceDef,
            ThingDef gravelOrderDef,
            Designator_Cancel cancelDesignator,
            AllowToolApi api,
            ProbeReport report)
        {
            const string scenario = "Allow Tool / Cancel selected";

            if (GetSpawnedRelocationOrdersForAllowTool(map).Count != 0)
            {
                report.Skip(
                    scenario,
                    "The current map already contains relocation orders. Use a clean runtime-test map so the global " +
                    "same-type action cannot cancel player-owned orders.");
                return;
            }

            if (!TryFindAllowToolDigCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    2,
                    out List<IntVec3> digCells) || !TryFindVanillaCancelRockCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    3,
                    5,
                    out List<IntVec3> placeCells))
            {
                report.Skip(scenario, "Could not find clear Dig/Place fixture cells without designations.");
                return;
            }

            var originalTerrains = new List<CellTerrainState>
            {
                new CellTerrainState(digCells[0], map.terrainGrid.TerrainAt(digCells[0])),
                new CellTerrainState(digCells[1], map.terrainGrid.TerrainAt(digCells[1])),
                new CellTerrainState(placeCells[0], map.terrainGrid.TerrainAt(placeCells[0])),
                new CellTerrainState(placeCells[1], map.terrainGrid.TerrainAt(placeCells[1])),
                new CellTerrainState(placeCells[2], map.terrainGrid.TerrainAt(placeCells[2]))
            };
            DigRelocationOrder selectedDig = null;
            DigRelocationOrder matchingDig = null;
            RelocationMaterialOrder selectedSand = null;
            RelocationMaterialOrder matchingReadySand = null;
            RelocationMaterialOrder unrelatedGravel = null;
            Thing readySandResource = null;
            int initialSandUnits = CountSpawnedResourceUnits(map, sandResourceDef);

            try
            {
                selectedDig = SpawnDigOrder(definitions.DigOrderDef, digCells[0], map);
                matchingDig = SpawnDigOrder(definitions.DigOrderDef, digCells[1], map);
                selectedSand = SpawnOrder(sandOrderDef, placeCells[0], map, null);
                matchingReadySand = SpawnOrder(sandOrderDef, placeCells[1], map, null);
                unrelatedGravel = SpawnOrder(gravelOrderDef, placeCells[2], map, null);

                readySandResource = ThingMaker.MakeThing(sandResourceDef);
                readySandResource.stackCount = 1;

                if (!matchingReadySand.GetDirectlyHeldThings().TryAdd(readySandResource))
                {
                    throw new InvalidOperationException("Ready Sand fixture rejected its canonical resource.");
                }

                if (!TrySetAllowToolProbeSelection(
                        new object[] { selectedDig, selectedSand },
                        out string selectionError))
                {
                    report.Fail(scenario, selectionError);
                    return;
                }

                object result = api.InvokeCancelSelected(cancelDesignator, map);
                var expectedMessage = "Designator_context_cancel_selected_succ".Translate(2, 4).ToString();
                bool resultValid = api.ResultMatches(
                    result,
                    expectedMessage,
                    MessageTypeDefOf.TaskCompletion,
                    out string resultDescription);
                bool targetOrdersDestroyed = selectedDig.Destroyed && matchingDig.Destroyed && selectedSand.Destroyed &&
                                             matchingReadySand.Destroyed;
                bool unrelatedOrderPreserved = unrelatedGravel != null && !unrelatedGravel.Destroyed &&
                                               unrelatedGravel.Spawned;
                bool terrainPreserved = originalTerrains.All(state => ReferenceEquals(
                    map.terrainGrid.TerrainAt(state.Cell),
                    state.Terrain));
                int finalSandUnits = CountSpawnedResourceUnits(map, sandResourceDef);
                bool accountingValid = finalSandUnits == initialSandUnits + 1 && !readySandResource.Destroyed &&
                                       readySandResource.Spawned &&
                                       matchingReadySand.GetDirectlyHeldThings().Count == 0;

                if (!resultValid || !targetOrdersDestroyed || !unrelatedOrderPreserved || !terrainPreserved ||
                    !accountingValid)
                {
                    report.Fail(
                        scenario,
                        "result=" + resultDescription + ", targetOrdersDestroyed=" + targetOrdersDestroyed +
                        ", unrelatedGravelPreserved=" + unrelatedOrderPreserved + ", terrainPreserved=" +
                        terrainPreserved + ", sandUnits=" + initialSandUnits + "->" + finalSandUnits + ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Selecting one Dig and one Sand Place order cancels all map orders of exactly those two ThingDef " +
                    "types, preserves the unrelated Gravel family, refunds the ready Sand material exactly once, " +
                    "preserves terrain, and reports Allow Tool success counts 2 types / 4 affected targets.");
            }
            finally
            {
                Find.Selector?.ClearSelection();
                DestroyUxOrder(selectedDig);
                DestroyUxOrder(matchingDig);
                DestroyUxOrder(selectedSand);
                DestroyUxOrder(matchingReadySand);
                DestroyUxOrder(unrelatedGravel);
                DestroyLooseThing(readySandResource);
                RestoreCellTerrains(map, originalTerrains);
            }
        }

        private static void ProbeAllowToolCancelDesignations(
            Map map,
            Pawn anchorPawn,
            UxDefinitions definitions,
            ThingDef sandOrderDef,
            ThingDef sandResourceDef,
            ThingDef gravelOrderDef,
            ThingDef gravelResourceDef,
            Designator_Cancel cancelDesignator,
            AllowToolApi api,
            ProbeReport report)
        {
            const string scenario = "Allow Tool / Cancel designations";

            if (GetSpawnedRelocationOrdersForAllowTool(map).Count != 0)
            {
                report.Skip(
                    scenario,
                    "The current map already contains relocation orders. Use a clean runtime-test map so the global " +
                    "action cannot cancel player-owned orders.");
                return;
            }

            if (HasCancelableNonPlanDesignation(map))
            {
                report.Skip(
                    scenario,
                    "The current map already contains cancelable vanilla designations. Use a clean runtime-test map " +
                    "so the global Allow Tool action does not remove unrelated player orders.");
                return;
            }

            if (!TryFindVanillaCancelDigCell(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    out IntVec3 digCell) || !TryFindVanillaCancelRockCells(
                    map,
                    anchorPawn.Position,
                    4,
                    UxFixtureMaximumRadius,
                    2,
                    5,
                    out List<IntVec3> placeCells))
            {
                report.Skip(scenario, "Could not find clear Dig/Place fixture cells without designations.");
                return;
            }

            var originalTerrains = new List<CellTerrainState>
            {
                new CellTerrainState(digCell, map.terrainGrid.TerrainAt(digCell)),
                new CellTerrainState(placeCells[0], map.terrainGrid.TerrainAt(placeCells[0])),
                new CellTerrainState(placeCells[1], map.terrainGrid.TerrainAt(placeCells[1]))
            };
            DigRelocationOrder digOrder = null;
            RelocationMaterialOrder awaitingSand = null;
            RelocationMaterialOrder readyGravel = null;
            Thing readyGravelResource = null;
            int initialSandUnits = CountSpawnedResourceUnits(map, sandResourceDef);
            int initialGravelUnits = CountSpawnedResourceUnits(map, gravelResourceDef);

            try
            {
                digOrder = SpawnDigOrder(definitions.DigOrderDef, digCell, map);
                awaitingSand = SpawnOrder(sandOrderDef, placeCells[0], map, null);
                readyGravel = SpawnOrder(gravelOrderDef, placeCells[1], map, null);

                readyGravelResource = ThingMaker.MakeThing(gravelResourceDef);
                readyGravelResource.stackCount = 1;

                if (!readyGravel.GetDirectlyHeldThings().TryAdd(readyGravelResource))
                {
                    throw new InvalidOperationException("Ready Gravel fixture rejected its canonical resource.");
                }

                object result = api.InvokeCancelDesignations(cancelDesignator, map);
                var expectedMessage = "Designator_context_cancel_desig_msg".Translate(3, 0).ToString();
                bool resultValid = api.ResultMatches(
                    result,
                    expectedMessage,
                    MessageTypeDefOf.TaskCompletion,
                    out string resultDescription);
                bool allOrdersDestroyed = digOrder.Destroyed && awaitingSand.Destroyed && readyGravel.Destroyed;
                bool terrainPreserved = originalTerrains.All(state => ReferenceEquals(
                    map.terrainGrid.TerrainAt(state.Cell),
                    state.Terrain));
                int finalSandUnits = CountSpawnedResourceUnits(map, sandResourceDef);
                int finalGravelUnits = CountSpawnedResourceUnits(map, gravelResourceDef);
                bool accountingValid = finalSandUnits == initialSandUnits &&
                                       finalGravelUnits == initialGravelUnits + 1 && !readyGravelResource.Destroyed &&
                                       readyGravelResource.Spawned && readyGravel.GetDirectlyHeldThings().Count == 0;

                if (!resultValid || !allOrdersDestroyed || !terrainPreserved || !accountingValid)
                {
                    report.Fail(
                        scenario,
                        "result=" + resultDescription + ", allOrdersDestroyed=" + allOrdersDestroyed +
                        ", terrainPreserved=" + terrainPreserved + ", sandUnits=" + initialSandUnits + "->" +
                        finalSandUnits + ", gravelUnits=" + initialGravelUnits + "->" + finalGravelUnits + ".");
                    return;
                }

                report.Pass(
                    scenario,
                    "Cancel designations removes all spawned Dig/Place relocation orders through TryCancel, creates " +
                    "no material for the awaiting order, refunds the ready Gravel material exactly once, preserves " +
                    "terrain, and includes the three relocation Things in Allow Tool's result count.");
            }
            finally
            {
                Find.Selector?.ClearSelection();
                DestroyUxOrder(digOrder);
                DestroyUxOrder(awaitingSand);
                DestroyUxOrder(readyGravel);
                DestroyLooseThing(readyGravelResource);
                RestoreCellTerrains(map, originalTerrains);
            }
        }

        private static bool TryResolveAllowToolApi(ProbeReport report, out AllowToolApi api)
        {
            Type baseContextMenuEntryType = FindLoadedType(AllowToolBaseContextMenuEntryTypeName);
            Type cancelSelectedType = FindLoadedType(AllowToolCancelSelectedTypeName);
            Type cancelDesignationsType = FindLoadedType(AllowToolCancelDesignationsTypeName);
            Type activationResultType = FindLoadedType(AllowToolActivationResultTypeName);

            if (baseContextMenuEntryType == null && cancelSelectedType == null && cancelDesignationsType == null &&
                activationResultType == null)
            {
                api = null;
                report.Skip(
                    "Allow Tool API",
                    "Allow Tool is not loaded. This is the expected soft-dependency result when the optional mod is " +
                    "absent.");
                return false;
            }

            MethodInfo cancelSelectedActivate = ResolveAllowToolActivateMethod(cancelSelectedType);
            MethodInfo cancelDesignationsActivate = ResolveAllowToolActivateMethod(cancelDesignationsType);
            PropertyInfo messageProperty = activationResultType?.GetProperty(
                "Message",
                BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo messageTypeProperty = activationResultType?.GetProperty(
                "MessageType",
                BindingFlags.Instance | BindingFlags.Public);
            ConstructorInfo activationResultConstructor = activationResultType?.GetConstructor(
                new[] { typeof(string), typeof(MessageTypeDef) });

            if (cancelSelectedType == null || cancelDesignationsType == null || activationResultType == null ||
                cancelSelectedActivate == null || cancelDesignationsActivate == null || messageProperty == null ||
                messageTypeProperty == null || activationResultConstructor == null ||
                cancelSelectedActivate.ReturnType != activationResultType ||
                cancelDesignationsActivate.ReturnType != activationResultType)
            {
                api = null;
                report.Fail(
                    "Allow Tool API",
                    "Allow Tool is loaded, but the expected RimWorld 1.6 cancellation API contract could not be " +
                    "resolved.");
                return false;
            }

            api = new AllowToolApi(
                cancelSelectedType,
                cancelSelectedActivate,
                cancelDesignationsType,
                cancelDesignationsActivate,
                messageProperty,
                messageTypeProperty);
            return true;
        }

        private static MethodInfo ResolveAllowToolActivateMethod(Type type)
        {
            return type?.GetMethod(
                "Activate",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Designator), typeof(Map) },
                null);
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

        private static bool TrySetAllowToolProbeSelection(IEnumerable<object> objects, out string error)
        {
            error = null;
            object selector = Find.Selector;

            if (selector == null)
            {
                error = "Vanilla Selector is unavailable.";
                return false;
            }

            var objectList = objects.ToList();

            MethodInfo selectMethod = selector.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == "Select").Where(method =>
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length > 0 && parameters[0].ParameterType == typeof(object) &&
                           parameters.Skip(1).All(parameter => parameter.IsOptional);
                }).OrderBy(method => method.GetParameters().Length).FirstOrDefault();

            if (selectMethod == null)
            {
                error = "Could not resolve vanilla Selector.Select(object, ... optional arguments).";
                return false;
            }

            Find.Selector.ClearSelection();

            foreach (object selected in objectList)
            {
                ParameterInfo[] parameters = selectMethod.GetParameters();
                var arguments = new object[parameters.Length];
                arguments[0] = selected;

                for (var i = 1; i < parameters.Length; i++)
                {
                    arguments[i] = parameters[i].DefaultValue;
                }

                selectMethod.Invoke(selector, arguments);
            }

            List<object> selectedObjects = Find.Selector.SelectedObjectsListForReading;
            var expected = new HashSet<object>(objectList);
            HashSet<object> actual =
                selectedObjects == null ? new HashSet<object>() : new HashSet<object>(selectedObjects);

            if (!expected.SetEquals(actual))
            {
                error = "Vanilla Selector did not retain the exact Allow Tool probe selection. Selected=" +
                        DescribeSelectedObjects(selectedObjects ?? new List<object>()) + ".";
                return false;
            }

            return true;
        }

        private static bool TryFindAllowToolDigCells(
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
                if (!IsClearDigCell(map, center, cell, minimumRadius) || CellHasAnyDesignation(map, cell) ||
                    result.Contains(cell))
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

        private static List<RelocationOrder> GetSpawnedRelocationOrdersForAllowTool(Map map)
        {
            if (map?.listerThings?.AllThings == null)
            {
                return new List<RelocationOrder>();
            }

            return map.listerThings.AllThings.OfType<RelocationOrder>().Where(order =>
                order.Spawned && !order.Destroyed && ReferenceEquals(order.Map, map)).ToList();
        }

        private static bool HasCancelableNonPlanDesignation(Map map)
        {
            return map?.designationManager != null && map.designationManager.AllDesignations.Any(designation =>
                designation?.def != null && designation.def.designateCancelable &&
                designation.def != DesignationDefOf.Plan);
        }

        private sealed class AllowToolApi
        {
            private readonly Type _cancelSelectedType;
            private readonly MethodInfo _cancelSelectedActivate;
            private readonly Type _cancelDesignationsType;
            private readonly MethodInfo _cancelDesignationsActivate;
            private readonly PropertyInfo _messageProperty;
            private readonly PropertyInfo _messageTypeProperty;

            public AllowToolApi(
                Type cancelSelectedType,
                MethodInfo cancelSelectedActivate,
                Type cancelDesignationsType,
                MethodInfo cancelDesignationsActivate,
                PropertyInfo messageProperty,
                PropertyInfo messageTypeProperty)
            {
                _cancelSelectedType = cancelSelectedType;
                _cancelSelectedActivate = cancelSelectedActivate;
                _cancelDesignationsType = cancelDesignationsType;
                _cancelDesignationsActivate = cancelDesignationsActivate;
                _messageProperty = messageProperty;
                _messageTypeProperty = messageTypeProperty;
            }

            public object InvokeCancelSelected(Designator designator, Map map)
            {
                object entry = Activator.CreateInstance(_cancelSelectedType);
                return _cancelSelectedActivate.Invoke(entry, new object[] { designator, map });
            }

            public object InvokeCancelDesignations(Designator designator, Map map)
            {
                object entry = Activator.CreateInstance(_cancelDesignationsType);
                return _cancelDesignationsActivate.Invoke(entry, new object[] { designator, map });
            }

            public bool ResultMatches(
                object result,
                string expectedMessage,
                MessageTypeDef expectedMessageType,
                out string description)
            {
                if (result == null)
                {
                    description = "<null>";
                    return false;
                }

                var message = _messageProperty.GetValue(result) as string;
                var messageType = _messageTypeProperty.GetValue(result) as MessageTypeDef;
                description = "message='" + (message ?? "<null>") + "', messageType=" +
                              (messageType?.defName ?? "<null>");

                return message == expectedMessage && ReferenceEquals(messageType, expectedMessageType);
            }
        }
    }
}
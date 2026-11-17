using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SoilRelocation.Relocation;
using Verse;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace SoilRelocation.Compatibility
{
    internal static class AllowToolCancellationCompatibility
    {
        private const string CancelSelectedTypeName = "AllowTool.Context.MenuEntry_CancelSelected";
        private const string CancelDesignationsTypeName = "AllowTool.Context.MenuEntry_CancelDesignations";
        private const string ActivationResultTypeName = "AllowTool.Context.ActivationResult";
        private const string BaseContextMenuEntryTypeName = "AllowTool.Context.BaseContextMenuEntry";

        private static bool _apiWarningLogged;

        internal static bool TryResolveCancelSelectedTarget(out MethodBase target)
        {
            return TryResolveTarget(CancelSelectedTypeName, out target);
        }

        internal static bool TryResolveCancelDesignationsTarget(out MethodBase target)
        {
            return TryResolveTarget(CancelDesignationsTypeName, out target);
        }

        internal static List<RelocationOrder> SnapshotOrders(Map map, Predicate<RelocationOrder> predicate = null)
        {
            if (map?.listerThings?.AllThings == null)
            {
                return [];
            }

            return map.listerThings.AllThings.OfType<RelocationOrder>()
                .Where(order =>
                    !order.Destroyed &&
                    order.Spawned &&
                    ReferenceEquals(order.Map, map) &&
                    (predicate == null || predicate(order)))
                .ToList();
        }

        internal static int CancelOrders(
            IEnumerable<RelocationOrder> orders,
            ISet<LocalTargetInfo> affectedTargets = null)
        {
            if (orders == null)
            {
                return 0;
            }

            var cancelledCount = 0;

            foreach (RelocationOrder order in orders.ToList())
            {
                if (order == null || order.Destroyed || !order.Spawned)
                {
                    continue;
                }

                LocalTargetInfo target = order;

                if (!order.TryCancel())
                {
                    continue;
                }

                cancelledCount++;
                affectedTargets?.Add(target);
            }

            return cancelledCount;
        }

        internal static object CreateActivationResult(string message, MessageTypeDef messageType)
        {
            if (!TryResolveActivationResultConstructor(out ConstructorInfo constructor))
            {
                throw new MissingMethodException(
                    "Allow Tool ActivationResult(string, MessageTypeDef) constructor is unavailable.");
            }

            return constructor.Invoke([message, messageType]);
        }

        internal static void LogCompatibilityFailure(string operation, Exception exception)
        {
            Log.Error(
                "[SoilRelocation] Allow Tool cancellation compatibility failed while processing " +
                operation +
                ". Allow Tool's original action was allowed to run. Exception: " +
                exception);
        }

        private static bool TryResolveTarget(string typeName, out MethodBase target)
        {
            target = null;
            Type entryType = AccessTools.TypeByName(typeName);

            if (entryType == null)
            {
                if (IsAllowToolPresent())
                {
                    WarnApiMismatch("expected type '" + typeName + "' was not found.");
                }

                return false;
            }

            MethodInfo activateMethod = entryType.GetMethod(
                "Activate",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                [typeof(Designator), typeof(Map)],
                null);

            if (activateMethod == null)
            {
                WarnApiMismatch("expected method '" + typeName + ".Activate(Designator, Map)' was not found.");
                return false;
            }

            Type activationResultType = AccessTools.TypeByName(ActivationResultTypeName);

            if (activationResultType == null || activateMethod.ReturnType != activationResultType)
            {
                WarnApiMismatch(
                    "expected Allow Tool ActivationResult return contract is unavailable for '" + typeName + "'.");
                return false;
            }

            if (!TryResolveActivationResultConstructor(out _))
            {
                WarnApiMismatch("expected ActivationResult(string, MessageTypeDef) constructor was not found.");
                return false;
            }

            target = activateMethod;
            return true;
        }

        private static bool TryResolveActivationResultConstructor(out ConstructorInfo constructor)
        {
            Type activationResultType = AccessTools.TypeByName(ActivationResultTypeName);
            constructor = activationResultType?.GetConstructor([typeof(string), typeof(MessageTypeDef)]);
            return constructor != null;
        }

        private static bool IsAllowToolPresent()
        {
            return AccessTools.TypeByName(BaseContextMenuEntryTypeName) != null;
        }

        private static void WarnApiMismatch(string detail)
        {
            if (_apiWarningLogged)
            {
                return;
            }

            _apiWarningLogged = true;
            Log.Warning(
                "[SoilRelocation] Allow Tool was detected with an unsupported cancellation API. " +
                "Optional bulk cancellation is unavailable. " +
                detail);
        }
    }

    [HarmonyPatch]
    internal static class AllowToolCancelSelectedPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AllowToolCancellationCompatibility.TryResolveCancelSelectedTarget(out _);
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            AllowToolCancellationCompatibility.TryResolveCancelSelectedTarget(out MethodBase target);
            return target;
        }

        [HarmonyPrefix]
        private static void Prefix(Map map, out CancelSelectedState __state)
        {
            __state = null;

            try
            {
                if (map?.designationManager == null || Find.Selector == null)
                {
                    return;
                }

                List<object> selectedObjectsList = Find.Selector.SelectedObjectsListForReading;

                if (selectedObjectsList == null || selectedObjectsList.Count == 0)
                {
                    return;
                }

                var selectedObjects = new HashSet<object>(selectedObjectsList);
                var selectedRelocationDefs = new HashSet<ThingDef>(
                    selectedObjects.OfType<RelocationOrder>()
                        .Where(order =>
                            !order.Destroyed && order.Spawned && ReferenceEquals(order.Map, map) && order.def != null)
                        .Select(order => order.def));

                if (selectedRelocationDefs.Count == 0)
                {
                    return;
                }

                var selectedTilePositions =
                    new HashSet<IntVec3>(selectedObjects.OfType<Thing>().Select(thing => thing.Position));
                var allDesignations = map.designationManager.AllDesignations.ToList();
                var selectedDesignationDefs = new HashSet<DesignationDef>(
                    allDesignations
                        .Where(designation =>
                            designation.target.HasThing
                                ? selectedObjects.Contains(designation.target.Thing)
                                : selectedTilePositions.Contains(designation.target.Cell))
                        .Select(designation => designation.def));
                var affectedTargets = new HashSet<LocalTargetInfo>(
                    allDesignations.Where(designation => selectedDesignationDefs.Contains(designation.def))
                        .Select(designation => designation.target));

                __state = new CancelSelectedState(
                    selectedRelocationDefs,
                    selectedDesignationDefs.Count,
                    affectedTargets);
            }
            catch (Exception exception)
            {
                AllowToolCancellationCompatibility.LogCompatibilityFailure("Cancel selected prefix", exception);
                __state = null;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(Map map, CancelSelectedState __state, ref object __result)
        {
            if (map == null || __state == null)
            {
                return;
            }

            try
            {
                List<RelocationOrder> orders = AllowToolCancellationCompatibility.SnapshotOrders(
                    map,
                    order => order.def != null && __state.SelectedRelocationDefs.Contains(order.def));
                AllowToolCancellationCompatibility.CancelOrders(orders, __state.AffectedTargets);

                int typeCount = __state.VanillaDesignationTypeCount + __state.SelectedRelocationDefs.Count;
                int affectedCount = __state.AffectedTargets.Count;

                if (affectedCount > 0)
                {
                    var message = "Designator_context_cancel_selected_succ".Translate(typeCount, affectedCount)
                        .ToString();
                    __result = AllowToolCancellationCompatibility.CreateActivationResult(
                        message,
                        MessageTypeDefOf.TaskCompletion);
                }
                else
                {
                    var message = "Designator_context_cancel_selected_fail".Translate().ToString();
                    __result = AllowToolCancellationCompatibility.CreateActivationResult(
                        message,
                        MessageTypeDefOf.RejectInput);
                }
            }
            catch (Exception exception)
            {
                AllowToolCancellationCompatibility.LogCompatibilityFailure("Cancel selected postfix", exception);
            }
        }

        private sealed class CancelSelectedState(
            HashSet<ThingDef> selectedRelocationDefs,
            int vanillaDesignationTypeCount,
            HashSet<LocalTargetInfo> affectedTargets)
        {
            public HashSet<ThingDef> SelectedRelocationDefs { get; } = selectedRelocationDefs;

            public int VanillaDesignationTypeCount { get; } = vanillaDesignationTypeCount;

            public HashSet<LocalTargetInfo> AffectedTargets { get; } = affectedTargets;
        }
    }

    [HarmonyPatch]
    internal static class AllowToolCancelDesignationsPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AllowToolCancellationCompatibility.TryResolveCancelDesignationsTarget(out _);
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            AllowToolCancellationCompatibility.TryResolveCancelDesignationsTarget(out MethodBase target);
            return target;
        }

        [HarmonyPrefix]
        private static void Prefix(Map map, out CancelDesignationsState __state)
        {
            __state = null;

            try
            {
                if (map?.designationManager == null)
                {
                    return;
                }

                var thingCount = 0;
                var tileCount = 0;

                foreach (Designation designation in map.designationManager.AllDesignations)
                {
                    if (designation?.def == null ||
                        !designation.def.designateCancelable ||
                        designation.def == DesignationDefOf.Plan)
                    {
                        continue;
                    }

                    if (designation.target.Thing != null)
                    {
                        thingCount++;
                    }
                    else
                    {
                        tileCount++;
                    }
                }

                __state = new CancelDesignationsState(thingCount, tileCount);
            }
            catch (Exception exception)
            {
                AllowToolCancellationCompatibility.LogCompatibilityFailure("Cancel designations prefix", exception);
                __state = null;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(Map map, CancelDesignationsState __state, ref object __result)
        {
            if (map == null || __state == null)
            {
                return;
            }

            try
            {
                List<RelocationOrder> orders = AllowToolCancellationCompatibility.SnapshotOrders(map);
                int cancelledCount = AllowToolCancellationCompatibility.CancelOrders(orders);

                if (cancelledCount == 0)
                {
                    return;
                }

                int thingCount = __state.VanillaThingCount + cancelledCount;
                var message = "Designator_context_cancel_desig_msg".Translate(thingCount, __state.VanillaTileCount)
                    .ToString();
                __result = AllowToolCancellationCompatibility.CreateActivationResult(
                    message,
                    MessageTypeDefOf.TaskCompletion);
            }
            catch (Exception exception)
            {
                AllowToolCancellationCompatibility.LogCompatibilityFailure("Cancel designations postfix", exception);
            }
        }

        private sealed class CancelDesignationsState(int vanillaThingCount, int vanillaTileCount)
        {
            public int VanillaThingCount { get; } = vanillaThingCount;

            public int VanillaTileCount { get; } = vanillaTileCount;
        }
    }
}
using HarmonyLib;
using RimWorld;
using SoilRelocation.Relocation;
using Verse;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace SoilRelocation.Patches
{
    internal static class DesignatorCancelPatchUtility
    {
        internal static bool IsCancelableRelocationOrder(Designator_Cancel designator, Thing thing)
        {
            return designator != null && thing is RelocationOrder order && !order.Destroyed && order.Spawned &&
                   order.Map != null && ReferenceEquals(order.Map, designator.Map);
        }
    }

    [HarmonyPatch(typeof(Designator_Cancel), nameof(Designator_Cancel.CanDesignateThing))]
    internal static class DesignatorCancelCanDesignateThingPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Designator_Cancel __instance, Thing t, ref AcceptanceReport __result)
        {
            if (__result.Accepted || !DesignatorCancelPatchUtility.IsCancelableRelocationOrder(__instance, t))
            {
                return;
            }

            __result = true;
        }
    }

    [HarmonyPatch(typeof(Designator_Cancel), nameof(Designator_Cancel.DesignateThing))]
    internal static class DesignatorCancelDesignateThingPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Designator_Cancel __instance, Thing t)
        {
            if (!(t is RelocationOrder order))
            {
                return true;
            }

            if (DesignatorCancelPatchUtility.IsCancelableRelocationOrder(__instance, order))
            {
                order.TryCancel();
            }

            return false;
        }
    }
}
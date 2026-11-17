using System.Collections.Generic;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Placement
{
    internal static class RelocationMaterialOrderUtility
    {
        internal static bool IsBuiltInMaterialOrder(ThingDef thingDef)
        {
            return thingDef == SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceSandOrder ||
                   thingDef == SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceGravelOrder ||
                   thingDef == SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceSoilOrder ||
                   thingDef == SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceRichSoilOrder;
        }

        internal static bool HasAnyBuiltInMaterialOrder(Map map)
        {
            if (map == null)
            {
                return false;
            }

            return HasAnyOfDef(map, SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceSandOrder) ||
                   HasAnyOfDef(map, SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceGravelOrder) ||
                   HasAnyOfDef(map, SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceSoilOrder) ||
                   HasAnyOfDef(map, SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceRichSoilOrder);
        }

        internal static bool HasBuiltInMaterialOrderAt(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            foreach (Thing thing in cell.GetThingList(map))
            {
                if (IsBuiltInMaterialOrder(thing.def))
                {
                    return true;
                }
            }

            return false;
        }

        internal static IEnumerable<Thing> GetBuiltInMaterialOrders(Map map)
        {
            if (map == null)
            {
                yield break;
            }

            foreach (Thing thing in GetThingsOfDef(map, SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceSandOrder))
            {
                yield return thing;
            }

            foreach (Thing thing in GetThingsOfDef(
                         map,
                         SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceGravelOrder))
            {
                yield return thing;
            }

            foreach (Thing thing in GetThingsOfDef(map, SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceSoilOrder))
            {
                yield return thing;
            }

            foreach (Thing thing in GetThingsOfDef(
                         map,
                         SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceRichSoilOrder))
            {
                yield return thing;
            }
        }

        internal static bool TryResolveFamily(ThingDef thingDef, out RelocationFamilyDef family)
        {
            family = null;

            int extensionCount = FindMaterialOrderExtensions(thingDef, out RelocationMaterialOrderExtension extension);

            if (extensionCount != 1 ||
                extension?.Family == null ||
                !RelocationRuleResolver.IsFamilyValid(extension.Family))
            {
                return false;
            }

            family = extension.Family;
            return true;
        }

        private static bool HasAnyOfDef(Map map, ThingDef thingDef)
        {
            return thingDef != null && map.listerThings.ThingsOfDef(thingDef).Count > 0;
        }

        private static IEnumerable<Thing> GetThingsOfDef(Map map, ThingDef thingDef)
        {
            if (thingDef == null)
            {
                yield break;
            }

            List<Thing> things = map.listerThings.ThingsOfDef(thingDef);

            for (var i = 0; i < things.Count; i++)
            {
                yield return things[i];
            }
        }

        private static int FindMaterialOrderExtensions(
            ThingDef thingDef,
            out RelocationMaterialOrderExtension firstExtension)
        {
            firstExtension = null;

            if (thingDef?.modExtensions == null)
            {
                return 0;
            }

            var count = 0;
            List<DefModExtension> modExtensions = thingDef.modExtensions;

            for (var i = 0; i < modExtensions.Count; i++)
            {
                if (!(modExtensions[i] is RelocationMaterialOrderExtension extension))
                {
                    continue;
                }

                count++;

                firstExtension ??= extension;
            }

            return count;
        }
    }
}
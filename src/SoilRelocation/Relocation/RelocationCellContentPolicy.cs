using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SoilRelocation.Relocation
{
    internal static class RelocationCellContentPolicy
    {
        private const string PowerConduitDefName = "PowerConduit";
        private const string HiddenConduitDefName = "HiddenConduit";
        private const string WaterproofConduitDefName = "WaterproofConduit";

        internal static bool HasStructuralBlocker(Map map, IntVec3 cell, bool allowRelocationUnderConduits)
        {
            if (map == null || !cell.InBounds(map))
            {
                return true;
            }

            List<Thing> things = cell.GetThingList(map);

            for (var i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];

                if (thing is Blueprint || thing is Frame)
                {
                    BuildableDef intendedDef = thing.def.entityDefToBuild;

                    if (intendedDef is TerrainDef)
                    {
                        return true;
                    }

                    if (!(intendedDef is ThingDef intendedThingDef) ||
                        intendedThingDef.category != ThingCategory.Building)
                    {
                        continue;
                    }

                    if (allowRelocationUnderConduits && IsConduitDef(intendedThingDef))
                    {
                        continue;
                    }

                    return true;
                }

                if (!(thing is Building))
                {
                    continue;
                }

                if (allowRelocationUnderConduits && IsConduitDef(thing.def))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        internal static Plant FindPlant(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return null;
            }

            List<Thing> things = cell.GetThingList(map);

            for (var i = 0; i < things.Count; i++)
            {
                if (things[i] is Plant plant)
                {
                    return plant;
                }
            }

            return null;
        }

        internal static bool HasFire(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            List<Thing> things = cell.GetThingList(map);

            for (var i = 0; i < things.Count; i++)
            {
                if (things[i] is Fire)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsConduitDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }

            return def.defName == PowerConduitDefName || def.defName == HiddenConduitDefName ||
                   def.defName == WaterproofConduitDefName;
        }
    }
}
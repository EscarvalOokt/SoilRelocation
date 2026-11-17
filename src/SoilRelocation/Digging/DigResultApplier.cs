using RimWorld;
using SoilRelocation.Substrate;
using Verse;

namespace SoilRelocation.Digging
{
    internal static class DigResultApplier
    {
        internal static bool TryApply(Map map, IntVec3 cell, DigResultPreparation preparation)
        {
            if (map == null ||
                !cell.InBounds(map) ||
                preparation.PortableResourceDef == null ||
                preparation.ExposedTerrain == null)
            {
                return false;
            }

            Thing resource = ThingMaker.MakeThing(preparation.PortableResourceDef);

            if (resource == null)
            {
                return false;
            }

            resource.stackCount = 1;

            if (!GenPlace.TryPlaceThing(resource, cell, map, ThingPlaceMode.Near, out Thing resultingThing))
            {
                return false;
            }

            if (!TryCommitNaturalRockProfile(map, preparation.NaturalRockResolution))
            {
                RollBackPlacedResource(resource, resultingThing);
                return false;
            }

            map.terrainGrid.SetTerrain(cell, preparation.ExposedTerrain);
            map.snowGrid.SetDepth(cell, 0f);
            FilthMaker.RemoveAllFilth(cell, map);

            return true;
        }

        private static bool TryCommitNaturalRockProfile(Map map, NaturalRockResolution naturalRockResolution)
        {
            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();

            return component != null && component.TryCommitPreparedProfile(naturalRockResolution);
        }

        private static void RollBackPlacedResource(Thing createdResource, Thing resultingThing)
        {
            if (createdResource is { Spawned: true })
            {
                createdResource.Destroy();
                return;
            }

            if (resultingThing == null || resultingThing.Destroyed || ReferenceEquals(resultingThing, createdResource))
            {
                return;
            }

            if (resultingThing.stackCount > 1)
            {
                resultingThing.stackCount--;
                return;
            }

            resultingThing.Destroy();
        }
    }
}
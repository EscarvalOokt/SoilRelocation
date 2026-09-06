using System.Collections.Generic;
using RimWorld;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Placement
{
    internal static class PlacementTerrainResolver
    {
        internal static TerrainDef Resolve(
            Map map,
            IntVec3 cell,
            RelocationFamilyDef family,
            PlacementResolutionMode resolutionMode)
        {
            if (map == null || !cell.InBounds(map) || !RelocationRuleResolver.IsFamilyValid(family))
            {
                return null;
            }

            if (resolutionMode == PlacementResolutionMode.Canonical)
            {
                return RelocationPlacementPolicy.ResolveTerrain(family, resolutionMode, null, null);
            }

            if (resolutionMode != PlacementResolutionMode.BiomeNative)
            {
                return null;
            }

            BiomeDef biome = map.BiomeAt(cell);

            if (biome == null)
            {
                return family.CanonicalTerrain;
            }

            var fertilityTerrains = new List<TerrainDef>();

            if (biome.terrainsByFertility != null)
            {
                for (var i = 0; i < biome.terrainsByFertility.Count; i++)
                {
                    TerrainThreshold threshold = biome.terrainsByFertility[i];

                    if (threshold?.terrain != null)
                    {
                        fertilityTerrains.Add(threshold.terrain);
                    }
                }
            }

            return RelocationPlacementPolicy.ResolveTerrain(
                family,
                resolutionMode,
                biome.gravelTerrain,
                fertilityTerrains);
        }
    }
}
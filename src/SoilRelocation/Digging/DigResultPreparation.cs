using SoilRelocation.Substrate;
using Verse;

namespace SoilRelocation.Digging
{
    internal readonly struct DigResultPreparation
    {
        internal DigResultPreparation(
            ThingDef portableResourceDef,
            TerrainDef exposedTerrain,
            NaturalRockResolution naturalRockResolution)
        {
            PortableResourceDef = portableResourceDef;
            ExposedTerrain = exposedTerrain;
            NaturalRockResolution = naturalRockResolution;
        }

        internal ThingDef PortableResourceDef { get; }

        internal TerrainDef ExposedTerrain { get; }

        internal NaturalRockResolution NaturalRockResolution { get; }
    }
}
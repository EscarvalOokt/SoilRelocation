using SoilRelocation.Substrate;
using Verse;

namespace SoilRelocation.Digging
{
    internal static class DigResultResolver
    {
        internal static bool TryPrepare(
            Map map,
            IntVec3 cell,
            DiggingValidationResult validation,
            out DigResultPreparation preparation)
        {
            preparation = default(DigResultPreparation);

            if (map == null || !cell.InBounds(map) || !validation.IsValid)
            {
                return false;
            }

            ThingDef portableResourceDef = validation.Family?.CanonicalPortableResource;

            if (portableResourceDef == null)
            {
                return false;
            }

            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();

            if (component == null || !component.TryPrepareResolution(
                    cell,
                    out NaturalRockResolution naturalRockResolution))
            {
                return false;
            }

            ThingDef resolvedRock = naturalRockResolution.ResolvedRock;
            TerrainDef naturalTerrain = resolvedRock?.building?.naturalTerrain;

            if (naturalTerrain == null)
            {
                return false;
            }

            preparation = new DigResultPreparation(portableResourceDef, naturalTerrain, naturalRockResolution);

            return true;
        }
    }
}
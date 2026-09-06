using RimWorld;
using Verse;

// ReSharper disable UnassignedField.Global

namespace SoilRelocation.Placement
{
    [DefOf]
    public static class SoilRelocationMaterialOrderDefOf
    {
        public static ThingDef SoilRelocation_PlaceSandOrder;
        public static ThingDef SoilRelocation_PlaceGravelOrder;
        public static ThingDef SoilRelocation_PlaceSoilOrder;
        public static ThingDef SoilRelocation_PlaceRichSoilOrder;

        static SoilRelocationMaterialOrderDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoilRelocationMaterialOrderDefOf));
        }
    }

    [DefOf]
    public static class SoilRelocationPlacementJobDefOf
    {
        public static JobDef SoilRelocation_PlaceTerrain;

        static SoilRelocationPlacementJobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoilRelocationPlacementJobDefOf));
        }
    }
}
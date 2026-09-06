using RimWorld;
using Verse;

namespace SoilRelocation.Digging
{
    [DefOf]
    public static class SoilRelocationDigOrderDefOf
    {
        // ReSharper disable once UnassignedField.Global
        public static ThingDef SoilRelocation_DigOrder;

        static SoilRelocationDigOrderDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoilRelocationDigOrderDefOf));
        }
    }

    [DefOf]
    public static class SoilRelocationJobDefOf
    {
        // ReSharper disable once UnassignedField.Global
        public static JobDef SoilRelocation_DigTerrain;

        static SoilRelocationJobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoilRelocationJobDefOf));
        }
    }
}
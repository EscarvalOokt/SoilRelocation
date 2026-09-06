using System.Collections.Generic;
using Verse;

namespace SoilRelocation.Domain
{
    public sealed class RelocationFamilyDef : Def
    {
        public TerrainDef CanonicalTerrain;
        public ThingDef CanonicalPortableResource;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (CanonicalTerrain == null)
            {
                yield return "canonicalTerrain must be set.";
            }

            if (CanonicalPortableResource == null)
            {
                yield return "canonicalPortableResource must be set.";
            }
        }
    }
}
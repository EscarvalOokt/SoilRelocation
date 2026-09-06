using Verse;

namespace SoilRelocation.Domain
{
    public sealed class RelocationTerrainExtension : DefModExtension
    {
        public bool Enabled = true;
        public RelocationFamilyDef Family;
    }
}
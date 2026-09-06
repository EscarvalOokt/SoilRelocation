using Verse;

namespace SoilRelocation.Domain
{
    [StaticConstructorOnStartup]
    internal static class RelocationRuleDiagnostics
    {
        static RelocationRuleDiagnostics()
        {
            foreach (TerrainDef terrain in DefDatabase<TerrainDef>.AllDefsListForReading)
            {
                ValidateTerrainMembership(terrain);
            }
        }

        private static void ValidateTerrainMembership(TerrainDef terrain)
        {
            int extensionCount = RelocationRuleResolver.FindMembershipExtensions(
                terrain,
                out RelocationTerrainExtension extension);

            if (extensionCount > 1)
            {
                Log.Error(
                    $"{SoilRelocationMod.LogPrefix} TerrainDef '{terrain.defName}' has " +
                    $"{extensionCount} Soil Relocation membership extensions. " +
                    "The terrain will be treated as unsupported.");

                return;
            }

            if (extensionCount == 0 || !extension.Enabled)
            {
                return;
            }

            if (extension.Family == null)
            {
                Log.Error(
                    $"{SoilRelocationMod.LogPrefix} TerrainDef '{terrain.defName}' has " +
                    "an enabled Soil Relocation membership extension without a family. " +
                    "The terrain will be treated as unsupported.");

                return;
            }

            if (!RelocationRuleResolver.IsFamilyValid(extension.Family))
            {
                Log.Error(
                    $"{SoilRelocationMod.LogPrefix} TerrainDef '{terrain.defName}' references " +
                    $"invalid relocation family '{extension.Family.defName}'. " +
                    "The terrain will be treated as unsupported.");
            }
        }
    }
}
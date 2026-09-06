using HarmonyLib;
using SoilRelocation.Domain;
using Verse;

// ReSharper disable UnusedMember.Local

namespace SoilRelocation.Patches
{
    [HarmonyPatch(typeof(LoadedLanguage), "InjectIntoData_AfterImpliedDefs")]
    internal static class LoadedLanguageInjectIntoDataAfterImpliedDefsPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            RelocationPlacementPolicy.InvalidateNaturalStoneDestinationCache();
        }
    }
}
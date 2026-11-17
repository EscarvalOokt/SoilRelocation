using System;
using System.Collections.Generic;
using Verse;

namespace SoilRelocation.Substrate
{
    internal static class NaturalRockProfileRepairer
    {
        internal static bool TryRepair(
            NaturalRockReconstructionProfile profile,
            Func<string, ThingDef> resolveRock,
            IReadOnlyList<ThingDef> currentEnvironmentRocks,
            out NaturalRockReconstructionProfile repairedProfile,
            out bool changed,
            out bool usedEnvironmentRecovery)
        {
            repairedProfile = null;
            changed = false;
            usedEnvironmentRecovery = false;

            if (profile == null || resolveRock == null || !profile.IsStructurallyValid())
            {
                return false;
            }

            var savedRocks = new ThingDef[profile.SlotCount];
            for (var i = 0; i < profile.SlotCount; i++)
            {
                savedRocks[i] = resolveRock(profile.GetRockDefName(i));
            }

            return NaturalRockReconstructionProfile.TryCreate(
                savedRocks,
                currentEnvironmentRocks,
                out repairedProfile,
                out changed,
                out usedEnvironmentRecovery);
        }
    }
}
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

            var savedCandidates = new List<ThingDef>();
            var missingSlotIndices = new List<int>();

            for (var i = 0; i < profile.SlotCount; i++)
            {
                ThingDef rock = resolveRock(profile.GetRockDefName(i));

                if (rock == null)
                {
                    missingSlotIndices.Add(i);
                    continue;
                }

                savedCandidates.Add(rock);
            }

            if (missingSlotIndices.Count == 0)
            {
                repairedProfile = profile.Copy();
                return true;
            }

            IReadOnlyList<ThingDef> replacementCandidates = savedCandidates;

            if (savedCandidates.Count == 0)
            {
                var environmentCandidates = new List<ThingDef>();

                if (currentEnvironmentRocks != null)
                {
                    for (var i = 0; i < currentEnvironmentRocks.Count; i++)
                    {
                        ThingDef rock = currentEnvironmentRocks[i];

                        if (rock == null || string.IsNullOrWhiteSpace(rock.defName))
                        {
                            continue;
                        }

                        environmentCandidates.Add(rock);
                    }
                }

                if (environmentCandidates.Count == 0)
                {
                    return false;
                }

                replacementCandidates = environmentCandidates;
                usedEnvironmentRecovery = true;
            }

            NaturalRockReconstructionProfile candidateProfile = profile.Copy();

            for (var i = 0; i < missingSlotIndices.Count; i++)
            {
                int slotIndex = missingSlotIndices[i];
                ThingDef replacement = replacementCandidates[slotIndex % replacementCandidates.Count];

                candidateProfile.ReplaceRockDefName(slotIndex, replacement.defName);
            }

            if (!candidateProfile.IsStructurallyValid())
            {
                return false;
            }

            repairedProfile = candidateProfile;
            changed = true;
            return true;
        }
    }
}
using System.Collections.Generic;
using Verse;

namespace SoilRelocation.Substrate
{
    public sealed class NaturalRockMapComponent(Map map) : MapComponent(map)
    {
        private const string ProfileSaveKey = "naturalRockReconstructionProfile";
        private bool _invalidProfileDiagnosticLogged;

        private NaturalRockReconstructionProfile _profile;

        internal bool HasProfile => _profile != null;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _profile, ProfileSaveKey);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _invalidProfileDiagnosticLogged = false;
            }
        }

        internal bool TryPrepareResolution(IntVec3 cell, out NaturalRockResolution resolution)
        {
            resolution = default(NaturalRockResolution);

            if (_profile == null)
            {
                if (!TryCaptureCurrentProfile(out NaturalRockReconstructionProfile candidateProfile))
                {
                    LogInvalidProfileOnce(
                        "Unable to create a natural-rock reconstruction profile from the current map environment.");

                    return false;
                }

                if (!NaturalRockResolver.TryResolve(map, cell, candidateProfile, out ThingDef resolvedRock))
                {
                    LogInvalidProfileOnce(
                        "Unable to resolve associated natural rock from the candidate reconstruction profile.");

                    return false;
                }

                resolution = new NaturalRockResolution(resolvedRock, candidateProfile);

                return true;
            }

            if (!TryGetUsableCommittedProfile(out NaturalRockReconstructionProfile committedProfile))
            {
                return false;
            }

            if (!NaturalRockResolver.TryResolve(map, cell, committedProfile, out ThingDef committedResolvedRock))
            {
                LogInvalidProfileOnce(
                    "Unable to resolve associated natural rock from the committed reconstruction profile.");

                return false;
            }

            resolution = new NaturalRockResolution(committedResolvedRock, null);

            return true;
        }

        internal bool TryCommitPreparedProfile(NaturalRockResolution resolution)
        {
            if (!resolution.RequiresProfileCommit)
            {
                return true;
            }

            NaturalRockReconstructionProfile pendingProfile = resolution.PendingProfile;

            if (pendingProfile == null || !pendingProfile.IsStructurallyValid())
            {
                return false;
            }

            if (_profile == null)
            {
                _profile = pendingProfile.Copy();
                return true;
            }

            return _profile.IsEquivalentTo(pendingProfile);
        }

        private bool TryGetUsableCommittedProfile(out NaturalRockReconstructionProfile committedProfile)
        {
            committedProfile = null;

            if (_profile == null || !_profile.IsStructurallyValid())
            {
                LogInvalidProfileOnce("The persisted natural-rock reconstruction profile is unsupported or corrupt.");

                return false;
            }

            List<ThingDef> currentEnvironmentRocks = GetCurrentNaturalRocks();

            if (!NaturalRockProfileRepairer.TryRepair(
                    _profile,
                    ResolveRockDef,
                    currentEnvironmentRocks,
                    out NaturalRockReconstructionProfile repairedProfile,
                    out bool changed,
                    out bool usedEnvironmentRecovery))
            {
                LogInvalidProfileOnce(
                    "The persisted natural-rock reconstruction profile has no usable rock definitions for repair.");

                return false;
            }

            if (changed)
            {
                _profile = repairedProfile;

                if (usedEnvironmentRecovery)
                {
                    Log.Warning(
                        $"{SoilRelocationMod.LogPrefix} Natural-rock reconstruction profile for map " +
                        $"'{map.uniqueID}' could not use any saved rock definitions. Unusable slots were repaired " +
                        "from the current reconstructible natural-rock environment.");
                }
            }

            committedProfile = _profile;
            return true;
        }

        private bool TryCaptureCurrentProfile(out NaturalRockReconstructionProfile profile)
        {
            return NaturalRockReconstructionProfile.TryCreate(
                GetCurrentNaturalRocks(),
                null,
                out profile,
                out _,
                out _);
        }

        private List<ThingDef> GetCurrentNaturalRocks()
        {
            var rocks = new List<ThingDef>();

            if (Find.World == null)
            {
                return rocks;
            }

            IEnumerable<ThingDef> currentRocks = Find.World.NaturalRockTypesIn(map.Tile);

            if (currentRocks == null)
            {
                return rocks;
            }

            foreach (ThingDef rock in currentRocks)
            {
                rocks.Add(rock);
            }

            return rocks;
        }

        private static ThingDef ResolveRockDef(string defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return null;
            }

            return DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        }

        private void LogInvalidProfileOnce(string message)
        {
            if (_invalidProfileDiagnosticLogged)
            {
                return;
            }

            _invalidProfileDiagnosticLogged = true;

            Log.Error(
                $"{SoilRelocationMod.LogPrefix} {message} " +
                $"Natural-stone reconstruction is unavailable for map '{map.uniqueID}'.");
        }
    }
}
using Verse;

namespace SoilRelocation.Substrate
{
    internal readonly struct NaturalRockResolution
    {
        internal NaturalRockResolution(ThingDef resolvedRock, NaturalRockReconstructionProfile pendingProfile)
        {
            ResolvedRock = resolvedRock;
            PendingProfile = pendingProfile;
        }

        internal ThingDef ResolvedRock { get; }

        internal NaturalRockReconstructionProfile PendingProfile { get; }

        internal bool RequiresProfileCommit => PendingProfile != null;
    }
}
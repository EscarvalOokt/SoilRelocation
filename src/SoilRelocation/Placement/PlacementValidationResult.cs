using RimWorld;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Placement
{
    internal readonly struct PlacementValidationResult
    {
        internal PlacementValidationResult(
            PlacementValidationStatus status,
            RelocationFamilyDef family = null,
            TerrainDef currentTerrain = null,
            TerrainDef resolvedTerrain = null,
            Plant plant = null)
        {
            Status = status;
            Family = family;
            CurrentTerrain = currentTerrain;
            ResolvedTerrain = resolvedTerrain;
            Plant = plant;
        }

        internal PlacementValidationStatus Status { get; }

        internal RelocationFamilyDef Family { get; }

        internal TerrainDef CurrentTerrain { get; }

        internal TerrainDef ResolvedTerrain { get; }

        internal Plant Plant { get; }

        internal bool IsValid => Status == PlacementValidationStatus.Valid;

        internal bool IsPermanentFailure =>
            Status == PlacementValidationStatus.InvalidFamily ||
            Status == PlacementValidationStatus.InvalidDestination || Status == PlacementValidationStatus.NoOp ||
            Status == PlacementValidationStatus.StructuralBlocker;

        internal bool IsTransientBlocker =>
            Status == PlacementValidationStatus.PlantPrerequisite ||
            Status == PlacementValidationStatus.PlantCannotBeRemoved || Status == PlacementValidationStatus.Fire;
    }
}
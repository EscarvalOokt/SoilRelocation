using RimWorld;
using SoilRelocation.Domain;

namespace SoilRelocation.Digging
{
    internal readonly struct DiggingValidationResult
    {
        internal DiggingValidationResult(
            DiggingValidationStatus status,
            RelocationFamilyDef family = null,
            Plant plant = null)
        {
            Status = status;
            Family = family;
            Plant = plant;
        }

        internal DiggingValidationStatus Status { get; }

        internal RelocationFamilyDef Family { get; }

        internal Plant Plant { get; }

        internal bool IsValid => Status == DiggingValidationStatus.Valid;

        internal bool IsPermanentFailure =>
            Status == DiggingValidationStatus.UnsupportedTerrain || Status == DiggingValidationStatus.StructuralBlocker;

        internal bool IsTransientBlocker =>
            Status == DiggingValidationStatus.PlantPrerequisite ||
            Status == DiggingValidationStatus.PlantCannotBeRemoved || Status == DiggingValidationStatus.Fire;
    }
}
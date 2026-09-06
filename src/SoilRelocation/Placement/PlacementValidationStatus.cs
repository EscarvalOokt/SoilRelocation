namespace SoilRelocation.Placement
{
    internal enum PlacementValidationStatus
    {
        Valid = 0,
        InvalidFamily,
        InvalidDestination,
        NoOp,
        StructuralBlocker,
        PlantPrerequisite,
        PlantCannotBeRemoved,
        Fire
    }
}
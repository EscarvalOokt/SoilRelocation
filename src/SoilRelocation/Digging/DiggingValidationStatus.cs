namespace SoilRelocation.Digging
{
    internal enum DiggingValidationStatus
    {
        Valid = 0,
        UnsupportedTerrain,
        StructuralBlocker,
        PlantPrerequisite,
        PlantCannotBeRemoved,
        Fire
    }
}
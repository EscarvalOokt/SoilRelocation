namespace SoilRelocation.Placement
{
    public sealed class Designator_PlaceSoil : Designator_PlaceRelocatableTerrain
    {
        public Designator_PlaceSoil() : base(
            SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceSoilOrder,
            "SoilRelocation.Placement.Designator.Soil.Label",
            "SoilRelocation.Placement.Designator.Soil.Description")
        {
        }
    }
}
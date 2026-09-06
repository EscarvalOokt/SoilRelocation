namespace SoilRelocation.Placement
{
    public sealed class Designator_PlaceSand : Designator_PlaceRelocatableTerrain
    {
        public Designator_PlaceSand() : base(
            SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceSandOrder,
            "SoilRelocation.Placement.Designator.Sand.Label",
            "SoilRelocation.Placement.Designator.Sand.Description")
        {
        }
    }
}
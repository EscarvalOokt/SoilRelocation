namespace SoilRelocation.Placement
{
    public sealed class Designator_PlaceGravel : Designator_PlaceRelocatableTerrain
    {
        public Designator_PlaceGravel() : base(
            SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceGravelOrder,
            "SoilRelocation.Placement.Designator.Gravel.Label",
            "SoilRelocation.Placement.Designator.Gravel.Description")
        {
        }
    }
}
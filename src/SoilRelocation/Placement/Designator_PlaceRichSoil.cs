namespace SoilRelocation.Placement
{
    public sealed class Designator_PlaceRichSoil : Designator_PlaceRelocatableTerrain
    {
        public Designator_PlaceRichSoil() : base(
            SoilRelocationMaterialOrderDefOf.SoilRelocation_PlaceRichSoilOrder,
            "SoilRelocation.Placement.Designator.RichSoil.Label",
            "SoilRelocation.Placement.Designator.RichSoil.Description")
        {
        }
    }
}
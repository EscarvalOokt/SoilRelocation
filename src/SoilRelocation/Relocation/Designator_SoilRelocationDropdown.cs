using RimWorld;
using SoilRelocation.Digging;
using SoilRelocation.Placement;

namespace SoilRelocation.Relocation
{
    // ReSharper disable once InconsistentNaming
    public sealed class Designator_SoilRelocationDropdown : Designator_Dropdown
    {
        public Designator_SoilRelocationDropdown()
        {
            Add(new Designator_DigRelocatableTerrain());
            Add(new Designator_PlaceSand());
            Add(new Designator_PlaceGravel());
            Add(new Designator_PlaceSoil());
            Add(new Designator_PlaceRichSoil());
        }
    }
}
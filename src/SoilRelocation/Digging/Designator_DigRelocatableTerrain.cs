using RimWorld;
using SoilRelocation.Relocation;
using UnityEngine;
using Verse;

namespace SoilRelocation.Digging
{
    // ReSharper disable once InconsistentNaming
    public sealed class Designator_DigRelocatableTerrain : Designator_Cells
    {
        public Designator_DigRelocatableTerrain()
        {
            defaultLabel = "SoilRelocation.Digging.Designator.Label".Translate();
            defaultDesc = "SoilRelocation.Digging.Designator.Description".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/SoilRelocation/DigTerrain");
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        }

        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Mine;

        public override bool DragDrawMeasurements => true;

        public override AcceptanceReport CanDesignateCell(IntVec3 cell)
        {
            if (!cell.InBounds(Map) || cell.Fogged(Map))
            {
                return false;
            }

            if (HasDigOrderAt(Map, cell))
            {
                return "SoilRelocation.Digging.Reject.AlreadyDesignated".Translate();
            }

            DiggingValidationResult validation = DiggingValidator.Validate(Map, cell);

            switch (validation.Status)
            {
                case DiggingValidationStatus.Valid:
                case DiggingValidationStatus.PlantPrerequisite:
                    return true;

                case DiggingValidationStatus.UnsupportedTerrain:
                    return "SoilRelocation.Digging.Reject.UnsupportedTerrain".Translate();

                case DiggingValidationStatus.StructuralBlocker:
                    return "SoilRelocation.Digging.Reject.StructuralBlocker".Translate();

                case DiggingValidationStatus.PlantCannotBeRemoved:
                    return "SoilRelocation.Digging.Reject.PlantCannotBeRemoved".Translate();

                case DiggingValidationStatus.Fire:
                    return "SoilRelocation.Digging.Reject.Fire".Translate();

                default:
                    return false;
            }
        }

        public override void DesignateSingleCell(IntVec3 cell)
        {
            if (HasDigOrderAt(Map, cell))
            {
                return;
            }

            DiggingValidationResult validation = DiggingValidator.Validate(Map, cell);

            if (validation.Status == DiggingValidationStatus.PlantPrerequisite &&
                !RelocationPlantPrerequisite.TryEnsureRemoval(validation.Plant))
            {
                return;
            }

            if (validation.Status != DiggingValidationStatus.Valid &&
                validation.Status != DiggingValidationStatus.PlantPrerequisite)
            {
                return;
            }

            ThingDef orderDef = SoilRelocationDigOrderDefOf.SoilRelocation_DigOrder;
            DigRelocationOrder order = orderDef == null ? null : ThingMaker.MakeThing(orderDef) as DigRelocationOrder;

            if (order == null)
            {
                return;
            }

            GenSpawn.Spawn(order, cell, Map);
        }

        private static bool HasDigOrderAt(Map map, IntVec3 cell)
        {
            ThingDef orderDef = SoilRelocationDigOrderDefOf.SoilRelocation_DigOrder;

            if (map == null || orderDef == null || !cell.InBounds(map))
            {
                return false;
            }

            return cell.GetFirstThing(map, orderDef) != null;
        }
    }
}
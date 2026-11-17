using RimWorld;
using SoilRelocation.Domain;
using SoilRelocation.Relocation;
using UnityEngine;
using Verse;

namespace SoilRelocation.Placement
{
    // ReSharper disable once InconsistentNaming
    public abstract class Designator_PlaceRelocatableTerrain : Designator_Cells
    {
        private readonly ThingDef _orderDef;

        protected Designator_PlaceRelocatableTerrain(ThingDef orderDef, string labelKey, string descriptionKey)
        {
            _orderDef = orderDef;
            defaultLabel = labelKey.Translate();
            defaultDesc = descriptionKey.Translate();
            ConfigureIcon(orderDef);
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

            if (RelocationMaterialOrderUtility.HasBuiltInMaterialOrderAt(Map, cell))
            {
                return "SoilRelocation.Placement.Reject.AlreadyDesignated".Translate();
            }

            if (!TryResolveFamily(out RelocationFamilyDef family))
            {
                return false;
            }

            PlacementValidationResult validation = PlacementValidator.Validate(Map, cell, family);

            switch (validation.Status)
            {
                case PlacementValidationStatus.Valid:
                case PlacementValidationStatus.PlantPrerequisite:
                    return true;

                case PlacementValidationStatus.InvalidDestination:
                    return "SoilRelocation.Placement.Reject.InvalidDestination".Translate();

                case PlacementValidationStatus.NoOp:
                    return "SoilRelocation.Placement.Reject.NoOp".Translate();

                case PlacementValidationStatus.StructuralBlocker:
                    return "SoilRelocation.Placement.Reject.StructuralBlocker".Translate();

                case PlacementValidationStatus.PlantCannotBeRemoved:
                    return "SoilRelocation.Placement.Reject.PlantCannotBeRemoved".Translate();

                case PlacementValidationStatus.Fire:
                    return "SoilRelocation.Placement.Reject.Fire".Translate();

                default:
                    return false;
            }
        }

        public override void DesignateSingleCell(IntVec3 cell)
        {
            if (RelocationMaterialOrderUtility.HasBuiltInMaterialOrderAt(Map, cell) ||
                !TryResolveFamily(out RelocationFamilyDef family))
            {
                return;
            }

            PlacementValidationResult validation = PlacementValidator.Validate(Map, cell, family);

            if (validation.Status == PlacementValidationStatus.PlantPrerequisite &&
                !RelocationPlantPrerequisite.TryEnsureRemoval(validation.Plant))
            {
                return;
            }

            if (validation.Status != PlacementValidationStatus.Valid &&
                validation.Status != PlacementValidationStatus.PlantPrerequisite)
            {
                return;
            }

            var order = ThingMaker.MakeThing(_orderDef) as RelocationMaterialOrder;

            if (order == null)
            {
                return;
            }

            GenSpawn.Spawn(order, cell, Map);
        }

        private bool TryResolveFamily(out RelocationFamilyDef family)
        {
            family = null;

            return RelocationMaterialOrderUtility.IsBuiltInMaterialOrder(_orderDef) &&
                   RelocationMaterialOrderUtility.TryResolveFamily(_orderDef, out family);
        }

        private void ConfigureIcon(ThingDef orderDef)
        {
            if (!RelocationMaterialOrderUtility.IsBuiltInMaterialOrder(orderDef) ||
                !RelocationMaterialOrderUtility.TryResolveFamily(orderDef, out RelocationFamilyDef family))
            {
                return;
            }

            GraphicData graphicData = family.CanonicalPortableResource?.graphicData;

            if (graphicData == null || graphicData.texPath.NullOrEmpty())
            {
                return;
            }

            icon = ContentFinder<Texture2D>.Get(graphicData.texPath);
            defaultIconColor = graphicData.color;
        }
    }
}
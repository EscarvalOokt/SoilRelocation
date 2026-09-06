using RimWorld;
using Verse;

namespace SoilRelocation.Relocation
{
    internal static class RelocationPlantPrerequisite
    {
        internal static bool CanEnsureRemoval(Plant plant)
        {
            if (plant == null || plant.Destroyed || !plant.Spawned)
            {
                return false;
            }

            if (HasRemovalDesignation(plant))
            {
                return true;
            }

            var harvestWoodDesignator = new Designator_PlantsHarvestWood();

            if (harvestWoodDesignator.CanDesignateThing(plant).Accepted)
            {
                return true;
            }

            var cutPlantDesignator = new Designator_PlantsCut();
            return cutPlantDesignator.CanDesignateThing(plant).Accepted;
        }

        internal static bool TryEnsureRemoval(Plant plant)
        {
            if (plant == null || plant.Destroyed || !plant.Spawned)
            {
                return false;
            }

            if (HasRemovalDesignation(plant))
            {
                return true;
            }

            var harvestWoodDesignator = new Designator_PlantsHarvestWood();

            if (harvestWoodDesignator.CanDesignateThing(plant).Accepted)
            {
                harvestWoodDesignator.DesignateThing(plant);
                return HasRemovalDesignation(plant);
            }

            var cutPlantDesignator = new Designator_PlantsCut();

            if (!cutPlantDesignator.CanDesignateThing(plant).Accepted)
            {
                return false;
            }

            cutPlantDesignator.DesignateThing(plant);
            return HasRemovalDesignation(plant);
        }

        private static bool HasRemovalDesignation(Plant plant)
        {
            DesignationManager designationManager = plant.Map?.designationManager;

            if (designationManager == null)
            {
                return false;
            }

            return designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant) != null ||
                   designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) != null;
        }
    }
}
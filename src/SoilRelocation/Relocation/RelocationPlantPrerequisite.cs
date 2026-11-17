using RimWorld;
using Verse;

namespace SoilRelocation.Relocation
{
    internal static class RelocationPlantPrerequisite
    {
        internal static bool CanEnsureRemoval(Plant plant)
        {
            return ResolveRemovalDesignation(plant) != null;
        }

        internal static bool TryEnsureRemoval(Plant plant)
        {
            DesignationDef removal = ResolveRemovalDesignation(plant);

            if (removal == null)
            {
                return false;
            }

            DesignationManager manager = plant.Map.designationManager;
            if (manager.DesignationOn(plant, removal) != null)
            {
                return true;
            }

            Designator_PlantsHarvestWood.PossiblyWarnPlayerImportantPlantDesignateCut(plant);
            if (ModsConfig.IdeologyActive && plant.def.plant.IsTree && plant.def.plant.treeLoversCareIfChopped)
            {
                Designator_PlantsHarvestWood.PossiblyWarnPlayerOnDesignatingTreeCut();
            }

            // Plant designators write to Find.CurrentMap, while work givers run on every loaded map.
            manager.RemoveAllDesignationsOn(plant);
            manager.AddDesignation(new Designation(plant, removal));
            return manager.DesignationOn(plant, removal) != null;
        }

        private static DesignationDef ResolveRemovalDesignation(Plant plant)
        {
            if (plant is not { Spawned: true, def.plant: not null })
            {
                return null;
            }

            DesignationManager manager = plant.Map.designationManager;
            Designation existing = manager.DesignationOn(plant, DesignationDefOf.HarvestPlant) ??
                                   manager.DesignationOn(plant, DesignationDefOf.CutPlant);
            if (existing != null)
            {
                return existing.def;
            }

            if (plant.TryGetComp(out CompPlantPreventCutting comp) && comp.PreventCutting)
            {
                return null;
            }

            return plant.def.plant.IsTree && plant.HarvestableNow
                ? DesignationDefOf.HarvestPlant
                : DesignationDefOf.CutPlant;
        }
    }
}
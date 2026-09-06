using RimWorld;
using SoilRelocation.Domain;
using SoilRelocation.Relocation;
using SoilRelocation.Settings;
using Verse;

namespace SoilRelocation.Digging
{
    internal static class DiggingValidator
    {
        internal static DiggingValidationResult Validate(Map map, IntVec3 cell)
        {
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            bool allowRelocationUnderConduits = settings?.AllowRelocationUnderConduits ??
                                                SoilRelocationSettings.DefaultAllowRelocationUnderConduits;

            return Validate(map, cell, allowRelocationUnderConduits);
        }

        internal static DiggingValidationResult Validate(Map map, IntVec3 cell, bool allowRelocationUnderConduits)
        {
            if (map == null || !cell.InBounds(map))
            {
                return new DiggingValidationResult(DiggingValidationStatus.UnsupportedTerrain);
            }

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            RelocationRuleResolution rule = RelocationRuleResolver.Resolve(terrain);

            if (!rule.IsSupported)
            {
                return new DiggingValidationResult(DiggingValidationStatus.UnsupportedTerrain);
            }

            if (RelocationCellContentPolicy.HasStructuralBlocker(map, cell, allowRelocationUnderConduits))
            {
                return new DiggingValidationResult(DiggingValidationStatus.StructuralBlocker, rule.Family);
            }

            if (RelocationCellContentPolicy.HasFire(map, cell))
            {
                return new DiggingValidationResult(DiggingValidationStatus.Fire, rule.Family);
            }

            Plant plant = RelocationCellContentPolicy.FindPlant(map, cell);

            if (plant != null)
            {
                return RelocationPlantPrerequisite.CanEnsureRemoval(plant)
                    ? new DiggingValidationResult(DiggingValidationStatus.PlantPrerequisite, rule.Family, plant)
                    : new DiggingValidationResult(DiggingValidationStatus.PlantCannotBeRemoved, rule.Family, plant);
            }

            return new DiggingValidationResult(DiggingValidationStatus.Valid, rule.Family);
        }
    }
}
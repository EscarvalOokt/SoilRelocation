using RimWorld;
using SoilRelocation.Domain;
using SoilRelocation.Relocation;
using SoilRelocation.Settings;
using Verse;

namespace SoilRelocation.Placement
{
    internal static class PlacementValidator
    {
        internal static PlacementValidationResult Validate(Map map, IntVec3 cell, RelocationFamilyDef family)
        {
            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            PlacementResolutionMode placementResolutionMode = settings?.PlacementResolutionMode ??
                                                              SoilRelocationSettings.DefaultPlacementResolutionMode;

            bool allowRelocationUnderConduits = settings?.AllowRelocationUnderConduits ??
                                                SoilRelocationSettings.DefaultAllowRelocationUnderConduits;

            return Validate(map, cell, family, placementResolutionMode, allowRelocationUnderConduits);
        }

        internal static PlacementValidationResult Validate(
            Map map,
            IntVec3 cell,
            RelocationFamilyDef family,
            PlacementResolutionMode placementResolutionMode,
            bool allowRelocationUnderConduits)
        {
            if (map == null || !cell.InBounds(map) || !RelocationRuleResolver.IsFamilyValid(family))
            {
                return new PlacementValidationResult(PlacementValidationStatus.InvalidFamily, family);
            }

            TerrainDef currentTerrain = map.terrainGrid.TerrainAt(cell);

            if (!RelocationPlacementPolicy.IsDestinationEligible(currentTerrain))
            {
                return new PlacementValidationResult(
                    PlacementValidationStatus.InvalidDestination,
                    family,
                    currentTerrain);
            }

            TerrainDef resolvedTerrain = PlacementTerrainResolver.Resolve(map, cell, family, placementResolutionMode);

            if (resolvedTerrain == null)
            {
                return new PlacementValidationResult(PlacementValidationStatus.InvalidFamily, family, currentTerrain);
            }

            if (RelocationPlacementPolicy.IsNoOp(currentTerrain, resolvedTerrain))
            {
                return new PlacementValidationResult(
                    PlacementValidationStatus.NoOp,
                    family,
                    currentTerrain,
                    resolvedTerrain);
            }

            if (RelocationCellContentPolicy.HasStructuralBlocker(map, cell, allowRelocationUnderConduits))
            {
                return new PlacementValidationResult(
                    PlacementValidationStatus.StructuralBlocker,
                    family,
                    currentTerrain,
                    resolvedTerrain);
            }

            if (RelocationCellContentPolicy.HasFire(map, cell))
            {
                return new PlacementValidationResult(
                    PlacementValidationStatus.Fire,
                    family,
                    currentTerrain,
                    resolvedTerrain);
            }

            Plant plant = RelocationCellContentPolicy.FindPlant(map, cell);

            if (plant != null)
            {
                return RelocationPlantPrerequisite.CanEnsureRemoval(plant)
                    ? new PlacementValidationResult(
                        PlacementValidationStatus.PlantPrerequisite,
                        family,
                        currentTerrain,
                        resolvedTerrain,
                        plant)
                    : new PlacementValidationResult(
                        PlacementValidationStatus.PlantCannotBeRemoved,
                        family,
                        currentTerrain,
                        resolvedTerrain,
                        plant);
            }

            return new PlacementValidationResult(
                PlacementValidationStatus.Valid,
                family,
                currentTerrain,
                resolvedTerrain);
        }
    }
}
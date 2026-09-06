using RimWorld;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Placement
{
    internal static class PlacementResultApplier
    {
        internal static bool TryApply(RelocationMaterialOrder order, PlacementValidationResult validation)
        {
            if (order == null || order.Destroyed || !order.Spawned || order.Map == null ||
                !RelocationMaterialOrderUtility.IsBuiltInMaterialOrder(order.def) ||
                !RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family) ||
                !ReferenceEquals(validation.Family, family) || !validation.IsValid ||
                validation.ResolvedTerrain == null || !order.TryGetContainedMaterial(out Thing material) ||
                material == null || material.Destroyed ||
                !ReferenceEquals(material.def, family.CanonicalPortableResource) || material.stackCount != 1)
            {
                return false;
            }

            Map map = order.Map;
            IntVec3 cell = order.Position;

            if (!cell.InBounds(map) || !ReferenceEquals(map.terrainGrid.TerrainAt(cell), validation.CurrentTerrain) ||
                ReferenceEquals(validation.CurrentTerrain, validation.ResolvedTerrain))
            {
                return false;
            }

            if (!order.TryConsumeMaterial())
            {
                return false;
            }

            map.terrainGrid.SetTerrain(cell, validation.ResolvedTerrain);
            map.snowGrid.SetDepth(cell, 0f);
            FilthMaker.RemoveAllFilth(cell, map);

            order.Destroy();

            return order.Destroyed;
        }
    }
}
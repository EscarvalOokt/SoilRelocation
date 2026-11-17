using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace SoilRelocation.Domain
{
    internal static class RelocationPlacementPolicy
    {
        private const string IceTerrainDefName = "Ice";
        private const string VolcanicRockTerrainDefName = "VolcanicRock";
        private const string SmoothVolcanicRockTerrainDefName = "VolcanicRock_Smooth";

        private static Lazy<HashSet<TerrainDef>> _naturalStoneDestinationTerrains =
            CreateNaturalStoneDestinationCache();

        internal static bool IsDestinationEligible(TerrainDef currentTerrain)
        {
            if (currentTerrain == null)
            {
                return false;
            }

            if (currentTerrain.IsRock)
            {
                return true;
            }

            if (MatchesFinalTerrainDef(currentTerrain, IceTerrainDefName) ||
                MatchesFinalTerrainDef(currentTerrain, VolcanicRockTerrainDefName) ||
                MatchesFinalTerrainDef(currentTerrain, SmoothVolcanicRockTerrainDefName))
            {
                return true;
            }

            return _naturalStoneDestinationTerrains.Value.Contains(currentTerrain);
        }

        internal static void InvalidateNaturalStoneDestinationCache()
        {
            _naturalStoneDestinationTerrains = CreateNaturalStoneDestinationCache();
        }

        internal static HashSet<TerrainDef> BuildNaturalStoneDestinationTerrains(IEnumerable<ThingDef> thingDefs)
        {
            var result = new HashSet<TerrainDef>(TerrainDefReferenceComparer.Instance);

            if (thingDefs == null)
            {
                return result;
            }

            foreach (ThingDef thingDef in thingDefs)
            {
                if (!IsNaturalStoneRoot(thingDef))
                {
                    continue;
                }

                TerrainDef naturalTerrain = thingDef.building.naturalTerrain;
                TerrainDef leaveTerrain = thingDef.building.leaveTerrain;

                AddTerrain(result, naturalTerrain);
                AddTerrain(result, leaveTerrain);
                AddTerrain(result, naturalTerrain.smoothedTerrain);
                AddTerrain(result, leaveTerrain?.smoothedTerrain);
            }

            return result;
        }

        internal static TerrainDef ResolveTerrain(
            RelocationFamilyDef family,
            PlacementResolutionMode resolutionMode,
            TerrainDef biomeGravelTerrain,
            IEnumerable<TerrainDef> biomeFertilityTerrains)
        {
            if (!RelocationRuleResolver.IsFamilyValid(family))
            {
                return null;
            }

            if (resolutionMode == PlacementResolutionMode.Canonical)
            {
                return family.CanonicalTerrain;
            }

            if (resolutionMode != PlacementResolutionMode.BiomeNative)
            {
                return null;
            }

            if (IsSupportedFamilyCandidate(biomeGravelTerrain, family))
            {
                return biomeGravelTerrain;
            }

            TerrainDef uniqueCandidate = null;

            if (biomeFertilityTerrains != null)
            {
                foreach (TerrainDef terrain in biomeFertilityTerrains)
                {
                    if (!IsSupportedFamilyCandidate(terrain, family))
                    {
                        continue;
                    }

                    if (uniqueCandidate == null)
                    {
                        uniqueCandidate = terrain;
                        continue;
                    }

                    if (!ReferenceEquals(uniqueCandidate, terrain))
                    {
                        return family.CanonicalTerrain;
                    }
                }
            }

            return uniqueCandidate ?? family.CanonicalTerrain;
        }

        internal static bool IsNoOp(TerrainDef currentTerrain, TerrainDef resolvedTerrain)
        {
            return ReferenceEquals(currentTerrain, resolvedTerrain);
        }

        private static Lazy<HashSet<TerrainDef>> CreateNaturalStoneDestinationCache()
        {
            return new Lazy<HashSet<TerrainDef>>(() =>
                BuildNaturalStoneDestinationTerrains(DefDatabase<ThingDef>.AllDefsListForReading));
        }

        private static bool MatchesFinalTerrainDef(TerrainDef currentTerrain, string defName)
        {
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
            return terrain != null && ReferenceEquals(currentTerrain, terrain);
        }

        private static bool IsNaturalStoneRoot(ThingDef thingDef)
        {
            return thingDef?.building is { isNaturalRock: true, isResourceRock: false, naturalTerrain.IsRock: true };
        }

        private static void AddTerrain(HashSet<TerrainDef> terrains, TerrainDef terrain)
        {
            if (terrain != null)
            {
                terrains.Add(terrain);
            }
        }

        private static bool IsSupportedFamilyCandidate(TerrainDef terrain, RelocationFamilyDef family)
        {
            if (terrain == null)
            {
                return false;
            }

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);
            return resolution.IsSupported && ReferenceEquals(resolution.Family, family);
        }

        private sealed class TerrainDefReferenceComparer : IEqualityComparer<TerrainDef>
        {
            internal static readonly TerrainDefReferenceComparer Instance = new TerrainDefReferenceComparer();

            public bool Equals(TerrainDef x, TerrainDef y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(TerrainDef obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
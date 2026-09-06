using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
using RimWorld;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Tests.Domain
{
    [TestFixture]
    public sealed class RelocationPlacementPolicyTests
    {
        [Test]
        public void IsDestinationEligible_NaturalRock_ReturnsTrue()
        {
            TerrainDef terrain = CreateNaturalRockTerrain();

            bool result = RelocationPlacementPolicy.IsDestinationEligible(terrain);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsDestinationEligible_NonRock_ReturnsFalse()
        {
            bool result = RelocationPlacementPolicy.IsDestinationEligible(CreateTerrain());

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsDestinationEligible_UnrelatedIceTaggedTerrain_ReturnsFalse()
        {
            TerrainDef terrain = CreateTerrain();
            terrain.tags = new List<string> { "Ice" };

            bool result = RelocationPlacementPolicy.IsDestinationEligible(terrain);

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsDestinationEligible_UnrelatedNaturalStoneLikeTerrain_ReturnsFalse()
        {
            TerrainDef terrain = CreateTerrain();
            TerrainDef smoothTerrain = CreateTerrain();

            terrain.natural = true;
            terrain.categoryType = TerrainDef.TerrainCategoryType.Stone;
            terrain.smoothedTerrain = smoothTerrain;

            smoothTerrain.natural = true;
            smoothTerrain.categoryType = TerrainDef.TerrainCategoryType.Stone;

            bool result = RelocationPlacementPolicy.IsDestinationEligible(terrain);

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsDestinationEligible_Null_ReturnsFalse()
        {
            bool result = RelocationPlacementPolicy.IsDestinationEligible(null);

            Assert.That(result, Is.False);
        }

        [Test]
        public void BuildNaturalStoneDestinationTerrains_ValidRoot_IncludesRoughRoughHewnAndSmooth()
        {
            TerrainDef rough = CreateNaturalRockTerrain();
            TerrainDef roughHewn = CreateTerrain();
            TerrainDef smooth = CreateTerrain();
            rough.smoothedTerrain = smooth;
            roughHewn.smoothedTerrain = smooth;
            ThingDef root = CreateNaturalRockThing(rough, roughHewn);

            HashSet<TerrainDef> result = RelocationPlacementPolicy.BuildNaturalStoneDestinationTerrains(new[] { root });

            Assert.That(result.Contains(rough), Is.True);
            Assert.That(result.Contains(roughHewn), Is.True);
            Assert.That(result.Contains(smooth), Is.True);
        }

        [Test]
        public void BuildNaturalStoneDestinationTerrains_DistinctSmoothReferences_IncludesBoth()
        {
            TerrainDef rough = CreateNaturalRockTerrain();
            TerrainDef roughHewn = CreateTerrain();
            TerrainDef smoothFromRough = CreateTerrain();
            TerrainDef smoothFromRoughHewn = CreateTerrain();
            rough.smoothedTerrain = smoothFromRough;
            roughHewn.smoothedTerrain = smoothFromRoughHewn;
            ThingDef root = CreateNaturalRockThing(rough, roughHewn);

            HashSet<TerrainDef> result = RelocationPlacementPolicy.BuildNaturalStoneDestinationTerrains(new[] { root });

            Assert.That(result.Contains(smoothFromRough), Is.True);
            Assert.That(result.Contains(smoothFromRoughHewn), Is.True);
        }

        [Test]
        public void BuildNaturalStoneDestinationTerrains_NonRockNaturalTerrain_IgnoresRoot()
        {
            TerrainDef ice = CreateTerrain();
            ThingDef solidIce = CreateNaturalRockThing(ice, ice);

            HashSet<TerrainDef> result =
                RelocationPlacementPolicy.BuildNaturalStoneDestinationTerrains(new[] { solidIce });

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildNaturalStoneDestinationTerrains_ResourceRock_IgnoresDerivedTerrains()
        {
            TerrainDef rough = CreateNaturalRockTerrain();
            TerrainDef roughHewn = CreateTerrain();
            ThingDef resourceRock = CreateNaturalRockThing(rough, roughHewn, isResourceRock: true);

            HashSet<TerrainDef> result =
                RelocationPlacementPolicy.BuildNaturalStoneDestinationTerrains(new[] { resourceRock });

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildNaturalStoneDestinationTerrains_StandaloneStoneWithSmoothResult_DoesNotIncludeEitherTerrain()
        {
            TerrainDef volcanicRock = CreateTerrain();
            TerrainDef smoothVolcanicRock = CreateTerrain();
            volcanicRock.categoryType = TerrainDef.TerrainCategoryType.Stone;
            smoothVolcanicRock.categoryType = TerrainDef.TerrainCategoryType.Stone;
            volcanicRock.smoothedTerrain = smoothVolcanicRock;

            ThingDef unrelatedRoot = CreateUninitialized<ThingDef>();
            unrelatedRoot.building = new BuildingProperties
            {
                isNaturalRock = false,
                naturalTerrain = volcanicRock
            };

            HashSet<TerrainDef> result =
                RelocationPlacementPolicy.BuildNaturalStoneDestinationTerrains(new[] { unrelatedRoot });

            Assert.That(result.Contains(volcanicRock), Is.False);
            Assert.That(result.Contains(smoothVolcanicRock), Is.False);
        }

        [Test]
        public void BuildNaturalStoneDestinationTerrains_ConstructedStoneFloor_DoesNotIncludeTerrain()
        {
            TerrainDef constructedFloor = CreateTerrain();
            constructedFloor.categoryType = TerrainDef.TerrainCategoryType.Stone;
            constructedFloor.tags = new List<string> { "Floor" };

            TerrainDef rough = CreateNaturalRockTerrain();
            ThingDef root = CreateNaturalRockThing(rough, CreateTerrain());

            HashSet<TerrainDef> result = RelocationPlacementPolicy.BuildNaturalStoneDestinationTerrains(new[] { root });

            Assert.That(result.Contains(rough), Is.True);
            Assert.That(result.Contains(constructedFloor), Is.False);
        }

        [Test]
        public void BuildNaturalStoneDestinationTerrains_ExplicitLeaveTerrain_FollowsFinalRootReference()
        {
            TerrainDef rough = CreateNaturalRockTerrain();
            TerrainDef explicitLeaveTerrain = CreateTerrain();
            explicitLeaveTerrain.categoryType = TerrainDef.TerrainCategoryType.Misc;
            ThingDef root = CreateNaturalRockThing(rough, explicitLeaveTerrain);

            HashSet<TerrainDef> result = RelocationPlacementPolicy.BuildNaturalStoneDestinationTerrains(new[] { root });

            Assert.That(result.Contains(explicitLeaveTerrain), Is.True);
        }

        [Test]
        public void BuildNaturalStoneDestinationTerrains_NullDefinitions_ReturnsEmptySet()
        {
            HashSet<TerrainDef> result = RelocationPlacementPolicy.BuildNaturalStoneDestinationTerrains(null);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ResolveTerrain_Canonical_ReturnsCanonicalTerrain()
        {
            RelocationFamilyDef family = CreateFamily();
            TerrainDef biomeGravel = CreateSupportedTerrain(family);
            TerrainDef fertilityTerrain = CreateSupportedTerrain(family);

            TerrainDef result = RelocationPlacementPolicy.ResolveTerrain(
                family,
                PlacementResolutionMode.Canonical,
                biomeGravel,
                new[] { fertilityTerrain });

            Assert.That(result, Is.SameAs(family.CanonicalTerrain));
        }

        [Test]
        public void ResolveTerrain_BiomeNative_UsesSameFamilyGravelTerrainFirst()
        {
            RelocationFamilyDef family = CreateFamily();
            TerrainDef biomeGravel = CreateSupportedTerrain(family);
            TerrainDef fertilityTerrain = CreateSupportedTerrain(family);

            TerrainDef result = RelocationPlacementPolicy.ResolveTerrain(
                family,
                PlacementResolutionMode.BiomeNative,
                biomeGravel,
                new[] { fertilityTerrain });

            Assert.That(result, Is.SameAs(biomeGravel));
        }

        [Test]
        public void ResolveTerrain_BiomeNative_UsesExactlyOneSameFamilyFertilityCandidate()
        {
            RelocationFamilyDef family = CreateFamily();
            TerrainDef candidate = CreateSupportedTerrain(family);

            TerrainDef result = RelocationPlacementPolicy.ResolveTerrain(
                family,
                PlacementResolutionMode.BiomeNative,
                null,
                new[] { CreateTerrain(), candidate, candidate });

            Assert.That(result, Is.SameAs(candidate));
        }

        [Test]
        public void ResolveTerrain_BiomeNative_IgnoresOtherFamilyCandidates()
        {
            RelocationFamilyDef requestedFamily = CreateFamily();
            RelocationFamilyDef otherFamily = CreateFamily();
            TerrainDef requestedCandidate = CreateSupportedTerrain(requestedFamily);
            TerrainDef otherCandidate = CreateSupportedTerrain(otherFamily);

            TerrainDef result = RelocationPlacementPolicy.ResolveTerrain(
                requestedFamily,
                PlacementResolutionMode.BiomeNative,
                otherCandidate,
                new[] { otherCandidate, requestedCandidate });

            Assert.That(result, Is.SameAs(requestedCandidate));
        }

        [Test]
        public void ResolveTerrain_BiomeNative_ZeroCandidates_FallsBackToCanonical()
        {
            RelocationFamilyDef family = CreateFamily();

            TerrainDef result = RelocationPlacementPolicy.ResolveTerrain(
                family,
                PlacementResolutionMode.BiomeNative,
                null,
                new[] { CreateTerrain() });

            Assert.That(result, Is.SameAs(family.CanonicalTerrain));
        }

        [Test]
        public void ResolveTerrain_BiomeNative_MultipleDistinctCandidates_FallsBackToCanonical()
        {
            RelocationFamilyDef family = CreateFamily();
            TerrainDef first = CreateSupportedTerrain(family);
            TerrainDef second = CreateSupportedTerrain(family);

            TerrainDef result = RelocationPlacementPolicy.ResolveTerrain(
                family,
                PlacementResolutionMode.BiomeNative,
                null,
                new[] { first, second });

            Assert.That(result, Is.SameAs(family.CanonicalTerrain));
        }

        [Test]
        public void IsNoOp_SameTerrainReference_ReturnsTrue()
        {
            TerrainDef terrain = CreateTerrain();

            bool result = RelocationPlacementPolicy.IsNoOp(terrain, terrain);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsNoOp_DifferentTerrainReferences_ReturnsFalse()
        {
            TerrainDef currentTerrain = CreateTerrain();
            TerrainDef resolvedTerrain = CreateTerrain();

            bool result = RelocationPlacementPolicy.IsNoOp(currentTerrain, resolvedTerrain);

            Assert.That(result, Is.False);
        }

        private static RelocationFamilyDef CreateFamily()
        {
            return new RelocationFamilyDef
            {
                CanonicalTerrain = CreateTerrain(),
                CanonicalPortableResource = CreateUninitialized<ThingDef>()
            };
        }

        private static TerrainDef CreateSupportedTerrain(RelocationFamilyDef family)
        {
            TerrainDef terrain = CreateTerrain();
            terrain.modExtensions = new List<DefModExtension>
            {
                new RelocationTerrainExtension
                {
                    Family = family
                }
            };

            return terrain;
        }

        private static ThingDef CreateNaturalRockThing(
            TerrainDef naturalTerrain,
            TerrainDef leaveTerrain,
            bool isResourceRock = false)
        {
            ThingDef thingDef = CreateUninitialized<ThingDef>();
            thingDef.building = new BuildingProperties
            {
                isNaturalRock = true,
                isResourceRock = isResourceRock,
                naturalTerrain = naturalTerrain,
                leaveTerrain = leaveTerrain
            };

            return thingDef;
        }

        private static TerrainDef CreateNaturalRockTerrain()
        {
            TerrainDef terrain = CreateTerrain();
            terrain.tags = new List<string> { "NaturalRock" };
            return terrain;
        }

        private static TerrainDef CreateTerrain()
        {
            return CreateUninitialized<TerrainDef>();
        }

        private static T CreateUninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
    }
}
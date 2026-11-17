using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Tests.Domain
{
    [TestFixture]
    public sealed class RelocationRuleResolverTests
    {
        [Test]
        public void Resolve_ValidMembership_ReturnsSupportedFamily()
        {
            RelocationFamilyDef family = CreateValidFamily();
            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Family = family
                });

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.Supported));
            Assert.That(resolution.IsSupported, Is.True);
            Assert.That(resolution.Family, Is.SameAs(family));
        }

        [Test]
        public void Resolve_NoMembership_ReturnsUnsupportedNoMembership()
        {
            TerrainDef terrain = CreateTerrain();

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedNoMembership));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_DriesToWithValidMembership_ReturnsUnsupportedDriesTo()
        {
            RelocationFamilyDef family = CreateValidFamily();
            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Family = family
                });

            terrain.driesTo = CreateUninitialized<TerrainDef>();

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedDriesTo));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void Resolve_TemporaryTerrainOrFoundationWithMembership_IsUnsupported(bool temporary, bool isFoundation)
        {
            TerrainDef terrain = CreateTerrain(new RelocationTerrainExtension { Family = CreateValidFamily() });
            terrain.temporary = temporary;
            terrain.isFoundation = isFoundation;

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedTerrainLayer));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_ExplicitExclusion_ReturnsUnsupportedExplicitExclusion()
        {
            RelocationFamilyDef family = CreateValidFamily();
            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Enabled = false,
                    Family = family
                });

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedExplicitExclusion));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_ExplicitExclusionWithoutFamily_ReturnsExplicitExclusion()
        {
            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Enabled = false,
                    Family = null
                });

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedExplicitExclusion));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_EnabledMembershipWithoutFamily_ReturnsUnsupportedInvalidMembership()
        {
            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Enabled = true,
                    Family = null
                });

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedInvalidMembership));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_FamilyWithoutCanonicalTerrain_ReturnsUnsupportedInvalidFamily()
        {
            var family = new RelocationFamilyDef
            {
                CanonicalPortableResource = CreateUninitialized<ThingDef>()
            };

            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Family = family
                });

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedInvalidFamily));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_FamilyWithoutCanonicalPortableResource_ReturnsUnsupportedInvalidFamily()
        {
            var family = new RelocationFamilyDef
            {
                CanonicalTerrain = CreateUninitialized<TerrainDef>()
            };

            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Family = family
                });

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedInvalidFamily));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_DuplicateMemberships_ReturnsUnsupportedAmbiguousMembership()
        {
            RelocationFamilyDef firstFamily = CreateValidFamily();
            RelocationFamilyDef secondFamily = CreateValidFamily();

            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Family = firstFamily
                },
                new RelocationTerrainExtension
                {
                    Family = secondFamily
                });

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedAmbiguousMembership));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_MixedDuplicateMemberships_DoesNotUseFirstOrLastExtension()
        {
            RelocationFamilyDef family = CreateValidFamily();

            TerrainDef enabledFirst = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Enabled = true,
                    Family = family
                },
                new RelocationTerrainExtension
                {
                    Enabled = false
                });

            TerrainDef excludedFirst = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Enabled = false
                },
                new RelocationTerrainExtension
                {
                    Enabled = true,
                    Family = family
                });

            RelocationRuleResolution enabledFirstResolution = RelocationRuleResolver.Resolve(enabledFirst);
            RelocationRuleResolution excludedFirstResolution = RelocationRuleResolver.Resolve(excludedFirst);

            Assert.That(enabledFirstResolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedAmbiguousMembership));

            Assert.That(enabledFirstResolution.IsSupported, Is.False);
            Assert.That(enabledFirstResolution.Family, Is.Null);

            Assert.That(
                excludedFirstResolution.Status,
                Is.EqualTo(RelocationRuleStatus.UnsupportedAmbiguousMembership));

            Assert.That(excludedFirstResolution.IsSupported, Is.False);
            Assert.That(excludedFirstResolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_UnrelatedModExtension_ReturnsUnsupportedNoMembership()
        {
            TerrainDef terrain = CreateTerrain(new TestModExtension());

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.UnsupportedNoMembership));
            Assert.That(resolution.IsSupported, Is.False);
            Assert.That(resolution.Family, Is.Null);
        }

        [Test]
        public void Resolve_TwoTerrainsInSameFamily_ReturnSameCanonicalFamilyData()
        {
            RelocationFamilyDef family = CreateValidFamily();

            TerrainDef firstTerrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Family = family
                });

            TerrainDef secondTerrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Family = family
                });

            RelocationRuleResolution firstResolution = RelocationRuleResolver.Resolve(firstTerrain);
            RelocationRuleResolution secondResolution = RelocationRuleResolver.Resolve(secondTerrain);

            Assert.That(firstResolution.IsSupported, Is.True);
            Assert.That(secondResolution.IsSupported, Is.True);

            Assert.That(firstResolution.Family, Is.SameAs(family));
            Assert.That(secondResolution.Family, Is.SameAs(family));
            Assert.That(firstResolution.Family, Is.SameAs(secondResolution.Family));

            Assert.That(firstResolution.Family.CanonicalTerrain, Is.SameAs(secondResolution.Family.CanonicalTerrain));

            Assert.That(
                firstResolution.Family.CanonicalPortableResource,
                Is.SameAs(secondResolution.Family.CanonicalPortableResource));
        }

        [Test]
        public void Resolve_FinalMembershipFamily_ReturnsThatFamily()
        {
            RelocationFamilyDef firstFamily = CreateValidFamily();
            RelocationFamilyDef finalFamily = CreateValidFamily();

            TerrainDef terrain = CreateTerrain(
                new RelocationTerrainExtension
                {
                    Family = finalFamily
                });

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Assert.That(resolution.Status, Is.EqualTo(RelocationRuleStatus.Supported));
            Assert.That(resolution.IsSupported, Is.True);
            Assert.That(resolution.Family, Is.SameAs(finalFamily));
            Assert.That(resolution.Family, Is.Not.SameAs(firstFamily));
        }

        [Test]
        public void Resolve_NullTerrain_ThrowsArgumentNullException()
        {
            void Action()
            {
                RelocationRuleResolver.Resolve(null);
            }

            Assert.Throws<ArgumentNullException>((Action)Action);
        }

        private static RelocationFamilyDef CreateValidFamily()
        {
            return new RelocationFamilyDef
            {
                CanonicalTerrain = CreateUninitialized<TerrainDef>(),
                CanonicalPortableResource = CreateUninitialized<ThingDef>()
            };
        }

        private static TerrainDef CreateTerrain(params DefModExtension[] extensions)
        {
            TerrainDef terrain = CreateUninitialized<TerrainDef>();

            if (extensions.Length > 0)
            {
                terrain.modExtensions = new List<DefModExtension>(extensions);
            }

            return terrain;
        }

        private static T CreateUninitialized<T>() where T : class
        {
            return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        }

        private sealed class TestModExtension : DefModExtension
        {
        }
    }
}
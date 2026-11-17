using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using RimWorld;
using SoilRelocation.Substrate;
using Verse;

namespace SoilRelocation.Tests.Substrate
{
    [TestFixture]
    public sealed class NaturalRockProfileRepairerTests
    {
        [Test]
        public void TryRepair_AllSavedRocksResolvable_ReturnsUnchangedEquivalentProfile()
        {
            ThingDef granite = CreateRock("Granite");
            ThingDef marble = CreateRock("Marble");

            NaturalRockReconstructionProfile profile = CreateProfile("Granite", "Marble");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(granite, marble),
                [],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironmentRecovery);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.False);
            Assert.That(usedEnvironmentRecovery, Is.False);
            Assert.That(repaired.IsEquivalentTo(profile), Is.True);
        }

        [Test]
        public void TryRepair_MissingSlot_UsesResolvableSavedProfileRock()
        {
            ThingDef granite = CreateRock("Granite");
            ThingDef marble = CreateRock("Marble");
            ThingDef environmentRock = CreateRock("Slate");

            NaturalRockReconstructionProfile profile = CreateProfile("Granite", "MissingRock", "Marble");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(granite, marble),
                [
                    environmentRock
                ],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironmentRecovery);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(usedEnvironmentRecovery, Is.False);

            Assert.That(repaired.SlotCount, Is.EqualTo(3));
            Assert.That(repaired.GetRockDefName(0), Is.EqualTo("Granite"));
            Assert.That(repaired.GetRockDefName(1), Is.EqualTo("Marble"));
            Assert.That(repaired.GetRockDefName(2), Is.EqualTo("Marble"));
        }

        [Test]
        public void TryRepair_DoesNotRemoveOrRenumberSlots()
        {
            ThingDef granite = CreateRock("Granite");
            ThingDef marble = CreateRock("Marble");

            NaturalRockReconstructionProfile profile = CreateProfile("MissingA", "Granite", "MissingB", "Marble");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(granite, marble),
                [],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironmentRecovery);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(usedEnvironmentRecovery, Is.False);

            Assert.That(repaired.SlotCount, Is.EqualTo(profile.SlotCount));
            Assert.That(repaired.GetRockDefName(1), Is.EqualTo("Granite"));
            Assert.That(repaired.GetRockDefName(3), Is.EqualTo("Marble"));
        }

        [Test]
        public void TryRepair_MultipleMissingSlots_MayUseSameReplacement()
        {
            ThingDef granite = CreateRock("Granite");

            NaturalRockReconstructionProfile profile = CreateProfile("Granite", "MissingA", "MissingB");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(granite),
                [],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironmentRecovery);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(usedEnvironmentRecovery, Is.False);

            Assert.That(repaired.GetRockDefName(0), Is.EqualTo("Granite"));
            Assert.That(repaired.GetRockDefName(1), Is.EqualTo("Granite"));
            Assert.That(repaired.GetRockDefName(2), Is.EqualTo("Granite"));
        }

        [Test]
        public void TryRepair_RepairedSlot_DoesNotBecomeCandidateForLaterMissingSlot()
        {
            ThingDef granite = CreateRock("Granite");
            ThingDef marble = CreateRock("Marble");

            NaturalRockReconstructionProfile profile = CreateProfile("MissingA", "Granite", "Marble", "MissingB");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(granite, marble),
                [],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironmentRecovery);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(usedEnvironmentRecovery, Is.False);

            Assert.That(repaired.GetRockDefName(0), Is.EqualTo("Granite"));
            Assert.That(repaired.GetRockDefName(3), Is.EqualTo("Marble"));
        }

        [Test]
        public void TryRepair_NoSavedRocksResolvable_UsesCurrentEnvironment()
        {
            ThingDef granite = CreateRock("Granite");
            ThingDef marble = CreateRock("Marble");
            ThingDef slate = CreateRock("Slate");

            NaturalRockReconstructionProfile profile = CreateProfile("MissingA", "MissingB");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(),
                [
                    granite,
                    marble,
                    slate
                ],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironmentRecovery);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(usedEnvironmentRecovery, Is.True);

            Assert.That(repaired.GetRockDefName(0), Is.EqualTo("Granite"));
            Assert.That(repaired.GetRockDefName(1), Is.EqualTo("Marble"));
        }

        [Test]
        public void TryRepair_SameInputTwice_ProducesEquivalentResult()
        {
            ThingDef granite = CreateRock("Granite");
            ThingDef marble = CreateRock("Marble");

            NaturalRockReconstructionProfile profile = CreateProfile("MissingA", "Granite", "MissingB", "Marble");

            Func<string, ThingDef> resolver = CreateResolver(granite, marble);

            bool firstResult = NaturalRockProfileRepairer.TryRepair(
                profile,
                resolver,
                [],
                out NaturalRockReconstructionProfile first,
                out bool firstChanged,
                out bool firstUsedEnvironmentRecovery);

            bool secondResult = NaturalRockProfileRepairer.TryRepair(
                profile,
                resolver,
                [],
                out NaturalRockReconstructionProfile second,
                out bool secondChanged,
                out bool secondUsedEnvironmentRecovery);

            Assert.That(firstResult, Is.True);
            Assert.That(secondResult, Is.True);
            Assert.That(firstChanged, Is.EqualTo(secondChanged));
            Assert.That(firstUsedEnvironmentRecovery, Is.EqualTo(secondUsedEnvironmentRecovery));

            Assert.That(first.IsEquivalentTo(second), Is.True);
        }

        [Test]
        public void TryRepair_SavedRockLostItsTerrain_UsesRemainingUsableRock()
        {
            ThingDef retired = CreateRock("RetiredRock");
            retired.building.naturalTerrain = null;
            ThingDef ice = CreateRock("SolidIce");
            ice.building.naturalTerrain.defName = "Ice";
            NaturalRockReconstructionProfile profile = CreateProfile("RetiredRock", "SolidIce", "Missing");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(retired, ice),
                [],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironment);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(usedEnvironment, Is.False);
            Assert.That(repaired.IsEquivalentTo(CreateProfile("SolidIce", "SolidIce", "SolidIce")), Is.True);
            Assert.That(profile.GetRockDefName(0), Is.EqualTo("RetiredRock"));
        }

        [Test]
        public void TryRepair_AllSavedRocksUnusable_OnlyUsesReconstructibleEnvironmentRocks()
        {
            ThingDef building = CreateRock("OldRock");
            building.building = null;
            ThingDef noTerrain = CreateRock("NoTerrain");
            noTerrain.building.naturalTerrain = null;
            ThingDef granite = CreateRock("Granite");

            bool result = NaturalRockProfileRepairer.TryRepair(
                CreateProfile("OldRock", "Missing"),
                CreateResolver(building),
                [noTerrain, granite],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironment);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(usedEnvironment, Is.True);
            Assert.That(repaired.IsEquivalentTo(CreateProfile("Granite", "Granite")), Is.True);
        }

        [Test]
        public void TryCreate_UnusableSlot_PreservesIndicesOfUsableRocks()
        {
            ThingDef missingSurface = CreateRock("MissingSurface");
            missingSurface.building.naturalTerrain = null;
            ThingDef forcedRock = CreateRock("BiomeForcedRock");
            forcedRock.building.isNaturalRock = false;

            bool result = NaturalRockReconstructionProfile.TryCreate(
                [CreateRock("Granite"), missingSurface, forcedRock],
                null,
                out NaturalRockReconstructionProfile profile,
                out bool changed,
                out bool usedEnvironment);

            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(usedEnvironment, Is.False);
            Assert.That(
                profile.IsEquivalentTo(CreateProfile("Granite", "BiomeForcedRock", "BiomeForcedRock")),
                Is.True);
        }

        [Test]
        public void TryCreate_NoReconstructibleTerrain_FailsWithoutProducingAProfile()
        {
            ThingDef noBuilding = CreateRock("OldRock");
            noBuilding.building = null;
            ThingDef noTerrain = CreateRock("NoTerrain");
            noTerrain.building.naturalTerrain = null;
            ThingDef temporary = CreateRock("Temporary");
            temporary.building.naturalTerrain.temporary = true;
            ThingDef foundation = CreateRock("Foundation");
            foundation.building.naturalTerrain.isFoundation = true;

            bool result = NaturalRockReconstructionProfile.TryCreate(
                [noBuilding, noTerrain, temporary, foundation],
                null,
                out NaturalRockReconstructionProfile profile,
                out _,
                out _);

            Assert.That(result, Is.False);
            Assert.That(profile, Is.Null);
        }

        [Test]
        public void TryRepair_NoSavedOrEnvironmentCandidates_ReturnsFalse()
        {
            NaturalRockReconstructionProfile profile = CreateProfile("MissingA", "MissingB");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(),
                [],
                out NaturalRockReconstructionProfile repaired,
                out bool changed,
                out bool usedEnvironmentRecovery);

            Assert.That(result, Is.False);
            Assert.That(repaired, Is.Null);
            Assert.That(changed, Is.False);
            Assert.That(usedEnvironmentRecovery, Is.False);
        }

        [Test]
        public void TryRepair_Failure_DoesNotMutateOriginalProfile()
        {
            NaturalRockReconstructionProfile profile = CreateProfile("MissingA", "MissingB");

            NaturalRockReconstructionProfile original = profile.Copy();

            bool result = NaturalRockProfileRepairer.TryRepair(profile, CreateResolver(), [], out _, out _, out _);

            Assert.That(result, Is.False);
            Assert.That(profile.IsEquivalentTo(original), Is.True);
            Assert.That(profile.GetRockDefName(0), Is.EqualTo("MissingA"));
            Assert.That(profile.GetRockDefName(1), Is.EqualTo("MissingB"));
        }

        private static NaturalRockReconstructionProfile CreateProfile(params string[] rockDefNames)
        {
            return new NaturalRockReconstructionProfile(NaturalRockReconstructionProfile.CurrentVersion, rockDefNames);
        }

        private static ThingDef CreateRock(string defName)
        {
            var rock = (ThingDef)RuntimeHelpers.GetUninitializedObject(typeof(ThingDef));

            rock.defName = defName;
            rock.building = new BuildingProperties
            {
                isNaturalRock = true,
                naturalTerrain = (TerrainDef)RuntimeHelpers.GetUninitializedObject(typeof(TerrainDef))
            };
            return rock;
        }

        private static Func<string, ThingDef> CreateResolver(params ThingDef[] rocks)
        {
            var rocksByDefName = new Dictionary<string, ThingDef>(StringComparer.Ordinal);

            for (var i = 0; i < rocks.Length; i++)
            {
                ThingDef rock = rocks[i];

                if (rock != null)
                {
                    rocksByDefName[rock.defName] = rock;
                }
            }

            return defName =>
            {
                rocksByDefName.TryGetValue(defName, out ThingDef rock);

                return rock;
            };
        }
    }
}
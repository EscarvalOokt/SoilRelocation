using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
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
                Array.Empty<ThingDef>(),
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
                new[]
                {
                    environmentRock
                },
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
                Array.Empty<ThingDef>(),
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
                Array.Empty<ThingDef>(),
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
                Array.Empty<ThingDef>(),
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
                new[]
                {
                    granite,
                    marble,
                    slate
                },
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
                Array.Empty<ThingDef>(),
                out NaturalRockReconstructionProfile first,
                out bool firstChanged,
                out bool firstUsedEnvironmentRecovery);

            bool secondResult = NaturalRockProfileRepairer.TryRepair(
                profile,
                resolver,
                Array.Empty<ThingDef>(),
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
        public void TryRepair_NoSavedOrEnvironmentCandidates_ReturnsFalse()
        {
            NaturalRockReconstructionProfile profile = CreateProfile("MissingA", "MissingB");

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(),
                Array.Empty<ThingDef>(),
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

            bool result = NaturalRockProfileRepairer.TryRepair(
                profile,
                CreateResolver(),
                Array.Empty<ThingDef>(),
                out _,
                out _,
                out _);

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
            var rock = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));

            rock.defName = defName;
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
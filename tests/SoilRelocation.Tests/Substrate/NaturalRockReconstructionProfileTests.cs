using NUnit.Framework;
using SoilRelocation.Substrate;

namespace SoilRelocation.Tests.Substrate
{
    [TestFixture]
    public sealed class NaturalRockReconstructionProfileTests
    {
        [Test]
        public void CurrentVersion_IsOne()
        {
            Assert.That(NaturalRockReconstructionProfile.CurrentVersion, Is.EqualTo(1));
        }

        [Test]
        public void Profile_PreservesOrderedRockIdentities()
        {
            NaturalRockReconstructionProfile profile = CreateProfile("Granite", "Marble", "Slate");

            Assert.That(profile.SlotCount, Is.EqualTo(3));
            Assert.That(profile.GetRockDefName(0), Is.EqualTo("Granite"));
            Assert.That(profile.GetRockDefName(1), Is.EqualTo("Marble"));
            Assert.That(profile.GetRockDefName(2), Is.EqualTo("Slate"));
        }

        [Test]
        public void IsStructurallyValid_ValidProfile_ReturnsTrue()
        {
            NaturalRockReconstructionProfile profile = CreateProfile("Granite", "Marble");

            Assert.That(profile.IsStructurallyValid(), Is.True);
        }

        [Test]
        public void IsStructurallyValid_EmptyProfile_ReturnsFalse()
        {
            var profile = new NaturalRockReconstructionProfile(NaturalRockReconstructionProfile.CurrentVersion, []);

            Assert.That(profile.IsStructurallyValid(), Is.False);
        }

        [Test]
        public void IsStructurallyValid_BlankRockIdentity_ReturnsFalse()
        {
            var profile = new NaturalRockReconstructionProfile(
                NaturalRockReconstructionProfile.CurrentVersion,
                [
                    "Granite",
                    " "
                ]);

            Assert.That(profile.IsStructurallyValid(), Is.False);
        }

        [Test]
        public void IsStructurallyValid_UnsupportedVersion_ReturnsFalse()
        {
            var profile = new NaturalRockReconstructionProfile(
                NaturalRockReconstructionProfile.CurrentVersion + 1,
                [
                    "Granite"
                ]);

            Assert.That(profile.IsStructurallyValid(), Is.False);
        }

        [Test]
        public void IsEquivalentTo_IdenticalOrderedProfiles_ReturnsTrue()
        {
            NaturalRockReconstructionProfile first = CreateProfile("Granite", "Marble", "Slate");

            NaturalRockReconstructionProfile second = CreateProfile("Granite", "Marble", "Slate");

            Assert.That(first.IsEquivalentTo(second), Is.True);
        }

        [Test]
        public void IsEquivalentTo_ReorderedProfiles_ReturnsFalse()
        {
            NaturalRockReconstructionProfile first = CreateProfile("Granite", "Marble");

            NaturalRockReconstructionProfile second = CreateProfile("Marble", "Granite");

            Assert.That(first.IsEquivalentTo(second), Is.False);
        }

        [Test]
        public void Copy_PreservesVersionAndOrderedSlots()
        {
            NaturalRockReconstructionProfile profile = CreateProfile("Granite", "Marble", "Slate");

            NaturalRockReconstructionProfile copy = profile.Copy();

            Assert.That(copy, Is.Not.SameAs(profile));
            Assert.That(copy.Version, Is.EqualTo(profile.Version));
            Assert.That(copy.IsEquivalentTo(profile), Is.True);
        }

        private static NaturalRockReconstructionProfile CreateProfile(params string[] rockDefNames)
        {
            return new NaturalRockReconstructionProfile(NaturalRockReconstructionProfile.CurrentVersion, rockDefNames);
        }
    }
}
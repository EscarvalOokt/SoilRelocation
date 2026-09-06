using NUnit.Framework;
using SoilRelocation.Digging;
using SoilRelocation.Domain;

namespace SoilRelocation.Tests.Digging
{
    [TestFixture]
    public sealed class DiggingValidationResultTests
    {
        [Test]
        public void Valid_IsNeitherFailureNorBlocker()
        {
            var family = new RelocationFamilyDef();
            var result = new DiggingValidationResult(DiggingValidationStatus.Valid, family);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.IsPermanentFailure, Is.False);
            Assert.That(result.IsTransientBlocker, Is.False);
            Assert.That(result.Family, Is.SameAs(family));
        }

        [Test]
        public void UnsupportedTerrain_IsPermanentFailure()
        {
            var result = new DiggingValidationResult(DiggingValidationStatus.UnsupportedTerrain);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.IsPermanentFailure, Is.True);
            Assert.That(result.IsTransientBlocker, Is.False);
        }

        [Test]
        public void StructuralBlocker_IsPermanentFailure()
        {
            var result = new DiggingValidationResult(DiggingValidationStatus.StructuralBlocker);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.IsPermanentFailure, Is.True);
            Assert.That(result.IsTransientBlocker, Is.False);
        }

        [Test]
        public void PlantPrerequisite_IsTransientBlocker()
        {
            var result = new DiggingValidationResult(DiggingValidationStatus.PlantPrerequisite);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.IsPermanentFailure, Is.False);
            Assert.That(result.IsTransientBlocker, Is.True);
        }

        [Test]
        public void PlantCannotBeRemoved_IsTransientBlocker()
        {
            var result = new DiggingValidationResult(DiggingValidationStatus.PlantCannotBeRemoved);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.IsPermanentFailure, Is.False);
            Assert.That(result.IsTransientBlocker, Is.True);
        }

        [Test]
        public void Fire_IsTransientBlocker()
        {
            var result = new DiggingValidationResult(DiggingValidationStatus.Fire);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.IsPermanentFailure, Is.False);
            Assert.That(result.IsTransientBlocker, Is.True);
        }
    }
}
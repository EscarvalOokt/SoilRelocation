using System.Runtime.Serialization;
using NUnit.Framework;
using SoilRelocation.Domain;
using SoilRelocation.Placement;
using Verse;

namespace SoilRelocation.Tests.Placement
{
    [TestFixture]
    public sealed class PlacementValidationResultTests
    {
        [Test]
        public void Valid_CarriesFamilyAndResolvedTerrain()
        {
            var family = new RelocationFamilyDef();
            TerrainDef currentTerrain = CreateTerrain();
            TerrainDef resolvedTerrain = CreateTerrain();
            var result = new PlacementValidationResult(
                PlacementValidationStatus.Valid,
                family,
                currentTerrain,
                resolvedTerrain);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.IsPermanentFailure, Is.False);
            Assert.That(result.IsTransientBlocker, Is.False);
            Assert.That(result.Family, Is.SameAs(family));
            Assert.That(result.CurrentTerrain, Is.SameAs(currentTerrain));
            Assert.That(result.ResolvedTerrain, Is.SameAs(resolvedTerrain));
        }

        [TestCase((int)PlacementValidationStatus.InvalidFamily)]
        [TestCase((int)PlacementValidationStatus.InvalidDestination)]
        [TestCase((int)PlacementValidationStatus.NoOp)]
        [TestCase((int)PlacementValidationStatus.StructuralBlocker)]
        public void PermanentStatuses_ArePermanentFailures(int statusValue)
        {
            var status = (PlacementValidationStatus)statusValue;
            var result = new PlacementValidationResult(status);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.IsPermanentFailure, Is.True);
            Assert.That(result.IsTransientBlocker, Is.False);
        }

        [TestCase((int)PlacementValidationStatus.PlantPrerequisite)]
        [TestCase((int)PlacementValidationStatus.PlantCannotBeRemoved)]
        [TestCase((int)PlacementValidationStatus.Fire)]
        public void TransientStatuses_AreNotPermanentFailures(int statusValue)
        {
            var status = (PlacementValidationStatus)statusValue;
            var result = new PlacementValidationResult(status);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.IsPermanentFailure, Is.False);
            Assert.That(result.IsTransientBlocker, Is.True);
        }

        private static TerrainDef CreateTerrain()
        {
            return (TerrainDef)FormatterServices.GetUninitializedObject(typeof(TerrainDef));
        }
    }
}
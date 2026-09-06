using System.Runtime.Serialization;
using NUnit.Framework;
using SoilRelocation.Relocation;
using Verse;

namespace SoilRelocation.Tests.Digging
{
    [TestFixture]
    public sealed class RelocationCellContentPolicyTests
    {
        [TestCase("PowerConduit")]
        [TestCase("HiddenConduit")]
        [TestCase("WaterproofConduit")]
        public void IsConduitDef_BuiltInConduit_ReturnsTrue(string defName)
        {
            ThingDef def = CreateThingDef(defName);

            Assert.That(RelocationCellContentPolicy.IsConduitDef(def), Is.True);
        }

        [Test]
        public void IsConduitDef_ModdedConduitLikeName_ReturnsFalse()
        {
            ThingDef def = CreateThingDef("CustomPowerConduit");

            Assert.That(RelocationCellContentPolicy.IsConduitDef(def), Is.False);
        }

        [Test]
        public void IsConduitDef_NormalBuilding_ReturnsFalse()
        {
            ThingDef def = CreateThingDef("Battery");

            Assert.That(RelocationCellContentPolicy.IsConduitDef(def), Is.False);
        }

        [Test]
        public void IsConduitDef_Null_ReturnsFalse()
        {
            Assert.That(RelocationCellContentPolicy.IsConduitDef(null), Is.False);
        }

        private static ThingDef CreateThingDef(string defName)
        {
            var def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));

            def.defName = defName;
            return def;
        }
    }
}
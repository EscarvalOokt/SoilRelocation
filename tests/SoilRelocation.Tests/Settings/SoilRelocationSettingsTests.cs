using NUnit.Framework;
using SoilRelocation.Domain;
using SoilRelocation.Settings;

namespace SoilRelocation.Tests.Settings
{
    [TestFixture]
    public sealed class SoilRelocationSettingsTests
    {
        [Test]
        public void NewSettings_PlacementResolutionMode_DefaultsToCanonical()
        {
            var settings = new SoilRelocationSettings();

            Assert.That(settings.PlacementResolutionMode, Is.EqualTo(PlacementResolutionMode.Canonical));
        }

        [Test]
        public void NewSettings_AllowRelocationUnderConduits_DefaultsToFalse()
        {
            var settings = new SoilRelocationSettings();

            Assert.That(settings.AllowRelocationUnderConduits, Is.False);
        }

        [Test]
        public void NewSettings_DigWorkAmount_DefaultsTo1000()
        {
            var settings = new SoilRelocationSettings();

            Assert.That(settings.DigWorkAmount, Is.EqualTo(1000));
        }

        [Test]
        public void NewSettings_PlaceWorkAmount_DefaultsTo1000()
        {
            var settings = new SoilRelocationSettings();

            Assert.That(settings.PlaceWorkAmount, Is.EqualTo(1000));
        }

        [Test]
        public void NewSettings_RawXpPerTick_DefaultsTo009()
        {
            var settings = new SoilRelocationSettings();

            Assert.That(settings.RawXpPerTick, Is.EqualTo(0.09f).Within(0.00001f));
        }

        [Test]
        public void NewSettings_DiggingSkills_DefaultsToNone()
        {
            var settings = new SoilRelocationSettings();

            Assert.That(settings.DiggingSkills, Is.EqualTo(RelocationSkillSelection.None));
        }

        [Test]
        public void NewSettings_PlacementSkills_DefaultsToNone()
        {
            var settings = new SoilRelocationSettings();

            Assert.That(settings.PlacementSkills, Is.EqualTo(RelocationSkillSelection.None));
        }

        [Test]
        public void ResetToDefaults_RestoresAllSettings()
        {
            var settings = new SoilRelocationSettings
            {
                PlacementResolutionMode = PlacementResolutionMode.BiomeNative,
                AllowRelocationUnderConduits = true,
                DigWorkAmount = 2500,
                PlaceWorkAmount = 3500,
                RawXpPerTick = 0.30f,
                DiggingSkills = RelocationSkillSelection.All,
                PlacementSkills = RelocationSkillSelection.Mining | RelocationSkillSelection.Plants
            };

            settings.ResetToDefaults();

            Assert.That(
                settings.PlacementResolutionMode,
                Is.EqualTo(SoilRelocationSettings.DefaultPlacementResolutionMode));
            Assert.That(
                settings.AllowRelocationUnderConduits,
                Is.EqualTo(SoilRelocationSettings.DefaultAllowRelocationUnderConduits));
            Assert.That(settings.DigWorkAmount, Is.EqualTo(SoilRelocationSettings.DefaultDigWorkAmount));
            Assert.That(settings.PlaceWorkAmount, Is.EqualTo(SoilRelocationSettings.DefaultPlaceWorkAmount));
            Assert.That(settings.RawXpPerTick, Is.EqualTo(SoilRelocationSettings.DefaultRawXpPerTick).Within(0.00001f));
            Assert.That(settings.DiggingSkills, Is.EqualTo(SoilRelocationSettings.DefaultDiggingSkills));
            Assert.That(settings.PlacementSkills, Is.EqualTo(SoilRelocationSettings.DefaultPlacementSkills));
        }

        [TestCase(49, 50)]
        [TestCase(50, 50)]
        [TestCase(54, 50)]
        [TestCase(55, 60)]
        [TestCase(253, 250)]
        [TestCase(256, 260)]
        [TestCase(999, 1000)]
        [TestCase(1000, 1000)]
        [TestCase(5000, 5000)]
        [TestCase(5001, 5000)]
        public void NormalizeWorkAmount_ClampsAndRoundsToAcceptedStep(int value, int expected)
        {
            Assert.That(SoilRelocationSettings.NormalizeWorkAmount(value), Is.EqualTo(expected));
        }

        [TestCase(0.01f, 0.03f)]
        [TestCase(0.03f, 0.03f)]
        [TestCase(0.044f, 0.03f)]
        [TestCase(0.045f, 0.06f)]
        [TestCase(0.06f, 0.06f)]
        [TestCase(0.08f, 0.09f)]
        [TestCase(0.09f, 0.09f)]
        [TestCase(0.10f, 0.09f)]
        [TestCase(0.14f, 0.15f)]
        [TestCase(0.30f, 0.30f)]
        [TestCase(0.31f, 0.30f)]
        public void NormalizeRawXpPerTick_ClampsAndRoundsToAcceptedStep(float value, float expected)
        {
            Assert.That(SoilRelocationSettings.NormalizeRawXpPerTick(value), Is.EqualTo(expected).Within(0.00001f));
        }
    }
}
using System.Linq;
using NUnit.Framework;
using SoilRelocation.Settings;
using SoilRelocation.Work;

namespace SoilRelocation.Tests.Work
{
    [TestFixture]
    public sealed class RelocationWorkUtilityTests
    {
        [TestCase((int)RelocationSkillSelection.None, 0)]
        [TestCase((int)RelocationSkillSelection.Mining, 1)]
        [TestCase((int)RelocationSkillSelection.Construction, 1)]
        [TestCase((int)RelocationSkillSelection.Plants, 1)]
        [TestCase((int)(RelocationSkillSelection.Mining | RelocationSkillSelection.Construction), 2)]
        [TestCase((int)(RelocationSkillSelection.Mining | RelocationSkillSelection.Plants), 2)]
        [TestCase((int)(RelocationSkillSelection.Construction | RelocationSkillSelection.Plants), 2)]
        [TestCase((int)RelocationSkillSelection.All, 3)]
        public void GetSelectedSkillCount_ReturnsNumberOfSelectedSkills(int selectionValue, int expected)
        {
            var selection = (RelocationSkillSelection)selectionValue;

            Assert.That(RelocationWorkUtility.GetSelectedSkillCount(selection), Is.EqualTo(expected));
        }

        [TestCase((int)RelocationSkillSelection.None, 0.09f, 0f)]
        [TestCase((int)RelocationSkillSelection.Mining, 0.03f, 0.03f)]
        [TestCase((int)RelocationSkillSelection.Mining, 0.09f, 0.09f)]
        [TestCase((int)RelocationSkillSelection.Mining, 0.30f, 0.30f)]
        [TestCase((int)(RelocationSkillSelection.Mining | RelocationSkillSelection.Construction), 0.03f, 0.015f)]
        [TestCase((int)(RelocationSkillSelection.Mining | RelocationSkillSelection.Construction), 0.09f, 0.045f)]
        [TestCase((int)(RelocationSkillSelection.Mining | RelocationSkillSelection.Construction), 0.30f, 0.15f)]
        [TestCase((int)RelocationSkillSelection.All, 0.03f, 0.01f)]
        [TestCase((int)RelocationSkillSelection.All, 0.09f, 0.03f)]
        [TestCase((int)RelocationSkillSelection.All, 0.30f, 0.10f)]
        public void GetRawXpPerSelectedSkill_SplitsSuppliedBudgetEvenly(
            int selectionValue,
            float rawXpPerTick,
            float expected)
        {
            var selection = (RelocationSkillSelection)selectionValue;

            Assert.That(
                RelocationWorkUtility.GetRawXpPerSelectedSkill(selection, rawXpPerTick),
                Is.EqualTo(expected).Within(0.00001f));
        }

        [TestCase(0.03f)]
        [TestCase(0.09f)]
        [TestCase(0.30f)]
        public void NonEmptySelections_KeepTotalRawXpAtSuppliedBudget(float rawXpPerTick)
        {
            for (var value = 1; value <= (int)RelocationSkillSelection.All; value++)
            {
                var selection = (RelocationSkillSelection)value;
                int count = RelocationWorkUtility.GetSelectedSkillCount(selection);
                float totalXp = RelocationWorkUtility.GetRawXpPerSelectedSkill(selection, rawXpPerTick) * count;

                Assert.That(totalXp, Is.EqualTo(rawXpPerTick).Within(0.00001f));
            }
        }

        [Test]
        public void AllValidSelections_EnumerateEachSelectedSkillExactlyOnce()
        {
            for (var value = 0; value <= (int)RelocationSkillSelection.All; value++)
            {
                var selection = (RelocationSkillSelection)value;
                RelocationSkillSelection[] skills = RelocationWorkUtility.GetSelectedSkills(selection).ToArray();

                Assert.That(skills.Distinct().Count(), Is.EqualTo(skills.Length));
                Assert.That(skills.Length, Is.EqualTo(RelocationWorkUtility.GetSelectedSkillCount(selection)));
            }
        }

        [Test]
        public void Normalize_RemovesUnknownBits()
        {
            var selection = (RelocationSkillSelection)15;

            Assert.That(RelocationWorkUtility.Normalize(selection), Is.EqualTo(RelocationSkillSelection.All));
        }
    }
}
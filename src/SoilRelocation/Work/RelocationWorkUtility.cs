using System.Collections.Generic;
using RimWorld;
using SoilRelocation.Settings;
using Verse;

namespace SoilRelocation.Work
{
    internal static class RelocationWorkUtility
    {
        internal static bool CanPerform(Pawn pawn, RelocationSkillSelection selection)
        {
            foreach (RelocationSkillSelection skill in GetSelectedSkills(selection))
            {
                if (pawn.skills?.GetSkill(GetSkillDef(skill)) is not { TotallyDisabled: false } ||
                    GetSpeedStat(skill).Worker.IsDisabledFor(pawn))
                {
                    return false;
                }
            }

            return true;
        }

        internal static float GetWorkSpeed(Pawn pawn, RelocationSkillSelection selection)
        {
            var totalSpeed = 0f;
            var skillCount = 0;

            foreach (RelocationSkillSelection skill in GetSelectedSkills(selection))
            {
                totalSpeed += pawn.GetStatValue(GetSpeedStat(skill));
                skillCount++;
            }

            return skillCount == 0 ? pawn.GetStatValue(StatDefOf.GeneralLaborSpeed) : totalSpeed / skillCount;
        }

        internal static void Learn(Pawn pawn, RelocationSkillSelection selection, float rawXpPerTick, int delta)
        {
            if (pawn.skills == null || rawXpPerTick <= 0f || delta <= 0)
            {
                return;
            }

            float xpPerSkill = GetRawXpPerSelectedSkill(selection, rawXpPerTick) * delta;

            if (xpPerSkill <= 0f)
            {
                return;
            }

            foreach (RelocationSkillSelection skill in GetSelectedSkills(selection))
            {
                pawn.skills.Learn(GetSkillDef(skill), xpPerSkill);
            }
        }

        internal static int GetSelectedSkillCount(RelocationSkillSelection selection)
        {
            var count = 0;
            RelocationSkillSelection normalized = Normalize(selection);

            if ((normalized & RelocationSkillSelection.Mining) != 0)
            {
                count++;
            }

            if ((normalized & RelocationSkillSelection.Construction) != 0)
            {
                count++;
            }

            if ((normalized & RelocationSkillSelection.Plants) != 0)
            {
                count++;
            }

            return count;
        }

        internal static float GetRawXpPerSelectedSkill(RelocationSkillSelection selection, float rawXpPerTick)
        {
            int skillCount = GetSelectedSkillCount(selection);
            return skillCount == 0 || rawXpPerTick <= 0f ? 0f : rawXpPerTick / skillCount;
        }

        internal static IEnumerable<RelocationSkillSelection> GetSelectedSkills(RelocationSkillSelection selection)
        {
            RelocationSkillSelection normalized = Normalize(selection);

            if ((normalized & RelocationSkillSelection.Mining) != 0)
            {
                yield return RelocationSkillSelection.Mining;
            }

            if ((normalized & RelocationSkillSelection.Construction) != 0)
            {
                yield return RelocationSkillSelection.Construction;
            }

            if ((normalized & RelocationSkillSelection.Plants) != 0)
            {
                yield return RelocationSkillSelection.Plants;
            }
        }

        internal static RelocationSkillSelection Normalize(RelocationSkillSelection selection)
        {
            return selection & RelocationSkillSelection.All;
        }

        private static StatDef GetSpeedStat(RelocationSkillSelection skill)
        {
            switch (skill)
            {
                case RelocationSkillSelection.Mining:
                    return StatDefOf.MiningSpeed;
                case RelocationSkillSelection.Construction:
                    return StatDefOf.ConstructionSpeed;
                case RelocationSkillSelection.Plants:
                    return StatDefOf.PlantWorkSpeed;
                default:
                    return StatDefOf.GeneralLaborSpeed;
            }
        }

        private static SkillDef GetSkillDef(RelocationSkillSelection skill)
        {
            switch (skill)
            {
                case RelocationSkillSelection.Mining:
                    return SkillDefOf.Mining;
                case RelocationSkillSelection.Construction:
                    return SkillDefOf.Construction;
                case RelocationSkillSelection.Plants:
                    return SkillDefOf.Plants;
                default:
                    return null;
            }
        }
    }
}
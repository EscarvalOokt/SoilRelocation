using System;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Settings
{
    internal sealed class SoilRelocationSettings : ModSettings
    {
        internal const PlacementResolutionMode DefaultPlacementResolutionMode = PlacementResolutionMode.Canonical;
        internal const bool DefaultAllowRelocationUnderConduits = false;
        internal const int MinimumWorkAmount = 50;
        internal const int MaximumWorkAmount = 5000;
        internal const int WorkAmountStep = 10;
        internal const int DefaultDigWorkAmount = 1000;
        internal const int DefaultPlaceWorkAmount = 1000;
        internal const float MinimumRawXpPerTick = 0.03f;
        internal const float MaximumRawXpPerTick = 0.30f;
        internal const float RawXpPerTickStep = 0.03f;
        internal const float DefaultRawXpPerTick = 0.09f;
        internal const RelocationSkillSelection DefaultDiggingSkills = RelocationSkillSelection.None;
        internal const RelocationSkillSelection DefaultPlacementSkills = RelocationSkillSelection.None;

        internal PlacementResolutionMode PlacementResolutionMode = DefaultPlacementResolutionMode;
        internal bool AllowRelocationUnderConduits = DefaultAllowRelocationUnderConduits;
        internal int DigWorkAmount = DefaultDigWorkAmount;
        internal int PlaceWorkAmount = DefaultPlaceWorkAmount;
        internal float RawXpPerTick = DefaultRawXpPerTick;
        internal RelocationSkillSelection DiggingSkills = DefaultDiggingSkills;
        internal RelocationSkillSelection PlacementSkills = DefaultPlacementSkills;

        public override void ExposeData()
        {
            base.ExposeData();

            NormalizeValues();

            Scribe_Values.Look(ref PlacementResolutionMode, "placementResolutionMode");

            Scribe_Values.Look(ref AllowRelocationUnderConduits, "allowRelocationUnderConduits");

            Scribe_Values.Look(ref DigWorkAmount, "digWorkAmount", DefaultDigWorkAmount);
            Scribe_Values.Look(ref PlaceWorkAmount, "placeWorkAmount", DefaultPlaceWorkAmount);
            Scribe_Values.Look(ref RawXpPerTick, "rawXpPerTick", DefaultRawXpPerTick);
            Scribe_Values.Look(ref DiggingSkills, "diggingSkills");
            Scribe_Values.Look(ref PlacementSkills, "placementSkills");

            NormalizeValues();
        }

        internal void ResetToDefaults()
        {
            PlacementResolutionMode = DefaultPlacementResolutionMode;
            AllowRelocationUnderConduits = DefaultAllowRelocationUnderConduits;
            DigWorkAmount = DefaultDigWorkAmount;
            PlaceWorkAmount = DefaultPlaceWorkAmount;
            RawXpPerTick = DefaultRawXpPerTick;
            DiggingSkills = DefaultDiggingSkills;
            PlacementSkills = DefaultPlacementSkills;
        }

        private void NormalizeValues()
        {
            DigWorkAmount = NormalizeWorkAmount(DigWorkAmount);
            PlaceWorkAmount = NormalizeWorkAmount(PlaceWorkAmount);
            RawXpPerTick = NormalizeRawXpPerTick(RawXpPerTick);
            DiggingSkills &= RelocationSkillSelection.All;
            PlacementSkills &= RelocationSkillSelection.All;
        }

        internal static int NormalizeWorkAmount(int value)
        {
            int clamped = Math.Max(MinimumWorkAmount, Math.Min(MaximumWorkAmount, value));
            int offset = clamped - MinimumWorkAmount;
            int stepIndex = (offset + WorkAmountStep / 2) / WorkAmountStep;
            int normalized = MinimumWorkAmount + stepIndex * WorkAmountStep;

            return Math.Max(MinimumWorkAmount, Math.Min(MaximumWorkAmount, normalized));
        }

        internal static float NormalizeRawXpPerTick(float value)
        {
            float clamped = Math.Max(MinimumRawXpPerTick, Math.Min(MaximumRawXpPerTick, value));
            double offset = clamped - MinimumRawXpPerTick;
            var stepIndex = (int)Math.Floor(offset / RawXpPerTickStep + 0.500001d);
            float normalized = MinimumRawXpPerTick + stepIndex * RawXpPerTickStep;

            return Math.Max(MinimumRawXpPerTick, Math.Min(MaximumRawXpPerTick, normalized));
        }
    }
}
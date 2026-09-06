using System;
using System.Globalization;
using HarmonyLib;
using RimWorld;
using SoilRelocation.Domain;
using SoilRelocation.Settings;
using UnityEngine;
using Verse;

namespace SoilRelocation
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class SoilRelocationMod : Mod
    {
        internal const string LogPrefix = "[SoilRelocation]";

        private const string HarmonyId = "escarval.soilrelocation";
        private const float NumericFieldWidth = 90f;
        private const float NumericControlGap = 12f;
        private const float ResetButtonWidth = 180f;
        private const float ResetButtonHeight = 30f;
        private const float SectionGap = 4f;

        private readonly SoilRelocationSettings _settings;
        private string _digWorkAmountBuffer;
        private string _placeWorkAmountBuffer;
        private string _rawXpPerTickBuffer;

        private bool _terrainExpanded = true;
        private bool _diggingExpanded = true;
        private bool _placementExpanded = true;
        private bool _experienceExpanded = true;

        public SoilRelocationMod(ModContentPack content) : base(content)
        {
            new Harmony(HarmonyId).PatchAll();

            _settings = GetSettings<SoilRelocationSettings>();
            SynchronizeNumericBuffers();

            Log.Message($"{LogPrefix} Loaded for RimWorld {VersionControl.CurrentVersionStringWithRev}.");
        }

        internal SoilRelocationSettings Settings => _settings;

        public override string SettingsCategory()
        {
            return "SoilRelocation.Settings.Category".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            DrawCollapsibleSectionHeader(listing, "SoilRelocation.Settings.Section.Terrain", ref _terrainExpanded);

            if (_terrainExpanded)
            {
                DrawTerrainSettings(listing);
            }

            listing.Gap(SectionGap);

            bool diggingToggled = DrawCollapsibleSectionHeader(
                listing,
                "SoilRelocation.Settings.Section.Digging",
                ref _diggingExpanded);

            if (diggingToggled && !_diggingExpanded)
            {
                CommitWorkAmountBuffer(ref _settings.DigWorkAmount, ref _digWorkAmountBuffer);
            }

            if (_diggingExpanded)
            {
                DrawDiggingSettings(listing);
            }

            listing.Gap(SectionGap);

            bool placementToggled = DrawCollapsibleSectionHeader(
                listing,
                "SoilRelocation.Settings.Section.Placement",
                ref _placementExpanded);

            if (placementToggled && !_placementExpanded)
            {
                CommitWorkAmountBuffer(ref _settings.PlaceWorkAmount, ref _placeWorkAmountBuffer);
            }

            if (_placementExpanded)
            {
                DrawPlacementSettings(listing);
            }

            listing.Gap(SectionGap);

            bool experienceToggled = DrawCollapsibleSectionHeader(
                listing,
                "SoilRelocation.Settings.Section.Experience",
                ref _experienceExpanded);

            if (experienceToggled && !_experienceExpanded)
            {
                CommitRawXpPerTickBuffer(ref _settings.RawXpPerTick, ref _rawXpPerTickBuffer);
            }

            if (_experienceExpanded)
            {
                DrawExperienceSettings(listing);
            }

            listing.Gap(6f);
            DrawResetButton(listing);

            listing.End();

            base.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            CommitWorkAmountBuffer(ref _settings.DigWorkAmount, ref _digWorkAmountBuffer);
            CommitWorkAmountBuffer(ref _settings.PlaceWorkAmount, ref _placeWorkAmountBuffer);
            CommitRawXpPerTickBuffer(ref _settings.RawXpPerTick, ref _rawXpPerTickBuffer);

            base.WriteSettings();
        }

        private void DrawTerrainSettings(Listing_Standard listing)
        {
            listing.Label(
                "SoilRelocation.Settings.PlacementResolution.Label".Translate(),
                -1f,
                "SoilRelocation.Settings.PlacementResolution.Description".Translate());

            if (listing.RadioButton(
                    "SoilRelocation.Settings.PlacementResolution.Canonical.Label".Translate(),
                    _settings.PlacementResolutionMode == PlacementResolutionMode.Canonical,
                    12f,
                    "SoilRelocation.Settings.PlacementResolution.Canonical.Description".Translate()))
            {
                _settings.PlacementResolutionMode = PlacementResolutionMode.Canonical;
            }

            if (listing.RadioButton(
                    "SoilRelocation.Settings.PlacementResolution.BiomeNative.Label".Translate(),
                    _settings.PlacementResolutionMode == PlacementResolutionMode.BiomeNative,
                    12f,
                    "SoilRelocation.Settings.PlacementResolution.BiomeNative.Description".Translate()))
            {
                _settings.PlacementResolutionMode = PlacementResolutionMode.BiomeNative;
            }

            listing.Gap();

            listing.CheckboxLabeled(
                "SoilRelocation.Settings.AllowRelocationUnderConduits.Label".Translate(),
                ref _settings.AllowRelocationUnderConduits,
                "SoilRelocation.Settings.AllowRelocationUnderConduits.Description".Translate());
        }

        private void DrawDiggingSettings(Listing_Standard listing)
        {
            DrawSteppedWorkAmount(
                listing,
                "SoilRelocation.Settings.DigWorkAmount.Label",
                "SoilRelocation.Settings.DigWorkAmount.Description",
                "SoilRelocation_DigWorkAmount",
                ref _settings.DigWorkAmount,
                ref _digWorkAmountBuffer);

            DrawSkillSelection(
                listing,
                "SoilRelocation.Settings.DiggingSkills.Label",
                "SoilRelocation.Settings.DiggingSkills.Description",
                ref _settings.DiggingSkills);
        }

        private void DrawPlacementSettings(Listing_Standard listing)
        {
            DrawSteppedWorkAmount(
                listing,
                "SoilRelocation.Settings.PlaceWorkAmount.Label",
                "SoilRelocation.Settings.PlaceWorkAmount.Description",
                "SoilRelocation_PlaceWorkAmount",
                ref _settings.PlaceWorkAmount,
                ref _placeWorkAmountBuffer);

            DrawSkillSelection(
                listing,
                "SoilRelocation.Settings.PlacementSkills.Label",
                "SoilRelocation.Settings.PlacementSkills.Description",
                ref _settings.PlacementSkills);
        }

        private void DrawExperienceSettings(Listing_Standard listing)
        {
            DrawSteppedRawXpPerTick(
                listing,
                "SoilRelocation.Settings.RawXpPerTick.Label",
                "SoilRelocation.Settings.RawXpPerTick.Description",
                "SoilRelocation_RawXpPerTick",
                ref _settings.RawXpPerTick,
                ref _rawXpPerTickBuffer);
        }

        private static bool DrawCollapsibleSectionHeader(Listing_Standard listing, string labelKey, ref bool expanded)
        {
            Rect rect = listing.GetRect(Text.LineHeight);

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            string indicator = expanded ? "▼" : "▶";
            Widgets.Label(rect, $"{indicator} {labelKey.Translate()}");

            if (!Widgets.ButtonInvisible(rect))
            {
                return false;
            }

            expanded = !expanded;
            return true;
        }

        private void DrawResetButton(Listing_Standard listing)
        {
            Rect rowRect = listing.GetRect(ResetButtonHeight);
            var buttonRect = new Rect(rowRect.xMax - ResetButtonWidth, rowRect.y, ResetButtonWidth, ResetButtonHeight);

            string description = "SoilRelocation.Settings.Reset.Description".Translate();

            if (Widgets.ButtonText(buttonRect, "SoilRelocation.Settings.Reset.Label".Translate()))
            {
                Find.WindowStack.Add(
                    new Dialog_MessageBox(
                        "SoilRelocation.Settings.Reset.Confirm".Translate(),
                        "SoilRelocation.Settings.Reset.ConfirmButton".Translate(),
                        ResetSettings,
                        "CancelButton".Translate(),
                        null,
                        null,
                        false,
                        ResetSettings,
                        delegate { }));
            }

            TooltipHandler.TipRegion(buttonRect, description);
        }

        private void ResetSettings()
        {
            _settings.ResetToDefaults();
            SynchronizeNumericBuffers();
        }

        private void SynchronizeNumericBuffers()
        {
            _digWorkAmountBuffer = FormatWorkAmount(_settings.DigWorkAmount);
            _placeWorkAmountBuffer = FormatWorkAmount(_settings.PlaceWorkAmount);
            _rawXpPerTickBuffer = FormatRawXpPerTick(_settings.RawXpPerTick);
        }

        private static void DrawSteppedWorkAmount(
            Listing_Standard listing,
            string labelKey,
            string descriptionKey,
            string controlName,
            ref int value,
            ref string buffer)
        {
            listing.Label(labelKey.Translate(), -1f, descriptionKey.Translate());

            Rect controlRect = listing.GetRect(Text.LineHeight);
            Rect sliderRect = GetSliderRect(controlRect);
            Rect fieldRect = GetNumericFieldRect(controlRect);

            float sliderValue = Widgets.HorizontalSlider(
                sliderRect,
                value,
                SoilRelocationSettings.MinimumWorkAmount,
                SoilRelocationSettings.MaximumWorkAmount);

            int normalizedSliderValue = SoilRelocationSettings.NormalizeWorkAmount((int)Math.Floor(sliderValue + 0.5f));

            if (normalizedSliderValue != value)
            {
                value = normalizedSliderValue;
                buffer = FormatWorkAmount(value);
            }

            GUI.SetNextControlName(controlName);
            buffer = Widgets.TextField(fieldRect, buffer);

            bool fieldFocused = GUI.GetNameOfFocusedControl() == controlName;
            bool enterPressed = fieldFocused && Event.current.type == EventType.KeyDown &&
                                (Event.current.keyCode == KeyCode.Return ||
                                 Event.current.keyCode == KeyCode.KeypadEnter);

            if (!fieldFocused || enterPressed)
            {
                CommitWorkAmountBuffer(ref value, ref buffer);

                if (enterPressed)
                {
                    Event.current.Use();
                }
            }

            TooltipHandler.TipRegion(controlRect, descriptionKey.Translate());
        }

        private static void DrawSteppedRawXpPerTick(
            Listing_Standard listing,
            string labelKey,
            string descriptionKey,
            string controlName,
            ref float value,
            ref string buffer)
        {
            listing.Label(labelKey.Translate(), -1f, descriptionKey.Translate());

            Rect controlRect = listing.GetRect(Text.LineHeight);
            Rect sliderRect = GetSliderRect(controlRect);
            Rect fieldRect = GetNumericFieldRect(controlRect);

            float sliderValue = Widgets.HorizontalSlider(
                sliderRect,
                value,
                SoilRelocationSettings.MinimumRawXpPerTick,
                SoilRelocationSettings.MaximumRawXpPerTick);

            float normalizedSliderValue = SoilRelocationSettings.NormalizeRawXpPerTick(sliderValue);
            if (Math.Abs(normalizedSliderValue - value) > 0.000001f)
            {
                value = normalizedSliderValue;
                buffer = FormatRawXpPerTick(value);
            }

            GUI.SetNextControlName(controlName);
            buffer = Widgets.TextField(fieldRect, buffer);

            bool fieldFocused = GUI.GetNameOfFocusedControl() == controlName;
            bool enterPressed = fieldFocused && Event.current.type == EventType.KeyDown &&
                                (Event.current.keyCode == KeyCode.Return ||
                                 Event.current.keyCode == KeyCode.KeypadEnter);

            if (!fieldFocused || enterPressed)
            {
                CommitRawXpPerTickBuffer(ref value, ref buffer);

                if (enterPressed)
                {
                    Event.current.Use();
                }
            }

            TooltipHandler.TipRegion(controlRect, descriptionKey.Translate());
        }

        private static Rect GetSliderRect(Rect controlRect)
        {
            return new Rect(
                controlRect.x,
                controlRect.y,
                controlRect.width - NumericFieldWidth - NumericControlGap,
                controlRect.height);
        }

        private static Rect GetNumericFieldRect(Rect controlRect)
        {
            return new Rect(controlRect.xMax - NumericFieldWidth, controlRect.y, NumericFieldWidth, controlRect.height);
        }

        private static void CommitWorkAmountBuffer(ref int value, ref string buffer)
        {
            value = SoilRelocationSettings.NormalizeWorkAmount(
                int.TryParse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    ? parsed
                    : value);

            buffer = FormatWorkAmount(value);
        }

        private static void CommitRawXpPerTickBuffer(ref float value, ref string buffer)
        {
            value = SoilRelocationSettings.NormalizeRawXpPerTick(
                TryParseFloat(buffer, out float parsed) ? parsed : value);

            buffer = FormatRawXpPerTick(value);
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value);
        }

        private static string FormatWorkAmount(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatRawXpPerTick(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static void DrawSkillSelection(
            Listing_Standard listing,
            string labelKey,
            string descriptionKey,
            ref RelocationSkillSelection selection)
        {
            listing.Label(labelKey.Translate(), -1f, descriptionKey.Translate());

            DrawSkillCheckbox(listing, SkillDefOf.Mining, RelocationSkillSelection.Mining, ref selection);
            DrawSkillCheckbox(listing, SkillDefOf.Construction, RelocationSkillSelection.Construction, ref selection);
            DrawSkillCheckbox(listing, SkillDefOf.Plants, RelocationSkillSelection.Plants, ref selection);

            selection &= RelocationSkillSelection.All;
        }

        private static void DrawSkillCheckbox(
            Listing_Standard listing,
            SkillDef skillDef,
            RelocationSkillSelection flag,
            ref RelocationSkillSelection selection)
        {
            bool selected = (selection & flag) != 0;
            listing.CheckboxLabeled(skillDef.LabelCap, ref selected);

            if (selected)
            {
                selection |= flag;
            }
            else
            {
                selection &= ~flag;
            }
        }
    }
}
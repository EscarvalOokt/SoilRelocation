using System;
using System.Collections.Generic;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Extensibility.RuntimeTests
{
    [StaticConstructorOnStartup]
    internal static class ExtensibilityRuntimeValidator
    {
        private const string LogPrefix = "[SoilRelocation.ExtensibilityTests]";

        private const string DirectOptInTerrainDefName = "SoilRelocation_ExtTest_DirectOptIn";
        private const string OverrideTerrainDefName = "SoilRelocation_ExtTest_Override";
        private const string ExcludedTerrainDefName = "SoilRelocation_ExtTest_Excluded";
        private const string DriesToTerrainDefName = "SoilRelocation_ExtTest_DriesTo";
        private const string DriesToTargetTerrainDefName = "SoilRelocation_ExtTest_DriesToTarget";
        private const string DuplicateTerrainDefName = "SoilRelocation_ExtTest_Duplicate";
        private const string UnmappedTerrainDefName = "SoilRelocation_ExtTest_Unmapped";

        private const string SandFamilyDefName = "SoilRelocation_SandFamily";
        private const string GravelFamilyDefName = "SoilRelocation_GravelFamily";
        private const string SoilFamilyDefName = "SoilRelocation_SoilFamily";
        private const string RichSoilFamilyDefName = "SoilRelocation_RichSoilFamily";

        static ExtensibilityRuntimeValidator()
        {
            try
            {
                var failures = new List<string>();

                ValidateDirectOptIn(failures);
                ValidateMembershipOverride(failures);
                ValidateExplicitExclusion(failures);
                ValidateDriesToPrecedence(failures);
                ValidateDuplicateMembership(failures);
                ValidateMissingMembership(failures);
                ValidatePlacementResolution(failures);

                if (failures.Count == 0)
                {
                    Log.Message(
                        $"{LogPrefix} PASS: the synthetic third-party terrain contract passed all runtime checks. " +
                        $"The '{DuplicateTerrainDefName}' fixture intentionally triggers the existing Soil Relocation " +
                        "duplicate-membership diagnostic.");

                    return;
                }

                for (var i = 0; i < failures.Count; i++)
                {
                    Log.Error($"{LogPrefix} FAIL: {failures[i]}");
                }

                Log.Error($"{LogPrefix} FAILED with {failures.Count} validation error(s).");
            }
            catch (Exception exception)
            {
                Log.Error($"{LogPrefix} FAILED with an unexpected exception: {exception}");
            }
        }

        private static void ValidateDirectOptIn(List<string> failures)
        {
            const string scenario = "Direct opt-in";

            TerrainDef terrain = RequireTerrain(DirectOptInTerrainDefName, scenario, failures);
            RelocationFamilyDef soilFamily = RequireFamily(SoilFamilyDefName, scenario, failures);

            if (terrain == null || soilFamily == null)
            {
                return;
            }

            AssertSingleMembership(terrain, true, soilFamily, scenario, failures);
            AssertResolution(terrain, RelocationRuleStatus.Supported, soilFamily, scenario, failures);
            AssertCanonicalFamilyData(soilFamily, "Soil", "SoilRelocation_Soil", scenario, failures);
        }

        private static void ValidateMembershipOverride(List<string> failures)
        {
            const string scenario = "Membership override";

            TerrainDef terrain = RequireTerrain(OverrideTerrainDefName, scenario, failures);
            RelocationFamilyDef gravelFamily = RequireFamily(GravelFamilyDefName, scenario, failures);
            RelocationFamilyDef sandFamily = RequireFamily(SandFamilyDefName, scenario, failures);

            if (terrain == null || gravelFamily == null || sandFamily == null)
            {
                return;
            }

            RelocationTerrainExtension extension = AssertSingleMembership(
                terrain,
                true,
                gravelFamily,
                scenario,
                failures);

            if (extension != null)
            {
                Check(
                    !ReferenceEquals(extension.Family, sandFamily),
                    scenario,
                    "the final processed membership still points to the pre-patch Sand family",
                    failures);
            }

            AssertResolution(terrain, RelocationRuleStatus.Supported, gravelFamily, scenario, failures);
            AssertCanonicalFamilyData(gravelFamily, "Gravel", "SoilRelocation_Gravel", scenario, failures);
        }

        private static void ValidateExplicitExclusion(List<string> failures)
        {
            const string scenario = "Explicit exclusion";

            TerrainDef terrain = RequireTerrain(ExcludedTerrainDefName, scenario, failures);
            RelocationFamilyDef richSoilFamily = RequireFamily(RichSoilFamilyDefName, scenario, failures);

            if (terrain == null || richSoilFamily == null)
            {
                return;
            }

            AssertSingleMembership(terrain, false, richSoilFamily, scenario, failures);
            AssertResolution(terrain, RelocationRuleStatus.UnsupportedExplicitExclusion, null, scenario, failures);
        }

        private static void ValidateDriesToPrecedence(List<string> failures)
        {
            const string scenario = "driesTo precedence";

            TerrainDef terrain = RequireTerrain(DriesToTerrainDefName, scenario, failures);
            TerrainDef driesToTarget = RequireTerrain(DriesToTargetTerrainDefName, scenario, failures);
            RelocationFamilyDef sandFamily = RequireFamily(SandFamilyDefName, scenario, failures);

            if (terrain == null || driesToTarget == null || sandFamily == null)
            {
                return;
            }

            AssertSingleMembership(terrain, true, sandFamily, scenario, failures);
            Check(
                ReferenceEquals(terrain.driesTo, driesToTarget),
                scenario,
                "the synthetic driesTo cross-reference was not resolved to the expected target TerrainDef",
                failures);

            AssertResolution(terrain, RelocationRuleStatus.UnsupportedDriesTo, null, scenario, failures);
        }

        private static void ValidateDuplicateMembership(List<string> failures)
        {
            const string scenario = "Duplicate membership";

            TerrainDef terrain = RequireTerrain(DuplicateTerrainDefName, scenario, failures);

            if (terrain == null)
            {
                return;
            }

            int extensionCount = RelocationRuleResolver.FindMembershipExtensions(
                terrain,
                out RelocationTerrainExtension firstExtension);

            Check(
                extensionCount == 2,
                scenario,
                $"expected 2 membership extensions after Def processing, got {extensionCount}",
                failures);

            Check(
                firstExtension != null,
                scenario,
                "the final processed terrain contains no readable membership extension",
                failures);

            AssertResolution(terrain, RelocationRuleStatus.UnsupportedAmbiguousMembership, null, scenario, failures);
        }

        private static void ValidateMissingMembership(List<string> failures)
        {
            const string scenario = "Missing membership";

            TerrainDef terrain = RequireTerrain(UnmappedTerrainDefName, scenario, failures);

            if (terrain == null)
            {
                return;
            }

            int extensionCount = RelocationRuleResolver.FindMembershipExtensions(
                terrain,
                out RelocationTerrainExtension extension);

            Check(
                extensionCount == 0,
                scenario,
                $"expected no membership extension after Def processing, got {extensionCount}",
                failures);

            Check(
                extension == null,
                scenario,
                "the unmapped terrain unexpectedly exposes a Soil Relocation membership extension",
                failures);

            AssertResolution(terrain, RelocationRuleStatus.UnsupportedNoMembership, null, scenario, failures);
        }

        private static void ValidatePlacementResolution(List<string> failures)
        {
            const string scenario = "Placement family semantics";

            TerrainDef soilCandidate = RequireTerrain(DirectOptInTerrainDefName, scenario, failures);
            TerrainDef gravelCandidate = RequireTerrain(OverrideTerrainDefName, scenario, failures);
            RelocationFamilyDef soilFamily = RequireFamily(SoilFamilyDefName, scenario, failures);
            RelocationFamilyDef gravelFamily = RequireFamily(GravelFamilyDefName, scenario, failures);

            if (soilCandidate == null || gravelCandidate == null || soilFamily == null || gravelFamily == null)
            {
                return;
            }

            TerrainDef canonicalResult = RelocationPlacementPolicy.ResolveTerrain(
                soilFamily,
                PlacementResolutionMode.Canonical,
                gravelCandidate,
                new[] { soilCandidate });

            Check(
                ReferenceEquals(canonicalResult, soilFamily.CanonicalTerrain),
                scenario,
                "Canonical resolution did not return the selected family's production canonical terrain",
                failures);

            TerrainDef soilBiomeNativeResult = RelocationPlacementPolicy.ResolveTerrain(
                soilFamily,
                PlacementResolutionMode.BiomeNative,
                gravelCandidate,
                new[] { gravelCandidate, soilCandidate, soilCandidate });

            Check(
                ReferenceEquals(soilBiomeNativeResult, soilCandidate),
                scenario,
                "Biome-native resolution did not select the processed same-family terrain candidate",
                failures);

            TerrainDef gravelBiomeNativeResult = RelocationPlacementPolicy.ResolveTerrain(
                gravelFamily,
                PlacementResolutionMode.BiomeNative,
                gravelCandidate,
                new[] { soilCandidate });

            Check(
                ReferenceEquals(gravelBiomeNativeResult, gravelCandidate),
                scenario,
                "Biome-native Gravel resolution did not use the terrain whose membership was overridden to Gravel",
                failures);

            TerrainDef otherFamilyOnlyResult = RelocationPlacementPolicy.ResolveTerrain(
                soilFamily,
                PlacementResolutionMode.BiomeNative,
                gravelCandidate,
                new[] { gravelCandidate });

            Check(
                ReferenceEquals(otherFamilyOnlyResult, soilFamily.CanonicalTerrain),
                scenario,
                "Biome-native resolution accepted a candidate from a different processed relocation family",
                failures);
        }

        private static RelocationTerrainExtension AssertSingleMembership(
            TerrainDef terrain,
            bool expectedEnabled,
            RelocationFamilyDef expectedFamily,
            string scenario,
            List<string> failures)
        {
            int extensionCount = RelocationRuleResolver.FindMembershipExtensions(
                terrain,
                out RelocationTerrainExtension extension);

            Check(
                extensionCount == 1,
                scenario,
                $"expected exactly 1 membership extension after Def processing, got {extensionCount}",
                failures);

            if (extensionCount != 1 || extension == null)
            {
                return extension;
            }

            Check(
                extension.Enabled == expectedEnabled,
                scenario,
                $"expected membership Enabled={expectedEnabled}, got Enabled={extension.Enabled}",
                failures);

            Check(
                ReferenceEquals(extension.Family, expectedFamily),
                scenario,
                $"expected family '{expectedFamily.defName}', got '{extension.Family?.defName ?? "null"}'",
                failures);

            return extension;
        }

        private static void AssertResolution(
            TerrainDef terrain,
            RelocationRuleStatus expectedStatus,
            RelocationFamilyDef expectedFamily,
            string scenario,
            List<string> failures)
        {
            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            Check(
                resolution.Status == expectedStatus,
                scenario,
                $"expected resolver status '{expectedStatus}', got '{resolution.Status}'",
                failures);

            bool expectedSupported = expectedStatus == RelocationRuleStatus.Supported;

            Check(
                resolution.IsSupported == expectedSupported,
                scenario,
                $"expected IsSupported={expectedSupported}, got IsSupported={resolution.IsSupported}",
                failures);

            Check(
                ReferenceEquals(resolution.Family, expectedFamily),
                scenario,
                $"expected resolved family '{expectedFamily?.defName ?? "null"}', " +
                $"got '{resolution.Family?.defName ?? "null"}'",
                failures);
        }

        private static void AssertCanonicalFamilyData(
            RelocationFamilyDef family,
            string expectedTerrainDefName,
            string expectedResourceDefName,
            string scenario,
            List<string> failures)
        {
            TerrainDef expectedTerrain = RequireTerrain(expectedTerrainDefName, scenario, failures);
            ThingDef expectedResource = RequireThingDef(expectedResourceDefName, scenario, failures);

            if (expectedTerrain == null || expectedResource == null)
            {
                return;
            }

            Check(
                ReferenceEquals(family.CanonicalTerrain, expectedTerrain),
                scenario,
                $"family '{family.defName}' does not expose canonical terrain '{expectedTerrainDefName}'",
                failures);

            Check(
                ReferenceEquals(family.CanonicalPortableResource, expectedResource),
                scenario,
                $"family '{family.defName}' does not expose canonical resource '{expectedResourceDefName}'",
                failures);
        }

        private static TerrainDef RequireTerrain(string defName, string scenario, List<string> failures)
        {
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);

            Check(
                terrain != null,
                scenario,
                $"TerrainDef '{defName}' was not present in the final Def database",
                failures);

            return terrain;
        }

        private static ThingDef RequireThingDef(string defName, string scenario, List<string> failures)
        {
            ThingDef thing = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

            Check(thing != null, scenario, $"ThingDef '{defName}' was not present in the final Def database", failures);

            return thing;
        }

        private static RelocationFamilyDef RequireFamily(string defName, string scenario, List<string> failures)
        {
            RelocationFamilyDef family = DefDatabase<RelocationFamilyDef>.GetNamedSilentFail(defName);

            Check(
                family != null,
                scenario,
                $"RelocationFamilyDef '{defName}' was not present in the final Def database",
                failures);

            return family;
        }

        private static void Check(bool condition, string scenario, string message, List<string> failures)
        {
            if (!condition)
            {
                failures.Add($"{scenario}: {message}.");
            }
        }
    }
}
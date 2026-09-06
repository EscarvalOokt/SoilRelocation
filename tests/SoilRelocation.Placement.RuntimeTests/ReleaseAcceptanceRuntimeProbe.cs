using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using SoilRelocation.Digging;
using SoilRelocation.Domain;
using SoilRelocation.Relocation;
using SoilRelocation.Substrate;
using UnityEngine;
using Verse;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        private const string CorePackageId = "ludeon.rimworld";
        private const string RoyaltyPackageId = "ludeon.rimworld.royalty";
        private const string IdeologyPackageId = "ludeon.rimworld.ideology";
        private const string BiotechPackageId = "ludeon.rimworld.biotech";
        private const string AnomalyPackageId = "ludeon.rimworld.anomaly";
        private const string OdysseyPackageId = "ludeon.rimworld.odyssey";
        private const string HarmonyPackageId = "brrainz.harmony";
        private const string SoilRelocationPackageId = "escarval.soilrelocation";
        private const string PlacementTestsPackageId = "escarval.soilrelocation.placementtests";

        private static readonly string[] _allOfficialPackageIds =
        {
            CorePackageId,
            RoyaltyPackageId,
            IdeologyPackageId,
            BiotechPackageId,
            AnomalyPackageId,
            OdysseyPackageId
        };

        [DebugAction(
            "Soil Relocation",
            "Validate release acceptance profile",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ValidateReleaseAcceptanceProfile()
        {
            var report = new ProbeReport();

            try
            {
                RunReleaseAcceptanceProfile(report);
            }
            catch (Exception exception)
            {
                report.Fail("Release acceptance runner", "Unhandled exception: " + exception);
            }

            WriteReport("Release acceptance profile", report);
        }

        private static void RunReleaseAcceptanceProfile(ProbeReport report)
        {
            ProbeReleaseDependencies(report);

            ReleaseAcceptanceProfile profile = DetectReleaseProfile(out string description);
            report.Info("Release profile", description);

            switch (profile)
            {
                case ReleaseAcceptanceProfile.CoreBaseline:
                    report.Pass("Release profile", "Detected profile A - Core baseline.");
                    RunCoreBaselineAcceptance(report);
                    break;

                case ReleaseAcceptanceProfile.OdysseyFocused:
                    report.Pass("Release profile", "Detected profile B - Odyssey focused.");
                    RunOdysseyFocusedAcceptance(report);
                    break;

                case ReleaseAcceptanceProfile.AllOfficial:
                    report.Pass("Release profile", "Detected profile C - all official DLC without Allow Tool.");
                    RunAllOfficialAcceptance(report);
                    break;

                case ReleaseAcceptanceProfile.AllOfficialWithAllowTool:
                    report.Pass("Release profile", "Detected profile D - all official DLC with Allow Tool.");
                    RunAllowToolAcceptance(report);
                    break;

                default:
                    report.Fail(
                        "Release profile",
                        "The current official-content/Allow Tool combination is not one of the agreed A-D release " +
                        "acceptance profiles.");
                    break;
            }

            if (profile != ReleaseAcceptanceProfile.Unsupported)
            {
                report.Info(
                    "Third-party extensibility acceptance",
                    "Run SoilRelocation.Extensibility.TestMod as a separate controlled pass; its synthetic Def " +
                    "patches " + "are intentionally not mixed into profiles A-D.");
            }
        }

        private static void RunCoreBaselineAcceptance(ProbeReport report)
        {
            ProbeCoreReleaseContracts(report);
            RunCoreNaturalStoneDestinationProbe(report);
            RunDiggingRuntimeProbe(report);
            RunIntegratedProbe(report);
            RunRelocationOrderUxContracts(report);
            RunVanillaRelocationCancellationProbe(report);
            ProbeRuntimeContentAndTranslations(report);

            report.Info(
                "Profile A / follow-up",
                "Also run 'Prepare digging save-load cases' and the existing placement/UX save-load fixtures on this " +
                "Core-only environment. The optional Allow Tool API must remain absent in profile A.");
        }

        private static void RunOdysseyFocusedAcceptance(ProbeReport report)
        {
            ProbeOdysseyLoadedDefContracts(report);
            ProbeOdysseyBiomeNativeContracts(report);
            ProbeOdysseyNaturalRockDefContracts(report);
            RunCoreNaturalStoneDestinationProbe(report);
            RunOdysseyNaturalStoneDestinationProbe(report);
            ProbeCurrentOdysseyNaturalRockMap(report);
            ProbeRuntimeContentAndTranslations(report);

            report.Info(
                "Profile B / map coverage",
                "The Def-level Odyssey assertions run on any map. For full focused runtime coverage, run this action " +
                "once on a map whose effective biome forceRockTypes is exactly Vacstone (for example Orbit) and once " +
                "on a map whose effective biome extraRockTypes includes SolidIce.");
        }

        private static void RunAllOfficialAcceptance(ProbeReport report)
        {
            ProbeCoreReleaseContracts(report);
            ProbeOdysseyLoadedDefContracts(report);
            ProbeOdysseyBiomeNativeContracts(report);
            ProbeOdysseyNaturalRockDefContracts(report);
            RunCoreNaturalStoneDestinationProbe(report);
            RunOdysseyNaturalStoneDestinationProbe(report);
            RunDiggingRuntimeProbe(report);
            RunIntegratedProbe(report);
            RunRelocationOrderUxContracts(report);
            RunVanillaRelocationCancellationProbe(report);
            ProbeRuntimeContentAndTranslations(report);

            report.Info(
                "Profile C / language smoke",
                "Repeat the UI smoke in English, Russian, and Ukrainian. Check the Architect dropdown, designator " +
                "and " +
                "Inspect labels, settings, rejection messages, progress bars, Allow/Forbid/Cancel presentation, and " +
                "the log for missing translation or texture errors.");
        }

        private static void RunAllowToolAcceptance(ProbeReport report)
        {
            ProbeCoreReleaseContracts(report);
            RunVanillaRelocationCancellationProbe(report);
            RunAllowToolRelocationCancellationProbe(report);

            report.Info(
                "Profile D / manual Allow Tool smoke",
                "Confirm the Allow Tool context-menu/hotkey paths for Cancel selected and Cancel designations in the " +
                "actual UI after the automated probe. Profile C carries the full non-Allow-Tool regression pass.");
        }

        private static void ProbeReleaseDependencies(ProbeReport report)
        {
            ProbeRunningPackage(HarmonyPackageId, "Harmony dependency", required: true, report);
            ProbeRunningPackage(SoilRelocationPackageId, "Soil Relocation", required: true, report);
            ProbeRunningPackage(PlacementTestsPackageId, "Placement runtime test mod", required: true, report);
        }

        private static void ProbeRunningPackage(string packageId, string scenario, bool required, ProbeReport report)
        {
            bool active = IsRunningPackage(packageId);

            if (active == required)
            {
                report.Pass(scenario, "packageId='" + packageId + "', active=" + active + ".");
            }
            else
            {
                report.Fail(scenario, "packageId='" + packageId + "', active=" + active + ".");
            }
        }

        private static ReleaseAcceptanceProfile DetectReleaseProfile(out string description)
        {
            HashSet<string> official = GetActiveOfficialPackageIds();
            bool allowToolLoaded = IsAllowToolApiPresent();
            var packages = string.Join(", ", official.OrderBy(value => value));
            description = "official=[" + packages + "], AllowToolApi=" + allowToolLoaded + ".";

            var coreOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CorePackageId };
            var coreOdyssey = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                CorePackageId,
                OdysseyPackageId
            };
            var allOfficial = new HashSet<string>(_allOfficialPackageIds, StringComparer.OrdinalIgnoreCase);

            if (official.SetEquals(coreOnly) && !allowToolLoaded)
            {
                return ReleaseAcceptanceProfile.CoreBaseline;
            }

            if (official.SetEquals(coreOdyssey) && !allowToolLoaded)
            {
                return ReleaseAcceptanceProfile.OdysseyFocused;
            }

            if (official.SetEquals(allOfficial))
            {
                return allowToolLoaded
                    ? ReleaseAcceptanceProfile.AllOfficialWithAllowTool
                    : ReleaseAcceptanceProfile.AllOfficial;
            }

            return ReleaseAcceptanceProfile.Unsupported;
        }

        private static HashSet<string> GetActiveOfficialPackageIds()
        {
            var officialIds = new HashSet<string>(_allOfficialPackageIds, StringComparer.OrdinalIgnoreCase);
            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (mod != null && !string.IsNullOrEmpty(mod.PackageId) && officialIds.Contains(mod.PackageId))
                {
                    active.Add(mod.PackageId);
                }
            }

            return active;
        }

        private static bool IsRunningPackage(string packageId)
        {
            return LoadedModManager.RunningModsListForReading.Any(mod =>
                mod != null && string.Equals(mod.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAllowToolApiPresent()
        {
            return FindLoadedType(AllowToolBaseContextMenuEntryTypeName) != null ||
                   FindLoadedType(AllowToolCancelSelectedTypeName) != null ||
                   FindLoadedType(AllowToolCancelDesignationsTypeName) != null ||
                   FindLoadedType(AllowToolActivationResultTypeName) != null;
        }

        private static void ProbeCoreReleaseContracts(ProbeReport report)
        {
            ProbeExpectedDefProvenance<TerrainDef>("MossyTerrain", CorePackageId, report);
            ProbeExpectedDefProvenance<TerrainDef>("Riverbank", CorePackageId, report);
            ProbeExpectedDefProvenance<ThingDef>("PowerConduit", CorePackageId, report);
            ProbeExpectedDefProvenance<ThingDef>("HiddenConduit", CorePackageId, report);
            ProbeExpectedDefProvenance<ThingDef>("WaterproofConduit", CorePackageId, report);

            ProbeCoreTerrainMembership("MossyTerrain", "Soil", report);
            ProbeCoreTerrainMembership("Riverbank", "SoilRich", report);
            ProbeCoreConduitPolicy(report);
        }

        private static void ProbeCoreTerrainMembership(
            string terrainDefName,
            string canonicalTerrainDefName,
            ProbeReport report)
        {
            string scenario = "Core terrain membership / " + terrainDefName;
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(terrainDefName);
            TerrainDef canonical = DefDatabase<TerrainDef>.GetNamedSilentFail(canonicalTerrainDefName);

            if (terrain == null || canonical == null)
            {
                report.Fail(scenario, "Expected terrain Def is unavailable.");
                return;
            }

            RelocationRuleResolution resolution = RelocationRuleResolver.Resolve(terrain);

            if (!resolution.IsSupported || resolution.Family == null ||
                !ReferenceEquals(resolution.Family.CanonicalTerrain, canonical))
            {
                report.Fail(
                    scenario,
                    "The final loaded relocation family does not resolve to canonical " + canonicalTerrainDefName +
                    ".");
                return;
            }

            report.Pass(
                scenario,
                terrainDefName + " remains a supported source member of the " + canonicalTerrainDefName + " family.");
        }

        private static void ProbeCoreConduitPolicy(ProbeReport report)
        {
            const string scenario = "Core conduit exception policy";
            Map map = Find.CurrentMap;
            Pawn pawn = map == null ? null : ResolvePawn(map, requireEmptyCarry: false);

            if (map == null || pawn == null || !TryFindClearRockCell(map, pawn.Position, 4, 35, out IntVec3 cell))
            {
                report.Skip(scenario, "No clear natural-rock fixture cell is available.");
                return;
            }

            string[] conduitNames = { "PowerConduit", "HiddenConduit", "WaterproofConduit" };

            foreach (string conduitName in conduitNames)
            {
                ThingDef conduitDef = DefDatabase<ThingDef>.GetNamedSilentFail(conduitName);
                Thing conduit = null;

                if (conduitDef == null)
                {
                    report.Fail(scenario, conduitName + " is unavailable.");
                    return;
                }

                try
                {
                    conduit = ThingMaker.MakeThing(conduitDef);
                    GenSpawn.Spawn(conduit, cell, map);
                    bool blockedWhenDisabled = RelocationCellContentPolicy.HasStructuralBlocker(
                        map,
                        cell,
                        allowRelocationUnderConduits: false);
                    bool blockedWhenEnabled = RelocationCellContentPolicy.HasStructuralBlocker(
                        map,
                        cell,
                        allowRelocationUnderConduits: true);

                    if (!blockedWhenDisabled || blockedWhenEnabled ||
                        !RelocationCellContentPolicy.IsConduitDef(conduitDef))
                    {
                        report.Fail(
                            scenario,
                            conduitName + " did not obey the exact built-in conduit allow-list contract.");
                        return;
                    }
                }
                finally
                {
                    DestroyLooseThing(conduit);
                }
            }

            report.Pass(
                scenario,
                "PowerConduit, HiddenConduit, and WaterproofConduit block relocation when the setting is disabled " +
                "and are the exact built-in exceptions when it is enabled.");
        }

        private static void ProbeOdysseyLoadedDefContracts(ProbeReport report)
        {
            ProbeExpectedDefProvenance<BiomeDef>("Glowforest", OdysseyPackageId, report);
            ProbeExpectedDefProvenance<TerrainDef>("GlowforestSoil", OdysseyPackageId, report);
            ProbeExpectedDefProvenance<BiomeDef>("Grasslands", OdysseyPackageId, report);
            ProbeExpectedDefProvenance<TerrainDef>("GrasslandSoil", OdysseyPackageId, report);
            ProbeExpectedDefProvenance<BiomeDef>("GlacialPlain", OdysseyPackageId, report);
            ProbeExpectedDefProvenance<ThingDef>("SolidIce", OdysseyPackageId, report);
            ProbeExpectedDefProvenance<BiomeDef>("Space", OdysseyPackageId, report);
            ProbeExpectedDefProvenance<BiomeDef>("Orbit", OdysseyPackageId, report);
            ProbeExpectedDefProvenance<ThingDef>("Vacstone", OdysseyPackageId, report);
        }

        private static void ProbeExpectedDefProvenance<TDef>(
            string defName,
            string expectedPackageId,
            ProbeReport report) where TDef : Def
        {
            string scenario = "Def provenance / " + typeof(TDef).Name + " " + defName;
            TDef def = DefDatabase<TDef>.GetNamedSilentFail(defName);

            if (def == null)
            {
                report.Fail(scenario, "Def is unavailable in the final loaded database.");
                return;
            }

            ModContentPack pack = def.modContentPack;
            bool valid = pack != null && pack.IsOfficialMod && string.Equals(
                pack.PackageId,
                expectedPackageId,
                StringComparison.OrdinalIgnoreCase);

            if (!valid)
            {
                report.Fail(
                    scenario,
                    "Expected official package '" + expectedPackageId + "', actual='" + (pack?.PackageId ?? "<null>") +
                    "'.");
                return;
            }

            report.Pass(scenario, "packageId='" + pack.PackageId + "', official=True.");
        }

        private static void ProbeOdysseyBiomeNativeContracts(ProbeReport report)
        {
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            TerrainDef glowforestSoil = DefDatabase<TerrainDef>.GetNamedSilentFail("GlowforestSoil");
            TerrainDef grasslandSoil = DefDatabase<TerrainDef>.GetNamedSilentFail("GrasslandSoil");
            BiomeDef glowforest = DefDatabase<BiomeDef>.GetNamedSilentFail("Glowforest");
            BiomeDef grasslands = DefDatabase<BiomeDef>.GetNamedSilentFail("Grasslands");

            if (soil == null || glowforestSoil == null || grasslandSoil == null || glowforest == null ||
                grasslands == null)
            {
                report.Fail("Odyssey biome-native", "Required Odyssey/Core terrain or biome Def is unavailable.");
                return;
            }

            RelocationRuleResolution soilRule = RelocationRuleResolver.Resolve(soil);

            if (!soilRule.IsSupported || soilRule.Family == null)
            {
                report.Fail("Odyssey biome-native", "Canonical Soil does not resolve to a valid relocation family.");
                return;
            }

            ProbeBiomeNativeResolution(glowforest, soilRule.Family, soil, glowforestSoil, report);
            ProbeBiomeNativeResolution(grasslands, soilRule.Family, soil, grasslandSoil, report);
        }

        private static void ProbeBiomeNativeResolution(
            BiomeDef biome,
            RelocationFamilyDef family,
            TerrainDef canonicalTerrain,
            TerrainDef expectedNativeTerrain,
            ProbeReport report)
        {
            string scenario = "Odyssey biome-native / " + biome.defName;
            List<TerrainDef> fertilityTerrains = biome.terrainsByFertility?.Where(record => record?.terrain != null)
                .Select(record => record.terrain).ToList() ?? new List<TerrainDef>();
            TerrainDef canonical = RelocationPlacementPolicy.ResolveTerrain(
                family,
                PlacementResolutionMode.Canonical,
                biome.gravelTerrain,
                fertilityTerrains);
            TerrainDef native = RelocationPlacementPolicy.ResolveTerrain(
                family,
                PlacementResolutionMode.BiomeNative,
                biome.gravelTerrain,
                fertilityTerrains);

            if (!ReferenceEquals(canonical, canonicalTerrain) || !ReferenceEquals(native, expectedNativeTerrain))
            {
                report.Fail(
                    scenario,
                    "Expected Canonical='" + canonicalTerrain.defName + "' and BiomeNative='" +
                    expectedNativeTerrain.defName + "', actual Canonical='" + (canonical?.defName ?? "<null>") +
                    "', BiomeNative='" + (native?.defName ?? "<null>") + "'.");
                return;
            }

            report.Pass(
                scenario,
                "Canonical remains " + canonicalTerrain.defName + " while BiomeNative resolves to " +
                expectedNativeTerrain.defName + ".");
        }

        private static void ProbeOdysseyNaturalRockDefContracts(ProbeReport report)
        {
            const string forceScenario = "Odyssey natural rocks / Space forceRockTypes";
            const string orbitScenario = "Odyssey natural rocks / Orbit inherited forceRockTypes";
            const string extraScenario = "Odyssey natural rocks / GlacialPlain extraRockTypes";
            BiomeDef space = DefDatabase<BiomeDef>.GetNamedSilentFail("Space");
            BiomeDef orbit = DefDatabase<BiomeDef>.GetNamedSilentFail("Orbit");
            BiomeDef glacial = DefDatabase<BiomeDef>.GetNamedSilentFail("GlacialPlain");
            ThingDef vacstone = DefDatabase<ThingDef>.GetNamedSilentFail("Vacstone");
            ThingDef solidIce = DefDatabase<ThingDef>.GetNamedSilentFail("SolidIce");
            TerrainDef ice = DefDatabase<TerrainDef>.GetNamedSilentFail("Ice");

            if (!HasExactForcedRock(space, vacstone))
            {
                report.Fail(forceScenario, "Space no longer forces the single Vacstone natural-rock profile.");
            }
            else
            {
                report.Pass(forceScenario, "Space forceRockTypes contains exactly Vacstone.");
            }

            if (!HasExactForcedRock(orbit, vacstone))
            {
                report.Fail(
                    orbitScenario,
                    "Orbit no longer inherits/effectively exposes the single forced Vacstone natural-rock profile.");
            }
            else
            {
                report.Pass(
                    orbitScenario,
                    "Orbit effectively exposes the same single forced Vacstone rock profile used by Space-derived " +
                    "maps.");
            }

            bool glacialValid = glacial != null && solidIce != null && ice != null && HasExtraRock(glacial, solidIce) &&
                                solidIce.building != null && solidIce.building.biomeSpecific &&
                                ReferenceEquals(solidIce.building.naturalTerrain, ice) && !ice.IsRock;

            if (!glacialValid)
            {
                report.Fail(
                    extraScenario,
                    "GlacialPlain/SolidIce no longer matches the biome-specific rock -> non-rock Ice terrain edge " +
                    "case required by the acceptance matrix.");
            }
            else
            {
                report.Pass(
                    extraScenario,
                    "GlacialPlain allows biome-specific SolidIce; SolidIce exposes Ice, and Ice remains outside the " +
                    "natural-stone-derived placement classification.");
            }
        }

        private static bool HasExactForcedRock(BiomeDef biome, ThingDef rock)
        {
            return biome != null && rock != null && biome.forceRockTypes != null && biome.forceRockTypes.Count == 1 &&
                   ReferenceEquals(biome.forceRockTypes[0], rock);
        }

        private static bool HasExtraRock(BiomeDef biome, ThingDef rock)
        {
            return biome != null && rock != null && biome.extraRockTypes != null && biome.extraRockTypes.Contains(rock);
        }

        private static void ProbeCurrentOdysseyNaturalRockMap(ProbeReport report)
        {
            const string scenario = "Odyssey natural rocks / current map";
            Map map = Find.CurrentMap;

            if (map == null || Find.World == null)
            {
                report.Skip(scenario, "No current map/world is available.");
                return;
            }

            BiomeDef biome = map.BiomeAt(map.Center);

            if (biome == null)
            {
                report.Skip(scenario, "The current map does not expose a biome at its center cell.");
                return;
            }

            ThingDef vacstone = DefDatabase<ThingDef>.GetNamedSilentFail("Vacstone");
            ThingDef solidIce = DefDatabase<ThingDef>.GetNamedSilentFail("SolidIce");

            if (vacstone == null || solidIce == null)
            {
                report.Fail(scenario, "Vacstone or SolidIce is unavailable in the final loaded Odyssey Def database.");
                return;
            }

            if (HasExactForcedRock(biome, vacstone))
            {
                ProbeCurrentForcedVacstoneMap(map, biome, vacstone, report);
                return;
            }

            if (HasExtraRock(biome, solidIce))
            {
                ProbeCurrentSolidIceMap(map, biome, solidIce, report);
                return;
            }

            report.Skip(
                scenario,
                "Current biome is '" + biome.defName + "' and exposes neither the exact forced Vacstone profile nor " +
                "the SolidIce extra-rock profile. Run profile B on representatives of both special reconstruction " +
                "paths.");
        }

        private static void ProbeCurrentForcedVacstoneMap(
            Map map,
            BiomeDef biome,
            ThingDef vacstone,
            ProbeReport report)
        {
            const string scenario = "Odyssey current map / forced Vacstone";
            List<ThingDef> rocks = Find.World.NaturalRockTypesIn(map.Tile)?.ToList() ?? new List<ThingDef>();

            if (rocks.Count != 1 || !ReferenceEquals(rocks[0], vacstone))
            {
                report.Fail(
                    scenario,
                    "Biome '" + biome.defName + "' exposes forceRockTypes=[Vacstone], but NaturalRockTypesIn for the " +
                    "current map does not resolve to the same single Vacstone set.");
                return;
            }

            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();

            if (component == null)
            {
                report.Fail(scenario, "NaturalRockMapComponent is unavailable on the current forced-Vacstone map.");
                return;
            }

            if (!TryFindClearRockCell(map, map.Center, 2, 50, out IntVec3 cell))
            {
                report.Skip(
                    scenario,
                    "Biome '" + biome.defName + "' has the forced Vacstone profile, but no usable current-map " +
                    "reconstruction fixture cell was found.");
                return;
            }

            if (!TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile originalProfile))
            {
                report.Fail(scenario, "Could not inspect the current map reconstruction profile.");
                return;
            }

            try
            {
                if (!TrySetNaturalRockProfile(component, null))
                {
                    report.Fail(scenario, "Could not clear the current map reconstruction profile for the fixture.");
                    return;
                }

                if (!component.TryPrepareResolution(cell, out NaturalRockResolution resolution) ||
                    !ReferenceEquals(resolution.ResolvedRock, vacstone))
                {
                    report.Fail(
                        scenario,
                        "NaturalRockMapComponent did not resolve Vacstone from biome '" + biome.defName +
                        "' forced rock set.");
                    return;
                }

                report.Pass(
                    scenario,
                    "Biome '" + biome.defName + "' exposes the forced single Vacstone rock set and feeds that " +
                    "identity through NaturalRockMapComponent.");
            }
            finally
            {
                TrySetNaturalRockProfile(component, originalProfile);
            }
        }

        private static void ProbeCurrentSolidIceMap(Map map, BiomeDef biome, ThingDef solidIce, ProbeReport report)
        {
            const string scenario = "Odyssey current map / SolidIce";
            const float fixtureSearchRadius = 75f;

            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            ThingDef digOrderDef = DefDatabase<ThingDef>.GetNamedSilentFail(DigOrderDefName);
            List<ThingDef> rocks = Find.World.NaturalRockTypesIn(map.Tile)?.ToList() ?? new List<ThingDef>();

            if (soil == null || digOrderDef == null || solidIce.building == null ||
                solidIce.building.naturalTerrain == null)
            {
                report.Fail(
                    scenario,
                    "Soil, the production Dig order Def, or SolidIce natural-terrain metadata is unavailable.");
                return;
            }

            if (!rocks.Contains(solidIce))
            {
                report.Skip(
                    scenario,
                    "Biome '" + biome.defName + "' allows SolidIce, but this generated natural-rock set did not " +
                    "select it. Generate/use a map whose NaturalRockTypesIn set includes SolidIce for the completion " +
                    "fixture.");
                return;
            }

            NaturalRockMapComponent component = map.GetComponent<NaturalRockMapComponent>();

            if (component == null)
            {
                report.Fail(scenario, "NaturalRockMapComponent is unavailable on the current SolidIce-capable map.");
                return;
            }

            if (!TryGetNaturalRockProfile(component, out NaturalRockReconstructionProfile originalProfile))
            {
                report.Fail(scenario, "Could not inspect the current map reconstruction profile.");
                return;
            }

            IntVec3 resolvedCell = IntVec3.Invalid;

            try
            {
                if (!TrySetNaturalRockProfile(component, null))
                {
                    report.Fail(scenario, "Could not clear the current map reconstruction profile for the fixture.");
                    return;
                }

                foreach (IntVec3 cell in GenRadial.RadialCellsAround(map.Center, fixtureSearchRadius, useCenter: true))
                {
                    if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map) || cell.GetThingList(map)
                            .Any(thing => thing is Building || thing is Plant || thing is Fire || thing is Blueprint))
                    {
                        continue;
                    }

                    if (component.TryPrepareResolution(cell, out NaturalRockResolution resolution) &&
                        ReferenceEquals(resolution.ResolvedRock, solidIce))
                    {
                        resolvedCell = cell;
                        break;
                    }
                }

                if (!resolvedCell.IsValid)
                {
                    report.Skip(
                        scenario,
                        "SolidIce is in the current rock set for biome '" + biome.defName +
                        "', but no suitable fixture cell resolving to it was found within the probe radius.");
                    return;
                }

                TerrainDef originalTerrain = map.terrainGrid.TerrainAt(resolvedCell);
                DigRelocationOrder order = null;
                var spawnedResources = new List<Thing>();

                try
                {
                    map.terrainGrid.SetTerrain(resolvedCell, soil);
                    order = SpawnDigOrder(digOrderDef, resolvedCell, map);
                    DiggingValidationResult validation = DiggingValidator.Validate(map, resolvedCell);

                    if (!validation.IsValid ||
                        !DigResultResolver.TryPrepare(
                            map,
                            resolvedCell,
                            validation,
                            out DigResultPreparation preparation) ||
                        !ReferenceEquals(preparation.NaturalRockResolution.ResolvedRock, solidIce) || !ReferenceEquals(
                            preparation.ExposedTerrain,
                            solidIce.building.naturalTerrain))
                    {
                        report.Fail(scenario, "Could not prepare a Dig result resolving to SolidIce -> Ice.");
                        return;
                    }

                    List<Thing> before = CaptureSpawnedThings(map, preparation.PortableResourceDef);

                    if (!DigResultApplier.TryApply(map, resolvedCell, preparation))
                    {
                        report.Fail(scenario, "The controlled SolidIce reconstruction could not be applied.");
                        return;
                    }

                    spawnedResources.AddRange(FindNewSpawnedThings(map, preparation.PortableResourceDef, before));
                    order.TryCancel();
                    order = null;

                    if (!ReferenceEquals(map.terrainGrid.TerrainAt(resolvedCell), solidIce.building.naturalTerrain) ||
                        !RelocationPlacementPolicy.IsDestinationEligible(map.terrainGrid.TerrainAt(resolvedCell)) ||
                        spawnedResources.Count != 1)
                    {
                        report.Fail(
                            scenario,
                            "SolidIce completion did not expose explicit placement-eligible Ice with exact 1:1 " +
                            "material accounting.");
                        return;
                    }

                    report.Pass(
                        scenario,
                        "Biome '" + biome.defName + "' produced a current-map cell resolving to SolidIce; Dig " +
                        "completion exposed Ice, preserved exact 1:1 material accounting, and confirmed Ice as an " +
                        "explicit placement destination.");
                }
                finally
                {
                    DestroyUxOrder(order);
                    DestroyThings(spawnedResources);
                    RestoreTerrain(map, resolvedCell, originalTerrain);
                }
            }
            finally
            {
                TrySetNaturalRockProfile(component, originalProfile);
            }
        }

        private static void ProbeRuntimeContentAndTranslations(ProbeReport report)
        {
            ProbeRuntimeTexture("Runtime asset / Dig designator", "UI/Designators/SoilRelocation/DigTerrain", report);
            ProbeRuntimeTexture(
                "Runtime asset / portable terrain",
                "Things/Item/Resource/SoilRelocation/PortableTerrain",
                report);
            ProbeRuntimeTexture("Runtime asset / Dig world graphic", DigWorldGraphicPath, report);
            ProbeRuntimeTexture("Runtime asset / Place world graphic", PlaceWorldGraphicPath, report);

            string[] translationKeys =
            {
                "SoilRelocation.Digging.Designator.Label",
                "SoilRelocation.Digging.Reject.UnsupportedTerrain",
                "SoilRelocation.Placement.Designator.Soil.Label",
                "SoilRelocation.Placement.Reject.InvalidDestination",
                "SoilRelocation.Settings.Category",
                "SoilRelocation.Settings.DigWorkAmount.Label",
                "SoilRelocation.Settings.PlacementResolution.BiomeNative.Label"
            };

            foreach (string key in translationKeys)
            {
                ProbeRuntimeTranslation(key, report);
            }

            report.Info(
                "Runtime localization",
                "Active language=" + (LanguageDatabase.activeLanguage?.ToString() ?? "<null>") + ".");
        }

        private static void ProbeRuntimeTexture(string scenario, string path, ProbeReport report)
        {
            Texture2D texture = ContentFinder<Texture2D>.Get(path, reportFailure: false);

            if (texture == null)
            {
                report.Fail(scenario, "ContentFinder could not resolve texture path '" + path + "'.");
            }
            else
            {
                report.Pass(scenario, "Resolved texture path '" + path + "'.");
            }
        }

        private static void ProbeRuntimeTranslation(string key, ProbeReport report)
        {
            string scenario = "Runtime translation / " + key;
            var value = key.Translate().ToString();
            bool missing = string.IsNullOrEmpty(value) || value == key ||
                           value.IndexOf("ERR:", StringComparison.Ordinal) >= 0;

            if (missing)
            {
                report.Fail(scenario, "Active-language translation did not resolve: '" + value + "'.");
            }
            else
            {
                report.Pass(scenario, "Resolved active-language value '" + value + "'.");
            }
        }

        private enum ReleaseAcceptanceProfile
        {
            Unsupported,
            CoreBaseline,
            OdysseyFocused,
            AllOfficial,
            AllOfficialWithAllowTool
        }
    }
}
using System;
using System.Collections.Generic;
using LudeonTK;
using SoilRelocation.Domain;
using Verse;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        [DebugAction(
            "Soil Relocation",
            "Validate natural-stone placement destinations",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ValidateNaturalStonePlacementDestinations()
        {
            var report = new ProbeReport();

            try
            {
                RunCoreNaturalStoneDestinationProbe(report);

                if (ModsConfig.OdysseyActive)
                {
                    RunOdysseyNaturalStoneDestinationProbe(report);
                }
                else
                {
                    report.Skip(
                        "Odyssey natural-stone destinations",
                        "Odyssey is not active; Vacstone, Ice, ThinIce, VolcanicRock, and lava terrain cases were not checked.");
                }
            }
            catch (Exception exception)
            {
                report.Fail("Natural-stone destination probe", "Unhandled exception: " + exception);
            }

            WriteReport("Natural-stone placement destination probe", report);
        }

        private static void RunCoreNaturalStoneDestinationProbe(ProbeReport report)
        {
            var positiveTerrains = new List<TerrainDef>();

            if (TryResolveNaturalStoneTerrainGraph(
                    "Granite",
                    "Core Granite",
                    report,
                    out NaturalStoneTerrainGraph granite))
            {
                ProbeDestinationEligibility("Core Granite Rough", granite.Rough, expected: true, report);
                ProbeDestinationEligibility("Core Granite RoughHewn", granite.RoughHewn, expected: true, report);
                ProbeDestinationEligibility("Core Granite Smooth", granite.Smooth, expected: true, report);

                positiveTerrains.Add(granite.Rough);
                positiveTerrains.Add(granite.RoughHewn);
                positiveTerrains.Add(granite.Smooth);
            }

            ProbeNamedTerrainEligibility("Core constructed TileGranite", "TileGranite", expected: false, report);
            ProbeNamedTerrainEligibility(
                "Core constructed FlagstoneGranite",
                "FlagstoneGranite",
                expected: false,
                report);

            ProbePlacementValidatorDestinations("Core natural-stone validator integration", positiveTerrains, report);
        }

        private static void RunOdysseyNaturalStoneDestinationProbe(ProbeReport report)
        {
            var positiveTerrains = new List<TerrainDef>();

            if (TryResolveNaturalStoneTerrainGraph(
                    "Vacstone",
                    "Odyssey Vacstone",
                    report,
                    out NaturalStoneTerrainGraph vacstone))
            {
                ProbeDestinationEligibility("Odyssey Vacstone Rough", vacstone.Rough, expected: true, report);
                ProbeDestinationEligibility("Odyssey Vacstone RoughHewn", vacstone.RoughHewn, expected: true, report);
                ProbeDestinationEligibility("Odyssey Vacstone Smooth", vacstone.Smooth, expected: true, report);

                positiveTerrains.Add(vacstone.Rough);
                positiveTerrains.Add(vacstone.RoughHewn);
                positiveTerrains.Add(vacstone.Smooth);
            }

            TerrainDef ice = DefDatabase<TerrainDef>.GetNamedSilentFail("Ice");

            if (ice == null)
            {
                report.Fail("Odyssey Ice", "TerrainDef 'Ice' was not found.");
            }
            else
            {
                ProbeDestinationEligibility("Odyssey Ice", ice, expected: true, report);
                positiveTerrains.Add(ice);
            }

            ProbeNamedTerrainEligibility("Odyssey ThinIce", "ThinIce", expected: false, report);

            TerrainDef volcanicRock = DefDatabase<TerrainDef>.GetNamedSilentFail("VolcanicRock");

            if (volcanicRock == null)
            {
                report.Fail("Odyssey VolcanicRock", "TerrainDef 'VolcanicRock' was not found.");
            }
            else
            {
                ProbeDestinationEligibility("Odyssey VolcanicRock", volcanicRock, expected: true, report);
                positiveTerrains.Add(volcanicRock);
            }

            TerrainDef smoothVolcanicRock = DefDatabase<TerrainDef>.GetNamedSilentFail("VolcanicRock_Smooth");

            if (smoothVolcanicRock == null)
            {
                report.Fail("Odyssey VolcanicRock Smooth", "TerrainDef 'VolcanicRock_Smooth' was not found.");
            }
            else
            {
                ProbeDestinationEligibility("Odyssey VolcanicRock Smooth", smoothVolcanicRock, expected: true, report);
                positiveTerrains.Add(smoothVolcanicRock);
            }

            ProbeNamedTerrainEligibility("Odyssey CooledLava", "CooledLava", expected: false, report);
            ProbeNamedTerrainEligibility("Odyssey LavaDeep", "LavaDeep", expected: false, report);
            ProbeNamedTerrainEligibility("Odyssey LavaShallow", "LavaShallow", expected: false, report);

            ProbePlacementValidatorDestinations("Odyssey destination validator integration", positiveTerrains, report);
        }

        private static bool TryResolveNaturalStoneTerrainGraph(
            string rootDefName,
            string scenario,
            ProbeReport report,
            out NaturalStoneTerrainGraph graph)
        {
            graph = default;
            ThingDef root = DefDatabase<ThingDef>.GetNamedSilentFail(rootDefName);

            if (root?.building == null)
            {
                report.Fail(scenario, "ThingDef '" + rootDefName + "' or its BuildingProperties was not found.");
                return false;
            }

            if (!root.building.isNaturalRock || root.building.isResourceRock)
            {
                report.Fail(
                    scenario,
                    "Expected a non-resource natural-rock root, got isNaturalRock=" +
                    root.building.isNaturalRock +
                    ", isResourceRock=" +
                    root.building.isResourceRock +
                    ".");
                return false;
            }

            TerrainDef rough = root.building.naturalTerrain;
            TerrainDef roughHewn = root.building.leaveTerrain;

            if (rough == null || !rough.IsRock)
            {
                report.Fail(
                    scenario,
                    "Expected building.naturalTerrain to resolve to a NaturalRock-tagged Rough terrain.");
                return false;
            }

            if (roughHewn == null)
            {
                report.Fail(scenario, "Expected building.leaveTerrain to resolve to RoughHewn terrain.");
                return false;
            }

            TerrainDef smoothFromRough = rough.smoothedTerrain;
            TerrainDef smoothFromRoughHewn = roughHewn.smoothedTerrain;

            if (smoothFromRough == null || smoothFromRoughHewn == null)
            {
                report.Fail(
                    scenario,
                    "Expected both Rough and RoughHewn terrains to resolve a smoothedTerrain reference.");
                return false;
            }

            if (!ReferenceEquals(smoothFromRough, smoothFromRoughHewn))
            {
                report.Fail(
                    scenario,
                    "Rough and RoughHewn resolve different Smooth terrains: '" +
                    smoothFromRough.defName +
                    "' and '" +
                    smoothFromRoughHewn.defName +
                    "'.");
                return false;
            }

            graph = new NaturalStoneTerrainGraph(rough, roughHewn, smoothFromRough);
            report.Pass(
                scenario,
                "root='" +
                root.defName +
                "', Rough='" +
                rough.defName +
                "', RoughHewn='" +
                roughHewn.defName +
                "', Smooth='" +
                smoothFromRough.defName +
                "'.");
            return true;
        }

        private static void ProbeNamedTerrainEligibility(
            string scenario,
            string terrainDefName,
            bool expected,
            ProbeReport report)
        {
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(terrainDefName);

            if (terrain == null)
            {
                report.Fail(scenario, "TerrainDef '" + terrainDefName + "' was not found.");
                return;
            }

            ProbeDestinationEligibility(scenario, terrain, expected, report);
        }

        private static void ProbeDestinationEligibility(
            string scenario,
            TerrainDef terrain,
            bool expected,
            ProbeReport report)
        {
            bool actual = RelocationPlacementPolicy.IsDestinationEligible(terrain);

            if (actual == expected)
            {
                report.Pass(scenario, "terrain='" + (terrain?.defName ?? "<null>") + "', eligible=" + actual + ".");
            }
            else
            {
                report.Fail(
                    scenario,
                    "terrain='" +
                    (terrain?.defName ?? "<null>") +
                    "', expected eligible=" +
                    expected +
                    ", actual=" +
                    actual +
                    ".");
            }
        }

        private static void ProbePlacementValidatorDestinations(
            string scenario,
            IReadOnlyList<TerrainDef> terrains,
            ProbeReport report)
        {
            if (terrains == null || terrains.Count == 0)
            {
                report.Fail(scenario, "No positive destination terrain cases were resolved.");
                return;
            }

            Map map = Find.CurrentMap;
            Pawn pawn = map == null ? null : ResolvePawn(map, requireEmptyCarry: false);

            if (map == null || pawn == null)
            {
                report.Skip(
                    scenario,
                    "A current map with a free spawned colonist is required for validator integration.");
                return;
            }

            if (!TryFindClearRockCell(map, pawn.Position, 4, 30, out IntVec3 targetCell))
            {
                report.Skip(scenario, "No clear natural-rock cell was available for a temporary validator fixture.");
                return;
            }

            FamilyCase familyCase = ResolveFamilyCase("Soil");

            if (familyCase == null ||
                !familyCase.TryResolve(out ThingDef orderDef, out _) ||
                !RelocationMaterialOrderUtility.TryResolveFamily(orderDef, out RelocationFamilyDef family))
            {
                report.Fail(scenario, "Could not resolve the built-in Soil placement family.");
                return;
            }

            TerrainDef originalTerrain = map.terrainGrid.TerrainAt(targetCell);

            try
            {
                for (var i = 0; i < terrains.Count; i++)
                {
                    TerrainDef terrain = terrains[i];
                    map.terrainGrid.SetTerrain(targetCell, terrain);

                    PlacementValidationResult validation = PlacementValidator.Validate(
                        map,
                        targetCell,
                        family,
                        PlacementResolutionMode.Canonical,
                        allowRelocationUnderConduits: false);

                    if (validation.IsValid)
                    {
                        report.Pass(scenario, "terrain='" + terrain.defName + "' accepted through PlacementValidator.");
                    }
                    else
                    {
                        report.Fail(
                            scenario,
                            "terrain='" +
                            terrain.defName +
                            "' rejected through PlacementValidator with status=" +
                            validation.Status +
                            ".");
                    }
                }
            }
            finally
            {
                RestoreTerrain(map, targetCell, originalTerrain);
            }
        }

        private readonly struct NaturalStoneTerrainGraph(TerrainDef rough, TerrainDef roughHewn, TerrainDef smooth)
        {
            public TerrainDef Rough { get; } = rough;

            public TerrainDef RoughHewn { get; } = roughHewn;

            public TerrainDef Smooth { get; } = smooth;
        }
    }
}
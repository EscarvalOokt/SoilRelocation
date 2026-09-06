using System;
using System.Collections;
using System.Reflection;
using LudeonTK;
using RimWorld;
using Verse;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        private const string VolcanicRockTerrainDefName = "VolcanicRock";
        private const string SmoothVolcanicRockTerrainDefName = "VolcanicRock_Smooth";

        private static readonly MethodInfo _lavaFlowCanPotentiallySpreadIntoMethod =
            typeof(GameCondition_LavaFlow).GetMethod(
                "CanLavaPotentiallySpreadInto",
                BindingFlags.Instance | BindingFlags.NonPublic);

        [DebugAction(
            "Soil Relocation",
            "Probe volcanic smoothing against Lava Flow",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        // ReSharper disable once UnusedMember.Local
        private static void ProbeVolcanicSmoothingAgainstLavaFlow()
        {
            var report = new ProbeReport();

            try
            {
                RunVolcanicSmoothingAgainstLavaFlowProbe(report);
            }
            catch (Exception exception)
            {
                report.Fail("Volcanic destination safety probe", "Unhandled exception: " + exception);
            }

            WriteReport("Volcanic smoothing / Lava Flow probe", report);
        }

        private static void RunVolcanicSmoothingAgainstLavaFlowProbe(ProbeReport report)
        {
            if (!ModsConfig.OdysseyActive)
            {
                report.Skip(
                    "Environment",
                    "Odyssey is not active; VolcanicRock and Lava Flow runtime behavior cannot be evaluated.");
                return;
            }

            Map map = Find.CurrentMap;

            if (map == null)
            {
                report.Fail("Environment", "No current map is available.");
                return;
            }

            report.Info("Environment", "Map=" + map.uniqueID + ", biome='" + (map.Biome?.defName ?? "<null>") + "'.");

            TerrainDef volcanicRock = DefDatabase<TerrainDef>.GetNamedSilentFail(VolcanicRockTerrainDefName);
            TerrainDef smoothVolcanicRock =
                DefDatabase<TerrainDef>.GetNamedSilentFail(SmoothVolcanicRockTerrainDefName);

            if (volcanicRock == null || smoothVolcanicRock == null)
            {
                report.Fail(
                    "Terrain definitions",
                    "Required Odyssey terrain Defs could not be resolved: VolcanicRock=" +
                    FormatVolcanicTerrain(volcanicRock) + ", VolcanicRock_Smooth=" +
                    FormatVolcanicTerrain(smoothVolcanicRock) + ".");
                return;
            }

            if (!ReferenceEquals(volcanicRock.smoothedTerrain, smoothVolcanicRock))
            {
                report.Fail(
                    "Vanilla smoothing Def graph",
                    "VolcanicRock.smoothedTerrain resolved to '" + (volcanicRock.smoothedTerrain?.defName ?? "<null>") +
                    "' instead of VolcanicRock_Smooth.");
                return;
            }

            report.Pass(
                "Vanilla smoothing Def graph",
                "VolcanicRock resolves directly to VolcanicRock_Smooth through TerrainDef.smoothedTerrain.");

            if (_lavaFlowCanPotentiallySpreadIntoMethod == null ||
                _lavaFlowCanPotentiallySpreadIntoMethod.ReturnType != typeof(bool))
            {
                report.Fail(
                    "Lava Flow runtime predicate",
                    "Could not resolve private GameCondition_LavaFlow.CanLavaPotentiallySpreadInto(IntVec3).");
                return;
            }

            bool conditionIsActive = map.GameConditionManager.ConditionIsActive(GameConditionDefOf.LavaFlow);
            bool liveConditionResolved = TryResolveActiveLavaFlowCondition(map, out GameCondition_LavaFlow lavaFlow);

            report.Info(
                "Lava Flow detection",
                "ConditionIsActive(LavaFlow)=" + conditionIsActive + ", live GameCondition_LavaFlow resolved=" +
                liveConditionResolved + ".");

            if (!conditionIsActive)
            {
                report.Skip(
                    "Active Lava Flow",
                    "The public GameConditionManager reports that Lava Flow is not active on the current map. " +
                    "If the incident was just triggered, it may already have ended because vanilla could not " +
                    "initialize any valid Lava Flow cells.");
                return;
            }

            if (!liveConditionResolved || lavaFlow == null)
            {
                report.Fail(
                    "Active Lava Flow",
                    "The public GameConditionManager reports an active Lava Flow, but the probe could not resolve " +
                    "the live GameCondition_LavaFlow instance required to evaluate its private spread predicate.");
                return;
            }

            report.Pass(
                "Active Lava Flow",
                "The public manager reports Lava Flow as active and the live GameCondition_LavaFlow instance was " +
                "resolved.");

            var smoothDesignator = new Designator_SmoothFloors();

            if (!TryPrepareVolcanicProbeCell(
                    map,
                    volcanicRock,
                    smoothVolcanicRock,
                    lavaFlow,
                    smoothDesignator,
                    report,
                    out IntVec3 targetCell,
                    out TerrainDef originalTerrain))
            {
                return;
            }

            report.Pass(
                "Probe fixture",
                "Prepared controlled cell " + targetCell + ": original terrain='" +
                FormatVolcanicTerrain(originalTerrain) +
                "', temporary probe terrain='VolcanicRock'. The live Lava Flow predicate and vanilla floor " +
                "smoothing both accept this cell.");

            bool originalGodMode = DebugSettings.godMode;
            var assertionsCompleted = false;

            try
            {
                AcceptanceReport smoothingAcceptance = smoothDesignator.CanDesignateCell(targetCell);

                if (!smoothingAcceptance.Accepted)
                {
                    report.Fail(
                        "Vanilla smoothing eligibility",
                        "The controlled VolcanicRock fixture was accepted during setup but is no longer accepted " +
                        "by Designator_SmoothFloors: " + smoothingAcceptance.Reason + ".");
                }
                else
                {
                    report.Pass(
                        "Vanilla smoothing eligibility",
                        "Designator_SmoothFloors accepts the controlled VolcanicRock fixture.");

                    if (!TryInvokeLavaFlowPotentialSpread(lavaFlow, targetCell, out bool eligibleBeforeSmoothing))
                    {
                        report.Fail(
                            "Lava Flow before smoothing",
                            "Could not invoke GameCondition_LavaFlow.CanLavaPotentiallySpreadInto for the " +
                            "controlled VolcanicRock fixture.");
                    }
                    else if (!eligibleBeforeSmoothing)
                    {
                        report.Fail(
                            "Lava Flow before smoothing",
                            "The controlled VolcanicRock fixture was accepted during setup but is no longer " +
                            "accepted by the same live Lava Flow predicate.");
                    }
                    else
                    {
                        report.Pass(
                            "Lava Flow before smoothing",
                            "The same live vanilla Lava Flow accepts the controlled VolcanicRock fixture.");

                        DebugSettings.godMode = true;
                        smoothDesignator.DesignateSingleCell(targetCell);

                        TerrainDef currentTerrain = map.terrainGrid.TerrainAt(targetCell);
                        TerrainDef topTerrain = map.terrainGrid.TopTerrainAt(targetCell);
                        TerrainDef underTerrain = map.terrainGrid.UnderTerrainAt(targetCell);
                        TerrainDef tempTerrain = map.terrainGrid.TempTerrainAt(targetCell);
                        TerrainDef foundation = map.terrainGrid.FoundationAt(targetCell);

                        if (!ReferenceEquals(currentTerrain, smoothVolcanicRock) ||
                            !ReferenceEquals(topTerrain, smoothVolcanicRock) || underTerrain != null ||
                            tempTerrain != null || foundation != null)
                        {
                            report.Fail(
                                "Vanilla smoothing result",
                                "Unexpected terrain-grid state after smoothing: current='" +
                                FormatVolcanicTerrain(currentTerrain) + "', top='" + FormatVolcanicTerrain(topTerrain) +
                                "', under='" + FormatVolcanicTerrain(underTerrain) + "', temp='" +
                                FormatVolcanicTerrain(tempTerrain) + "', foundation='" +
                                FormatVolcanicTerrain(foundation) + "'.");
                        }
                        else
                        {
                            report.Pass(
                                "Vanilla smoothing result",
                                "Vanilla smoothing replaced persistent VolcanicRock with VolcanicRock_Smooth and " +
                                "did not preserve VolcanicRock in underGrid.");

                            if (!TryInvokeLavaFlowPotentialSpread(
                                    lavaFlow,
                                    targetCell,
                                    out bool eligibleAfterSmoothing))
                            {
                                report.Fail(
                                    "Lava Flow after smoothing",
                                    "Could not invoke GameCondition_LavaFlow.CanLavaPotentiallySpreadInto after " +
                                    "vanilla smoothing.");
                            }
                            else if (eligibleAfterSmoothing)
                            {
                                report.Fail(
                                    "Lava Flow after smoothing",
                                    "The same live Lava Flow still accepts VolcanicRock_Smooth, contrary to the " +
                                    "source-level exact-VolcanicRock substrate contract.");
                            }
                            else
                            {
                                report.Pass(
                                    "Lava Flow after smoothing",
                                    "The same live vanilla Lava Flow rejects the same cell after VolcanicRock is " +
                                    "smoothed to VolcanicRock_Smooth.");

                                assertionsCompleted = true;
                            }
                        }
                    }
                }
            }
            finally
            {
                DebugSettings.godMode = originalGodMode;
                RestoreTerrain(map, targetCell, originalTerrain);
            }

            TerrainDef restoredCurrent = map.terrainGrid.TerrainAt(targetCell);
            TerrainDef restoredTop = map.terrainGrid.TopTerrainAt(targetCell);
            TerrainDef restoredUnder = map.terrainGrid.UnderTerrainAt(targetCell);
            TerrainDef restoredTemp = map.terrainGrid.TempTerrainAt(targetCell);
            TerrainDef restoredFoundation = map.terrainGrid.FoundationAt(targetCell);

            if (!ReferenceEquals(restoredCurrent, originalTerrain) || !ReferenceEquals(restoredTop, originalTerrain) ||
                restoredUnder != null || restoredTemp != null || restoredFoundation != null)
            {
                report.Fail(
                    "Fixture restoration",
                    "The controlled cell was not restored to its original simple persistent terrain state: " +
                    "expected='" + FormatVolcanicTerrain(originalTerrain) + "', current='" +
                    FormatVolcanicTerrain(restoredCurrent) + "', top='" + FormatVolcanicTerrain(restoredTop) +
                    "', under='" + FormatVolcanicTerrain(restoredUnder) + "', temp='" +
                    FormatVolcanicTerrain(restoredTemp) + "', foundation='" +
                    FormatVolcanicTerrain(restoredFoundation) + "'.");
                return;
            }

            report.Pass(
                "Fixture restoration",
                "The controlled cell was restored from the temporary volcanic fixture to its original terrain '" +
                FormatVolcanicTerrain(originalTerrain) + "'.");

            if (!assertionsCompleted)
            {
                return;
            }

            report.Pass(
                "Research outcome",
                "Runtime behavior confirms on the same cell and the same live GameCondition_LavaFlow instance that " +
                "exact VolcanicRock is spread-eligible, while vanilla smoothing to VolcanicRock_Smooth removes " +
                "that eligibility without requiring hidden VolcanicRock substrate state.");
        }

        private static bool TryPrepareVolcanicProbeCell(
            Map map,
            TerrainDef volcanicRock,
            TerrainDef smoothVolcanicRock,
            GameCondition_LavaFlow lavaFlow,
            Designator_SmoothFloors smoothDesignator,
            ProbeReport report,
            out IntVec3 result,
            out TerrainDef originalTerrain)
        {
            result = IntVec3.Invalid;
            originalTerrain = null;

            foreach (IntVec3 cell in map.AllCells)
            {
                if (!IsSafeVolcanicProbeFixtureCell(
                        map,
                        cell,
                        volcanicRock,
                        smoothVolcanicRock,
                        out TerrainDef candidateOriginalTerrain))
                {
                    continue;
                }

                map.terrainGrid.SetTerrain(cell, volcanicRock);

                if (!HasSimplePersistentTerrainState(map, cell, volcanicRock))
                {
                    RestoreTerrain(map, cell, candidateOriginalTerrain);

                    if (!HasSimplePersistentTerrainState(map, cell, candidateOriginalTerrain))
                    {
                        report.Fail(
                            "Probe fixture",
                            "A candidate cell could not be restored after the temporary VolcanicRock setup " +
                            "produced an unexpected terrain-grid state.");
                        return false;
                    }

                    continue;
                }

                if (!TryInvokeLavaFlowPotentialSpread(lavaFlow, cell, out bool eligible))
                {
                    RestoreTerrain(map, cell, candidateOriginalTerrain);

                    report.Fail(
                        "Probe fixture",
                        "Could not invoke GameCondition_LavaFlow.CanLavaPotentiallySpreadInto while evaluating a " +
                        "controlled VolcanicRock candidate.");
                    return false;
                }

                if (!eligible)
                {
                    RestoreTerrain(map, cell, candidateOriginalTerrain);

                    if (!HasSimplePersistentTerrainState(map, cell, candidateOriginalTerrain))
                    {
                        report.Fail(
                            "Probe fixture",
                            "A Lava Flow-ineligible candidate cell could not be restored to its original terrain " +
                            "after evaluating the runtime predicate.");
                        return false;
                    }

                    continue;
                }

                AcceptanceReport smoothingAcceptance = smoothDesignator.CanDesignateCell(cell);

                if (!smoothingAcceptance.Accepted)
                {
                    RestoreTerrain(map, cell, candidateOriginalTerrain);

                    if (!HasSimplePersistentTerrainState(map, cell, candidateOriginalTerrain))
                    {
                        report.Fail(
                            "Probe fixture",
                            "A vanilla-smoothing-ineligible candidate cell could not be restored to its original " +
                            "terrain after evaluating Designator_SmoothFloors.");
                        return false;
                    }

                    continue;
                }

                result = cell;
                originalTerrain = candidateOriginalTerrain;
                return true;
            }

            report.Skip(
                "Probe fixture",
                "No safe empty persistent terrain cell could be converted temporarily to VolcanicRock and accepted " +
                "by both the currently active Lava Flow predicate and vanilla Designator_SmoothFloors. The probe " +
                "did not modify production behavior.");
            return false;
        }

        private static bool IsSafeVolcanicProbeFixtureCell(
            Map map,
            IntVec3 cell,
            TerrainDef volcanicRock,
            TerrainDef smoothVolcanicRock,
            out TerrainDef originalTerrain)
        {
            originalTerrain = null;

            if (map == null || !cell.IsValid || !cell.InBounds(map) || cell.Fogged(map))
            {
                return false;
            }

            TerrainDef currentTerrain = map.terrainGrid.TerrainAt(cell);

            if (currentTerrain == null || ReferenceEquals(currentTerrain, volcanicRock) ||
                ReferenceEquals(currentTerrain, smoothVolcanicRock))
            {
                return false;
            }

            if (!currentTerrain.natural || currentTerrain.temporary || currentTerrain.isFoundation ||
                currentTerrain.layerable || currentTerrain.dangerous || currentTerrain.IsWater ||
                currentTerrain.passability == Traversability.Impassable)
            {
                return false;
            }

            if (!ReferenceEquals(map.terrainGrid.TopTerrainAt(cell), currentTerrain) ||
                map.terrainGrid.UnderTerrainAt(cell) != null || map.terrainGrid.TempTerrainAt(cell) != null ||
                map.terrainGrid.FoundationAt(cell) != null || map.terrainGrid.ColorAt(cell) != null)
            {
                return false;
            }

            if (cell.GetThingList(map).Count != 0 || CellHasAnyDesignation(map, cell))
            {
                return false;
            }

            originalTerrain = currentTerrain;
            return true;
        }

        private static bool HasSimplePersistentTerrainState(Map map, IntVec3 cell, TerrainDef expectedTerrain)
        {
            if (map == null || expectedTerrain == null || !cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            return ReferenceEquals(map.terrainGrid.TerrainAt(cell), expectedTerrain) &&
                   ReferenceEquals(map.terrainGrid.TopTerrainAt(cell), expectedTerrain) &&
                   map.terrainGrid.UnderTerrainAt(cell) == null && map.terrainGrid.TempTerrainAt(cell) == null &&
                   map.terrainGrid.FoundationAt(cell) == null && map.terrainGrid.ColorAt(cell) == null;
        }

        private static bool TryInvokeLavaFlowPotentialSpread(
            GameCondition_LavaFlow lavaFlow,
            IntVec3 cell,
            out bool result)
        {
            result = false;

            if (lavaFlow == null || _lavaFlowCanPotentiallySpreadIntoMethod == null)
            {
                return false;
            }

            try
            {
                object value = _lavaFlowCanPotentiallySpreadIntoMethod.Invoke(
                    lavaFlow,
                    new object[]
                    {
                        cell
                    });

                if (!(value is bool boolValue))
                {
                    return false;
                }

                result = boolValue;
                return true;
            }
            catch (TargetInvocationException exception)
            {
                Log.Error(
                    LogPrefix + " Failed to invoke GameCondition_LavaFlow.CanLavaPotentiallySpreadInto: " +
                    (exception.InnerException ?? exception));
                return false;
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Failed to invoke GameCondition_LavaFlow.CanLavaPotentiallySpreadInto: " + exception);
                return false;
            }
        }

        private static bool TryResolveActiveLavaFlowCondition(Map map, out GameCondition_LavaFlow lavaFlow)
        {
            lavaFlow = null;

            if (map?.GameConditionManager == null)
            {
                return false;
            }

            object manager = map.GameConditionManager;

            for (Type type = manager.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                for (var i = 0; i < fields.Length; i++)
                {
                    object value;

                    try
                    {
                        value = fields[i].GetValue(manager);
                    }
                    catch
                    {
                        continue;
                    }

                    if (value is GameCondition_LavaFlow directCondition)
                    {
                        lavaFlow = directCondition;
                        return true;
                    }

                    if (!(value is IEnumerable enumerable) || value is string)
                    {
                        continue;
                    }

                    try
                    {
                        foreach (object item in enumerable)
                        {
                            if (item is GameCondition_LavaFlow condition)
                            {
                                lavaFlow = condition;
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore unrelated manager collections that cannot be enumerated safely here.
                    }
                }
            }

            return false;
        }

        private static string FormatVolcanicTerrain(TerrainDef terrain)
        {
            return terrain?.defName ?? "<null>";
        }
    }
}
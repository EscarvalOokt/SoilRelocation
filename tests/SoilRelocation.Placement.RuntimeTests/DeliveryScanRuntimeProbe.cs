using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SoilRelocation.Placement.RuntimeTests
{
    public static partial class PlacementWorkRuntimeProbe
    {
        private static void ProbeDeliveryScan(ProbeContext context, ProbeReport report)
        {
            const string scenario = "Delivery / one resource search per family and scan";
            FamilyCase family = ResolveFamilyCase("Soil");
            if (family == null || !family.TryResolve(out ThingDef orderDef, out ThingDef resourceDef))
            {
                report.Fail(scenario, "Soil order or resource Def is missing.");
                return;
            }

            Pawn pawn = context.Pawn;
            if (!TryFindClearRockCells(context.Map, pawn.Position, 4, 30, 2, out List<IntVec3> cells))
            {
                report.Skip(scenario, "Requires two natural-rock cells.");
                return;
            }

            var tracked = new List<Thing>();
            List<ResourceForbiddenState> states = CaptureAndForbidWorldResources(context.Map, resourceDef, pawn);
            try
            {
                RelocationMaterialOrder first = SpawnOrder(orderDef, cells[0], context.Map, tracked);
                RelocationMaterialOrder second = SpawnOrder(orderDef, cells[1], context.Map, tracked);
                Thing resource = SpawnResource(resourceDef, pawn.Position, context.Map, tracked);
                resource.SetForbidden(false);
                WorkGiver_DeliverRelocationMaterial worker = context.DeliveryWorker;
                if (!worker.HasJobOnThing(pawn, first) || !worker.HasJobOnThing(pawn, second))
                {
                    report.Skip(scenario, "Both orders must be reachable for normal hauling.");
                    return;
                }

                foreach (bool initiallyAvailable in new[] { true, false })
                {
                    resource.SetForbidden(!initiallyAvailable);
                    var visited = 0;
                    var scanMatches = true;
                    var forcedMatches = false;
                    var jobMatches = false;
                    GenClosest.ClosestThingReachable(
                        pawn.Position,
                        context.Map,
                        worker.PotentialWorkThingRequest,
                        worker.PathEndMode,
                        TraverseParms.For(pawn, worker.MaxPathDanger(pawn)),
                        9999f,
                        ValidateCandidate,
                        worker.PotentialWorkThingsGlobal(pawn),
                        forceAllowGlobalSearch: true);

                    if (visited != 2 ||
                        !scanMatches ||
                        !forcedMatches ||
                        !jobMatches ||
                        worker.HasJobOnThing(pawn, first) == initiallyAvailable)
                    {
                        report.Fail(
                            scenario,
                            $"Initially available={initiallyAvailable}, visited={visited}, " +
                            $"scan={scanMatches}, forced={forcedMatches}, final job={jobMatches}.");
                        return;
                    }

                    bool ValidateCandidate(Thing candidate)
                    {
                        if (candidate != first && candidate != second)
                        {
                            return false;
                        }

                        visited++;
                        scanMatches &= worker.HasJobOnThing(pawn, candidate) == initiallyAvailable;
                        if (visited == 1)
                        {
                            // Change the world between candidates so a repeated search would give the opposite answer.
                            resource.SetForbidden(initiallyAvailable);
                        }
                        else
                        {
                            forcedMatches = worker.HasJobOnThing(pawn, candidate, forced: true) != initiallyAvailable;
                            Job job = worker.JobOnThing(pawn, candidate);
                            jobMatches = initiallyAvailable
                                ? job == null
                                : IsExpectedDeliveryJob(job, resource, (RelocationMaterialOrder)candidate);
                        }

                        return false; // Let the game's global scanner visit both orders.
                    }
                }

                report.Pass(
                    scenario,
                    "Both positive and negative searches were reused within the vanilla scan. " +
                    "Forced checks, final job creation and the next query saw the new resource state.");
            }
            finally
            {
                RestoreResourceStates(states);
                DestroyTracked(tracked);
            }
        }
    }
}
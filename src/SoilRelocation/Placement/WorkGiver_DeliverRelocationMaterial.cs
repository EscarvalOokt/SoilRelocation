using System.Collections.Generic;
using RimWorld;
using SoilRelocation.Domain;
using SoilRelocation.Relocation;
using Verse;
using Verse.AI;

namespace SoilRelocation.Placement
{
    // ReSharper disable once InconsistentNaming
    public sealed class WorkGiver_DeliverRelocationMaterial : WorkGiver_Scanner
    {
        private (Pawn Pawn, Thing Order, Dictionary<ThingDef, Thing> Resources) _scanCandidate;

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn?.Map == null || !RelocationMaterialOrderUtility.HasAnyBuiltInMaterialOrder(pawn.Map);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                yield break;
            }

            // The vanilla scanner calls HasJobOnThing while enumerating these candidates.
            var resources = new Dictionary<ThingDef, Thing>();
            try
            {
                foreach (Thing order in RelocationMaterialOrderUtility.GetBuiltInMaterialOrders(pawn.Map))
                {
                    _scanCandidate = (pawn, order, resources);
                    yield return order;
                }
            }
            finally
            {
                if (ReferenceEquals(_scanCandidate.Resources, resources))
                {
                    _scanCandidate = default;
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            bool scanningCandidate = !forced && _scanCandidate.Pawn == pawn && _scanCandidate.Order == t;
            Dictionary<ThingDef, Thing> resources = scanningCandidate ? _scanCandidate.Resources : null;
            return FindDeliveryResource(pawn, t, forced, resources) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Thing resource = FindDeliveryResource(pawn, t, forced);
            return resource == null ? null : MakeDeliveryJob(resource, (RelocationMaterialOrder)t);
        }

        private static Thing FindDeliveryResource(
            Pawn pawn,
            Thing t,
            bool forced,
            Dictionary<ThingDef, Thing> scanResources = null)
        {
            if (pawn?.Map == null ||
                !(t is RelocationMaterialOrder order) ||
                !order.Spawned ||
                order.Map != pawn.Map ||
                !RelocationMaterialOrderUtility.IsBuiltInMaterialOrder(order.def) ||
                !RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family))
            {
                return null;
            }

            PlacementValidationResult validation = PlacementValidator.Validate(order.Map, order.Position, family);

            if (!validation.IsValid)
            {
                if (validation.IsPermanentFailure)
                {
                    if (scanResources != null && order.IsReadyForWork)
                    {
                        // Cancelling this order can put a previously unavailable resource back on the map.
                        scanResources.Remove(family.CanonicalPortableResource);
                    }

                    order.TryCancel();
                    return null;
                }

                if (validation.Status == PlacementValidationStatus.PlantPrerequisite && !order.IsForbidden(pawn))
                {
                    RelocationPlantPrerequisite.TryEnsureRemoval(validation.Plant);
                }

                return null;
            }

            if (order.IsForbidden(pawn))
            {
                return null;
            }

            ThingDef resourceDef = family.CanonicalPortableResource;

            if (order.GetSpaceRemainingWithEnroute(resourceDef, pawn) <= 0)
            {
                return null;
            }

            Danger danger = forced ? Danger.Deadly : pawn.NormalMaxDanger();

            if (!pawn.CanReach(order, PathEndMode.Touch, danger))
            {
                return null;
            }

            if (scanResources != null && scanResources.TryGetValue(resourceDef, out Thing cachedResource))
            {
                return cachedResource;
            }

            Thing resource = FindResource(pawn, resourceDef, forced);
            scanResources?.Add(resourceDef, resource);
            return resource;
        }

        private static Thing FindResource(Pawn pawn, ThingDef resourceDef, bool forced)
        {
            if (CanUseCarriedResource(pawn, resourceDef))
            {
                return pawn.carryTracker.CarriedThing;
            }

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(resourceDef),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, forced ? Danger.Deadly : pawn.NormalMaxDanger()),
                9999f,
                thing => IsUsableResource(pawn, thing, resourceDef, forced));
        }

        private static bool CanUseCarriedResource(Pawn pawn, ThingDef resourceDef)
        {
            Thing carriedThing = pawn.carryTracker?.CarriedThing;

            if (carriedThing?.def != resourceDef)
            {
                return false;
            }

            if (!KeyBindingDefOf.QueueOrder.IsDownEvent)
            {
                return true;
            }

            if (pawn.CurJob != null && !IsCompatibleQueuedJob(pawn.CurJob, carriedThing))
            {
                return false;
            }

            foreach (QueuedJob queuedJob in pawn.jobs.jobQueue)
            {
                if (!IsCompatibleQueuedJob(queuedJob.job, carriedThing))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCompatibleQueuedJob(Job job, Thing carriedThing)
        {
            return job.def != JobDefOf.HaulToContainer || job.targetA != carriedThing;
        }

        private static bool IsUsableResource(Pawn pawn, Thing thing, ThingDef resourceDef, bool forced)
        {
            return thing != null &&
                   thing.def == resourceDef &&
                   !thing.IsForbidden(pawn) &&
                   HaulAIUtility.PawnCanAutomaticallyHaulFast_NewTemp(pawn, thing, forced, checkReachability: false);
        }

        private static Job MakeDeliveryJob(Thing resource, RelocationMaterialOrder order)
        {
            Job job = JobMaker.MakeJob(JobDefOf.HaulToContainer);

            job.targetA = resource;
            job.targetB = order;
            job.targetC = order;
            job.count = 1;
            job.haulMode = HaulMode.ToContainer;

            return job;
        }
    }
}
using System.Collections.Generic;
using RimWorld;
using SoilRelocation.Domain;
using SoilRelocation.Relocation;
using SoilRelocation.Settings;
using SoilRelocation.Work;
using Verse;
using Verse.AI;

namespace SoilRelocation.Placement
{
    public sealed class WorkGiver_PlaceRelocatableTerrain : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

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

            foreach (Thing order in RelocationMaterialOrderUtility.GetBuiltInMaterialOrders(pawn.Map))
            {
                yield return order;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return TryPrepareJob(pawn, t, forced, out _);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return TryPrepareJob(pawn, t, forced, out Job job) ? job : null;
        }

        private static bool TryPrepareJob(Pawn pawn, Thing thing, bool forced, out Job job)
        {
            job = null;

            if (pawn?.Map == null || !(thing is RelocationMaterialOrder order) || !order.Spawned || order.Destroyed ||
                order.Map != pawn.Map || !RelocationMaterialOrderUtility.IsBuiltInMaterialOrder(order.def) ||
                !RelocationMaterialOrderUtility.TryResolveFamily(order.def, out RelocationFamilyDef family) ||
                !order.IsReadyForWork)
            {
                return false;
            }

            PlacementValidationResult validation = PlacementValidator.Validate(order.Map, order.Position, family);

            if (!validation.IsValid)
            {
                HandleBlockedOrder(pawn, order, validation);
                return false;
            }

            if (order.IsForbidden(pawn))
            {
                return false;
            }

            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            RelocationSkillSelection skillSelection =
                settings?.PlacementSkills ?? SoilRelocationSettings.DefaultPlacementSkills;

            if (!RelocationWorkUtility.CanPerform(pawn, skillSelection))
            {
                return false;
            }

            Danger danger = forced ? Danger.Deadly : pawn.NormalMaxDanger();

            if (!pawn.CanReach(order, PathEndMode.Touch, danger) || !pawn.CanReserve(order, 1, -1, null, forced))
            {
                return false;
            }

            job = JobMaker.MakeJob(SoilRelocationPlacementJobDefOf.SoilRelocation_PlaceTerrain, order);

            return true;
        }

        private static void HandleBlockedOrder(
            Pawn pawn,
            RelocationMaterialOrder order,
            PlacementValidationResult validation)
        {
            if (validation.IsPermanentFailure)
            {
                order.TryCancel();
                return;
            }

            if (validation.Status == PlacementValidationStatus.PlantPrerequisite && !order.IsForbidden(pawn))
            {
                RelocationPlantPrerequisite.TryEnsureRemoval(validation.Plant);
            }
        }
    }
}
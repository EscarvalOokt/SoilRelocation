using System.Collections.Generic;
using RimWorld;
using SoilRelocation.Relocation;
using SoilRelocation.Settings;
using SoilRelocation.Work;
using Verse;
using Verse.AI;

namespace SoilRelocation.Digging
{
    // ReSharper disable once ClassNeverInstantiated.Global
    // ReSharper disable once InconsistentNaming
    public sealed class WorkGiver_DigRelocatableTerrain : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            ThingDef orderDef = SoilRelocationDigOrderDefOf.SoilRelocation_DigOrder;

            return pawn?.Map == null || orderDef == null || pawn.Map.listerThings.ThingsOfDef(orderDef).Count == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            ThingDef orderDef = SoilRelocationDigOrderDefOf.SoilRelocation_DigOrder;

            if (pawn?.Map == null || orderDef == null)
            {
                yield break;
            }

            List<Thing> orders = pawn.Map.listerThings.ThingsOfDef(orderDef);

            for (var i = 0; i < orders.Count; i++)
            {
                yield return orders[i];
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

            if (pawn?.Map == null ||
                !(thing is DigRelocationOrder order) ||
                !order.Spawned ||
                order.Destroyed ||
                order.Map != pawn.Map ||
                !ReferenceEquals(order.def, SoilRelocationDigOrderDefOf.SoilRelocation_DigOrder))
            {
                return false;
            }

            DiggingValidationResult validation = DiggingValidator.Validate(order.Map, order.Position);

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
            RelocationSkillSelection skillSelection = settings?.DiggingSkills ??
                                                      SoilRelocationSettings.DefaultDiggingSkills;

            if (!RelocationWorkUtility.CanPerform(pawn, skillSelection))
            {
                return false;
            }

            Danger danger = forced ? Danger.Deadly : pawn.NormalMaxDanger();

            if (!pawn.CanReach(order, PathEndMode.Touch, danger) ||
                !pawn.CanReserve(order.Position, 1, -1, ReservationLayerDefOf.Floor, forced))
            {
                return false;
            }

            job = JobMaker.MakeJob(SoilRelocationJobDefOf.SoilRelocation_DigTerrain, order);
            return true;
        }

        private static void HandleBlockedOrder(Pawn pawn, DigRelocationOrder order, DiggingValidationResult validation)
        {
            if (validation.IsPermanentFailure)
            {
                order.TryCancel();
                return;
            }

            if (validation.Status == DiggingValidationStatus.PlantPrerequisite && !order.IsForbidden(pawn))
            {
                RelocationPlantPrerequisite.TryEnsureRemoval(validation.Plant);
            }
        }
    }
}
using System.Collections.Generic;
using RimWorld;
using SoilRelocation.Relocation;
using SoilRelocation.Settings;
using SoilRelocation.Work;
using Verse;
using Verse.AI;

namespace SoilRelocation.Digging
{
    public sealed class JobDriver_DigRelocatableTerrain : JobDriver
    {
        private float _workLeft;
        private float _totalWork;
        private RelocationSkillSelection _skillSelection;
        private float _rawXpPerTick = SoilRelocationSettings.DefaultRawXpPerTick;

        private DigRelocationOrder Order => job.targetA.Thing as DigRelocationOrder;

        private float WorkProgress => _totalWork <= 0f ? 0f : 1f - _workLeft / _totalWork;

        public override void Notify_Starting()
        {
            base.Notify_Starting();

            SoilRelocationSettings settings = LoadedModManager.GetMod<SoilRelocationMod>()?.Settings;

            _totalWork = SoilRelocationSettings.NormalizeWorkAmount(
                settings?.DigWorkAmount ?? SoilRelocationSettings.DefaultDigWorkAmount);
            _workLeft = _totalWork;

            _skillSelection = RelocationWorkUtility.Normalize(
                settings?.DiggingSkills ?? SoilRelocationSettings.DefaultDiggingSkills);

            _rawXpPerTick = SoilRelocationSettings.NormalizeRawXpPerTick(
                settings?.RawXpPerTick ?? SoilRelocationSettings.DefaultRawXpPerTick);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _workLeft, "workLeft");
            Scribe_Values.Look(ref _totalWork, "totalWork");
            Scribe_Values.Look(ref _skillSelection, "skillSelection");
            Scribe_Values.Look(
                ref _rawXpPerTick,
                "rawXpPerTick",
                SoilRelocationSettings.DefaultRawXpPerTick,
                forceSave: true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _totalWork <= 0f)
            {
                _totalWork = _workLeft > 0f ? _workLeft : 1f;
            }

            _skillSelection = RelocationWorkUtility.Normalize(_skillSelection);
            _rawXpPerTick = SoilRelocationSettings.NormalizeRawXpPerTick(_rawXpPerTick);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            DigRelocationOrder order = Order;

            return order != null && !order.Destroyed && order.Spawned && order.Map == pawn.Map && pawn.Reserve(
                order.Position,
                job,
                1,
                -1,
                ReservationLayerDefOf.Floor,
                errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return MakeStartValidationToil();
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return MakeWorkToil();
            yield return MakeFinishToil();
        }

        private Toil MakeStartValidationToil()
        {
            Toil toil = ToilMaker.MakeToil();

            toil.initAction = delegate
            {
                if (!TryValidateOrEndJob(out _))
                {
                    return;
                }

                if (!RelocationWorkUtility.CanPerform(pawn, _skillSelection))
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private Toil MakeWorkToil()
        {
            Toil toil = ToilMaker.MakeToil();

            toil.tickIntervalAction = delegate(int delta)
            {
                if (!TryValidateOrEndJob(out _))
                {
                    return;
                }

                if (!RelocationWorkUtility.CanPerform(pawn, _skillSelection))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                float workSpeed = RelocationWorkUtility.GetWorkSpeed(pawn, _skillSelection);
                RelocationWorkUtility.Learn(pawn, _skillSelection, _rawXpPerTick, delta);
                _workLeft -= workSpeed * delta;

                if (_workLeft <= 0f)
                {
                    ReadyForNextToil();
                }
            };

            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithProgressBar(TargetIndex.A, () => WorkProgress);

            return toil;
        }

        private Toil MakeFinishToil()
        {
            Toil toil = ToilMaker.MakeToil();

            toil.initAction = delegate
            {
                if (!TryValidateOrEndJob(out DiggingValidationResult validation))
                {
                    return;
                }

                DigRelocationOrder order = Order;

                if (order == null || order.Destroyed || !order.Spawned || order.Map != pawn.Map)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                Map map = order.Map;
                IntVec3 cell = order.Position;

                if (!DigResultResolver.TryPrepare(map, cell, validation, out DigResultPreparation preparation) ||
                    !DigResultApplier.TryApply(map, cell, preparation))
                {
                    order.TryCancel();
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                order.TryCancel();
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private bool TryValidateOrEndJob(out DiggingValidationResult validation)
        {
            validation = default(DiggingValidationResult);

            DigRelocationOrder order = Order;

            if (order == null || order.Destroyed || !order.Spawned || order.Map == null || order.Map != pawn.Map ||
                !ReferenceEquals(order.def, SoilRelocationDigOrderDefOf.SoilRelocation_DigOrder))
            {
                EndJobWith(JobCondition.Incompletable);
                return false;
            }

            validation = DiggingValidator.Validate(order.Map, order.Position);

            if (validation.IsValid)
            {
                return true;
            }

            if (validation.IsPermanentFailure)
            {
                order.TryCancel();
            }
            else if (validation.Status == DiggingValidationStatus.PlantPrerequisite)
            {
                RelocationPlantPrerequisite.TryEnsureRemoval(validation.Plant);
            }

            EndJobWith(JobCondition.Incompletable);
            return false;
        }
    }
}
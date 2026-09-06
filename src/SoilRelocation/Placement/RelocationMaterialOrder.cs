using System.Collections.Generic;
using RimWorld;
using SoilRelocation.Domain;
using SoilRelocation.Relocation;
using Verse;

namespace SoilRelocation.Placement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class RelocationMaterialOrder : RelocationOrder, IThingHolder, IHaulEnroute
    {
        private ThingOwner<Thing> _resourceContainer;

        public RelocationMaterialOrder()
        {
            _resourceContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
        }

        internal bool IsReadyForWork => TryGetContainedMaterial(out _);

        public ThingOwner GetDirectlyHeldThings()
        {
            return _resourceContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref _resourceContainer, "resourceContainer", this);

            if (_resourceContainer == null)
            {
                _resourceContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
            }
        }

        public int SpaceRemainingFor(ThingDef stuff)
        {
            if (stuff == null ||
                !RelocationMaterialOrderUtility.TryResolveFamily(def, out RelocationFamilyDef family) ||
                !ReferenceEquals(stuff, family.CanonicalPortableResource))
            {
                return 0;
            }

            if (IsReadyForWork)
            {
                return 0;
            }

            return _resourceContainer == null || _resourceContainer.Count == 0 ? 1 : 0;
        }

        internal bool TryGetContainedMaterial(out Thing material)
        {
            material = null;

            if (_resourceContainer == null || _resourceContainer.Count != 1 ||
                !RelocationMaterialOrderUtility.TryResolveFamily(def, out RelocationFamilyDef family))
            {
                return false;
            }

            Thing candidate = _resourceContainer[0];

            if (candidate == null || !ReferenceEquals(candidate.def, family.CanonicalPortableResource) ||
                candidate.stackCount != 1)
            {
                return false;
            }

            material = candidate;
            return true;
        }

        internal bool TryConsumeMaterial()
        {
            if (!TryGetContainedMaterial(out Thing material))
            {
                return false;
            }

            material.Destroy();

            return !IsReadyForWork && (_resourceContainer?.Count ?? 0) == 0;
        }

        protected override bool TryCancelCore()
        {
            if (Destroyed || !Spawned || Map == null)
            {
                return false;
            }

            Map map = Map;
            IntVec3 position = Position;

            map.enrouteManager.InterruptEnroutePawns(this, null);

            while (_resourceContainer != null && _resourceContainer.Count > 0)
            {
                Thing contained = _resourceContainer[0];

                if (!_resourceContainer.TryDrop(contained, position, map, ThingPlaceMode.Near, out _))
                {
                    return false;
                }
            }

            Destroy();
            return Destroyed;
        }
    }
}
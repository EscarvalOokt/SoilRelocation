using System.Collections.Generic;
using Verse;

namespace SoilRelocation.Substrate
{
    internal sealed class NaturalRockReconstructionProfile : IExposable
    {
        internal const int CurrentVersion = 1;

        private int _version = CurrentVersion;
        private List<NaturalRockSlot> _slots = new List<NaturalRockSlot>();

        public NaturalRockReconstructionProfile()
        {
        }

        internal NaturalRockReconstructionProfile(int version, IEnumerable<string> rockDefNames)
        {
            _version = version;
            _slots = new List<NaturalRockSlot>();

            if (rockDefNames == null)
            {
                return;
            }

            foreach (string rockDefName in rockDefNames)
            {
                _slots.Add(new NaturalRockSlot(rockDefName));
            }
        }

        internal int Version => _version;

        internal int SlotCount => _slots?.Count ?? 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref _version, "version");
            Scribe_Collections.Look(ref _slots, "slots", LookMode.Deep);
        }

        internal string GetRockDefName(int index)
        {
            return _slots[index].RockDefName;
        }

        internal void ReplaceRockDefName(int index, string rockDefName)
        {
            _slots[index] = new NaturalRockSlot(rockDefName);
        }

        internal bool IsStructurallyValid()
        {
            if (_version != CurrentVersion || _slots == null || _slots.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                NaturalRockSlot slot = _slots[i];

                if (slot == null || string.IsNullOrWhiteSpace(slot.RockDefName))
                {
                    return false;
                }
            }

            return true;
        }

        internal NaturalRockReconstructionProfile Copy()
        {
            var copy = new NaturalRockReconstructionProfile
            {
                _version = _version,
                _slots = new List<NaturalRockSlot>()
            };

            if (_slots == null)
            {
                copy._slots = null;
                return copy;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                copy._slots.Add(_slots[i]?.Copy());
            }

            return copy;
        }

        internal bool IsEquivalentTo(NaturalRockReconstructionProfile other)
        {
            if (other == null || _version != other._version)
            {
                return false;
            }

            if (_slots == null || other._slots == null)
            {
                return _slots == other._slots;
            }

            if (_slots.Count != other._slots.Count)
            {
                return false;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                string left = _slots[i]?.RockDefName;
                string right = other._slots[i]?.RockDefName;

                if (left != right)
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool TryCreate(IEnumerable<ThingDef> orderedRocks, out NaturalRockReconstructionProfile profile)
        {
            profile = null;

            if (orderedRocks == null)
            {
                return false;
            }

            var rockDefNames = new List<string>();

            foreach (ThingDef rock in orderedRocks)
            {
                if (rock == null || string.IsNullOrWhiteSpace(rock.defName))
                {
                    return false;
                }

                rockDefNames.Add(rock.defName);
            }

            var candidate = new NaturalRockReconstructionProfile(CurrentVersion, rockDefNames);

            if (!candidate.IsStructurallyValid())
            {
                return false;
            }

            profile = candidate;
            return true;
        }
    }
}
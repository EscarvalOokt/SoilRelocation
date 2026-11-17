using System.Collections.Generic;
using Verse;

namespace SoilRelocation.Substrate
{
    internal sealed class NaturalRockReconstructionProfile : IExposable
    {
        internal const int CurrentVersion = 1;
        private List<NaturalRockSlot> _slots = [];

        private int _version = CurrentVersion;

        public NaturalRockReconstructionProfile()
        {
        }

        internal NaturalRockReconstructionProfile(int version, IEnumerable<string> rockDefNames)
        {
            _version = version;
            _slots = [];

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
                _slots = []
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

        internal static bool TryCreate(
            IReadOnlyList<ThingDef> orderedRocks,
            IReadOnlyList<ThingDef> fallbackRocks,
            out NaturalRockReconstructionProfile profile,
            out bool replacedSlots,
            out bool usedFallback)
        {
            profile = null;
            replacedSlots = false;
            usedFallback = false;

            if (orderedRocks == null || orderedRocks.Count == 0)
            {
                return false;
            }

            var slotNames = new string[orderedRocks.Count];
            var usableNames = new List<string>();
            for (var i = 0; i < orderedRocks.Count; i++)
            {
                ThingDef rock = orderedRocks[i];
                if (CanReconstruct(rock))
                {
                    slotNames[i] = rock.defName;
                    usableNames.Add(rock.defName);
                }
            }

            if (usableNames.Count == 0 && fallbackRocks != null)
            {
                foreach (ThingDef rock in fallbackRocks)
                {
                    if (CanReconstruct(rock))
                    {
                        usableNames.Add(rock.defName);
                    }
                }

                usedFallback = usableNames.Count > 0;
            }

            if (usableNames.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < slotNames.Length; i++)
            {
                if (slotNames[i] == null)
                {
                    // Noise seeds depend on slot indices. Replace an unusable slot in place.
                    slotNames[i] = usableNames[i % usableNames.Count];
                    replacedSlots = true;
                }
            }

            profile = new NaturalRockReconstructionProfile(CurrentVersion, slotNames);
            return true;
        }

        private static bool CanReconstruct(ThingDef rock)
        {
            return rock?.building?.naturalTerrain is { temporary: false, isFoundation: false } &&
                   !string.IsNullOrWhiteSpace(rock.defName);
        }
    }
}
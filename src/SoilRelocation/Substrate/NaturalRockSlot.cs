using Verse;

namespace SoilRelocation.Substrate
{
    internal sealed class NaturalRockSlot : IExposable
    {
        private string _rockDefName;

        public NaturalRockSlot()
        {
        }

        internal NaturalRockSlot(string rockDefName)
        {
            _rockDefName = rockDefName;
        }

        internal string RockDefName => _rockDefName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref _rockDefName, "rockDefName");
        }

        internal NaturalRockSlot Copy()
        {
            return new NaturalRockSlot(_rockDefName);
        }
    }
}
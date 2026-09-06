using Verse;

namespace SoilRelocation.Relocation
{
    public abstract class RelocationOrder : ThingWithComps
    {
        internal bool TryCancel()
        {
            return TryCancelCore();
        }

        protected abstract bool TryCancelCore();
    }
}
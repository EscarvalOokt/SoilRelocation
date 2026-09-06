using SoilRelocation.Relocation;

namespace SoilRelocation.Digging
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class DigRelocationOrder : RelocationOrder
    {
        protected override bool TryCancelCore()
        {
            if (Destroyed || !Spawned || Map == null)
            {
                return false;
            }

            Destroy();
            return Destroyed;
        }
    }
}
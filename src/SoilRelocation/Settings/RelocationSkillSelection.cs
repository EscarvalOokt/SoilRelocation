using System;

namespace SoilRelocation.Settings
{
    [Flags]
    internal enum RelocationSkillSelection
    {
        None = 0,
        Mining = 1,
        Construction = 2,
        Plants = 4,
        All = Mining | Construction | Plants
    }
}
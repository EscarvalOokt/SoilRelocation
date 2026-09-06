namespace SoilRelocation.Domain
{
    internal enum RelocationRuleStatus
    {
        UnsupportedNoMembership = 0,
        Supported,
        UnsupportedDriesTo,
        UnsupportedExplicitExclusion,
        UnsupportedAmbiguousMembership,
        UnsupportedInvalidMembership,
        UnsupportedInvalidFamily
    }
}
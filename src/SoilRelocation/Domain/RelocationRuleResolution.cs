namespace SoilRelocation.Domain
{
    internal readonly struct RelocationRuleResolution
    {
        private RelocationRuleResolution(RelocationRuleStatus status, RelocationFamilyDef family)
        {
            Status = status;
            Family = family;
        }

        public RelocationRuleStatus Status { get; }

        public RelocationFamilyDef Family { get; }

        public bool IsSupported => Status == RelocationRuleStatus.Supported;

        internal static RelocationRuleResolution Supported(RelocationFamilyDef family)
        {
            return new RelocationRuleResolution(RelocationRuleStatus.Supported, family);
        }

        internal static RelocationRuleResolution Unsupported(RelocationRuleStatus status)
        {
            return new RelocationRuleResolution(status, null);
        }
    }
}
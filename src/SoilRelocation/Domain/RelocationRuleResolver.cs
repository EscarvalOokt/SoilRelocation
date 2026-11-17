using System;
using System.Collections.Generic;
using Verse;

namespace SoilRelocation.Domain
{
    internal static class RelocationRuleResolver
    {
        internal static RelocationRuleResolution Resolve(TerrainDef terrain)
        {
            if (terrain == null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }

            if (terrain.driesTo != null)
            {
                return RelocationRuleResolution.Unsupported(RelocationRuleStatus.UnsupportedDriesTo);
            }

            if (terrain.temporary || terrain.isFoundation)
            {
                return RelocationRuleResolution.Unsupported(RelocationRuleStatus.UnsupportedTerrainLayer);
            }

            int extensionCount = FindMembershipExtensions(terrain, out RelocationTerrainExtension extension);

            if (extensionCount == 0)
            {
                return RelocationRuleResolution.Unsupported(RelocationRuleStatus.UnsupportedNoMembership);
            }

            if (extensionCount > 1)
            {
                return RelocationRuleResolution.Unsupported(RelocationRuleStatus.UnsupportedAmbiguousMembership);
            }

            if (!extension.Enabled)
            {
                return RelocationRuleResolution.Unsupported(RelocationRuleStatus.UnsupportedExplicitExclusion);
            }

            if (extension.Family == null)
            {
                return RelocationRuleResolution.Unsupported(RelocationRuleStatus.UnsupportedInvalidMembership);
            }

            if (!IsFamilyValid(extension.Family))
            {
                return RelocationRuleResolution.Unsupported(RelocationRuleStatus.UnsupportedInvalidFamily);
            }

            return RelocationRuleResolution.Supported(extension.Family);
        }

        internal static int FindMembershipExtensions(TerrainDef terrain, out RelocationTerrainExtension firstExtension)
        {
            if (terrain == null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }

            firstExtension = null;
            var count = 0;

            List<DefModExtension> modExtensions = terrain.modExtensions;

            if (modExtensions == null)
            {
                return 0;
            }

            for (var i = 0; i < modExtensions.Count; i++)
            {
                if (!(modExtensions[i] is RelocationTerrainExtension extension))
                {
                    continue;
                }

                count++;

                firstExtension ??= extension;
            }

            return count;
        }

        internal static bool IsFamilyValid(RelocationFamilyDef family)
        {
            return family is { CanonicalTerrain: not null, CanonicalPortableResource: not null };
        }
    }
}
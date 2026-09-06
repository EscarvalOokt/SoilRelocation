using System;
using Verse;
using Verse.Noise;

namespace SoilRelocation.Substrate
{
    internal static class NaturalRockResolver
    {
        private const double Frequency = 0.004999999888241291;
        private const double Lacunarity = 2.0;
        private const double Persistence = 0.5;
        private const int OctaveCount = 6;

        internal static bool TryResolve(
            Map map,
            IntVec3 cell,
            NaturalRockReconstructionProfile profile,
            out ThingDef resolvedRock)
        {
            resolvedRock = null;

            if (map == null || profile == null || !profile.IsStructurallyValid())
            {
                return false;
            }

            if (!TryGetMapGenerationSeed(map, out int mapGenerationSeed))
            {
                return false;
            }

            ThingDef bestRock = null;
            float bestValue = -999999f;

            for (var slotIndex = 0; slotIndex < profile.SlotCount; slotIndex++)
            {
                string rockDefName = profile.GetRockDefName(slotIndex);
                ThingDef rock = DefDatabase<ThingDef>.GetNamedSilentFail(rockDefName);

                if (rock == null)
                {
                    return false;
                }

                int noiseSeed = GetNoiseSeed(mapGenerationSeed, slotIndex);

                var noise = new Perlin(Frequency, Lacunarity, Persistence, OctaveCount, noiseSeed, QualityMode.Medium);

                var value = (float)noise.GetValue(cell.x, cell.y, cell.z);

                if (value > bestValue)
                {
                    bestValue = value;
                    bestRock = rock;
                }
            }

            if (bestRock == null)
            {
                return false;
            }

            resolvedRock = bestRock;
            return true;
        }

        private static bool TryGetMapGenerationSeed(Map map, out int mapGenerationSeed)
        {
            mapGenerationSeed = 0;

            if (Find.World?.info == null)
            {
                return false;
            }

            if (map.IsPocketMap)
            {
                if (map.Parent == null)
                {
                    return false;
                }

                mapGenerationSeed = Gen.HashCombineInt(Find.World.info.Seed, map.Parent.ID);

                return true;
            }

            mapGenerationSeed = Gen.HashCombineInt(Find.World.info.Seed, map.Parent?.Tile.GetHashCode() ?? 0);

            return true;
        }

        private static int GetNoiseSeed(int mapGenerationSeed, int slotIndex)
        {
            return Math.Abs(MurmurHash.GetInt((uint)mapGenerationSeed, (uint)slotIndex) % int.MaxValue);
        }
    }
}
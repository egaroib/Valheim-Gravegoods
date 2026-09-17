using System;

namespace Gravegoods.Loot
{
    /// <summary>
    /// Progression rungs, in order. The numeric value is the ordering: "one tier behind"
    /// is simply one less, and a pool that is empty at one rung falls back to the rung
    /// below it, never above.
    /// </summary>
    internal enum Tier
    {
        Meadows = 0,
        BlackForest = 1,
        Swamp = 2,
        Mountain = 3,
        Plains = 4,
        Mistlands = 5,
        Ashlands = 6,
        DeepNorth = 7,
    }

    internal static class Tiers
    {
        internal const int Count = 8;

        /// <summary>
        /// The rung a chest belongs to, from the biome under it. Ocean and None have no
        /// rung, so chests there get no bonus roll at all.
        /// </summary>
        internal static bool TryFromBiome(Heightmap.Biome biome, out Tier tier)
        {
            switch (biome)
            {
                case Heightmap.Biome.Meadows: tier = Tier.Meadows; return true;
                case Heightmap.Biome.BlackForest: tier = Tier.BlackForest; return true;
                case Heightmap.Biome.Swamp: tier = Tier.Swamp; return true;
                case Heightmap.Biome.Mountain: tier = Tier.Mountain; return true;
                case Heightmap.Biome.Plains: tier = Tier.Plains; return true;
                case Heightmap.Biome.Mistlands: tier = Tier.Mistlands; return true;
                case Heightmap.Biome.AshLands: tier = Tier.Ashlands; return true;
                case Heightmap.Biome.DeepNorth: tier = Tier.DeepNorth; return true;
                default: tier = Tier.Meadows; return false;
            }
        }

        internal static bool TryParse(string text, out Tier tier)
        {
            tier = Tier.Meadows;
            if (string.IsNullOrEmpty(text)) return false;

            // Accept "Black Forest", "blackforest", "Ashlands" and the game's "AshLands".
            var key = text.Replace(" ", "").Replace("_", "").Trim();
            foreach (Tier t in Enum.GetValues(typeof(Tier)))
            {
                if (string.Equals(t.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    tier = t;
                    return true;
                }
            }
            return false;
        }

        internal static Tier Previous(Tier tier) =>
            tier == Tier.Meadows ? Tier.Meadows : (Tier)((int)tier - 1);
    }
}

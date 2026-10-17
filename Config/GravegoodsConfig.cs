using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Jotunn.Managers;

namespace Gravegoods.Config
{
    /// <summary>
    /// Every tunable value.
    ///
    /// Everything that changes what a chest can contain is admin-only: the owner of a
    /// chest's ZDO rolls its loot, and that can be any connected client, so the server has
    /// to dictate the odds or two players exploring side by side would find different
    /// loot. Only the local log toggle is left to each player.
    /// </summary>
    internal static class GravegoodsConfig
    {
        internal static ConfigEntry<bool> VerboseLogging;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> ExcludedContainers;

        internal static ConfigEntry<float> ResourceChance;
        internal static ConfigEntry<float> AmmoChance;
        internal static ConfigEntry<float> GearChance;

        internal static ConfigEntry<float> GearSameTierChance;
        internal static ConfigEntry<float> GearQuality2Chance;
        internal static ConfigEntry<float> GearQuality3Chance;
        internal static ConfigEntry<float> GearDurabilityMin;
        internal static ConfigEntry<float> GearDurabilityMax;
        internal static ConfigEntry<float> GearEnhancedChance;

        internal static ConfigEntry<float> AmmoSameTierChance;
        internal static ConfigEntry<int> AmmoStackMin;
        internal static ConfigEntry<int> AmmoStackMax;

        private static string[] _excludedCache = new string[0];
        private static string _excludedRaw;

        internal static void Bind(ConfigFile config)
        {
            var admin = new ConfigurationManagerAttributes { IsAdminOnly = true };

            VerboseLogging = config.Bind("General", "VerboseLogging", false,
                "Log every chest roll: biome, rung, and what was added. Local to this machine.");

            Enabled = config.Bind("General", "Enabled", true,
                new ConfigDescription("Master switch for bonus chest loot.", null, admin));

            ExcludedContainers = config.Bind("General", "ExcludedContainers", "MWL_portContainer",
                new ConfigDescription(
                    "Comma-separated prefab name fragments. A chest whose prefab name contains any " +
                    "of them never gets a bonus roll. The default skips More World Locations' " +
                    "shipment port containers.", null, admin));

            // Capped below 100% on purpose. Gravegoods makes exploring worthwhile; it is not meant
            // to turn every chest into a guaranteed payout, so no server can configure it into one.
            // BepInEx clamps an out-of-range value in the .cfg to these limits when it loads.
            ResourceChance = config.Bind("Chances", "ResourceChance", 0.08f,
                new ConfigDescription(
                    "Chance per chest to add one stack from the biome's resource list. Capped at 0.75.",
                    new AcceptableValueRange<float>(0f, 0.75f), admin));

            AmmoChance = config.Bind("Chances", "AmmoChance", 0.05f,
                new ConfigDescription(
                    "Chance per chest to add a stack of arrows or bolts. Capped at 0.45.",
                    new AcceptableValueRange<float>(0f, 0.45f), admin));

            GearChance = config.Bind("Chances", "GearChance", 0.02f,
                new ConfigDescription(
                    "Chance per chest to add one weapon or armor piece. Capped at 0.35.",
                    new AcceptableValueRange<float>(0f, 0.35f), admin));

            GearSameTierChance = config.Bind("Gear", "SameTierChance", 0.15f,
                new ConfigDescription(
                    "When gear drops, the chance it comes from the chest's own biome rung. Otherwise it " +
                    "comes from one rung behind. Meadows chests always use their own rung. Capped at 0.20.",
                    new AcceptableValueRange<float>(0f, 0.20f), admin));

            GearQuality2Chance = config.Bind("Gear", "Quality2Chance", 0.17f,
                new ConfigDescription(
                    "Chance that gear from one rung behind arrives upgraded once (quality 2). " +
                    "Same-rung gear is always quality 1. Capped at 0.25.",
                    new AcceptableValueRange<float>(0f, 0.25f), admin));

            GearQuality3Chance = config.Bind("Gear", "Quality3Chance", 0.03f,
                new ConfigDescription(
                    "Chance that gear from one rung behind arrives upgraded twice (quality 3). " +
                    "Checked before Quality2Chance; capped at the item's max quality. Capped at 0.15.",
                    new AcceptableValueRange<float>(0f, 0.15f), admin));

            // Deliberately low, and capped so it stays that way. Found gear is a stopgap for the
            // trip, not a replacement for a repaired kit: it should need a workbench before it can
            // carry a whole expedition. 0 is allowed and means the item arrives broken.
            GearDurabilityMin = config.Bind("Gear", "DurabilityMin", 0.05f,
                new ConfigDescription(
                    "Lowest durability found gear can have, as a fraction of max. Each item rolls " +
                    "uniformly at random between DurabilityMin and DurabilityMax. 0 means broken " +
                    "until repaired. Capped at 0.30.",
                    new AcceptableValueRange<float>(0f, 0.30f), admin));

            GearDurabilityMax = config.Bind("Gear", "DurabilityMax", 0.30f,
                new ConfigDescription(
                    "Highest durability found gear can have, as a fraction of max. Set equal to " +
                    "DurabilityMin for a fixed value. Capped at 0.30.",
                    new AcceptableValueRange<float>(0f, 0.30f), admin));

            // Elemental variants are a base weapon plus a gem or orb the player has not earned
            // yet, so they are a jackpot rather than a stopgap. Capped well below the point where
            // a chest would hand out variants as often as the weapons they are made from.
            GearEnhancedChance = config.Bind("Gear", "EnhancedChance", 0.05f,
                new ConfigDescription(
                    "When gear drops from a rung that has elemental variants (Ashlands, Deep North), " +
                    "the chance it is one of those instead of base gear. Variants are the items that " +
                    "need an [enhancers] gem or orb in loot.txt. They always arrive at quality 1. " +
                    "0 means they never drop. Capped at 0.25.",
                    new AcceptableValueRange<float>(0f, 0.25f), admin));

            AmmoSameTierChance = config.Bind("Ammo", "SameTierChance", 0.70f,
                new ConfigDescription(
                    "When ammo drops, the chance it comes from the chest's own biome rung. Otherwise it " +
                    "comes from one rung behind. Capped at 0.75.",
                    new AcceptableValueRange<float>(0f, 0.75f), admin));

            // Small handfuls, not quivers: enough to finish a fight on the way, not to stock up.
            // The two ranges do not overlap, so StackMax can never fall below StackMin.
            AmmoStackMin = config.Bind("Ammo", "StackMin", 3,
                new ConfigDescription(
                    "Smallest ammo stack. Each drop rolls uniformly between StackMin and StackMax. Range 1-3.",
                    new AcceptableValueRange<int>(1, 3), admin));

            AmmoStackMax = config.Bind("Ammo", "StackMax", 8,
                new ConfigDescription("Largest ammo stack. Range 5-15.",
                    new AcceptableValueRange<int>(5, 15), admin));
        }

        internal static bool IsExcludedContainer(string prefabName)
        {
            var raw = ExcludedContainers.Value ?? "";
            if (!ReferenceEquals(raw, _excludedRaw))
            {
                _excludedCache = raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                _excludedRaw = raw;
            }

            foreach (var fragment in _excludedCache)
            {
                if (prefabName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}

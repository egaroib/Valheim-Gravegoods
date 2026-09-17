using System.Collections.Generic;
using Gravegoods.Config;
using UnityEngine;

namespace Gravegoods.Loot
{
    /// <summary>What one chest's bonus roll produced.</summary>
    internal sealed class RollResult
    {
        internal readonly List<ItemDrop.ItemData> Items = new List<ItemDrop.ItemData>();

        /// <summary>One human-readable line per item, for the verbose log and /gravegoods sim.</summary>
        internal readonly List<string> Notes = new List<string>();

        /// <summary>Set for a hit that could not be delivered, e.g. an empty pool.</summary>
        internal readonly List<string> Misses = new List<string>();
    }

    /// <summary>
    /// The dice. Pure with respect to the world: it builds item data and returns it, and
    /// never touches a container, so the sim command can call it a thousand times safely.
    /// </summary>
    internal static class LootRoller
    {
        internal static RollResult Roll(Tier chestTier)
        {
            var result = new RollResult();
            var pools = LootPools.Get();
            if (pools == null)
            {
                result.Misses.Add("ObjectDB not ready");
                return result;
            }

            if (Random.value < GravegoodsConfig.ResourceChance.Value)
            {
                RollResource(chestTier, result);
            }
            if (Random.value < GravegoodsConfig.AmmoChance.Value)
            {
                RollAmmo(pools, chestTier, result);
            }
            if (Random.value < GravegoodsConfig.GearChance.Value)
            {
                RollGear(pools, chestTier, result);
            }
            return result;
        }

        private static void RollResource(Tier chestTier, RollResult result)
        {
            var entries = LootTables.Current.Resources[(int)chestTier];
            if (entries.Count == 0)
            {
                result.Misses.Add($"resource: no [resources {chestTier}] entries");
                return;
            }

            var total = 0f;
            foreach (var e in entries) total += e.Weight;
            var pick = Random.Range(0f, total);
            var chosen = entries[entries.Count - 1];
            foreach (var e in entries)
            {
                pick -= e.Weight;
                if (pick <= 0f)
                {
                    chosen = e;
                    break;
                }
            }

            var prefab = ObjectDB.instance.GetItemPrefab(chosen.Prefab);
            if (prefab == null || prefab.GetComponent<ItemDrop>() == null)
            {
                result.Misses.Add($"resource: '{chosen.Prefab}' is not an item");
                return;
            }

            var item = Make(prefab, Random.Range(chosen.Min, chosen.Max + 1), 1, 1f);
            result.Items.Add(item);
            result.Notes.Add($"resource {chosen.Prefab} x{item.m_stack}");
        }

        private static void RollAmmo(LootPools pools, Tier chestTier, RollResult result)
        {
            var wanted = Random.value < GravegoodsConfig.AmmoSameTierChance.Value
                ? chestTier
                : Tiers.Previous(chestTier);

            var pool = pools.AtOrBelow(PoolKind.Ammo, wanted, out var actual);
            if (pool == null)
            {
                result.Misses.Add($"ammo: no pool at or below {wanted}");
                return;
            }

            var pick = pool[Random.Range(0, pool.Count)];
            var min = GravegoodsConfig.AmmoStackMin.Value;
            var max = Mathf.Max(min, GravegoodsConfig.AmmoStackMax.Value);
            var item = Make(pick.Go, Random.Range(min, max + 1), 1, 1f);
            result.Items.Add(item);
            result.Notes.Add($"ammo {pick.Prefab} x{item.m_stack} (rung {actual})");
        }

        private static void RollGear(LootPools pools, Tier chestTier, RollResult result)
        {
            var sameTier = chestTier == Tier.Meadows ||
                           Random.value < GravegoodsConfig.GearSameTierChance.Value;
            var wanted = sameTier ? chestTier : Tiers.Previous(chestTier);

            var pool = pools.AtOrBelow(PoolKind.Gear, wanted, out var actual);
            if (pool == null)
            {
                result.Misses.Add($"gear: no pool at or below {wanted}");
                return;
            }

            // Upgrades are the reward for finding older gear, so the chest's own rung
            // never comes pre-upgraded: that would put it past what the biome can craft.
            var quality = 1;
            if (!sameTier)
            {
                var q = Random.value;
                if (q < GravegoodsConfig.GearQuality3Chance.Value) quality = 3;
                else if (q < GravegoodsConfig.GearQuality3Chance.Value + GravegoodsConfig.GearQuality2Chance.Value) quality = 2;
            }

            var lo = GravegoodsConfig.GearDurabilityMin.Value;
            var hi = Mathf.Max(lo, GravegoodsConfig.GearDurabilityMax.Value);
            var pick = pool[Random.Range(0, pool.Count)];
            var item = Make(pick.Go, 1, quality, Random.Range(lo, hi));
            result.Items.Add(item);
            result.Notes.Add(
                $"gear {pick.Prefab} q{item.m_quality} " +
                $"{Mathf.RoundToInt(item.m_durability / Mathf.Max(1f, item.GetMaxDurability()) * 100f)}% " +
                $"(rung {actual}{(sameTier ? ", same as chest" : "")})");
        }

        /// <summary>
        /// Builds item data the same way DropTable.AddItemToList does for vanilla chest
        /// loot, so bonus items are indistinguishable from native ones: no crafter, no
        /// custom data, not picked up.
        /// </summary>
        private static ItemDrop.ItemData Make(GameObject prefab, int stack, int quality, float durabilityFraction)
        {
            var source = prefab.GetComponent<ItemDrop>().m_itemData;
            var item = source.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = Mathf.Clamp(stack, 1, Mathf.Max(1, source.m_shared.m_maxStackSize));
            item.m_quality = Mathf.Clamp(quality, 1, Mathf.Max(1, source.m_shared.m_maxQuality));
            item.m_worldLevel = Game.m_worldLevel;

            if (item.m_shared.m_useDurability)
            {
                item.m_durability = item.GetMaxDurability(item.m_quality) * Mathf.Clamp01(durabilityFraction);
            }
            return item;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Gravegoods.Compat;
using Gravegoods.Config;

namespace Gravegoods.Loot
{
    /// <summary>
    /// Applies bonus rolls to real chests, and guarantees each chest rolls at most once.
    ///
    /// A chest is marked in its ZDO before any item is added, so an exception partway
    /// through can cost that chest its bonus but can never make it roll twice.
    /// </summary>
    internal static class ChestLoot
    {
        internal static readonly int RolledKey = "gravegoods.rolled".GetStableHashCode();

        /// <summary>Comma-separated prefab names this mod added, for RegeneratingWorld compat.</summary>
        internal static readonly int BonusKey = "gravegoods.bonus".GetStableHashCode();

        /// <summary>
        /// World key that lets chests generated before the mod was installed roll once when
        /// first opened. A global key rather than a config value because it belongs to the
        /// world, not the server: turning it on for one world should not affect another.
        /// </summary>
        internal const string LegacyGlobalKey = "gravegoods_legacy";

        /// <summary>
        /// Chests that loaded on a client before the server's loot tables arrived. Rolled
        /// when the tables land, if this client still owns them.
        /// </summary>
        private static readonly List<Container> Pending = new List<Container>();

        internal static bool LegacyEnabled =>
            ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(LegacyGlobalKey);

        /// <summary>
        /// World chests only. Player-built chests have an empty default table, which also
        /// covers the moment a freshly placed chest runs Awake before its creator is set.
        /// </summary>
        internal static bool IsWorldChest(Container container)
        {
            if (container.m_defaultItems?.m_drops == null || container.m_defaultItems.m_drops.Count == 0) return false;
            if (container.m_piece != null && container.m_piece.IsPlacedByPlayer()) return false;
            return !GravegoodsConfig.IsExcludedContainer(Utils.GetPrefabName(container.gameObject));
        }

        /// <summary>Runs right after vanilla fills a chest for the first time.</summary>
        internal static void OnDefaultItemsAdded(Container container)
        {
            if (!GravegoodsConfig.Enabled.Value) return;
            var zdo = OwnedZdo(container);
            if (zdo == null || zdo.GetBool(RolledKey) || !IsWorldChest(container)) return;

            if (!LootTables.IsReady)
            {
                Pending.Add(container);
                GravegoodsPlugin.LogVerbose($"Deferred {container.name}: waiting for server loot tables.");
                return;
            }

            Roll(container, zdo, "new");
        }

        /// <summary>
        /// Runs on the chest's current owner when anyone asks to open it. Only acts on chests
        /// that were filled before this mod existed.
        /// </summary>
        internal static void OnOpenRequested(Container container)
        {
            if (!GravegoodsConfig.Enabled.Value || !LegacyEnabled || !LootTables.IsReady) return;
            var zdo = OwnedZdo(container);
            if (zdo == null || zdo.GetBool(RolledKey) || !zdo.GetBool(ZDOVars.s_addedDefaultItems)) return;
            if (!IsWorldChest(container)) return;

            Roll(container, zdo, "legacy");
        }

        internal static void ProcessPending()
        {
            var waiting = Pending.ToArray();
            Pending.Clear();
            foreach (var container in waiting)
            {
                if (container == null) continue;
                var zdo = OwnedZdo(container);
                if (zdo == null || zdo.GetBool(RolledKey)) continue;
                Roll(container, zdo, "deferred");
            }
        }

        internal static void ClearPending() => Pending.Clear();

        private static ZDO OwnedZdo(Container container)
        {
            var nview = container != null ? container.m_nview : null;
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return null;
            return nview.GetZDO();
        }

        private static void Roll(Container container, ZDO zdo, string why)
        {
            zdo.Set(RolledKey, true);

            var position = container.transform.position;
            var biome = WorldGenerator.instance != null
                ? WorldGenerator.instance.GetBiome(position)
                : Heightmap.Biome.None;

            if (!Tiers.TryFromBiome(biome, out var tier))
            {
                GravegoodsPlugin.LogVerbose($"{container.name} ({why}) in {biome}: no rung, no bonus roll.");
                return;
            }

            RollResult result;
            try
            {
                result = LootRoller.Roll(tier);
            }
            catch (Exception e)
            {
                GravegoodsPlugin.LogError($"Roll failed for {container.name}: {e}");
                return;
            }

            foreach (var miss in result.Misses)
            {
                GravegoodsPlugin.LogWarning($"{container.name} in {biome}: {miss}");
            }

            if (result.Items.Count == 0)
            {
                GravegoodsPlugin.LogVerbose($"{container.name} ({why}) in {biome}: nothing this time.");
                return;
            }

            // RegeneratingWorld flags a chest as player-touched when its contents grow after
            // the first fill. Our additions are not a player's, so undo that flag if it was
            // clear before we started.
            var depositBefore = RegeneratingWorldCompat.ReadDeposit(zdo);
            // IsCheated, not GetBool: since 1.0.20 the flag is a revision number and the
            // game ignores pre-revision values, which GetBool would still read as cheated.
            var cheated = zdo.IsCheated() && !PlayerProfile.s_bypassCheatChecks;
            var inventory = container.GetInventory();
            var added = new List<string>();

            foreach (var item in result.Items)
            {
                item.m_cheated = cheated;
                if (inventory.AddItem(item))
                {
                    added.Add(item.m_dropPrefab.name);
                }
                else
                {
                    GravegoodsPlugin.LogVerbose($"{container.name}: no room for {item.m_dropPrefab.name}.");
                }
            }

            if (added.Count > 0)
            {
                var previous = zdo.GetString(BonusKey, "");
                var names = previous.Length == 0 ? added : previous.Split(',').Concat(added).ToList();
                zdo.Set(BonusKey, string.Join(",", names.ToArray()));
            }

            RegeneratingWorldCompat.RestoreDeposit(zdo, depositBefore);

            GravegoodsPlugin.LogVerbose(
                $"{container.name} ({why}) in {biome}, rung {tier}: " + string.Join("; ", result.Notes.ToArray()));
        }
    }
}

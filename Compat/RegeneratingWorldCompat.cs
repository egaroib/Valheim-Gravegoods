using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using Gravegoods.Loot;
using HarmonyLib;
using UnityEngine;

namespace Gravegoods.Compat
{
    /// <summary>
    /// Keeps bonus loot from blocking RegeneratingWorld's location resets.
    ///
    /// RegeneratingWorld (jg224, checked against 0.5.1) regenerates a location only when
    /// its chests look untouched, which it decides in LocationChestProtection.IsSensitive:
    /// every item still in the chest must be one of the prefab's default drops, at default
    /// quality, with no player metadata. A bonus item fails that test, so an unlooted
    /// bonus would pin the whole location forever. This postfix re-runs the same test
    /// while also accepting the names this mod recorded in the chest's ZDO.
    ///
    /// Everything here is reflection-bound and soft: without RegeneratingWorld, or if its
    /// internals move, the patch is skipped with a warning and the mod works normally.
    /// </summary>
    internal static class RegeneratingWorldCompat
    {
        internal const string Guid = "jg224.RegeneratingWorld";
        private const string ProtectionType = "JG224.RegeneratingWorld.Runtime.LocationChestProtection";
        private const string DepositKey = "jg224.rw.chest.deposit.v1";

        private static bool _active;

        internal static void TryPatch(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.ContainsKey(Guid)) return;

            var type = AccessTools.TypeByName(ProtectionType);
            var target = type == null
                ? null
                : AccessTools.Method(type, "IsSensitive", new[] { typeof(ZDO), typeof(GameObject) });

            if (target == null)
            {
                GravegoodsPlugin.LogWarning(
                    "RegeneratingWorld is loaded but LocationChestProtection.IsSensitive was not found. " +
                    "Unlooted bonus items may stop locations from regenerating.");
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(RegeneratingWorldCompat), nameof(IsSensitivePostfix)));
            _active = true;
            GravegoodsPlugin.LogInfo("  bound  RegeneratingWorld LocationChestProtection.IsSensitive (compat)");
        }

        internal static bool ReadDeposit(ZDO zdo) => _active && zdo.GetBool(DepositKey);

        /// <summary>Clears the deposit flag if it was clear before this mod added items.</summary>
        internal static void RestoreDeposit(ZDO zdo, bool before)
        {
            if (_active && !before && zdo.GetBool(DepositKey))
            {
                zdo.Set(DepositKey, false);
            }
        }

        private static void IsSensitivePostfix(ZDO zdo, GameObject prefab, ref bool __result)
        {
            if (!__result || zdo == null || prefab == null) return;

            try
            {
                var bonus = zdo.GetString(ChestLoot.BonusKey, "");
                if (bonus.Length == 0 || zdo.GetBool(DepositKey)) return;

                if (OnlyNativeOrBonus(zdo, prefab, new HashSet<string>(bonus.Split(','), StringComparer.Ordinal)))
                {
                    __result = false;
                    GravegoodsPlugin.LogVerbose($"RegeneratingWorld: chest {prefab.name} holds only native and bonus loot.");
                }
            }
            catch (Exception e)
            {
                // Leave RegeneratingWorld's cautious answer in place.
                GravegoodsPlugin.LogVerbose($"RegeneratingWorld compat check failed: {e.Message}");
            }
        }

        private static bool OnlyNativeOrBonus(ZDO zdo, GameObject prefab, HashSet<string> bonus)
        {
            var container = prefab.GetComponent<Container>();
            var bytes = zdo.GetByteArray(ZDOVars.s_items);
            if (container == null || bytes == null) return false;

            var native = new Dictionary<string, ItemDrop.ItemData>(StringComparer.Ordinal);
            foreach (var drop in container.m_defaultItems.m_drops)
            {
                var itemDrop = drop.m_item != null ? drop.m_item.GetComponent<ItemDrop>() : null;
                if (itemDrop != null) native[drop.m_item.name] = itemDrop.m_itemData;
            }

            // Vanilla's own decoder, into a throwaway inventory large enough to hold anything.
            var inventory = new Inventory("gravegoods-probe", null, 32, 32);
            inventory.Load(new ZPackage(bytes));

            foreach (var item in inventory.GetAllItems())
            {
                if (item.m_crafterID != 0L || !string.IsNullOrEmpty(item.m_crafterName) ||
                    item.m_customData.Count > 0 || item.m_equipped || item.m_pickedUp)
                {
                    return false;
                }

                var name = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
                if (native.TryGetValue(name, out var def) &&
                    item.m_quality == def.m_quality && item.m_variant == def.m_variant)
                {
                    continue;
                }
                if (!bonus.Contains(name)) return false;
            }
            return true;
        }
    }
}

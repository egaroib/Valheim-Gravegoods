using System;
using Gravegoods.Loot;
using HarmonyLib;

namespace Gravegoods.Patches
{
    /// <summary>
    /// Container.Awake calls AddDefaultItems exactly once per chest, on the ZDO owner,
    /// guarded by ZDOVars.s_addedDefaultItems, and sets that flag straight after. A postfix
    /// here is therefore the one moment a world chest is first filled.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.AddDefaultItems))]
    internal static class AddDefaultItemsPatch
    {
        private static void Postfix(Container __instance)
        {
            try
            {
                ChestLoot.OnDefaultItemsAdded(__instance);
            }
            catch (Exception e)
            {
                // Awake is on the load path of every chest; never let a bonus roll break one.
                GravegoodsPlugin.LogError($"Bonus roll failed on {__instance?.name}: {e}");
            }
        }
    }

    /// <summary>
    /// RPC_RequestOpen runs on whichever peer currently owns the chest, before it force-sends
    /// the chest's ZDO to the requester. Rolling legacy loot here means the owner adds it
    /// and the opener receives it with the chest.
    ///
    /// The owner side rather than the opener side because the opener does not reliably own
    /// the chest yet when its response arrives, and with MultiUserChest (on the OneDotOh
    /// server) it never needs to. Priority.First so this runs ahead of MultiUserChest's own
    /// prefix, which replaces the original method.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.RPC_RequestOpen))]
    internal static class RequestOpenPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Container __instance, long playerID)
        {
            try
            {
                if (__instance.CheckAccess(playerID)) ChestLoot.OnOpenRequested(__instance);
            }
            catch (Exception e)
            {
                GravegoodsPlugin.LogError($"Legacy roll failed on {__instance?.name}: {e}");
            }
        }
    }

    /// <summary>
    /// ZNet.Awake starts every session, before any connection. Forgetting the previous
    /// server's tables here means a client can never roll with a stale copy.
    /// </summary>
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    internal static class SessionStartPatch
    {
        private static void Postfix()
        {
            ChestLoot.ClearPending();
            LootTables.BeginWaitingForServer();
        }
    }
}

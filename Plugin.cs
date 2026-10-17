using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Gravegoods.Commands;
using Gravegoods.Compat;
using Gravegoods.Config;
using Gravegoods.Loot;
using Gravegoods.Net;
using Jotunn.Managers;
using Jotunn.Utils;

namespace Gravegoods
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Soft: load after RegeneratingWorld when it is present, so its types exist to patch.
    [BepInDependency(RegeneratingWorldCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    // EveryoneMustHaveMod. Chest loot is rolled by whichever client owns the chest's ZDO
    // when it first loads, so every client needs the mod and the server's tables, and the
    // server needs it to hand those tables out and to answer RegeneratingWorld's checks.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class GravegoodsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.ragemedia.gravegoods";
        public const string PluginName = "Gravegoods";
        public const string PluginVersion = "0.2.0";

        internal static GravegoodsPlugin Instance;

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        private void Awake()
        {
            Instance = this;
            GravegoodsConfig.Bind(Config);
            LootTables.LoadLocal();
            LootTableSync.Register();

            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            VerifyPatches();
            RegeneratingWorldCompat.TryPatch(_harmony);

            // Registers both 'gravegoods' in the F5 console and '/gravegoods' in chat.
            CommandManager.Instance.AddConsoleCommand(new GravegoodsCommand());
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// Reports what Harmony actually bound, and errors loudly on any [HarmonyPatch]
        /// class whose target method could not be resolved.
        ///
        /// This exists because a patch aimed at a method that no longer exists compiles
        /// cleanly and then does nothing at runtime. Without this check, a broken mod and
        /// a working mod produce identical logs.
        /// </summary>
        private void VerifyPatches()
        {
            var bound = _harmony.GetPatchedMethods().ToList();
            LogInfo($"Harmony bound {bound.Count} target(s):");
            foreach (var m in bound)
            {
                LogInfo($"  bound  {m.DeclaringType?.FullName}.{m.Name}");
            }

            var unresolved = 0;
            var patchClasses = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Any());

            foreach (var type in patchClasses)
            {
                var attrs = type.GetCustomAttributes(typeof(HarmonyPatch), true)
                    .Cast<HarmonyPatch>()
                    .Select(a => a.info)
                    .ToList();

                HarmonyMethod merged;
                try
                {
                    merged = HarmonyMethod.Merge(attrs);
                }
                catch (Exception e)
                {
                    LogError($"  UNRESOLVED  {type.Name}: could not merge patch attributes: {e.Message}");
                    unresolved++;
                    continue;
                }

                // Patches targeting constructors, getters, or resolved via TargetMethod()
                // cannot be checked this way. Skip rather than report a false alarm.
                if (merged?.declaringType == null || string.IsNullOrEmpty(merged.methodName))
                {
                    continue;
                }

                var target = AccessTools.Method(
                    merged.declaringType, merged.methodName, merged.argumentTypes);

                if (target == null)
                {
                    unresolved++;
                    LogError($"  UNRESOLVED  {type.Name} -> " +
                             $"{merged.declaringType.Name}.{merged.methodName} does not exist. " +
                             "This patch will never run. The game version likely changed; " +
                             "re-check the method against the decompiled source.");
                }
            }

            if (unresolved > 0)
            {
                LogError($"{PluginName}: {unresolved} patch target(s) failed to resolve. " +
                         "This mod is loaded but not fully functional.");
            }
            else if (bound.Count == 0)
            {
                LogWarning($"{PluginName}: loaded, but bound zero patches. " +
                           "Expected if this mod only adds content via Jotunn.");
            }
            else
            {
                LogInfo($"{PluginName} v{PluginVersion} ready.");
            }
        }

        // Consistent prefix so log triage is a grep, not an inference.
        internal static void LogInfo(string msg) => Instance.Logger.LogInfo($"[{PluginName}] {msg}");
        internal static void LogWarning(string msg) => Instance.Logger.LogWarning($"[{PluginName}] {msg}");
        internal static void LogError(string msg) => Instance.Logger.LogError($"[{PluginName}] {msg}");

        internal static void LogVerbose(string msg)
        {
            if (GravegoodsConfig.VerboseLogging != null && GravegoodsConfig.VerboseLogging.Value)
            {
                Instance.Logger.LogInfo($"[{PluginName}] {msg}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Gravegoods.Config;
using Gravegoods.Diagnostics;
using Gravegoods.Loot;
using Jotunn.Entities;
using Jotunn.Managers;

namespace Gravegoods.Commands
{
    /// <summary>
    /// <c>gravegoods</c> in the F5 console, or <c>/gravegoods</c> in chat.
    ///
    ///   gravegoods                     status: table source, pool sizes, legacy switch
    ///   gravegoods sim &lt;biome&gt; [n]      roll n imaginary chests and tally the results
    ///   gravegoods report              rewrite report.md
    ///   gravegoods legacy on|off       (admin) let pre-install chests roll once when opened
    /// </summary>
    internal sealed class GravegoodsCommand : ConsoleCommand
    {
        public override string Name => "gravegoods";

        public override string Help => "Bonus chest loot: status | sim <biome> [n] | report | legacy on|off";

        /// <summary>
        /// Not a cheat: sim and report only read tables, and legacy checks admin rights itself,
        /// so it works on servers without devcommands.
        /// </summary>
        public override bool IsCheat => false;

        public override List<string> CommandOptionList() =>
            new List<string> { "sim", "report", "legacy" };

        public override void Run(string[] args, Terminal context)
        {
            if (context == null) return;
            try
            {
                var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";
                switch (sub)
                {
                    case "":
                    case "status":
                        Status(context);
                        break;
                    case "sim":
                        Sim(context, args);
                        break;
                    case "report":
                        Report(context);
                        break;
                    case "legacy":
                        Legacy(context, args);
                        break;
                    default:
                        context.AddString(Help);
                        break;
                }
            }
            catch (Exception e)
            {
                context.AddString("Gravegoods command failed; see the log.");
                GravegoodsPlugin.LogError($"gravegoods {string.Join(" ", args)} failed: {e}");
            }
        }

        private static void Status(Terminal context)
        {
            context.AddString($"Gravegoods v{GravegoodsPlugin.PluginVersion} — {(GravegoodsConfig.Enabled.Value ? "enabled" : "DISABLED")}");
            context.AddString($"Loot tables: {LootTables.SourceDescription}");
            context.AddString(
                $"Chances per chest: resource {GravegoodsConfig.ResourceChance.Value:P0}, " +
                $"ammo {GravegoodsConfig.AmmoChance.Value:P0}, gear {GravegoodsConfig.GearChance.Value:P0} " +
                $"({GravegoodsConfig.GearEnhancedChance.Value:P0} of gear from enhanced pools)");
            context.AddString($"Legacy rolls for old chests: {(ChestLoot.LegacyEnabled ? "ON" : "off")}");

            var pools = LootTables.IsReady ? LootPools.Get() : null;
            if (pools == null)
            {
                context.AddString("Pools not built yet (load into a world first).");
                return;
            }

            for (var t = 0; t < Tiers.Count; t++)
            {
                context.AddString(
                    $"  {(Tier)t,-12} resources {LootTables.Current.Resources[t].Count,3}, " +
                    $"ammo {pools.Ammo[t].Count,3}, gear {pools.Gear[t].Count,3}" +
                    (pools.GearEnhanced[t].Count > 0 ? $" + {pools.GearEnhanced[t].Count} enhanced" : ""));
            }
            if (pools.MissingNames.Count > 0)
            {
                context.AddString($"{pools.MissingNames.Count} unknown name(s) in loot.txt; see report.md.");
            }
        }

        private static void Sim(Terminal context, string[] args)
        {
            if (args.Length < 2 || !Tiers.TryParse(args[1], out var tier))
            {
                context.AddString("usage: gravegoods sim <Meadows|BlackForest|Swamp|Mountain|Plains|Mistlands|Ashlands|DeepNorth> [chests]");
                return;
            }
            if (!LootTables.IsReady || LootPools.Get() == null)
            {
                context.AddString("Load into a world first.");
                return;
            }

            var count = 1000;
            if (args.Length > 2 && (!int.TryParse(args[2], out count) || count < 1 || count > 100000))
            {
                context.AddString("chests must be between 1 and 100000");
                return;
            }

            var hits = 0;
            var enhanced = 0;
            var tally = new Dictionary<string, int>(StringComparer.Ordinal);
            var misses = new HashSet<string>();
            for (var i = 0; i < count; i++)
            {
                var result = LootRoller.Roll(tier);
                if (result.Items.Count > 0) hits++;
                foreach (var note in result.Notes)
                {
                    if (note.EndsWith(", enhanced)")) enhanced++;
                    // Collapse stack sizes and durability so identical items group together.
                    var key = string.Join(" ", note.Split(' ').Take(3).ToArray());
                    key = key.StartsWith("gear") ? key : string.Join(" ", note.Split(' ').Take(2).ToArray());
                    tally[key] = tally.TryGetValue(key, out var n) ? n + 1 : 1;
                }
                foreach (var miss in result.Misses) misses.Add(miss);
            }

            context.AddString($"{count} {tier} chests: {hits} got something ({(float)hits / count:P1}).");
            foreach (var kind in new[] { "resource", "ammo", "gear" })
            {
                var rows = tally.Where(kv => kv.Key.StartsWith(kind)).OrderByDescending(kv => kv.Value).ToList();
                context.AddString($"  {kind}: {rows.Sum(kv => kv.Value)} total");
                foreach (var kv in rows.Take(12))
                {
                    context.AddString($"    {kv.Value,5}  {kv.Key.Substring(kind.Length + 1)}");
                }
                if (rows.Count > 12) context.AddString($"    ... and {rows.Count - 12} more");
            }
            context.AddString($"  enhanced gear: {enhanced}");
            foreach (var miss in misses) context.AddString($"  miss: {miss}");
        }

        private static void Report(Terminal context)
        {
            var pools = LootTables.IsReady ? LootPools.Get() : null;
            if (pools == null)
            {
                context.AddString("Load into a world first.");
                return;
            }
            LootReport.Write(pools);
            context.AddString($"Wrote {LootReport.FilePath}");
        }

        private static void Legacy(Terminal context, string[] args)
        {
            if (ZoneSystem.instance == null)
            {
                context.AddString("Load into a world first.");
                return;
            }

            if (args.Length < 2)
            {
                context.AddString($"Legacy rolls for old chests: {(ChestLoot.LegacyEnabled ? "ON" : "off")}. " +
                                  "Use 'gravegoods legacy on' or 'off'.");
                return;
            }

            if (!SynchronizationManager.Instance.PlayerIsAdmin)
            {
                context.AddString("Only server admins can change legacy rolls.");
                return;
            }

            switch (args[1].ToLowerInvariant())
            {
                case "on":
                    ZoneSystem.instance.SetGlobalKey(ChestLoot.LegacyGlobalKey);
                    context.AddString("Legacy rolls ON for this world: chests from before Gravegoods roll once when first opened.");
                    GravegoodsPlugin.LogInfo("Legacy rolls turned on.");
                    break;
                case "off":
                    ZoneSystem.instance.RemoveGlobalKey(ChestLoot.LegacyGlobalKey);
                    context.AddString("Legacy rolls off. Chests that already rolled keep their loot.");
                    GravegoodsPlugin.LogInfo("Legacy rolls turned off.");
                    break;
                default:
                    context.AddString("usage: gravegoods legacy on|off");
                    break;
            }
        }
    }
}

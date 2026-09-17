using System;
using System.IO;
using System.Linq;
using System.Text;
using Gravegoods.Loot;

namespace Gravegoods.Diagnostics
{
    /// <summary>
    /// Writes report.md next to loot.txt every time the pools are rebuilt.
    ///
    /// Which rung an item lands at depends on recipes inside asset bundles and on other
    /// mods' configs, none of which is readable from source. The report is the only honest
    /// answer to "what can a Swamp chest give me", and the place a misspelled name in
    /// loot.txt shows up instead of silently never dropping.
    /// </summary>
    internal static class LootReport
    {
        internal static string FilePath => Path.Combine(LootTables.Directory, "report.md");

        internal static void Write(LootPools pools)
        {
            try
            {
                Directory.CreateDirectory(LootTables.Directory);
                File.WriteAllText(FilePath, Build(pools));
                GravegoodsPlugin.LogInfo($"Wrote {FilePath}.");
            }
            catch (Exception e)
            {
                GravegoodsPlugin.LogWarning($"Could not write report: {e.Message}");
            }
        }

        private static string Build(LootPools pools)
        {
            var file = LootTables.Current;
            var sb = new StringBuilder();
            sb.AppendLine("# Gravegoods loot report");
            sb.AppendLine();
            sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}  ");
            sb.AppendLine($"Gravegoods v{GravegoodsPlugin.PluginVersion}  ");
            sb.AppendLine($"Loot tables: {LootTables.SourceDescription}  ");
            sb.AppendLine($"{pools.ItemCount} item prefabs in ObjectDB.");
            sb.AppendLine();

            if (file.Warnings.Count > 0 || pools.MissingNames.Count > 0)
            {
                sb.AppendLine("## Problems in loot.txt");
                sb.AppendLine();
                foreach (var w in file.Warnings) sb.AppendLine($"- {w}");
                if (pools.MissingNames.Count > 0)
                {
                    sb.AppendLine("- Not items in this install (typo, or the mod that adds them is not loaded): " +
                                  string.Join(", ", pools.MissingNames.Select(n => $"`{n}`").ToArray()));
                }
                sb.AppendLine();
            }

            for (var t = 0; t < Tiers.Count; t++)
            {
                var tier = (Tier)t;
                sb.AppendLine($"## {tier}");
                sb.AppendLine();

                var resources = file.Resources[t];
                sb.AppendLine($"**Resources** ({resources.Count})");
                sb.AppendLine();
                if (resources.Count == 0)
                {
                    sb.AppendLine("_none_");
                }
                else
                {
                    var total = resources.Sum(r => r.Weight);
                    foreach (var r in resources)
                    {
                        sb.AppendLine($"- `{r.Prefab}` {r.Min}-{r.Max}, {r.Weight / total * 100f:0}% of resource rolls");
                    }
                }
                sb.AppendLine();

                AppendPool(sb, "Ammo", pools.Ammo[t]);
                AppendPool(sb, "Gear", pools.Gear[t]);
            }

            sb.AppendLine("## Left out of the pools");
            sb.AppendLine();
            sb.AppendLine("Gear and ammo that exist in this install but will never drop. " +
                          "\"No enabled crafting recipe\" covers trader stock, boss drops and disabled recipes; " +
                          "add them under [overrides] to include them anyway.");
            sb.AppendLine();
            foreach (var group in pools.Excluded.GroupBy(GroupName).OrderBy(g => g.Key))
            {
                sb.AppendLine($"**{group.Key}** ({group.Count()})");
                sb.AppendLine();
                foreach (var e in group.OrderBy(e => e.Prefab, StringComparer.Ordinal))
                {
                    var detail = GroupName(e) != e.Reason ? $" — {e.Reason}" : "";
                    sb.AppendLine($"- `{e.Prefab}` ({e.Kind.ToString().ToLowerInvariant()}){detail}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Ingredients with no known rung");
            sb.AppendLine();
            sb.AppendLine("Seen in recipes but not listed under [materials] and not craftable. Every recipe that " +
                          "uses one is unusable, so the items above marked \"unknown ingredient\" stay out of the " +
                          "pools until these are given a rung under [materials].");
            sb.AppendLine();
            sb.AppendLine(pools.UnknownMaterials.Count == 0
                ? "_none_"
                : string.Join(", ", pools.UnknownMaterials.Select(n => $"`{n}`").ToArray()));
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>Folds per-item reasons into one heading per kind of reason.</summary>
        private static string GroupName(Exclusion e)
        {
            if (e.Reason.StartsWith("needs boss")) return "needs a boss material";
            if (e.Reason.StartsWith("unknown ingredient")) return "has an ingredient with no known rung";
            return e.Reason;
        }

        private static void AppendPool(StringBuilder sb, string title, System.Collections.Generic.List<PoolItem> items)
        {
            sb.AppendLine($"**{title}** ({items.Count})");
            sb.AppendLine();
            if (items.Count == 0)
            {
                sb.AppendLine("_none_");
            }
            else
            {
                sb.AppendLine("| Prefab | Name | Placed by |");
                sb.AppendLine("|---|---|---|");
                foreach (var item in items)
                {
                    sb.AppendLine($"| `{item.Prefab}` | {DisplayName(item)} | {item.Because} |");
                }
            }
            sb.AppendLine();
        }

        private static string DisplayName(PoolItem item)
        {
            var token = item.Go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? "";
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}

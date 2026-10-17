using System;
using System.Collections.Generic;
using System.Globalization;

namespace Gravegoods.Loot
{
    /// <summary>One line of a biome's resource list: a prefab, a stack range, a weight.</summary>
    internal sealed class ResourceEntry
    {
        internal string Prefab;
        internal int Min;
        internal int Max;
        internal float Weight;
    }

    /// <summary>
    /// The parsed contents of loot.txt.
    ///
    /// A plain line format rather than YAML so the mod has no dependency beyond Jotunn,
    /// and so an admin editing it on a server box cannot break it with indentation. The
    /// parser never throws: anything it cannot read becomes a warning in the report and
    /// the log, and the rest of the file still applies.
    /// </summary>
    internal sealed class LootFile
    {
        /// <summary>Crafting material to the rung it belongs to.</summary>
        internal readonly Dictionary<string, Tier> Materials = new Dictionary<string, Tier>(StringComparer.Ordinal);

        /// <summary>Anything crafted from one of these never enters a pool.</summary>
        internal readonly HashSet<string> BossMaterials = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Items kept out of every pool regardless of how they classify.</summary>
        internal readonly HashSet<string> Never = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Items forced into a rung, bypassing recipe classification entirely.</summary>
        internal readonly Dictionary<string, Tier> Overrides = new Dictionary<string, Tier>(StringComparer.Ordinal);

        /// <summary>
        /// Gems and orbs that turn a base weapon into an elemental variant. Gear that cannot be
        /// crafted without one goes to the separate, much rarer enhanced pool.
        /// </summary>
        internal readonly HashSet<string> Enhancers = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>True when the file had no [enhancers] section and the shipped list was used.</summary>
        internal bool EnhancersFromDefault;

        internal readonly List<ResourceEntry>[] Resources = NewTierLists();

        internal readonly List<string> Warnings = new List<string>();

        private static List<ResourceEntry>[] NewTierLists()
        {
            var lists = new List<ResourceEntry>[Tiers.Count];
            for (var i = 0; i < lists.Length; i++) lists[i] = new List<ResourceEntry>();
            return lists;
        }

        internal static LootFile Parse(string text)
        {
            var file = new LootFile();
            if (string.IsNullOrEmpty(text))
            {
                file.Warnings.Add("loot.txt is empty; no pools will be built.");
                return file;
            }

            var section = "";
            var sawEnhancers = false;
            var resourceTier = Tier.Meadows;
            var lines = text.Replace("\r", "").Split('\n');

            for (var n = 0; n < lines.Length; n++)
            {
                var line = StripComment(lines[n]).Trim();
                if (line.Length == 0) continue;
                var where = $"loot.txt line {n + 1}";

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    if (section == "enhancers") sawEnhancers = true;
                    if (section.StartsWith("resources"))
                    {
                        var tierName = section.Substring("resources".Length).Trim();
                        if (!Tiers.TryParse(tierName, out resourceTier))
                        {
                            file.Warnings.Add($"{where}: unknown biome '{tierName}' in section header.");
                            section = "ignored";
                        }
                        else
                        {
                            section = "resources";
                        }
                    }
                    continue;
                }

                switch (section)
                {
                    case "materials":
                        ParseTierList(file, line, where);
                        break;
                    case "boss materials":
                        foreach (var name in SplitNames(line)) file.BossMaterials.Add(name);
                        break;
                    case "never":
                        foreach (var name in SplitNames(line)) file.Never.Add(name);
                        break;
                    case "enhancers":
                        foreach (var name in SplitNames(line)) file.Enhancers.Add(name);
                        break;
                    case "overrides":
                        ParseOverride(file, line, where);
                        break;
                    case "resources":
                        ParseResource(file, resourceTier, line, where);
                        break;
                    case "ignored":
                        break;
                    default:
                        file.Warnings.Add($"{where}: line outside any known section, ignored.");
                        break;
                }
            }

            // loot.txt is never rewritten once it exists, so files from before 0.2.0 lack this
            // section. Without it every elemental variant would sit in the plain pool, which is
            // the problem the section exists to fix, so fall back to the shipped list. An empty
            // [enhancers] section is respected: that is an admin opting out on purpose.
            if (!sawEnhancers && !ReferenceEquals(text, DefaultLootFile.Text))
            {
                file.Enhancers.UnionWith(Parse(DefaultLootFile.Text).Enhancers);
                file.EnhancersFromDefault = true;
            }

            return file;
        }

        private static string StripComment(string line)
        {
            var i = line.IndexOf('#');
            return i < 0 ? line : line.Substring(0, i);
        }

        private static IEnumerable<string> SplitNames(string text)
        {
            foreach (var part in text.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return part.Trim();
            }
        }

        /// <summary><c>Swamp: Iron, IronOre, ElderBark</c></summary>
        private static void ParseTierList(LootFile file, string line, string where)
        {
            var colon = line.IndexOf(':');
            if (colon < 0 || !Tiers.TryParse(line.Substring(0, colon), out var tier))
            {
                file.Warnings.Add($"{where}: expected 'Biome: Item, Item, ...'.");
                return;
            }

            foreach (var name in SplitNames(line.Substring(colon + 1)))
            {
                if (file.Materials.TryGetValue(name, out var existing) && existing != tier)
                {
                    file.Warnings.Add($"{where}: {name} is listed under both {existing} and {tier}; using {tier}.");
                }
                file.Materials[name] = tier;
            }
        }

        /// <summary><c>SwordIron: Swamp</c></summary>
        private static void ParseOverride(LootFile file, string line, string where)
        {
            var colon = line.IndexOf(':');
            if (colon < 0 || !Tiers.TryParse(line.Substring(colon + 1), out var tier))
            {
                file.Warnings.Add($"{where}: expected 'Item: Biome'.");
                return;
            }
            file.Overrides[line.Substring(0, colon).Trim()] = tier;
        }

        /// <summary><c>IronScrap 2-4</c>, <c>Coins 20-40 2</c>, <c>Ruby 1</c></summary>
        private static void ParseResource(LootFile file, Tier tier, string line, string where)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts.Length > 3)
            {
                file.Warnings.Add($"{where}: expected 'Item min-max [weight]'.");
                return;
            }

            int min, max;
            var dash = parts[1].IndexOf('-');
            if (dash < 0)
            {
                if (!int.TryParse(parts[1], out min))
                {
                    file.Warnings.Add($"{where}: '{parts[1]}' is not an amount.");
                    return;
                }
                max = min;
            }
            else if (!int.TryParse(parts[1].Substring(0, dash), out min) ||
                     !int.TryParse(parts[1].Substring(dash + 1), out max))
            {
                file.Warnings.Add($"{where}: '{parts[1]}' is not a range like 2-4.");
                return;
            }

            var weight = 1f;
            if (parts.Length == 3 &&
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out weight))
            {
                file.Warnings.Add($"{where}: '{parts[2]}' is not a weight.");
                return;
            }

            if (min < 1 || max < min || weight <= 0f)
            {
                file.Warnings.Add($"{where}: amounts must be at least 1, max >= min, and weight above 0.");
                return;
            }

            file.Resources[(int)tier].Add(new ResourceEntry
            {
                Prefab = parts[0],
                Min = min,
                Max = max,
                Weight = weight,
            });
        }
    }
}

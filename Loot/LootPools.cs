using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gravegoods.Loot
{
    internal enum PoolKind
    {
        Gear,
        Ammo,
    }

    /// <summary>An item that can drop, and the rung it was placed at.</summary>
    internal sealed class PoolItem
    {
        internal string Prefab;
        internal GameObject Go;
        internal Tier Tier;
        internal PoolKind Kind;

        /// <summary>Why it landed at this rung, for the report: "Iron (Swamp)".</summary>
        internal string Because;

        /// <summary>The [enhancers] material it cannot be made without, or null for plain gear.</summary>
        internal string Enhancer;
    }

    internal sealed class Exclusion
    {
        internal string Prefab;
        internal PoolKind Kind;
        internal string Reason;
    }

    /// <summary>
    /// Every gear and ammo pool, built from the live ObjectDB so that items added by other
    /// mods (ValheimArmory, cape mods, ...) classify exactly as they are configured on this
    /// install, including any recipe changes the server pushed.
    /// </summary>
    internal sealed class LootPools
    {
        internal readonly List<PoolItem>[] Gear = NewLists();

        /// <summary>
        /// Elemental variants, kept apart from <see cref="Gear"/> so they can be far rarer than
        /// the base weapon they are made from. Indexed by the same rung.
        /// </summary>
        internal readonly List<PoolItem>[] GearEnhanced = NewLists();
        internal readonly List<PoolItem>[] Ammo = NewLists();
        internal readonly List<Exclusion> Excluded = new List<Exclusion>();

        /// <summary>Ingredients that no [materials] line or recipe could place.</summary>
        internal readonly SortedSet<string> UnknownMaterials = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Names in loot.txt that are not items in this install.</summary>
        internal readonly SortedSet<string> MissingNames = new SortedSet<string>(StringComparer.Ordinal);

        internal int ItemCount;

        private static List<PoolItem>[] NewLists()
        {
            var lists = new List<PoolItem>[Tiers.Count];
            for (var i = 0; i < lists.Length; i++) lists[i] = new List<PoolItem>();
            return lists;
        }

        /// <summary>
        /// The pool at <paramref name="tier"/>, or the nearest non-empty one below it. Never
        /// reaches upward: an empty pool is a reason to give less, not more.
        /// </summary>
        internal List<PoolItem> AtOrBelow(PoolKind kind, Tier tier, out Tier actual)
        {
            var lists = kind == PoolKind.Gear ? Gear : Ammo;
            for (var t = (int)tier; t >= 0; t--)
            {
                if (lists[t].Count > 0)
                {
                    actual = (Tier)t;
                    return lists[t];
                }
            }
            actual = tier;
            return null;
        }

        // --- Cache --------------------------------------------------------------------

        private static LootPools _cached;
        private static int _cachedRevision = -1;
        private static ObjectDB _cachedDb;
        private static int _cachedRecipeCount;

        /// <summary>
        /// The pools for the current tables and ObjectDB, or null before ObjectDB exists.
        /// Rebuilt when loot.txt changes, when ObjectDB is replaced (world load), or when
        /// the recipe count changes (a content mod registering late).
        /// </summary>
        internal static LootPools Get()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0) return null;

            if (_cached == null || _cachedRevision != LootTables.Revision ||
                !ReferenceEquals(_cachedDb, db) || _cachedRecipeCount != db.m_recipes.Count)
            {
                _cached = Build(LootTables.Current, db);
                _cachedRevision = LootTables.Revision;
                _cachedDb = db;
                _cachedRecipeCount = db.m_recipes.Count;

                GravegoodsPlugin.LogInfo(
                    "Built loot pools. Gear per rung: " + Summary(_cached.Gear) +
                    ". Enhanced gear per rung: " + Summary(_cached.GearEnhanced) +
                    ". Ammo per rung: " + Summary(_cached.Ammo) + ".");
                if (_cached.MissingNames.Count > 0)
                {
                    GravegoodsPlugin.LogWarning(
                        $"{_cached.MissingNames.Count} name(s) in loot.txt are not items in this install: " +
                        string.Join(", ", _cached.MissingNames.ToArray()));
                }
                Diagnostics.LootReport.Write(_cached);
            }
            return _cached;
        }

        private static string Summary(List<PoolItem>[] lists)
        {
            var parts = new List<string>();
            for (var t = 0; t < lists.Length; t++)
            {
                parts.Add($"{(Tier)t} {lists[t].Count}");
            }
            return string.Join(", ", parts.ToArray());
        }

        // --- Build --------------------------------------------------------------------

        private static readonly HashSet<ItemDrop.ItemData.ItemType> GearTypes = new HashSet<ItemDrop.ItemData.ItemType>
        {
            ItemDrop.ItemData.ItemType.OneHandedWeapon,
            ItemDrop.ItemData.ItemType.TwoHandedWeapon,
            ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft,
            ItemDrop.ItemData.ItemType.Bow,
            ItemDrop.ItemData.ItemType.Shield,
            ItemDrop.ItemData.ItemType.Helmet,
            ItemDrop.ItemData.ItemType.Chest,
            ItemDrop.ItemData.ItemType.Legs,
            ItemDrop.ItemData.ItemType.Hands,
            ItemDrop.ItemData.ItemType.Shoulder,
        };

        internal static bool TryGetKind(ItemDrop.ItemData.SharedData shared, out PoolKind kind)
        {
            kind = PoolKind.Gear;
            if (GearTypes.Contains(shared.m_itemType))
            {
                // Stackable "weapons" are throwables such as bombs, not gear.
                return shared.m_maxStackSize <= 1;
            }

            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo)
            {
                var ammo = (shared.m_ammoType ?? "").ToLowerInvariant();
                kind = PoolKind.Ammo;
                return (ammo.Contains("arrow") || ammo.Contains("bolt")) && !ammo.Contains("turret");
            }
            return false;
        }

        private static LootPools Build(LootFile file, ObjectDB db)
        {
            var pools = new LootPools();
            var classifier = new Classifier(file, db);

            foreach (var go in db.m_items)
            {
                if (go == null) continue;
                var drop = go.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData?.m_shared == null) continue;
                pools.ItemCount++;

                var shared = drop.m_itemData.m_shared;
                if (!TryGetKind(shared, out var kind)) continue;

                var name = go.name;
                string reason = null;
                Tier tier = Tier.Meadows;
                string because = null;

                if (file.Never.Contains(name))
                {
                    reason = "listed in [never]";
                }
                else if (file.Overrides.TryGetValue(name, out tier))
                {
                    because = "[overrides]";
                }
                else if (!string.IsNullOrEmpty(shared.m_dlc))
                {
                    reason = "DLC item";
                }
                else if (!classifier.HasRecipe(name))
                {
                    reason = "no enabled crafting recipe";
                }
                else
                {
                    var gate = classifier.BossGate(name);
                    if (gate != null)
                    {
                        reason = $"needs boss material {gate}";
                    }
                    else if (!classifier.TryResolve(name, out tier, out because))
                    {
                        // On failure, "because" names the ingredient that could not be placed.
                        reason = $"unknown ingredient {because}";
                    }
                }

                if (reason != null)
                {
                    pools.Excluded.Add(new Exclusion { Prefab = name, Kind = kind, Reason = reason });
                    continue;
                }

                var entry = new PoolItem { Prefab = name, Go = go, Tier = tier, Kind = kind, Because = because };
                if (kind == PoolKind.Ammo)
                {
                    pools.Ammo[(int)tier].Add(entry);
                }
                else
                {
                    entry.Enhancer = classifier.Enhancer(name);
                    (entry.Enhancer != null ? pools.GearEnhanced : pools.Gear)[(int)tier].Add(entry);
                }
            }

            foreach (var name in classifier.Unknown) pools.UnknownMaterials.Add(name);

            // Names an admin typed that this install does not have. Materials are checked
            // too, but only when nothing in the install could ever reference them is that
            // worth flagging, so they are reported alongside rather than as errors.
            foreach (var name in file.Materials.Keys.Concat(file.BossMaterials).Concat(file.Never)
                         .Concat(file.Enhancers)
                         .Concat(file.Overrides.Keys)
                         .Concat(file.Resources.SelectMany(r => r).Select(r => r.Prefab)))
            {
                if (db.GetItemPrefab(name) == null) pools.MissingNames.Add(name);
            }

            foreach (var list in pools.Gear.Concat(pools.GearEnhanced).Concat(pools.Ammo))
            {
                list.Sort((a, b) => string.CompareOrdinal(a.Prefab, b.Prefab));
            }
            return pools;
        }

        /// <summary>
        /// Places an item at a rung by walking its recipes down to known materials.
        ///
        /// An item's rung is the highest rung among its ingredients. With several recipes,
        /// the cheapest (lowest) rung wins. Ingredients that are not listed as materials
        /// but are themselves craftable (Bronze, nails, a base weapon for an elemental
        /// variant) are resolved recursively.
        ///
        /// A recipe with any ingredient that cannot be placed is unusable, not merely
        /// under-counted. Skipping the unknown ingredient would rank the item by whatever
        /// else it needs, so a modded sword of Wood and an unlisted endgame bar would land
        /// in Meadows. Failing closed means an unfamiliar item only drops once an admin has
        /// given its materials a rung.
        /// </summary>
        private sealed class Classifier
        {
            private readonly LootFile _file;
            private readonly Dictionary<string, List<Recipe>> _recipes = new Dictionary<string, List<Recipe>>(StringComparer.Ordinal);
            private readonly Dictionary<string, Resolution> _memo = new Dictionary<string, Resolution>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _gateMemo = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly HashSet<string> _visiting = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _gateVisiting = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _enhancerMemo = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly HashSet<string> _enhancerVisiting = new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<string> Unknown = new HashSet<string>(StringComparer.Ordinal);

            private sealed class Resolution
            {
                internal bool Known;
                internal Tier Tier;

                /// <summary>When known, the deciding ingredient. When not, the one that blocked it.</summary>
                internal string Because;
            }

            internal Classifier(LootFile file, ObjectDB db)
            {
                _file = file;
                foreach (var recipe in db.m_recipes)
                {
                    if (recipe == null || !recipe.m_enabled || recipe.m_item == null) continue;
                    var name = recipe.m_item.gameObject.name;
                    if (!_recipes.TryGetValue(name, out var list))
                    {
                        _recipes[name] = list = new List<Recipe>();
                    }
                    list.Add(recipe);
                }
            }

            internal bool HasRecipe(string name) => _recipes.ContainsKey(name);

            /// <summary>
            /// The boss material an item cannot be made without, or null. An item is gated
            /// only if every one of its recipes is: one boss-free recipe is enough to craft
            /// it through normal progression.
            /// </summary>
            internal string BossGate(string name) =>
                Gate(name, _file.BossMaterials, _gateMemo, _gateVisiting, BossGate, skipBossRecipes: false);

            /// <summary>
            /// The [enhancers] gem or orb an item cannot be made without, or null. Same rule as
            /// <see cref="BossGate"/>: one recipe without an enhancer makes it plain gear. Only
            /// boss-free recipes count, matching how <see cref="TryResolve"/> picks the rung.
            /// Walking through crafted ingredients means a variant made from another variant is
            /// enhanced too, while a variant's base weapon is not.
            /// </summary>
            internal string Enhancer(string name) =>
                Gate(name, _file.Enhancers, _enhancerMemo, _enhancerVisiting, Enhancer, skipBossRecipes: true);

            private string Gate(string name, HashSet<string> materials, Dictionary<string, string> memo,
                                HashSet<string> visiting, Func<string, string> recurse, bool skipBossRecipes)
            {
                if (materials.Contains(name)) return name;
                if (memo.TryGetValue(name, out var cached)) return cached;
                if (!_recipes.TryGetValue(name, out var recipes) || !visiting.Add(name)) return null;

                string gate = null;
                foreach (var recipe in recipes)
                {
                    if (skipBossRecipes && RecipeGate(recipe, BossGate) != null) continue;
                    var recipeGate = RecipeGate(recipe, recurse);
                    if (recipeGate == null)
                    {
                        gate = null;
                        break;
                    }
                    gate = gate ?? recipeGate;
                }

                visiting.Remove(name);
                memo[name] = gate;
                return gate;
            }

            /// <summary>
            /// Whether a requirement is needed to craft the item at all. Valheim 2.0 put
            /// Refinement Forge upgrade tokens (Upgrader0Weapon ... Upgrader6Armor) into most
            /// weapon and armor recipes, flagged m_upgraderResource; they are only consumed
            /// when upgrading at that forge. Requirements with no base amount are likewise
            /// upgrade-only. Neither says anything about when a player can first craft it.
            /// </summary>
            private static bool NeededToCraft(Piece.Requirement req) =>
                req?.m_resItem != null && !req.m_upgraderResource && req.m_amount > 0;

            private static string RecipeGate(Recipe recipe, Func<string, string> gateOf)
            {
                foreach (var req in recipe.m_resources)
                {
                    if (!NeededToCraft(req)) continue;
                    var gate = gateOf(req.m_resItem.gameObject.name);
                    if (gate != null) return gate;
                }
                return null;
            }

            internal bool TryResolve(string name, out Tier tier, out string because)
            {
                var r = Resolve(name);
                tier = r.Tier;
                because = r.Because;
                return r.Known;
            }

            private Resolution Resolve(string name)
            {
                if (_file.Materials.TryGetValue(name, out var listed))
                {
                    return new Resolution { Known = true, Tier = listed, Because = $"{name} ({listed})" };
                }
                if (_memo.TryGetValue(name, out var memo)) return memo;

                var result = new Resolution { Because = name };
                if (!_recipes.TryGetValue(name, out var recipes))
                {
                    Unknown.Add(name);
                    return result;
                }

                // A cycle (A made from B made from A) resolves as unknown on the inner hop.
                if (!_visiting.Add(name)) return result;

                string blocker = null;
                foreach (var recipe in recipes)
                {
                    if (RecipeGate(recipe, BossGate) != null) continue;

                    var recipeResult = ResolveRecipe(recipe);
                    if (!recipeResult.Known)
                    {
                        blocker = blocker ?? recipeResult.Because;
                    }
                    else if (!result.Known || recipeResult.Tier < result.Tier)
                    {
                        result = recipeResult;
                    }
                }
                if (!result.Known && blocker != null) result.Because = blocker;

                _visiting.Remove(name);
                _memo[name] = result;
                return result;
            }

            private Resolution ResolveRecipe(Recipe recipe)
            {
                Resolution result = null;
                foreach (var req in recipe.m_resources)
                {
                    if (!NeededToCraft(req)) continue;
                    var ingredient = Resolve(req.m_resItem.gameObject.name);
                    if (!ingredient.Known)
                    {
                        return new Resolution { Because = ingredient.Because };
                    }
                    if (result == null || ingredient.Tier > result.Tier)
                    {
                        result = ingredient;
                    }
                }
                // A recipe with no ingredients at all gives nothing to rank it by.
                return result ?? new Resolution { Because = "(recipe has no ingredients)" };
            }
        }
    }
}

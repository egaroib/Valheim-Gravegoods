# Gravegoods

Valheim's world is full of crypts, ruins, towers and shipwrecks with chests inside, and nearly every one holds the same few arrows and feathers. Gravegoods gives those chests a small chance of holding something worth the detour: biome resources, a handful of arrows or bolts, or a battered weapon or armor piece left behind by whoever didn't make it home.

It is built not to skip progression. Gear usually comes from the biome you have already beaten, arrives badly worn, and never includes boss-gated items.

## Who needs to install this

**Every player and the server.** Whichever player first loads a chest rolls its loot, so everyone needs the mod. The server sends its loot tables and config to every client. Clients without the mod are refused.

## What can drop

Each world chest rolls three independent chances, once, the first time it loads. Player-built chests are never touched.

| Roll | Default | Gives |
|---|---|---|
| Resource | 8% | One stack from that biome's resource list |
| Ammo | 5% | 3–8 arrows or bolts, 70% from the chest's biome tier, otherwise one tier behind |
| Gear | 2% | One weapon or armor piece at 5–30% durability |

Gear comes from **one tier behind** the chest's biome 85% of the time. That older gear can arrive upgraded: 17% quality 2, 3% quality 3. The other 15% of the time it comes from the chest's own tier, always at quality 1. Meadows chests always use Meadows gear.

### Elemental variants are much rarer

Ashlands and Deep North weapons come in elemental variants, made by adding a gem or orb to the base weapon: Storm Fang and Blood Fang from Ash Fang, Bloodgold weapons' Blood-Lightning and Frost-Fire versions, ValheimArmory's blood, lightning and primal gear. The base weapons drop like any other gear. The variants sit in a separate **enhanced** pool, and only 5% of gear drops from those rungs come from it. Enhanced gear always arrives at quality 1.

With default settings that works out to about one variant per 6,700 Ashlands chests, and one Ashlands variant per 1,200 Deep North chests.

Found gear is badly worn on purpose. Its durability is random anywhere between 5% and 30%, so a lucky find helps you push on but needs a trip home to the workbench before it can carry a whole expedition.

Tiers run Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North. The biome is read from the chest's map position, so dungeon chests count too. Ocean chests get nothing.

### How gear and ammo are tiered

Nothing is hand-listed. Every craftable weapon, armor piece, arrow and bolt in the game, including other mods' items like ValheimArmory's, is placed at the tier of its highest-tier ingredient. A sword made from Iron and Ancient Bark is Swamp gear. Crafted ingredients like Bronze or nails resolve through their own recipes.

If any ingredient has no known tier (a material another mod added that isn't listed in `loot.txt` yet), the item is **left out** rather than guessed at. `report.md` names the ingredient; give it a tier under `[materials]` and those items join the pools.

Anything that needs a **boss trophy or boss drop** is left out: antler, Elder, Bonemass, Moder, Yagluth, Queen and Fader gear. So are DLC items and anything without an enabled recipe.

## Files

`BepInEx/config/Gravegoods/loot.txt` decides what can drop:

- which tier each crafting material belongs to
- the boss materials list
- a `[never]` list
- `[enhancers]`, the gems and orbs that make an item an elemental variant
- `[overrides]` to force an item into a tier
- the resource list for each biome

It is plain text with comments explaining each section. On a server, the server's copy is used. Restart the server after editing it.

`BepInEx/config/Gravegoods/report.md` is written each time a world loads. It lists every item in every pool and why it's there, everything that was left out and why, and any name in loot.txt that doesn't exist in your install.

## Commands

In the F5 console as `gravegoods`, or in chat as `/gravegoods`:

- `gravegoods` shows where the tables came from, the chances, and pool sizes per tier.
- `gravegoods sim <biome> [chests]` rolls imaginary chests and tallies the results. It's useful for tuning odds and changes nothing in the world.
- `gravegoods report` rewrites report.md.
- `gravegoods legacy on|off` is admin only. By default, only chests that load for the first time after install get a roll. Turning legacy on lets chests generated before the mod roll **once** the first time anyone opens them. It's stored in the world, so each world has its own setting.

## Configuration

`BepInEx/config/com.ragemedia.gravegoods.cfg`. Everything except `VerboseLogging` is admin-only and synced from the server.

- **General:** `Enabled`, `ExcludedContainers` (prefab name fragments to skip; More World Locations' shipment ports by default), `VerboseLogging`
- **Chances:** `ResourceChance` (max 75%), `AmmoChance` (max 45%), `GearChance` (max 35%). The caps are deliberate: this is an exploration mod, so no setting makes a chest a guaranteed payout.
- **Gear:** `SameTierChance` (max 20%), `Quality2Chance` (max 25%), `Quality3Chance` (max 15%), `DurabilityMin` and `DurabilityMax` (both 0–30%; 0 means the item arrives broken), `EnhancedChance` (max 25%; 0 means elemental variants never drop)
- **Ammo:** `SameTierChance` (max 75%), `StackMin` (1–3), `StackMax` (5–15)

## Compatibility

### Item mods: tested with ValheimArmory, should work with others

Gravegoods has only been tested in game alongside **[ValheimArmory](https://valheim.thunderstore.io/package/MidnightMods/ValheimArmory/)**. Its weapons, bows, crossbows, shields, arrows and bolts all land in the right tiers, and its boss weapons (Antler, Elder, Bonemass, Moder, Yagluth, Queen, Fader) are correctly kept out.

Other mods that add weapons, armor, capes or ammo **should work without any changes**, because Gravegoods has no code for any particular mod. When a world loads, it reads every item and crafting recipe the game actually has, from vanilla and from every installed mod alike, and sorts them by their ingredients. An item added by another mod goes through exactly the same process as a vanilla one.

Where another mod may need a line or two in `loot.txt`:

- **New crafting materials.** If a mod adds its own ore, hide or wood, Gravegoods doesn't know which biome it belongs to. Items made from it are **left out** rather than guessed at, so nothing skips progression. `report.md` lists these materials; add each under `[materials]` with its biome.
- **New bosses.** Their trophies and drops aren't in `[boss materials]` yet, so gear made from them won't be held back until you add them.
- **Custom crafting systems.** Items that aren't made through the normal crafting recipes won't be picked up. Add them under `[overrides]` if you want them.
- **New resources.** Resource drops are hand-listed per biome, so another mod's resources only drop if you add them.

After installing a new item mod, load a world and check `report.md`.

### More World Locations AIO

Gravegoods was built with **[More World Locations AIO](https://valheim.thunderstore.io/package/warpalicious/More_World_Locations_AIO/)** in mind, since it adds many more structures with chests. Its chests keep their own biome loot tables and get Gravegoods' bonus roll on top, like vanilla chests. Its shipment port containers are skipped by default (the `ExcludedContainers` setting), so shipments are never mixed with bonus loot.

This support comes from reading More World Locations' code; it has not been through an in-game test yet.

### Other mods

- **RegeneratingWorld:** it normally stops regenerating a location when a chest there holds anything besides its default loot. Gravegoods tells it that bonus items count as default loot, so a leftover bonus doesn't stop a location from regenerating. Not yet tested in game.
- **MultiUserChest:** legacy rolls (see Commands) happen on whoever owns the chest, so they still work when several players can open it at once. Not yet tested in game.

## Requirements

- [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
- [Jotunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)

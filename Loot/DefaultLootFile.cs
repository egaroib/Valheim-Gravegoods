namespace Gravegoods.Loot
{
    /// <summary>
    /// The shipped loot.txt, written to BepInEx/config/Gravegoods/ on first run. After that
    /// the file on disk is authoritative and this text is never re-applied over it.
    ///
    /// Prefab names here are a best effort, not verified against a running game. Every
    /// name that does not resolve is listed in report.md, so a typo shows up as a line in
    /// the report rather than as an item that silently never drops.
    /// </summary>
    internal static class DefaultLootFile
    {
        internal const string Text = @"# Gravegoods loot tables
#
# World chests roll three independent bonus chances (resources, ammo, gear). The chances,
# quality odds and durability are in com.ragemedia.gravegoods.cfg. This file decides WHAT
# can drop. On a server, the server's copy is sent to every client; restart the server
# after editing it.
#
# After a world loads, BepInEx/config/Gravegoods/report.md lists every pool this file
# produced, every item left out and why, and every name below that did not resolve.
#
# Biome names: Meadows, BlackForest, Swamp, Mountain, Plains, Mistlands, Ashlands, DeepNorth
# Everything after a # is a comment.


# ---------------------------------------------------------------------------------------
# [materials]
# The rung each crafting material belongs to. Weapons, armor and ammo are not listed by
# hand: each one is placed at the rung of its highest-rung ingredient, so ValheimArmory
# and other content mods are picked up automatically. Intermediate items (Bronze,
# BronzeNails, ...) resolve through their own recipes if they are not listed.
# An item with any ingredient that is neither listed here nor craftable stays OUT of the
# pools, and the report names the ingredient. Add it here to let those items drop.
# ---------------------------------------------------------------------------------------
[materials]
Meadows: Wood, Stone, Flint, Resin, LeatherScraps, DeerHide, Feathers, Raspberry, Honey
Meadows: Mushroom, Dandelion, NeckTail, TrophyDeer, TrophyBoar, TrophyNeck

BlackForest: FineWood, RoundLog, CopperOre, Copper, CopperScrap, TinOre, Tin, Bronze
BlackForest: BronzeScrap, BronzeNails, GreydwarfEye, TrollHide, SurtlingCore, BoneFragments
BlackForest: Thistle, Blueberries, AncientSeed, Carrot, CarrotSeeds, MushroomYellow, Amber
BlackForest: Ruby, TrophyGreydwarf, TrophyGreydwarfBrute, TrophyGreydwarfShaman
BlackForest: TrophySkeleton, TrophySkeletonPoison, TrophyFrostTroll, Coal
BlackForest: BjornHide, BjornPaw, TrophyBjorn

Swamp: Iron, IronOre, IronScrap, IronNails, ElderBark, Guck, Ooze, Entrails, WitheredBone
Swamp: Chain, Bloodbag, Turnip, TurnipSeeds, AmberPearl, Chitin, SerpentScale
Swamp: TrophyBlob, TrophyDraugr, TrophyDraugrElite, TrophyLeech, TrophySurtling
Swamp: TrophyAbomination, TrophyWraith, TrophySerpent, Root

Mountain: Silver, SilverOre, Obsidian, FreezeGland, WolfPelt, WolfFang, WolfClaw, Crystal
Mountain: JuteRed, YmirRemains, Onion, OnionSeeds, TrophyWolf, TrophyFenring, TrophyUlv
Mountain: TrophyCultist, TrophyHatchling, TrophySGolem, WolfHairBundle

Plains: BlackMetal, BlackMetalScrap, LinenThread, Flax, Barley, BarleyFlour, Needle
Plains: LoxPelt, Tar, GoblinTotem, Thunderstone, Cloudberry, TrophyDeathsquito
Plains: TrophyGoblin, TrophyGoblinBrute, TrophyGoblinShaman, TrophyLox, TrophyGrowth

Mistlands: YggdrasilWood, BlackMarble, Eitr, Sap, Softtissue, Carapace, ScaleHide, JuteBlue
Mistlands: Mandible, Bilebag, BlackCore, DvergrNeedle, Wisp, RoyalJelly, MushroomJotunPuffs
Mistlands: MushroomMagecap, TrophyDvergr, TrophySeeker, TrophySeekerBrute, TrophyTick
Mistlands: TrophyGjall, TrophyHare

Ashlands: FlametalNew, FlametalOreNew, Flametal, FlametalOre, Blackwood, Grausten
Ashlands: CharredBone, Charredskull, AskHide, AskBladder, MorgenSinew, MorgenHeart
Ashlands: CelestialFeather, GemstoneRed, GemstoneBlue, GemstoneGreen, MoltenCore
Ashlands: ProustitePowder, CharredCogwheel, MechanicalSpring, Ironpit, Pukeberries
Ashlands: Vineberry, VineberrySeeds, Fiddleheadfern, TrophyAsksvin, TrophyMorgen
Ashlands: TrophyCharredMelee, TrophyCharredArcher, TrophyCharredMage, TrophyFallenValkyrie
Ashlands: TrophyVolture, BellFragment, CharcoalResin, BonemawSerpentTooth, SulfurStone

DeepNorth: Gold, GoldOre, MooseHide, MooseSinew, SealHide, Frostwood, NornThread
# Bloodgold gear is cast from a mold per weapon or armor type.
DeepNorth: MoldSword, MoldSword2H, MoldAxe, MoldAxe2H, MoldMace, MoldMace2H, MoldKnife
DeepNorth: MoldSpear, MoldAtgeir, MoldBow, MoldCrossbow, MoldFistweapon
DeepNorth: MoldShieldRound, MoldShieldBuckler, MoldShieldTower
DeepNorth: MoldArmorGoldHelmet, MoldArmorGoldChest, MoldArmorGoldLegs
DeepNorth: MoldArmorMediumHelmet, MoldArmormediumChest, MoldArmorMediumLegs
DeepNorth: MoldArmorMageHelmet, MoldArmorMageChest, MoldArmorMageLegs

# Valheim 2.0 materials whose biome is not confirmed are placed HIGH on purpose: a rung too
# high only makes an item rarer, a rung too low hands it out early. Move them down once known.
Plains: TrophyBjornUndead, UndeadBjornRibcage, WrithanRoots
Mistlands: GiantBloodSack
DeepNorth: Ice, BarkaBranch


# ---------------------------------------------------------------------------------------
# [boss materials]
# Boss trophies and boss drops. Anything whose every recipe needs one of these stays out
# of the pools: these are the progression gates, and a chest should never hand them over.
# ---------------------------------------------------------------------------------------
[boss materials]
TrophyEikthyr, HardAntler
TrophyTheElder, CryptKey
TrophyBonemass, Wishbone
TrophyDragonQueen, DragonTear
TrophyGoblinKing, YagluthDrop
TrophySeekerQueen, QueenDrop
TrophyFader, FaderDrop, FaderEmber
# Unique exploration rewards, not farmable materials.
DyrnwynHiltFragment, CrownJewel


# ---------------------------------------------------------------------------------------
# [never]
# Items that never appear, however they classify. One or more names per line.
# ---------------------------------------------------------------------------------------
[never]
# Novelty and tool items that happen to use a weapon or armor slot.
Tankard, Tankard_dvergr, GrapplingHook, Scythe, Shovel, HelmetFishingHat, AxeEarly


# ---------------------------------------------------------------------------------------
# [overrides]
# Force an item into a rung, skipping recipe classification. This also admits items that
# have no recipe at all (trader stock, drops). Boss materials are NOT checked here.
#   SwordIron: Swamp
# ---------------------------------------------------------------------------------------
[overrides]


# ---------------------------------------------------------------------------------------
# [resources <Biome>]
# What a resource roll can give, per biome.   Item  min-max  [weight, default 1]
# ---------------------------------------------------------------------------------------
[resources Meadows]
Flint 3-6
Resin 3-6
LeatherScraps 2-5
DeerHide 2-3
Honey 2-4
NeckTail 2-4
Coins 5-15 0.5

[resources BlackForest]
GreydwarfEye 3-6
Resin 4-8
Thistle 2-4
TrollHide 1-2 0.6
SurtlingCore 1-2 0.6
CopperOre 2-4 0.6
TinOre 2-4 0.6
AncientSeed 1 0.5
Amber 1 0.4
Ruby 1 0.3
Coins 10-30 0.7

[resources Swamp]
Guck 2-4
Entrails 3-6
Ooze 2-4
ElderBark 3-6
WitheredBone 1-2 0.6
IronScrap 2-4 0.6
Bloodbag 2-3 0.6
AmberPearl 1 0.4
Coins 20-40 0.7

[resources Mountain]
FreezeGland 2-4
WolfPelt 2-3
WolfFang 2-4
Obsidian 3-6
Onion 2-4
Crystal 1-2 0.5
SilverOre 2-3 0.5
Ruby 1 0.4
Coins 20-50 0.7

[resources Plains]
Barley 3-6
Flax 3-6
Needle 3-6
Cloudberry 3-6
Tar 2-4 0.7
LoxPelt 1-2 0.6
BlackMetalScrap 2-4 0.5
GoblinTotem 1 0.3
Coins 30-60 0.7

[resources Mistlands]
Softtissue 2-4
BlackMarble 3-6
MushroomJotunPuffs 3-6
MushroomMagecap 3-6
Carapace 2-4 0.7
ScaleHide 1-2 0.6
Mandible 1-2 0.5
Bilebag 1 0.4
Coins 40-80 0.7

[resources Ashlands]
Grausten 3-6
Blackwood 3-6
CharredBone 3-6
Vineberry 3-5
AskHide 1-2 0.6
CelestialFeather 1-2 0.5
FlametalOreNew 2-3 0.5
GemstoneRed 1 0.2
GemstoneBlue 1 0.2
GemstoneGreen 1 0.2
Coins 50-100 0.7

[resources DeepNorth]
Frostwood 3-6
MooseHide 1-2 0.6
SealHide 1-2 0.6
Coins 60-120 0.7
";
    }
}

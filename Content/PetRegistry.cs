using System;
using System.Collections.Generic;
using System.Linq;
using PetBestiary.Common.Configs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PetBestiary.Content;

public sealed class PetRegistry : ModSystem
{
    public static PetRegistry Instance { get; private set; }

    private readonly Dictionary<string, PetDefinition> byKey = new();
    private readonly Dictionary<int, PetDefinition> byItemType = new();
    private readonly Dictionary<int, PetDefinition> byBuffType = new();
    private readonly Dictionary<int, PetDefinition> byProjectileType = new();

    public IReadOnlyList<PetDefinition> AllPets { get; private set; } = Array.Empty<PetDefinition>();

    public override void OnModLoad()
    {
        Instance = this;
    }

    public override void Unload()
    {
        Instance = null;
    }

    public override void PostSetupContent()
    {
        Rebuild();
    }

    public bool TryGet(string key, out PetDefinition definition)
    {
        return byKey.TryGetValue(key, out definition);
    }

    public bool TryResolve(string key, out PetDefinition definition)
    {
        if (string.IsNullOrEmpty(key))
        {
            definition = null;
            return false;
        }

        return byKey.TryGetValue(key, out definition);
    }

    public bool TryResolve(string key, PetCategory expectedCategory, out PetDefinition definition)
    {
        return TryResolve(key, out definition) && definition.Category == expectedCategory;
    }

    public bool TryGetByItemType(int itemType, out PetDefinition definition)
    {
        return byItemType.TryGetValue(itemType, out definition);
    }

    public bool TryGetByBuffType(int buffType, out PetDefinition definition)
    {
        return byBuffType.TryGetValue(buffType, out definition);
    }

    public bool TryGetByProjectileType(int projectileType, out PetDefinition definition)
    {
        return byProjectileType.TryGetValue(projectileType, out definition);
    }

    public IReadOnlyList<PetDefinition> GetByCategory(PetCategory category)
    {
        return AllPets.Where(pet => pet.Category == category).ToList();
    }

    private void Rebuild()
    {
        byKey.Clear();
        byItemType.Clear();
        byBuffType.Clear();
        byProjectileType.Clear();

        bool includeModded = ModContent.GetInstance<PetBestiaryConfig>().EnableModdedPetDiscovery;

        for (int itemType = 1; itemType < ItemLoader.ItemCount; itemType++)
        {
            Item item = new();
            item.SetDefaults(itemType);

            if (item.IsAir || item.buffType <= 0 || item.shoot <= ProjectileID.None)
            {
                continue;
            }

            ModItem modItem = item.ModItem;
            bool isModded = modItem != null;
            if (isModded && !includeModded)
            {
                continue;
            }

            PetCategory? category = Classify(item.buffType);
            if (!category.HasValue)
            {
                continue;
            }

            string sourceMod = modItem?.Mod.Name ?? "Terraria";
            string itemName = modItem?.Name ?? ItemID.Search.GetName(itemType);
            string key = $"{sourceMod}/{itemName}";
            string displayName = ResolveDisplayName(item, item.shoot, item.buffType);

            PetDefinition definition = new(
                key,
                itemType,
                item.shoot,
                item.buffType,
                displayName,
                sourceMod,
                category.Value,
                VanillaPetUnlockHints.GetHint(itemType, sourceMod, displayName));

            byKey[key] = definition;
            byItemType[itemType] = definition;
            byBuffType[item.buffType] = definition;
            byProjectileType[item.shoot] = definition;
        }

        AllPets = byKey.Values
            .OrderBy(pet => pet.Category)
            .ThenBy(pet => pet.SourceMod)
            .ThenBy(pet => pet.DisplayName)
            .ToList();
    }

    private static PetCategory? Classify(int buffType)
    {
        if (buffType >= 0 && buffType < Main.lightPet.Length && Main.lightPet[buffType])
        {
            return PetCategory.Light;
        }

        if (buffType >= 0 && buffType < Main.vanityPet.Length && Main.vanityPet[buffType])
        {
            return PetCategory.Normal;
        }

        return null;
    }

    private static string ResolveDisplayName(Item item, int projectileType, int buffType)
    {
        string buffName = Lang.GetBuffName(buffType);
        if (IsUsableDisplayName(buffName))
        {
            return buffName;
        }

        if (projectileType > ProjectileID.None)
        {
            string projectileName = Lang.GetProjectileName(projectileType).Value;
            if (IsUsableDisplayName(projectileName))
            {
                return projectileName;
            }
        }

        if (IsUsableDisplayName(item.Name))
        {
            return item.Name;
        }

        ModItem modItem = item.ModItem;
        if (modItem != null && IsUsableDisplayName(modItem.Name))
        {
            return modItem.Name;
        }

        return ItemID.Search.GetName(item.type);
    }

    private static bool IsUsableDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.StartsWith("Mods.", StringComparison.Ordinal)
            && !value.StartsWith("BuffName.", StringComparison.Ordinal)
            && !value.StartsWith("ProjectileName.", StringComparison.Ordinal)
            && !value.StartsWith("ItemName.", StringComparison.Ordinal);
    }
}

internal static class VanillaPetUnlockHints
{
    private static readonly Dictionary<string, string> Hints = new()
    {
        // Manually maintained vanilla ItemID names. Modded pets still default to "???".
        ["AmberMosquito"] = "Rare extractinator reward from Silt, Slush, or Desert Fossil.",
        ["BabyGrinchMischiefWhistle"] = "Dropped during the Frost Moon event.",
        ["BambooLeaf"] = "Found by shaking jungle bamboo.",
        ["BallOfFuseWire"] = "Purchased from the Zoologist after completing 70% of the Bestiary.",
        ["BedazzledNectar"] = "Dropped by Queen Bee.",
        ["BerniePetItem"] = "Purchased from the Princess after defeating Plantera.",
        ["BirdieRattle"] = "Purchased from the Traveling Merchant.",
        ["BoneKey"] = "Dropped by Dungeon Guardian.",
        ["BoneRattle"] = "Dropped by Brain of Cthulhu.",
        ["BrainOfCthulhuPetItem"] = "Dropped by Brain of Cthulhu in Master Mode.",
        ["BlueEgg"] = "Purchased from the Traveling Merchant.",
        ["Carrot"] = "Collector's Edition bonus pet item.",
        ["CelestialWand"] = "Purchased from the Traveling Merchant.",
        ["ChesterPetItem"] = "Dropped by Deerclops.",
        ["CompanionCube"] = "Purchased from the Traveling Merchant.",
        ["CreeperEgg"] = "Dropped by the Ogre.",
        ["CursedSapling"] = "Dropped by Mourning Wood.",
        ["DD2BetsyPetItem"] = "Dropped by Betsy in Master Mode.",
        ["DD2OgrePetItem"] = "Dropped by the Ogre in Master Mode.",
        ["DD2PetDragon"] = "Dropped by Dark Mages during the Old One's Army event.",
        ["DD2PetGato"] = "Dropped by Dark Mages during the Old One's Army event.",
        ["DD2PetGhost"] = "Dropped by the Ogre during the Old One's Army event.",
        ["DeerclopsPetItem"] = "Dropped by Deerclops in Master Mode.",
        ["DestroyerPetItem"] = "Dropped by The Destroyer in Master Mode.",
        ["DirtiestBlock"] = "Rarely found by mining dirt.",
        ["DogWhistle"] = "Found in Christmas presents.",
        ["DukeFishronPetItem"] = "Dropped by Duke Fishron in Master Mode.",
        ["DynamiteKitten"] = "Purchased from the Zoologist after completing 70% of the Bestiary.",
        ["EatersBone"] = "Dropped by Eater of Worlds.",
        ["EaterOfWorldsPetItem"] = "Dropped by Eater of Worlds in Master Mode.",
        ["EucaluptusSap"] = "Rare drop from shaking forest trees.",
        ["EucalyptusSap"] = "Rare drop from shaking forest trees.",
        ["EverscreamPetItem"] = "Dropped by Everscream in Master Mode.",
        ["ExoticEasternChewToy"] = "Purchased from the Traveling Merchant.",
        ["EyeBone"] = "Dropped by Deerclops.",
        ["EyeOfCthulhuPetItem"] = "Dropped by Eye of Cthulhu in Master Mode.",
        ["EyeSpring"] = "Dropped by Eyezor during a Solar Eclipse.",
        ["FairyQueenPetItem"] = "Dropped by Empress of Light in Master Mode.",
        ["Fish"] = "Found in frozen chests or frozen crates.",
        ["FullMoonSqueakyToy"] = "Purchased from the Zoologist during a Hardmode Blood Moon.",
        ["GatoEgg"] = "Dropped by the Dark Mage.",
        ["GlommerPetItem"] = "Dropped by Derplings.",
        ["GlowTulip"] = "Rare plant found near the left and right world edges below the Surface.",
        ["GolemPetItem"] = "Dropped by Golem in Master Mode.",
        ["HellCake"] = "Found in Shadow Chests, Obsidian Crates, or Hellstone Crates.",
        ["IceQueenPetItem"] = "Dropped by Ice Queen in Master Mode.",
        ["JewelOfLight"] = "Dropped by Empress of Light.",
        ["JunimoPetItem"] = "Given by the Dryad after purifying Joja Cola.",
        ["KingSlimePetItem"] = "Dropped by King Slime in Master Mode.",
        ["LightningCarrot"] = "Purchased from the Zoologist after completing 50% of the Bestiary.",
        ["LizardEgg"] = "Dropped by Lihzahrds or Flying Snakes.",
        ["LunaticCultistPetItem"] = "Dropped by Lunatic Cultist in Master Mode.",
        ["MagicalPumpkinSeed"] = "Harvested rarely from pumpkins.",
        ["MartianPetItem"] = "Dropped by Martian Saucer in Master Mode.",
        ["MoonLordPetItem"] = "Dropped by Moon Lord in Master Mode.",
        ["MoonSquid"] = "Dropped by Moon Lord in Master Mode.",
        ["MudBud"] = "Purchased from the Zoologist after defeating Plantera.",
        ["Nectar"] = "Dropped by Queen Bee.",
        ["OrnateShadowKey"] = "Found in Shadow Chests, Obsidian Crates, or Hellstone Crates.",
        ["ParrotCracker"] = "Purchased from the Pirate.",
        ["PigPetItem"] = "Rare drop from most Corruption or Crimson enemies.",
        ["PieceOfMoonSquid"] = "Dropped by Moon Lord in Master Mode.",
        ["PieceofMoonSquid"] = "Dropped by Moon Lord in Master Mode.",
        ["PlanteraPetItem"] = "Dropped by Plantera in Master Mode.",
        ["Plantero"] = "Dropped by Plantera.",
        ["PuppyWhistle"] = "Found in Christmas presents.",
        ["PumpkingPetItem"] = "Dropped by Pumpking in Master Mode.",
        ["QueenBeePetItem"] = "Dropped by Queen Bee in Master Mode.",
        ["QueenSlimePetItem"] = "Dropped by Queen Slime in Master Mode.",
        ["RegalDelicacy"] = "Dropped by Queen Slime in Master Mode.",
        ["ResplendentDessert"] = "Dropped by King Slime or Queen Slime in Master Mode.",
        ["RoyalDelight"] = "Dropped by King Slime in Master Mode.",
        ["Seaweed"] = "Found in ivy chests or jungle crates.",
        ["Seedling"] = "Dropped by Plantera.",
        ["SharkBait"] = "Found in water chests or ocean crates.",
        ["SliceOfHellCake"] = "Found in Shadow Chests, Obsidian Crates, or Hellstone Crates.",
        ["SkeletronPetItem"] = "Dropped by Skeletron in Master Mode.",
        ["SkeletronPrimePetItem"] = "Dropped by Skeletron Prime in Master Mode.",
        ["SparklingHoney"] = "Dropped by Queen Bee in Master Mode.",
        ["SpiderEgg"] = "Dropped by the Pumpking.",
        ["SpiffoPlush"] = "Dropped by zombies.",
        ["StrangeGlowingMushroom"] = "Purchased from the Truffle.",
        // tModLoader 1.4.4 still has Tartar Sauce/Mini Minotaur. Terraria 1.4.5 replaces this with Beguiling Lyre/Faun.
        ["TartarSauce"] = "Found in Iron Crates or Mythril Crates in tModLoader 1.4.4.",
        ["TikiTotem"] = "Purchased from the Witch Doctor in the Jungle.",
        ["ToySled"] = "Dropped by Ice Mimics.",
        ["TwinsPetItem"] = "Dropped by The Twins in Master Mode.",
        ["UnluckyYarn"] = "Purchased from the Zoologist during a full moon.",
        ["ZephyrFish"] = "Rare fishing catch.",
        ["ShadowOrb"] = "Found by breaking Shadow Orbs.",
        ["CrimsonHeart"] = "Found by breaking Crimson Hearts.",
        ["FairyBell"] = "Crafted with Pixie Dust, Souls of Light, Souls of Sight, and a Bell.",
        ["MagicLantern"] = "Purchased from the Skeleton Merchant during a full moon.",
        ["SuspiciousLookingTentacle"] = "Moon Lord expert-mode treasure bag reward.",
        ["WispinaBottle"] = "Dropped by Blue Armored Bones, Hell Armored Bones, and Rusty Armored Bones."
    };

    private static readonly Dictionary<string, string> PetNameHints = new()
    {
        ["BabyImp"] = "Found in Shadow Chests, Obsidian Crates, or Hellstone Crates.",
        ["DynamiteKitten"] = "Purchased from the Zoologist after completing 70% of the Bestiary.",
        ["Flickerwick"] = "Dropped by the Ogre during the Old One's Army event.",
        ["MiniMinotaur"] = "Found in Iron Crates or Mythril Crates in tModLoader 1.4.4.",
        ["ResplendentDessert"] = "Dropped by King Slime or Queen Slime in Master Mode.",
        ["SlimePrince"] = "Dropped by King Slime in Master Mode.",
        ["SlimePrincess"] = "Dropped by Queen Slime in Master Mode.",
        ["SlimeRoyals"] = "Dropped by King Slime or Queen Slime in Master Mode."
    };

    public static string GetHint(int itemType, string sourceMod, string displayName)
    {
        if (sourceMod != "Terraria")
        {
            return "???";
        }

        string itemIdName = ItemID.Search.GetName(itemType);
        if (Hints.TryGetValue(itemIdName, out string hint))
        {
            return hint;
        }

        string petNameKey = NormalizeKey(displayName);
        return PetNameHints.TryGetValue(petNameKey, out hint)
            ? hint
            : "???";
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }
}

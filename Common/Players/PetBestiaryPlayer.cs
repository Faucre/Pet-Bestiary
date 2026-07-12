using System.Collections.Generic;
using System.Linq;
using PetBestiary.Common.Configs;
using PetBestiary.Common.Systems;
using PetBestiary.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PetBestiary.Common.Players;

public sealed class PetBestiaryPlayer : ModPlayer
{
    private const int SaveVersion = 5;
    private int inventoryScanTimer;

    public HashSet<string> UnlockedPets { get; private set; } = new();

    public List<string> ActiveNormalPets { get; private set; } = new();

    public List<string> ActiveLightPets { get; private set; } = new();

    public HashSet<string> LockedPets { get; private set; } = new();

    public List<PetPreset> Presets { get; private set; } = new();

    public Dictionary<string, PetDyeData> PetDyes { get; private set; } = new();

    public HashSet<string> UnlockedDyes { get; private set; } = new();

    public override void Initialize()
    {
        UnlockedPets = new HashSet<string>();
        ActiveNormalPets = new List<string>();
        ActiveLightPets = new List<string>();
        LockedPets = new HashSet<string>();
        Presets = new List<PetPreset>();
        PetDyes = new Dictionary<string, PetDyeData>();
        UnlockedDyes = new HashSet<string>();
        inventoryScanTimer = 0;
    }

    public override void SaveData(TagCompound tag)
    {
        tag["SaveVersion"] = SaveVersion;
        tag["UnlockedPets"] = UnlockedPets.OrderBy(key => key).ToList();
        tag["ActiveNormalPets"] = ActiveNormalPets.ToList();
        tag["ActiveLightPets"] = ActiveLightPets.ToList();
        tag["LockedPets"] = LockedPets.OrderBy(key => key).ToList();
        tag["Presets"] = Presets.Select(preset => preset.Save()).ToList();
        tag["PetDyes"] = PetDyes
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value?.HasShader == true)
            .Select(pair => new TagCompound
            {
                ["PetKey"] = pair.Key,
                ["Dye"] = pair.Value.Save()
            })
            .ToList();
        tag["UnlockedDyes"] = UnlockedDyes.OrderBy(key => key).ToList();
    }

    public override void LoadData(TagCompound tag)
    {
        UnlockedPets = new HashSet<string>(ReadStringList(tag, "UnlockedPets"));
        ActiveNormalPets = ReadStringList(tag, "ActiveNormalPets").ToList();
        ActiveLightPets = ReadStringList(tag, "ActiveLightPets").ToList();
        LockedPets = new HashSet<string>(ReadStringList(tag, "LockedPets"));
        Presets = tag.ContainsKey("Presets")
            ? tag.GetList<TagCompound>("Presets").Select(PetPreset.Load).ToList()
            : new List<PetPreset>();
        PetDyes = LoadDyes(tag);
        UnlockedDyes = new HashSet<string>(ReadStringList(tag, "UnlockedDyes"));
        foreach (PetDyeData dyeData in PetDyes.Values)
        {
            if (!string.IsNullOrWhiteSpace(dyeData.DyeItemKey))
            {
                UnlockedDyes.Add(dyeData.DyeItemKey);
            }
        }

        PruneInvalidActivePets();
    }

    public override void PostUpdate()
    {
        if (Player.whoAmI == Main.myPlayer)
        {
            inventoryScanTimer++;
            if (inventoryScanTimer >= 60)
            {
                inventoryScanTimer = 0;
                ScanInventoryForUnlocks();
            }
        }

        if (ShouldOwnLocalPetState())
        {
            PruneActivePetsForCurrentState(requireUnlocked: Main.netMode != NetmodeID.Server);
        }

        // Pet item use is intercepted before vanilla can create native pet state.
        // Do not clear native pet-slot buffs/projectiles every tick: real pet/light
        // equipment slots reapply them continuously, which causes summon sound spam.
        PetSpawnManager.MaintainPlayerPets(Player, this);
    }

    public override void OnEnterWorld()
    {
        if (Player.whoAmI == Main.myPlayer)
        {
            SyncActivePetState();
        }
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        global::PetBestiary.PetBestiary.SendActivePetState(Player, toWho, fromWho);
    }

    public override void CopyClientState(ModPlayer targetCopy)
    {
        PetBestiaryPlayer clone = (PetBestiaryPlayer)targetCopy;
        clone.ActiveNormalPets = ActiveNormalPets.ToList();
        clone.ActiveLightPets = ActiveLightPets.ToList();
        clone.LockedPets = LockedPets.ToHashSet();
        clone.PetDyes = PetDyes.ToDictionary(pair => pair.Key, pair => CloneDye(pair.Value));
    }

    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer && !SyncedStateEquals((PetBestiaryPlayer)clientPlayer))
        {
            SyncActivePetState();
        }
    }

    public override void Kill(double damage, int hitDirection, bool pvp, Terraria.DataStructures.PlayerDeathReason damageSource)
    {
        PetSpawnManager.CleanupPlayerPets(Player);
    }

    public bool IsUnlocked(string key)
    {
        return UnlockedPets.Contains(key);
    }

    public bool IsActive(string key)
    {
        return ActiveNormalPets.Contains(key) || ActiveLightPets.Contains(key);
    }

    public bool IsLocked(string key)
    {
        return LockedPets.Contains(key);
    }

    public bool TryTogglePet(string key)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null || !registry.TryResolve(key, out PetDefinition definition) || !IsUnlocked(key))
        {
            return false;
        }

        PruneActivePetsForCurrentState();

        List<string> activePets = GetActiveList(definition.Category);
        if (activePets.Contains(key) && IsLocked(key))
        {
            return false;
        }

        if (activePets.Remove(key))
        {
            LockedPets.Remove(key);
            PetSpawnManager.CleanupPet(Player, key);
            SyncActivePetState();
            return true;
        }

        int cap = GetCap(definition.Category);
        if (activePets.Count >= cap)
        {
            return false;
        }

        activePets.Add(key);
        SyncActivePetState();
        return true;
    }

    public bool TryUsePetItem(Item item, bool showMessage)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null || item == null || item.IsAir || !registry.TryGetByItemType(item.type, out PetDefinition definition))
        {
            return false;
        }

        UnlockPet(definition);
        bool toggled = TryTogglePet(definition.Key);
        if (showMessage && Player.whoAmI == Main.myPlayer)
        {
            string message = toggled
                ? $"{definition.DisplayName} toggled in Pet Bestiary."
                : $"{definition.DisplayName} is locked or the pet cap is full. Use Pet Bestiary to manage it.";
            Main.NewText(message, 255, 230, 130);
        }

        return true;
    }

    public void UnlockPet(PetDefinition definition)
    {
        if (definition != null && UnlockedPets.Add(definition.Key))
        {
            LogDebug($"Unlocked pet {definition.Key}");
        }
    }

    public bool TryToggleLock(string key)
    {
        if (!IsActive(key))
        {
            return false;
        }

        if (!LockedPets.Add(key))
        {
            LockedPets.Remove(key);
        }

        SyncActivePetState();
        return true;
    }

    public bool TryAssignDye(string petKey, Item dyeItem)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null || !registry.TryResolve(petKey, out _) || !IsUnlocked(petKey))
        {
            return false;
        }

        if (!PetDyeManager.TryCreateFromItem(dyeItem, out PetDyeData dyeData))
        {
            return false;
        }

        PetDyes[petKey] = dyeData;
        UnlockDye(dyeData);
        SyncActivePetState();
        return true;
    }

    public bool TryAssignDye(string petKey, PetDyeData dyeData)
    {
        if (!CanAssignDye(petKey, dyeData))
        {
            return false;
        }

        PetDyes[petKey] = dyeData;
        SyncActivePetState();
        return true;
    }

    public int TryAssignDyeToActivePets(PetDyeData dyeData)
    {
        if (dyeData?.HasShader != true || !IsDyeUnlocked(dyeData) || !PetDyeManager.TryResolve(dyeData, out _))
        {
            return 0;
        }

        int assigned = 0;
        foreach (string petKey in ActiveNormalPets.Concat(ActiveLightPets).Distinct())
        {
            if (CanAssignDye(petKey, dyeData))
            {
                PetDyes[petKey] = dyeData;
                assigned++;
            }
        }

        if (assigned > 0)
        {
            SyncActivePetState();
        }

        return assigned;
    }

    public bool TryGetDye(string petKey, out PetDyeData dyeData)
    {
        if (!PetDyes.TryGetValue(petKey, out dyeData) || dyeData?.HasShader != true)
        {
            dyeData = null;
            return false;
        }

        return true;
    }

    public bool ClearDye(string petKey)
    {
        bool removed = PetDyes.Remove(petKey);
        if (removed)
        {
            SyncActivePetState();
        }

        return removed;
    }

    public int ClearAllDyes()
    {
        int count = PetDyes.Count;
        PetDyes.Clear();
        if (count > 0)
        {
            SyncActivePetState();
        }

        return count;
    }

    public bool IsDyeUnlocked(PetDyeData dyeData)
    {
        return dyeData != null && !string.IsNullOrWhiteSpace(dyeData.DyeItemKey) && UnlockedDyes.Contains(dyeData.DyeItemKey);
    }

    public IReadOnlyList<PetDyeData> GetUnlockedDyes()
    {
        List<PetDyeData> dyes = new();
        foreach (string key in UnlockedDyes)
        {
            if (!PetDyeManager.TryResolveItemKey(key, out int itemType)
                || !PetDyeManager.TryCreateFromItemType(itemType, out PetDyeData dyeData)
                || !dyeData.HasShader)
            {
                continue;
            }

            dyes.Add(dyeData);
        }

        return dyes
            .OrderBy(dye => dye.DisplayName)
            .ToList();
    }

    public int EquipAll(PetCategory category)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return 0;
        }

        PruneActivePetsForCurrentState();

        List<string> activePets = GetActiveList(category);
        int cap = GetCap(category);
        int added = 0;

        foreach (PetDefinition pet in registry.GetByCategory(category))
        {
            if (activePets.Count >= cap)
            {
                break;
            }

            if (!UnlockedPets.Contains(pet.Key) || activePets.Contains(pet.Key))
            {
                continue;
            }

            activePets.Add(pet.Key);
            added++;
        }

        if (added > 0)
        {
            SyncActivePetState();
        }

        return added;
    }

    public int UnequipAll(PetCategory category)
    {
        List<string> activePets = GetActiveList(category);
        List<string> removed = activePets.Where(key => !LockedPets.Contains(key)).ToList();

        foreach (string key in removed)
        {
            activePets.Remove(key);
            LockedPets.Remove(key);
            PetSpawnManager.CleanupPet(Player, key);
        }

        if (removed.Count > 0)
        {
            SyncActivePetState();
        }

        return removed.Count;
    }

    public void SaveCurrentPreset()
    {
        int nextNumber = Presets.Count + 1;
        Presets.Add(new PetPreset(
            $"Preset {nextNumber}",
            ActiveNormalPets,
            ActiveLightPets,
            LockedPets.Where(IsActive),
            PetDyes.Where(pair => IsActive(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value)));
    }

    public bool DeletePreset(int index)
    {
        if (index < 0 || index >= Presets.Count)
        {
            return false;
        }

        Presets.RemoveAt(index);
        return true;
    }

    public PresetLoadResult LoadPreset(int index)
    {
        if (index < 0 || index >= Presets.Count)
        {
            return new PresetLoadResult(false, 0);
        }

        PetPreset preset = Presets[index];
        int missing = 0;
        List<string> normalPets = ResolvePresetPets(preset.NormalPets, PetCategory.Normal, ref missing);
        List<string> lightPets = ResolvePresetPets(preset.LightPets, PetCategory.Light, ref missing);

        ActiveNormalPets = ApplyCap(normalPets, preset.LockedPets, PetCategory.Normal);
        ActiveLightPets = ApplyCap(lightPets, preset.LockedPets, PetCategory.Light);

        HashSet<string> activeKeys = ActiveNormalPets.Concat(ActiveLightPets).ToHashSet();
        LockedPets = preset.LockedPets
            .Where(activeKeys.Contains)
            .ToHashSet();

        foreach (string key in activeKeys)
        {
            if (!preset.PetDyes.TryGetValue(key, out PetDyeData dyeData))
            {
                PetDyes.Remove(key);
                continue;
            }

            if (IsDyeUnlocked(dyeData) && PetDyeManager.TryResolve(dyeData, out _))
            {
                PetDyes[key] = dyeData;
                continue;
            }

            PetDyes.Remove(key);
        }

        PetSpawnManager.CleanupPlayerPets(Player);
        SyncActivePetState();
        return new PresetLoadResult(true, missing);
    }

    private void ScanInventoryForUnlocks()
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return;
        }

        ScanItemArray(Player.inventory, registry);
        ScanItemArray(Player.miscEquips, registry);
        ScanDyeArray(Player.inventory);
        ScanDyeArray(Player.dye);
        ScanDyeArray(Player.miscDyes);
    }

    private void ScanItemArray(Item[] items, PetRegistry registry)
    {
        foreach (Item item in items)
        {
            if (item == null || item.IsAir)
            {
                continue;
            }

            if (registry.TryGetByItemType(item.type, out PetDefinition definition))
            {
                UnlockPet(definition);
            }
        }
    }

    private void ScanDyeArray(Item[] items)
    {
        foreach (Item item in items)
        {
            if (item == null || item.IsAir || item.dye <= 0)
            {
                continue;
            }

            if (PetDyeManager.TryCreateFromItem(item, out PetDyeData dyeData))
            {
                UnlockDye(dyeData);
            }
        }
    }

    private void UnlockDye(PetDyeData dyeData)
    {
        if (dyeData != null && !string.IsNullOrWhiteSpace(dyeData.DyeItemKey) && UnlockedDyes.Add(dyeData.DyeItemKey))
        {
            LogDebug($"Unlocked dye {dyeData.DyeItemKey}");
        }
    }

    public int UnlockAllKnownDyes()
    {
        int before = UnlockedDyes.Count;
        for (int itemType = 1; itemType < ItemLoader.ItemCount; itemType++)
        {
            if (PetDyeManager.TryCreateFromItemType(itemType, out PetDyeData dyeData))
            {
                UnlockDye(dyeData);
            }
        }

        return UnlockedDyes.Count - before;
    }

    public int RelockAllDyes()
    {
        int count = UnlockedDyes.Count;
        bool hadAssignedDyes = PetDyes.Count > 0;
        UnlockedDyes.Clear();
        PetDyes.Clear();
        if (count > 0 || hadAssignedDyes)
        {
            SyncActivePetState();
        }

        return count;
    }

    public int UnlockAllKnownPets()
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return 0;
        }

        int before = UnlockedPets.Count;
        foreach (PetDefinition pet in registry.AllPets)
        {
            UnlockedPets.Add(pet.Key);
        }

        return UnlockedPets.Count - before;
    }

    public void RelockAllPets()
    {
        UnlockedPets.Clear();
        ActiveNormalPets.Clear();
        ActiveLightPets.Clear();
        LockedPets.Clear();
        Presets.Clear();
        PetDyes.Clear();
        PetSpawnManager.CleanupPlayerPets(Player);
        PetSpawnManager.ClearNativePetState(Player);
        SyncActivePetState();
    }

    public void ClearActivePets()
    {
        ActiveNormalPets.Clear();
        ActiveLightPets.Clear();
        LockedPets.Clear();
        PetSpawnManager.CleanupPlayerPets(Player);
        SyncActivePetState();
    }

    internal void ApplySyncedActivePetState(IEnumerable<string> normalPets, IEnumerable<string> lightPets, IEnumerable<string> lockedPets, IDictionary<string, PetDyeData> dyes)
    {
        List<string> previousActive = ActiveNormalPets.Concat(ActiveLightPets).ToList();

        ActiveNormalPets = NormalizeSyncedActivePets(normalPets, PetCategory.Normal);
        ActiveLightPets = NormalizeSyncedActivePets(lightPets, PetCategory.Light);
        HashSet<string> activeKeys = ActiveNormalPets.Concat(ActiveLightPets).ToHashSet();
        LockedPets = lockedPets
            .Where(activeKeys.Contains)
            .ToHashSet();
        PetDyes = dyes
            .Where(pair => activeKeys.Contains(pair.Key) && pair.Value?.HasShader == true)
            .ToDictionary(pair => pair.Key, pair => CloneDye(pair.Value));

        if (Main.netMode != NetmodeID.MultiplayerClient && !previousActive.SequenceEqual(ActiveNormalPets.Concat(ActiveLightPets)))
        {
            PetSpawnManager.CleanupPlayerPets(Player);
        }
    }

    private List<string> GetActiveList(PetCategory category)
    {
        return category == PetCategory.Light ? ActiveLightPets : ActiveNormalPets;
    }

    public int GetCap(PetCategory category)
    {
        return PetCapCalculator.GetCap(category);
    }

    public int GetActiveCount(PetCategory category)
    {
        return GetActiveList(category).Count;
    }

    public bool IsPetSlotLimitReached(PetCategory category)
    {
        return GetActiveCount(category) >= GetCap(category);
    }

    public bool IsProgressionModeEnabledFor(PetCategory category)
    {
        return PetCapCalculator.IsProgressionModeEnabledFor(category);
    }

    private void PruneInvalidActivePets()
    {
        PruneActivePetsForCurrentState(cleanupProjectiles: false);
    }

    public void PruneActivePetsForCurrentState(bool cleanupProjectiles = true, bool requireUnlocked = true)
    {
        List<string> removed = new();
        ActiveNormalPets = PruneActiveList(ActiveNormalPets, PetCategory.Normal, removed, requireUnlocked);
        ActiveLightPets = PruneActiveList(ActiveLightPets, PetCategory.Light, removed, requireUnlocked);
        LockedPets = LockedPets.Where(IsActive).ToHashSet();

        if (!cleanupProjectiles)
        {
            return;
        }

        foreach (string key in removed.Distinct())
        {
            PetSpawnManager.CleanupPet(Player, key);
        }

        if (removed.Count > 0)
        {
            SyncActivePetState();
        }
    }

    private List<string> PruneActiveList(IEnumerable<string> activePets, PetCategory category, List<string> removed, bool requireUnlocked)
    {
        PetRegistry registry = PetRegistry.Instance;
        List<string> original = activePets.ToList();
        List<string> candidates = new();

        foreach (string key in original)
        {
            if (candidates.Contains(key))
            {
                removed.Add(key);
                continue;
            }

            if (requireUnlocked && !UnlockedPets.Contains(key))
            {
                removed.Add(key);
                continue;
            }

            // Keep unresolved keys in unlock history and presets, but do not keep them active:
            // there is no safe projectile/buff to spawn while the source mod is unloaded.
            if (registry != null && !registry.TryResolve(key, category, out _))
            {
                removed.Add(key);
                continue;
            }

            candidates.Add(key);
        }

        List<string> kept = ApplyCap(candidates, LockedPets, category);
        removed.AddRange(candidates.Where(key => !kept.Contains(key)));
        return kept;
    }

    private List<string> ApplyCap(IEnumerable<string> petKeys, IEnumerable<string> lockedKeys, PetCategory category)
    {
        int cap = GetCap(category);
        HashSet<string> locked = lockedKeys.ToHashSet();
        List<string> distinct = petKeys.Distinct().ToList();
        return distinct
            .Where(locked.Contains)
            .Concat(distinct.Where(key => !locked.Contains(key)))
            .Take(cap)
            .ToList();
    }

    private List<string> NormalizeSyncedActivePets(IEnumerable<string> petKeys, PetCategory category)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return new List<string>();
        }

        List<string> result = new();
        foreach (string key in petKeys)
        {
            if (string.IsNullOrWhiteSpace(key)
                || result.Contains(key)
                || !registry.TryResolve(key, category, out _))
            {
                continue;
            }

            result.Add(key);
            if (result.Count >= GetCap(category))
            {
                break;
            }
        }

        return result;
    }

    private bool CanAssignDye(string petKey, PetDyeData dyeData)
    {
        PetRegistry registry = PetRegistry.Instance;
        return registry != null
            && registry.TryResolve(petKey, out _)
            && IsUnlocked(petKey)
            && dyeData?.HasShader == true
            && IsDyeUnlocked(dyeData)
            && PetDyeManager.TryResolve(dyeData, out _);
    }

    private List<string> ResolvePresetPets(IEnumerable<string> petKeys, PetCategory expectedCategory, ref int missing)
    {
        PetRegistry registry = PetRegistry.Instance;
        List<string> result = new();
        foreach (string key in petKeys.Distinct())
        {
            if (registry == null
                || !registry.TryResolve(key, expectedCategory, out _)
                || !UnlockedPets.Contains(key))
            {
                missing++;
                continue;
            }

            result.Add(key);
        }

        return result;
    }

    private static IList<string> ReadStringList(TagCompound tag, string key)
    {
        return tag.ContainsKey(key) ? tag.GetList<string>(key) : new List<string>();
    }

    private static Dictionary<string, PetDyeData> LoadDyes(TagCompound tag)
    {
        Dictionary<string, PetDyeData> result = new();
        if (!tag.ContainsKey("PetDyes"))
        {
            return result;
        }

        foreach (TagCompound entry in tag.GetList<TagCompound>("PetDyes"))
        {
            string petKey = entry.ContainsKey("PetKey") ? entry.GetString("PetKey") : string.Empty;
            if (string.IsNullOrWhiteSpace(petKey) || !entry.ContainsKey("Dye"))
            {
                continue;
            }

            PetDyeData dyeData = PetDyeData.Load(entry.GetCompound("Dye"));
            if (dyeData.HasShader)
            {
                result[petKey] = dyeData;
            }
        }

        return result;
    }

    private bool ShouldOwnLocalPetState()
    {
        return Main.netMode != NetmodeID.MultiplayerClient || Player.whoAmI == Main.myPlayer;
    }

    private void SyncActivePetState()
    {
        if (Main.netMode != NetmodeID.SinglePlayer && Player.whoAmI == Main.myPlayer)
        {
            global::PetBestiary.PetBestiary.SendActivePetState(Player);
        }
    }

    private bool SyncedStateEquals(PetBestiaryPlayer other)
    {
        return other != null
            && ActiveNormalPets.SequenceEqual(other.ActiveNormalPets)
            && ActiveLightPets.SequenceEqual(other.ActiveLightPets)
            && LockedPets.SetEquals(other.LockedPets)
            && DyesEqual(PetDyes, other.PetDyes);
    }

    private static bool DyesEqual(Dictionary<string, PetDyeData> left, Dictionary<string, PetDyeData> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach ((string petKey, PetDyeData leftDye) in left)
        {
            if (!right.TryGetValue(petKey, out PetDyeData rightDye) || !DyeEquals(leftDye, rightDye))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DyeEquals(PetDyeData left, PetDyeData right)
    {
        return left?.DyeItemType == right?.DyeItemType
            && left?.DyeShaderId == right?.DyeShaderId
            && left?.DyeItemKey == right?.DyeItemKey
            && left?.DisplayName == right?.DisplayName;
    }

    private static PetDyeData CloneDye(PetDyeData dyeData)
    {
        if (dyeData == null)
        {
            return null;
        }

        return new PetDyeData
        {
            DyeItemType = dyeData.DyeItemType,
            DyeShaderId = dyeData.DyeShaderId,
            DyeItemKey = dyeData.DyeItemKey,
            DisplayName = dyeData.DisplayName
        };
    }

    private void LogDebug(string message)
    {
        if (ModContent.GetInstance<PetBestiaryConfig>().DebugLogging)
        {
            Mod.Logger.Info(message);
        }
    }
}

public readonly struct PresetLoadResult
{
    public PresetLoadResult(bool loaded, int missingCount)
    {
        Loaded = loaded;
        MissingCount = missingCount;
    }

    public bool Loaded { get; }

    public int MissingCount { get; }
}

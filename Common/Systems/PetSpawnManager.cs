using System.Collections.Generic;
using PetBestiary.Common.Configs;
using PetBestiary.Common.Globals;
using PetBestiary.Common.Players;
using PetBestiary.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace PetBestiary.Common.Systems;

public static class PetSpawnManager
{
    public static void MaintainPlayerPets(Player player, PetBestiaryPlayer petPlayer)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && player.whoAmI != Main.myPlayer)
        {
            return;
        }

        HashSet<string> activeKeys = new();
        MaintainCategory(player, petPlayer.ActiveNormalPets, activeKeys);
        MaintainCategory(player, petPlayer.ActiveLightPets, activeKeys);
        CleanupInactivePets(player, activeKeys);
    }

    public static void CleanupPlayerPets(Player player)
    {
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (IsOwnedPetBestiaryProjectile(projectile, player.whoAmI))
            {
                projectile.Kill();
            }
        }
    }

    public static void CleanupPet(Player player, string petKey)
    {
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (!IsOwnedPetBestiaryProjectile(projectile, player.whoAmI))
            {
                continue;
            }

            PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
            if (global.PetKey == petKey)
            {
                projectile.Kill();
            }
        }
    }

    public static void ClearNativePetState(Player player)
    {
        GetEquippedNativePetState(player, out HashSet<int> equippedBuffTypes, out HashSet<int> equippedProjectileTypes);
        ClearNativePetBuffs(player, equippedBuffTypes);
        ClearNativePetProjectiles(player, equippedProjectileTypes);
    }

    private static void MaintainCategory(Player player, IEnumerable<string> petKeys, HashSet<string> activeKeys)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return;
        }

        foreach (string petKey in petKeys)
        {
            if (!registry.TryGet(petKey, out PetDefinition definition))
            {
                continue;
            }

            activeKeys.Add(petKey);
            if (ModContent.GetInstance<PetBestiaryConfig>().PetRuntimeDebugMode == PetRuntimeDebugMode.MaintainActivePetBuffs && definition.BuffType > 0)
            {
                // Debug-only experiment: some vanilla pet AI expects its buff/player
                // state to exist. This can conflict with multi-pet buff behavior, so it
                // stays behind PetRuntimeDebugMode instead of becoming production logic.
                player.AddBuff(definition.BuffType, 2);
            }

            foreach (int projectileType in GetProjectileTypes(definition))
            {
                MaintainProjectile(player, petKey, projectileType);
            }
        }
    }

    private static void MaintainProjectile(Player player, string petKey, int projectileType)
    {
        if (projectileType <= 0)
        {
            return;
        }

        Projectile existing = FindOwnedProjectile(player.whoAmI, petKey, projectileType);
        if (existing != null)
        {
            // Many pet AIs rely on their buff to keep timeLeft alive. Phase 1 avoids adding
            // pet buffs because vanilla removes other pet buffs of the same category.
            existing.timeLeft = 60;
            existing.hide = false;
            return;
        }

        if (PlayerAlreadyOwnsProjectileType(player, projectileType))
        {
            return;
        }

        int projectileIndex = Projectile.NewProjectile(
            player.GetSource_Misc("PetBestiary"),
            player.Center,
            Vector2.Zero,
            projectileType,
            0,
            0f,
            player.whoAmI);

        if (projectileIndex < 0 || projectileIndex >= Main.maxProjectiles)
        {
            return;
        }

        Projectile projectile = Main.projectile[projectileIndex];
        projectile.originalDamage = 0;
        projectile.timeLeft = 60;

        PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
        global.SpawnedByPetBestiary = true;
        global.PetKey = petKey;
        projectile.netUpdate = true;
    }

    private static IEnumerable<int> GetProjectileTypes(PetDefinition definition)
    {
        if (IsSlimeRoyalsPet(definition))
        {
            return new[] { (int)ProjectileID.KingSlimePet, (int)ProjectileID.QueenSlimePet };
        }

        return new[] { definition.ProjectileType };
    }

    private static bool IsSlimeRoyalsPet(PetDefinition definition)
    {
        return definition.Key.Contains("ResplendentDessert", System.StringComparison.OrdinalIgnoreCase)
            || definition.DisplayName.Contains("Slime Royals", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanupInactivePets(Player player, HashSet<string> activeKeys)
    {
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (!IsOwnedPetBestiaryProjectile(projectile, player.whoAmI))
            {
                continue;
            }

            PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
            if (!activeKeys.Contains(global.PetKey))
            {
                projectile.Kill();
            }
        }
    }

    private static void GetEquippedNativePetState(Player player, out HashSet<int> buffTypes, out HashSet<int> projectileTypes)
    {
        buffTypes = new HashSet<int>();
        projectileTypes = new HashSet<int>();

        PetRegistry registry = PetRegistry.Instance;
        if (registry == null || player?.miscEquips == null)
        {
            return;
        }

        foreach (Item item in player.miscEquips)
        {
            if (item == null || item.IsAir || !registry.TryGetByItemType(item.type, out PetDefinition definition))
            {
                continue;
            }

            if (definition.BuffType > 0)
            {
                buffTypes.Add(definition.BuffType);
            }

            foreach (int projectileType in GetProjectileTypes(definition))
            {
                if (projectileType > ProjectileID.None)
                {
                    projectileTypes.Add(projectileType);
                }
            }
        }
    }

    private static void ClearNativePetBuffs(Player player, HashSet<int> preservedBuffTypes)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return;
        }

        for (int i = 0; i < player.buffType.Length; i++)
        {
            int buffType = player.buffType[i];
            if (buffType > 0 && !preservedBuffTypes.Contains(buffType) && registry.TryGetByBuffType(buffType, out _))
            {
                player.DelBuff(i);
                i--;
            }
        }
    }

    private static void ClearNativePetProjectiles(Player player, HashSet<int> preservedProjectileTypes)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return;
        }

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (!projectile.active || projectile.owner != player.whoAmI)
            {
                continue;
            }

            PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
            if (!global.SpawnedByPetBestiary && !preservedProjectileTypes.Contains(projectile.type) && registry.TryGetByProjectileType(projectile.type, out _))
            {
                projectile.Kill();
            }
        }
    }

    private static Projectile FindOwnedProjectile(int playerId, string petKey, int projectileType)
    {
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (!IsOwnedPetBestiaryProjectile(projectile, playerId) || projectile.type != projectileType)
            {
                continue;
            }

            PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
            if (global.PetKey == petKey)
            {
                return projectile;
            }
        }

        return null;
    }

    private static bool PlayerAlreadyOwnsProjectileType(Player player, int projectileType)
    {
        return projectileType >= 0
            && projectileType < player.ownedProjectileCounts.Length
            && player.ownedProjectileCounts[projectileType] > 0;
    }

    private static bool IsOwnedPetBestiaryProjectile(Projectile projectile, int playerId)
    {
        if (!projectile.active || projectile.owner != playerId)
        {
            return false;
        }

        PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
        return projectile.active
            && global.SpawnedByPetBestiary;
    }
}

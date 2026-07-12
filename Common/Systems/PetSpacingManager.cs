using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using PetBestiary.Common.Configs;
using PetBestiary.Common.Globals;
using PetBestiary.Common.Players;
using PetBestiary.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PetBestiary.Common.Systems;

public static class PetSpacingManager
{
    private static readonly Vector2[] FormationPattern =
    {
        Vector2.Zero,
        new(-1f, -0.45f),
        new(1f, 0.45f),
        new(1f, -0.45f),
        new(-1f, 0.45f),
        new(0f, -0.95f),
        new(0f, 0.95f),
        new(-1.45f, 0f),
        new(1.45f, 0f),
        new(-1.35f, -0.9f),
        new(1.35f, 0.9f),
        new(1.35f, -0.9f),
        new(-1.35f, 0.9f)
    };

    public static bool TryResolvePetBestiaryProjectile(Projectile projectile, out Player owner, out string petKey, out PetDefinition definition, out string failReason)
    {
        owner = null;
        petKey = string.Empty;
        definition = null;
        failReason = string.Empty;

        if (projectile == null)
        {
            failReason = "projectile is null";
            return false;
        }

        if (!projectile.active)
        {
            failReason = "projectile is inactive";
            return false;
        }

        if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
        {
            failReason = $"invalid owner {projectile.owner}";
            return false;
        }

        owner = Main.player[projectile.owner];
        if (owner == null || !owner.active)
        {
            failReason = $"owner {projectile.owner} is inactive";
            return false;
        }

        PetBestiaryPlayer petPlayer = owner.GetModPlayer<PetBestiaryPlayer>();
        PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
        if (global.SpawnedByPetBestiary && !string.IsNullOrEmpty(global.PetKey))
        {
            petKey = global.PetKey;
            if (!petPlayer.IsActive(petKey))
            {
                failReason = $"pet key is not active: {petKey}";
                return false;
            }

            if (PetRegistry.Instance == null || !PetRegistry.Instance.TryResolve(petKey, out definition))
            {
                failReason = $"pet definition resolve failed: {petKey}";
                return false;
            }

            return true;
        }

        if (TryResolveActivePetByProjectileType(projectile.type, petPlayer, out petKey, out definition))
        {
            return true;
        }

        failReason = "projectile was not tagged by Pet Bestiary and did not match an active Pet Bestiary pet type";
        return false;
    }

    private static bool TryResolveActivePetByProjectileType(int projectileType, PetBestiaryPlayer petPlayer, out string petKey, out PetDefinition definition)
    {
        petKey = string.Empty;
        definition = null;
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null || petPlayer == null)
        {
            return false;
        }

        foreach (string activePetKey in petPlayer.ActiveNormalPets.Concat(petPlayer.ActiveLightPets))
        {
            if (!registry.TryResolve(activePetKey, out PetDefinition candidate) || !UsesProjectileType(candidate, projectileType))
            {
                continue;
            }

            petKey = activePetKey;
            definition = candidate;
            return true;
        }

        return false;
    }

    private static bool UsesProjectileType(PetDefinition definition, int projectileType)
    {
        if (definition == null)
        {
            return false;
        }

        return definition.ProjectileType == projectileType
            || IsSlimeRoyalsPet(definition) && (projectileType == ProjectileID.KingSlimePet || projectileType == ProjectileID.QueenSlimePet);
    }

    private static bool IsSlimeRoyalsPet(PetDefinition definition)
    {
        return definition.Key.Contains("ResplendentDessert", StringComparison.OrdinalIgnoreCase)
            || definition.DisplayName.Contains("Slime Royals", StringComparison.OrdinalIgnoreCase);
    }

    public static int GetFormationIndex(Player player, string petKey, PetCategory category)
    {
        if (player == null || string.IsNullOrWhiteSpace(petKey))
        {
            return -1;
        }

        PetBestiaryPlayer petPlayer = player.GetModPlayer<PetBestiaryPlayer>();
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        if (config.SeparateNormalAndLightPetSpacingGroups)
        {
            return IndexOf(GetActiveList(petPlayer, category), petKey);
        }

        int normalIndex = IndexOf(petPlayer.ActiveNormalPets, petKey);
        if (normalIndex >= 0)
        {
            return normalIndex;
        }

        int lightIndex = IndexOf(petPlayer.ActiveLightPets, petKey);
        return lightIndex >= 0 ? petPlayer.ActiveNormalPets.Count + lightIndex : -1;
    }

    public static Vector2 GetDrawOffset(Projectile projectile, Player player, string petKey, PetCategory category)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        float scale = MathHelper.Clamp(config.PetSpacingPixels, 0, 200);
        if (scale <= 0f)
        {
            return Vector2.Zero;
        }

        if (UsesUnsafeCustomSpacing(projectile, petKey))
        {
            return Vector2.Zero;
        }

        int formationIndex = GetFormationIndex(player, petKey, category);
        if (formationIndex < 0)
        {
            return Vector2.Zero;
        }

        bool groundLikePet = UsesGroundSpacing(projectile);
        Vector2 offset = FormationPattern[formationIndex % FormationPattern.Length] * scale;
        int wrap = formationIndex / FormationPattern.Length;
        if (wrap > 0)
        {
            // Keep large pet counts from drifting too far away. Repeated wraps get
            // a tiny deterministic nudge instead of a physics-style separation pass.
            int clampedWrap = Math.Min(wrap, 4);
            offset += new Vector2((clampedWrap % 2 == 0 ? -1f : 1f) * scale * 0.3f, clampedWrap * scale * 0.18f);
        }

        offset += GetCategoryAnchorOffset(player, category, scale);
        if (groundLikePet)
        {
            // Ground pets already have vanilla AI keeping them on the floor. Only
            // spread them horizontally so the visual offset does not make them hover.
            offset.Y = 0f;
        }
        else
        {
            // Floating pets that normally ignore tiles should not be pushed down
            // into the ground by the formation. Sideways and upward spread are safe.
            offset.Y = Math.Min(offset.Y, 0f);
        }

        if (!config.PetSpacingAvoidSolidTiles || !projectile.tileCollide)
        {
            return offset;
        }

        return groundLikePet
            ? AvoidSolidTilesForGroundPet(projectile, offset)
            : AvoidSolidTilesForFlyingPet(projectile, offset);
    }

    private static bool UsesGroundSpacing(Projectile projectile)
    {
        return projectile?.tileCollide == true;
    }

    private static bool UsesUnsafeCustomSpacing(Projectile projectile, string petKey)
    {
        if (projectile == null || string.IsNullOrWhiteSpace(petKey) || PetRegistry.Instance == null)
        {
            return false;
        }

        if (!PetRegistry.Instance.TryResolve(petKey, out PetDefinition definition))
        {
            return false;
        }

        // These pets drive multi-part or orbital visuals from their real projectile
        // position. Draw-only offsets desynchronize heads/segments or move orbiting
        // pets off their intended center point.
        return IsCalamityPet(definition)
            && (ContainsAny(petKey, "Thief", "Dime", "Burrower", "Controller", "Worm")
                || ContainsAny(definition.DisplayName, "Thief", "Dime", "Burrower", "Controller", "Worm"));
    }

    private static bool IsCalamityPet(PetDefinition definition)
    {
        return string.Equals(definition.SourceMod, "CalamityMod", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (string needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2 AvoidSolidTilesForGroundPet(Projectile projectile, Vector2 desiredOffset)
    {
        if (projectile == null || desiredOffset == Vector2.Zero)
        {
            return desiredOffset;
        }

        if (!WouldDrawInsideSolid(projectile, desiredOffset))
        {
            return desiredOffset;
        }

        for (float factor = 0.85f; factor >= 0.05f; factor -= 0.1f)
        {
            Vector2 reduced = new(desiredOffset.X * factor, 0f);
            if (!WouldDrawInsideSolid(projectile, reduced))
            {
                return reduced;
            }
        }

        return Vector2.Zero;
    }

    private static Vector2 AvoidSolidTilesForFlyingPet(Projectile projectile, Vector2 desiredOffset)
    {
        if (projectile == null || desiredOffset == Vector2.Zero)
        {
            return desiredOffset;
        }

        if (!WouldDrawInsideSolid(projectile, desiredOffset))
        {
            return desiredOffset;
        }

        for (float lift = 8f; lift <= 96f; lift += 8f)
        {
            Vector2 lifted = desiredOffset + new Vector2(0f, -lift);
            if (!WouldDrawInsideSolid(projectile, lifted))
            {
                return lifted;
            }
        }

        for (float factor = 0.85f; factor >= 0.05f; factor -= 0.1f)
        {
            Vector2 reduced = desiredOffset * factor;
            if (!WouldDrawInsideSolid(projectile, reduced))
            {
                return reduced;
            }

            for (float lift = 8f; lift <= 96f; lift += 8f)
            {
                Vector2 lifted = reduced + new Vector2(0f, -lift);
                if (!WouldDrawInsideSolid(projectile, lifted))
                {
                    return lifted;
                }
            }
        }

        return Vector2.Zero;
    }

    private static bool WouldDrawInsideSolid(Projectile projectile, Vector2 offset)
    {
        int width = Math.Clamp(projectile.width, 8, 48);
        int height = Math.Clamp(projectile.height, 8, 48);
        Vector2 probeTopLeft = projectile.Center + offset - new Vector2(width, height) * 0.5f;
        return Collision.SolidCollision(probeTopLeft, width, height);
    }

    private static Vector2 GetCategoryAnchorOffset(Player player, PetCategory category, float scale)
    {
        if (player == null)
        {
            return Vector2.Zero;
        }

        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        if (!config.SeparateNormalAndLightPetSpacingGroups)
        {
            return Vector2.Zero;
        }

        PetBestiaryPlayer petPlayer = player.GetModPlayer<PetBestiaryPlayer>();
        if (petPlayer.ActiveNormalPets.Count <= 0 || petPlayer.ActiveLightPets.Count <= 0)
        {
            return Vector2.Zero;
        }

        float anchor = MathHelper.Clamp(scale * 0.35f, 3f, 10f);
        return category == PetCategory.Light
            ? new Vector2(anchor, -anchor)
            : new Vector2(-anchor, anchor);
    }

    private static IReadOnlyList<string> GetActiveList(PetBestiaryPlayer petPlayer, PetCategory category)
    {
        return category == PetCategory.Light ? petPlayer.ActiveLightPets : petPlayer.ActiveNormalPets;
    }

    private static int IndexOf(IReadOnlyList<string> values, string petKey)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], petKey, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

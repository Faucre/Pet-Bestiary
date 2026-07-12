using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PetBestiary.Common.Configs;
using PetBestiary.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;

namespace PetBestiary.UI;

internal static class PetIconRenderer
{
    public static void Draw(SpriteBatch spriteBatch, PetDefinition pet, Rectangle bounds, bool unlocked)
    {
        Draw(spriteBatch, pet, bounds, unlocked, false);
    }

    public static void Draw(SpriteBatch spriteBatch, PetDefinition pet, Rectangle bounds, bool unlocked, bool animated)
    {
        Draw(spriteBatch, pet, bounds, unlocked, animated, 0);
    }

    public static void Draw(SpriteBatch spriteBatch, PetDefinition pet, Rectangle bounds, bool unlocked, bool animated, int shaderId)
    {
        Draw(spriteBatch, pet, bounds, unlocked, animated, shaderId, null);
    }

    public static void Draw(SpriteBatch spriteBatch, PetDefinition pet, Rectangle bounds, bool unlocked, bool animated, int shaderId, Color? forcedColor)
    {
        Draw(spriteBatch, pet, bounds, unlocked, animated, shaderId, forcedColor, DyeRendererDebugMode.ArmorShaderGetSecondaryShaderByShaderId);
    }

    public static void Draw(SpriteBatch spriteBatch, PetDefinition pet, Rectangle bounds, bool unlocked, bool animated, int shaderId, Color? forcedColor, DyeRendererDebugMode mode)
    {
        Draw(spriteBatch, pet, bounds, unlocked, animated, shaderId, forcedColor, mode, 0);
    }

    public static void Draw(SpriteBatch spriteBatch, PetDefinition pet, Rectangle bounds, bool unlocked, bool animated, int shaderId, Color? forcedColor, DyeRendererDebugMode mode, int dyeItemType)
    {
        if (pet == null)
        {
            DrawUnknown(spriteBatch, bounds);
            return;
        }

        Color color = forcedColor ?? (unlocked ? Color.White : Color.Black * 0.85f);
        if (IsSlimeRoyalsPet(pet))
        {
            DrawSlimeRoyals(spriteBatch, bounds, color, animated, shaderId, mode, dyeItemType);
            return;
        }

        if (TryDrawPetProjectile(spriteBatch, pet, pet.ProjectileType, bounds, color, animated, shaderId, mode, dyeItemType))
        {
            return;
        }

        if (TryDrawItem(spriteBatch, pet.ItemType, bounds, color))
        {
            return;
        }

        if (TryDrawBuff(spriteBatch, pet.BuffType, bounds, color))
        {
            return;
        }

        DrawUnknown(spriteBatch, bounds);
    }

    private static bool IsSlimeRoyalsPet(PetDefinition pet)
    {
        return pet.Key.Contains("ResplendentDessert", StringComparison.OrdinalIgnoreCase)
            || pet.DisplayName.Contains("Slime Royals", StringComparison.OrdinalIgnoreCase);
    }

    private static void DrawSlimeRoyals(SpriteBatch spriteBatch, Rectangle bounds, Color color, bool animated, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        Rectangle princeBounds = new(bounds.X, bounds.Y, (int)(bounds.Width * 0.68f), bounds.Height);
        Rectangle princessBounds = new(bounds.Right - (int)(bounds.Width * 0.68f), bounds.Y, (int)(bounds.Width * 0.68f), bounds.Height);
        TryDrawPetProjectile(spriteBatch, null, ProjectileID.KingSlimePet, princeBounds, color, animated, shaderId, mode, dyeItemType);
        TryDrawPetProjectile(spriteBatch, null, ProjectileID.QueenSlimePet, princessBounds, color, animated, shaderId, mode, dyeItemType);
    }

    private static bool TryDrawPetProjectile(SpriteBatch spriteBatch, PetDefinition pet, int projectileType, Rectangle bounds, Color color, bool animated, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        return TryDrawProjectile(spriteBatch, pet, projectileType, bounds, color, animated, shaderId, mode, dyeItemType);
    }

    private static bool TryDrawProjectile(SpriteBatch spriteBatch, PetDefinition pet, int projectileType, Rectangle bounds, Color color, bool animated, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        if (projectileType <= ProjectileID.None || projectileType >= TextureAssets.Projectile.Length)
        {
            return false;
        }

        try
        {
            Main.instance.LoadProjectile(projectileType);
            Texture2D texture = TextureAssets.Projectile[projectileType].Value;
            int frameCount = Main.projFrames[projectileType] > 0 ? Main.projFrames[projectileType] : 1;
            int frameHeight = texture.Height / frameCount;
            ulong updateCount = (ulong)Main.GameUpdateCount;
            int frame = animated && frameCount > 1 ? (int)((updateCount / 8UL) % (ulong)frameCount) : 0;
            Rectangle source = new(0, frame * frameHeight, texture.Width, frameHeight);
            if (projectileType == ProjectileID.SuspiciousTentacle)
            {
                DrawSuspiciousLookingEyePreview(spriteBatch, texture, source, bounds, color, animated, shaderId, mode, dyeItemType);
                return true;
            }

            if (ShouldUseFallbackIcon(pet, projectileType, source))
            {
                return false;
            }

            DrawPreviewProjectileTexture(spriteBatch, texture, source, bounds, color, animated, projectileType, shaderId, mode, dyeItemType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldUseFallbackIcon(PetDefinition pet, int projectileType, Rectangle source)
    {
        if (source.Width <= 0 || source.Height <= 0)
        {
            return true;
        }

        float aspect = source.Height / (float)source.Width;
        if (aspect > 3.25f)
        {
            return true;
        }

        if (pet != null && pet.SourceMod != "Terraria" && aspect > 1.8f && source.Height > 64)
        {
            return true;
        }

        return false;
    }

    private static void DrawSuspiciousLookingEyePreview(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Rectangle bounds, Color color, bool animated, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        float maxSize = Math.Max(18f, Math.Min(bounds.Width, bounds.Height) - 10f);
        float scale = Math.Min(1f, maxSize / Math.Max(source.Width, source.Height));
        Vector2 center = bounds.Center.ToVector2();
        if (animated)
        {
            center.Y -= 4f + (float)Math.Sin(Main.GameUpdateCount / 18f) * 3f;
        }

        DrawTexture(spriteBatch, texture, source, center, color, 0f, scale, SpriteEffects.None, shaderId, mode, dyeItemType);

        if (color == Color.Black * 0.85f)
        {
            return;
        }

        float eyeRadius = MathHelper.Clamp(Math.Min(source.Width, source.Height) * scale * 0.16f, 3f, 8f);
        Vector2 pupilOffset = animated
            ? new Vector2((float)Math.Sin(Main.GameUpdateCount / 42f) * eyeRadius * 0.55f, (float)Math.Cos(Main.GameUpdateCount / 54f) * eyeRadius * 0.35f)
            : Vector2.Zero;
        Vector2 pupilCenter = center + pupilOffset + new Vector2(source.Width * scale * 0.02f, source.Height * scale * 0.01f);
        DrawPreviewPixel(spriteBatch, pupilCenter, eyeRadius + 2f, new Color(235, 255, 255, 220));
        DrawPreviewPixel(spriteBatch, pupilCenter, eyeRadius, new Color(40, 160, 225, 245));
        DrawPreviewPixel(spriteBatch, pupilCenter + new Vector2(eyeRadius * 0.25f, -eyeRadius * 0.25f), Math.Max(2f, eyeRadius * 0.35f), Color.White);
    }

    private static void DrawPreviewPixel(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
    {
        int pixelSize = Math.Max(1, (int)Math.Round(size));
        Rectangle bounds = new((int)Math.Round(center.X - pixelSize / 2f), (int)Math.Round(center.Y - pixelSize / 2f), pixelSize, pixelSize);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, color);
    }

    private static void DrawPreviewProjectileTexture(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Rectangle bounds, Color color, bool animated, int projectileType, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        float maxSize = Math.Max(18f, Math.Min(bounds.Width, bounds.Height) - 10f);
        float scale = Math.Min(1f, maxSize / Math.Max(source.Width, source.Height));
        Vector2 center = bounds.Center.ToVector2();
        SpriteEffects effects = SpriteEffects.None;

        if (animated)
        {
            bool floating = IsFloatingPreviewProjectile(projectileType);
            float pulse = (float)Math.Sin(Main.GameUpdateCount / 18f);
            if (floating)
            {
                center.Y -= 4f + pulse * 3f;
            }
            else
            {
                center.Y += MathHelper.Clamp(bounds.Height * 0.18f, 4f, 14f);
            }

            if ((Main.GameUpdateCount / 90) % 2 == 1)
            {
                effects = SpriteEffects.FlipHorizontally;
            }
        }

        DrawTexture(spriteBatch, texture, source, center, color, 0f, scale, effects, shaderId, mode, dyeItemType);
    }

    private static bool IsFloatingPreviewProjectile(int projectileType)
    {
        if (projectileType > ProjectileID.None && projectileType < ProjectileID.Sets.LightPet.Length && ProjectileID.Sets.LightPet[projectileType])
        {
            return true;
        }

        try
        {
            Projectile projectile = new();
            projectile.SetDefaults(projectileType);
            return !projectile.tileCollide;
        }
        catch
        {
            return true;
        }
    }

    private static bool TryDrawItem(SpriteBatch spriteBatch, int itemType, Rectangle bounds, Color color)
    {
        if (itemType <= 0 || itemType >= TextureAssets.Item.Length)
        {
            return false;
        }

        try
        {
            Main.instance.LoadItem(itemType);
            Texture2D texture = TextureAssets.Item[itemType].Value;
            Rectangle source = Main.itemAnimations[itemType]?.GetFrame(texture) ?? texture.Bounds;
            DrawTexture(spriteBatch, texture, source, bounds, color);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDrawBuff(SpriteBatch spriteBatch, int buffType, Rectangle bounds, Color color)
    {
        if (buffType <= 0 || buffType >= TextureAssets.Buff.Length)
        {
            return false;
        }

        try
        {
            Texture2D texture = TextureAssets.Buff[buffType].Value;
            DrawTexture(spriteBatch, texture, texture.Bounds, bounds, color);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DrawTexture(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Rectangle bounds, Color color)
    {
        DrawTexture(spriteBatch, texture, source, bounds, color, 0);
    }

    private static void DrawTexture(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Rectangle bounds, Color color, int shaderId)
    {
        DrawTexture(spriteBatch, texture, source, bounds, color, shaderId, DyeRendererDebugMode.ArmorShaderGetSecondaryShaderByShaderId);
    }

    private static void DrawTexture(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Rectangle bounds, Color color, int shaderId, DyeRendererDebugMode mode)
    {
        DrawTexture(spriteBatch, texture, source, bounds, color, shaderId, mode, 0);
    }

    private static void DrawTexture(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Rectangle bounds, Color color, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        float maxSize = Math.Max(18f, Math.Min(bounds.Width, bounds.Height) - 10f);
        float scale = Math.Min(1f, maxSize / Math.Max(source.Width, source.Height));
        Vector2 center = new(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
        DrawTexture(spriteBatch, texture, source, center, color, 0f, scale, SpriteEffects.None, shaderId, mode, dyeItemType);
    }

    private static void DrawTexture(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Vector2 center, Color color, float rotation, float scale, SpriteEffects effects, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        if (shaderId <= 0)
        {
            spriteBatch.Draw(texture, center, source, color, rotation, source.Size() / 2f, scale, effects, 0f);
            return;
        }

        DrawData drawData = new(texture, center, source, color, rotation, source.Size() / 2f, scale, effects, 0f)
        {
            shader = shaderId
        };
        DrawShaderImmediate(spriteBatch, drawData, shaderId, mode, dyeItemType);
    }

    private static void DrawShaderImmediate(SpriteBatch spriteBatch, DrawData drawData, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        bool spriteBatchEnded = false;
        bool drew = false;
        try
        {
            spriteBatch.End();
            spriteBatchEnded = true;
            spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.UIScaleMatrix);

            ApplyPreviewShader(drawData, shaderId, mode, dyeItemType);
            drawData.Draw(spriteBatch);
            drew = true;
        }
        catch
        {
            drew = false;
        }
        finally
        {
            if (spriteBatchEnded)
            {
                spriteBatch.End();
                spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise,
                    null,
                    Main.UIScaleMatrix);
            }
        }

        if (!drew)
        {
            drawData.Draw(spriteBatch);
        }
    }

    private static void ApplyPreviewShader(DrawData drawData, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        switch (mode)
        {
            case DyeRendererDebugMode.ArmorShaderApplyByShaderId:
                GameShaders.Armor.Apply(shaderId, Main.LocalPlayer, drawData);
                break;
            case DyeRendererDebugMode.ArmorShaderApplySecondaryByShaderId:
                GameShaders.Armor.ApplySecondary(shaderId, Main.LocalPlayer, drawData);
                break;
            case DyeRendererDebugMode.ArmorShaderGetSecondaryShaderByShaderId:
                GameShaders.Armor.GetSecondaryShader(shaderId, Main.LocalPlayer).Apply(Main.LocalPlayer, drawData);
                break;
            case DyeRendererDebugMode.ArmorShaderFromItemType:
                GameShaders.Armor.GetShaderFromItemId(dyeItemType)?.Apply(Main.LocalPlayer, drawData);
                break;
        }
    }

    private static void DrawUnknown(SpriteBatch spriteBatch, Rectangle bounds)
    {
        int size = Math.Min(32, Math.Min(bounds.Width, bounds.Height) - 10);
        Rectangle iconBounds = new(bounds.Center.X - size / 2, bounds.Center.Y - size / 2, size, size);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, iconBounds, Color.Black * 0.75f);
        Utils.DrawBorderString(spriteBatch, "?", new Vector2(bounds.Center.X - 5f, bounds.Center.Y - 9f), Color.Gray, 0.8f);
    }
}

using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PetBestiary.Common.Configs;
using PetBestiary.Common.Globals;
using PetBestiary.Common.Players;
using PetBestiary.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace PetBestiary.Common.Systems;

public static class PetDyeManager
{
    private const ulong DiagnosticLogIntervalTicks = 120;
    private const DyeRendererDebugMode ManualShaderMode = DyeRendererDebugMode.ArmorShaderApplyByShaderId;
    private static ulong nextDiagnosticLogTick;
    private static readonly Dictionary<string, ulong> nextThiefsDimeProjectileLogTicks = new();

    public static void LoadHooks()
    {
        if (!Main.dedServ)
        {
            On_Main.DrawProjectiles += DrawProjectilesWithUniformDyeFallback;
            On_Main.DrawProj += DrawProjWithVanillaPreservingDye;
        }
    }

    public static void UnloadHooks()
    {
        if (!Main.dedServ)
        {
            On_Main.DrawProj -= DrawProjWithVanillaPreservingDye;
            On_Main.DrawProjectiles -= DrawProjectilesWithUniformDyeFallback;
        }

        nextThiefsDimeProjectileLogTicks.Clear();
    }

    public static bool TryCreateFromItem(Item item, out PetDyeData dyeData)
    {
        dyeData = null;
        if (item == null || item.IsAir || item.dye <= 0)
        {
            return false;
        }

        dyeData = new PetDyeData
        {
            DyeItemType = item.type,
            DyeShaderId = item.dye,
            DyeItemKey = GetItemKey(item),
            DisplayName = item.Name
        };
        return true;
    }

    public static bool TryCreateFromItemType(int itemType, out PetDyeData dyeData)
    {
        dyeData = null;
        if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount)
        {
            return false;
        }

        Item item = new();
        item.SetDefaults(itemType);
        return TryCreateFromItem(item, out dyeData);
    }

    public static bool TryResolve(PetDyeData dyeData, out int shaderId)
    {
        shaderId = 0;
        if (dyeData == null || dyeData.DyeShaderId <= 0)
        {
            return false;
        }

        if (TryResolveItemType(dyeData, out int itemType))
        {
            Item item = new();
            item.SetDefaults(itemType);
            if (!item.IsAir && item.dye > 0)
            {
                shaderId = item.dye;
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveItemType(PetDyeData dyeData, out int itemType)
    {
        itemType = 0;
        if (dyeData == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dyeData.DyeItemKey))
        {
            return TryResolveItemKey(dyeData.DyeItemKey, out itemType);
        }

        if (dyeData.DyeItemType > ItemID.None && dyeData.DyeItemType < ItemLoader.ItemCount)
        {
            Item item = new();
            item.SetDefaults(dyeData.DyeItemType);
            if (!item.IsAir && item.dye > 0)
            {
                itemType = dyeData.DyeItemType;
                return true;
            }
        }

        return false;
    }

    private static void DrawProjectilesWithUniformDyeFallback(On_Main.orig_DrawProjectiles orig, Main self)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        if (!config.EnablePerPetDye || config.DyeRenderMode != DyeRenderMode.VanillaPreserving)
        {
            orig(self);
            return;
        }

        int[] originalPetShaders = new int[Main.maxPlayers];
        int[] originalLightShaders = new int[Main.maxPlayers];
        bool[] changed = new bool[Main.maxPlayers];

        try
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player == null || !player.active)
                {
                    continue;
                }

                PetBestiaryPlayer petPlayer = player.GetModPlayer<PetBestiaryPlayer>();
                if (HasThiefsDimeActive(petPlayer, out string thiefPetKey, out string thiefDisplayName))
                {
                    bool wouldSetPet = TryGetCategoryFallbackDyeShader(petPlayer, PetCategory.Normal, out int wouldPetShader, logEnabled: false);
                    bool wouldSetLight = TryGetCategoryFallbackDyeShader(petPlayer, PetCategory.Light, out int wouldLightShader, logEnabled: false);
                    LogThiefsDimeDiagnostic(player, thiefPetKey, thiefDisplayName, $"category fallback disabled; wouldSetPet={wouldSetPet}, wouldPetShader={wouldPetShader}, wouldSetLight={wouldSetLight}, wouldLightShader={wouldLightShader}, currentPet={player.cPet}, currentLight={player.cLight}");
                    continue;
                }

                bool setPet = TryGetCategoryFallbackDyeShader(petPlayer, PetCategory.Normal, out int petShader);
                bool setLight = TryGetCategoryFallbackDyeShader(petPlayer, PetCategory.Light, out int lightShader);
                if (!setPet && !setLight)
                {
                    continue;
                }

                originalPetShaders[i] = player.cPet;
                originalLightShaders[i] = player.cLight;
                changed[i] = true;

                if (setPet)
                {
                    player.cPet = petShader;
                }

                if (setLight)
                {
                    player.cLight = lightShader;
                }
            }

            orig(self);
        }
        finally
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (!changed[i])
                {
                    continue;
                }

                Player player = Main.player[i];
                if (player == null)
                {
                    continue;
                }

                player.cPet = originalPetShaders[i];
                player.cLight = originalLightShaders[i];
            }
        }
    }

    private static bool HasThiefsDimeActive(PetBestiaryPlayer petPlayer, out string matchedPetKey, out string matchedDisplayName)
    {
        matchedPetKey = string.Empty;
        matchedDisplayName = string.Empty;
        if (petPlayer == null || PetRegistry.Instance == null)
        {
            return false;
        }

        foreach (string petKey in petPlayer.ActiveNormalPets.Concat(petPlayer.ActiveLightPets))
        {
            if (!PetRegistry.Instance.TryResolve(petKey, out PetDefinition definition) || definition.SourceMod != "CalamityMod")
            {
                continue;
            }

            bool keyMatches = petKey.Contains("Thief", System.StringComparison.OrdinalIgnoreCase)
                || petKey.Contains("Dime", System.StringComparison.OrdinalIgnoreCase);
            bool nameMatches = definition.DisplayName.Contains("Thief", System.StringComparison.OrdinalIgnoreCase)
                || definition.DisplayName.Contains("Dime", System.StringComparison.OrdinalIgnoreCase);
            if (keyMatches || nameMatches)
            {
                matchedPetKey = petKey;
                matchedDisplayName = definition.DisplayName;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetCategoryFallbackDyeShader(PetBestiaryPlayer petPlayer, PetCategory category, out int shaderId, bool logEnabled = true)
    {
        shaderId = 0;
        if (petPlayer == null)
        {
            return false;
        }

        var activePets = category == PetCategory.Light ? petPlayer.ActiveLightPets : petPlayer.ActiveNormalPets;
        if (activePets.Count <= 0)
        {
            return false;
        }

        Dictionary<int, int> shaderCounts = new();
        int dyedPets = 0;
        foreach (string petKey in activePets)
        {
            if (!petPlayer.TryGetDye(petKey, out PetDyeData dyeData) || !TryResolve(dyeData, out int petShaderId))
            {
                // This full DrawProjectiles fallback exists for vanilla projectile
                // draw paths that cache DrawData and read Player.cPet/cLight later.
                // A single undyed or odd modded pet should not disable the category
                // fallback for every other pet that shares a dye.
                continue;
            }

            dyedPets++;
            shaderCounts.TryGetValue(petShaderId, out int count);
            shaderCounts[petShaderId] = count + 1;
        }

        if (dyedPets <= 0 || shaderCounts.Count <= 0)
        {
            return false;
        }

        shaderId = shaderCounts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .First()
            .Key;

        bool enabled = shaderId > 0;
        if (enabled && logEnabled)
        {
            string mode = shaderCounts.Count > 1 ? "dominant mixed-shader" : "uniform";
            LogCategoryFallbackDiagnostic(category, $"enabled: mode={mode}, shader={shaderId}, dyedPets={dyedPets}, activePets={activePets.Count}");
        }

        return enabled;
    }

    public static bool PreDrawPetProjectile(Projectile projectile, Color lightColor)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        DyeRendererDebugMode mode = config.DyeRendererDebugMode;
        bool hasDiagnosticOverride = IsDiagnosticOverrideMode(mode);
        if ((!config.EnablePerPetDye || config.DyeRenderMode == DyeRenderMode.Off) && !hasDiagnosticOverride)
        {
            return true;
        }

        bool matched = TryResolvePetBestiaryProjectile(projectile, out Player owner, out string petKey, out PetDyeData dyeData, out string failReason);
        if (IsProjectileDiagnosticMode(mode) || IsArmorShaderMode(mode) || IsVanillaHookDiagnosticMode(mode))
        {
            LogProjectileDiagnostic(projectile, mode, matched, owner, petKey, dyeData, failReason);
        }

        if (!matched)
        {
            return true;
        }

        if (mode == DyeRendererDebugMode.HideMatchedProjectile)
        {
            return false;
        }

        if (mode == DyeRendererDebugMode.ForceLimeProjectile)
        {
            return !TryDrawProjectileSprite(projectile, lightColor, Color.Lime, 0, owner, petKey);
        }

        if (IsArmorShaderMode(mode))
        {
            return DrawManualShaderOrVanilla(projectile, lightColor, owner, petKey, dyeData, mode, logDiagnostics: true);
        }

        if (IsVanillaHookDiagnosticMode(mode))
        {
            return true;
        }

        if (config.DyeRenderMode == DyeRenderMode.ManualShaderExperimental)
        {
            // Experimental fallback. This path replaces vanilla projectile drawing,
            // so pets with custom draw behavior can lose offsets, extra layers, or
            // special animation. Prefer VanillaPreserving whenever it works.
            return DrawManualShaderOrVanilla(projectile, lightColor, owner, petKey, dyeData, ManualShaderMode, logDiagnostics: false);
        }

        return true;
    }

    private static void DrawProjWithVanillaPreservingDye(On_Main.orig_DrawProj orig, Main self, int i)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        DyeRendererDebugMode debugMode = config.DyeRendererDebugMode;
        bool vanillaHookDiagnostic = IsVanillaHookDiagnosticMode(debugMode);
        bool vanillaDyeEnabled = config.EnablePerPetDye && config.DyeRenderMode == DyeRenderMode.VanillaPreserving;
        bool spacingEnabled = config.PetSpacingPixels > 0;
        if ((!vanillaDyeEnabled && !vanillaHookDiagnostic && !spacingEnabled) || i < 0 || i >= Main.projectile.Length)
        {
            orig(self, i);
            return;
        }

        Projectile projectile = Main.projectile[i];
        bool matched = PetSpacingManager.TryResolvePetBestiaryProjectile(projectile, out Player owner, out string petKey, out PetDefinition definition, out string failReason);
        PetDyeData dyeData = null;
        bool hasDye = matched && owner.GetModPlayer<PetBestiaryPlayer>().TryGetDye(petKey, out dyeData);
        int shaderId = 0;
        string invalidShaderReason = string.Empty;
        bool validShader = hasDye && TryValidateShaderMode(ManualShaderMode, dyeData, out shaderId, out invalidShaderReason);
        if (!matched)
        {
            if (debugMode != DyeRendererDebugMode.Off || IsTaggedPetBestiaryProjectile(projectile))
            {
                LogVanillaHookDiagnostic(projectile, debugMode, matched: false, petKey, dyeData, shaderId, definition, failReason);
            }

            orig(self, i);
            return;
        }

        Vector2 drawOffset = spacingEnabled
            ? PetSpacingManager.GetDrawOffset(projectile, owner, petKey, definition.Category)
            : Vector2.Zero;

        if (debugMode == DyeRendererDebugMode.VanillaHookLogOnly)
        {
            LogVanillaHookDiagnostic(projectile, debugMode, matched: true, petKey, dyeData, shaderId, definition, $"log only; calling vanilla without shader override; drawOffset=({drawOffset.X:0.##},{drawOffset.Y:0.##})");
            DrawOriginalWithScreenOffset(orig, self, i, drawOffset);
            return;
        }

        int originalPetShader = owner.cPet;
        int originalLightShader = owner.cLight;
        try
        {
            bool setPetField = vanillaDyeEnabled && validShader && definition.Category != PetCategory.Light;
            bool setLightField = vanillaDyeEnabled && validShader && definition.Category == PetCategory.Light;
            if (debugMode == DyeRendererDebugMode.VanillaHookForcePetField)
            {
                setPetField = validShader;
                setLightField = false;
            }
            else if (debugMode == DyeRendererDebugMode.VanillaHookForceLightField)
            {
                setPetField = false;
                setLightField = validShader;
            }
            else if (debugMode == DyeRendererDebugMode.VanillaHookForceBothFields)
            {
                setPetField = validShader;
                setLightField = validShader;
            }

            if (setPetField)
            {
                owner.cPet = shaderId;
            }

            if (setLightField)
            {
                owner.cLight = shaderId;
            }

            string shaderState = hasDye
                ? validShader ? "valid dye shader" : invalidShaderReason
                : "no assigned dye";
            string thiefState = HasThiefsDimeActive(owner.GetModPlayer<PetBestiaryPlayer>(), out string thiefPetKey, out string thiefDisplayName)
                ? $"thiefActive=true, thiefPetKey={thiefPetKey}, thiefName={thiefDisplayName}"
                : "thiefActive=false";
            LogVanillaHookDiagnostic(projectile, debugMode, matched: true, petKey, dyeData, shaderId, definition, $"setPet={setPetField}, setLight={setLightField}, originalPet={originalPetShader}, originalLight={originalLightShader}, shaderState={shaderState}, drawOffset=({drawOffset.X:0.##},{drawOffset.Y:0.##}), {thiefState}");
            if (thiefState.StartsWith("thiefActive=true", System.StringComparison.Ordinal))
            {
                LogThiefsDimeProjectileDiagnostic(projectile, owner, petKey, definition, dyeData, shaderId, validShader, setPetField, setLightField, originalPetShader, originalLightShader, drawOffset);
            }

            DrawOriginalWithScreenOffset(orig, self, i, drawOffset);
        }
        finally
        {
            owner.cPet = originalPetShader;
            owner.cLight = originalLightShader;
        }
    }

    private static void DrawOriginalWithScreenOffset(On_Main.orig_DrawProj orig, Main self, int projectileIndex, Vector2 drawOffset)
    {
        if (drawOffset == Vector2.Zero)
        {
            orig(self, projectileIndex);
            return;
        }

        Projectile projectile = Main.projectile[projectileIndex];
        Vector2 originalPosition = projectile.position;
        Vector2[] originalOldPositions = null;
        if (projectile.oldPos != null && projectile.oldPos.Length > 0)
        {
            originalOldPositions = new Vector2[projectile.oldPos.Length];
            projectile.oldPos.CopyTo(originalOldPositions, 0);
        }

        try
        {
            // Draw-only spacing fallback for tModLoader 1.4.4: DrawProj does not
            // expose a draw-position hook for vanilla projectiles. Keep this scoped
            // to the draw call and restore immediately so AI/physics never see it.
            projectile.position += drawOffset;
            if (projectile.oldPos != null)
            {
                for (int i = 0; i < projectile.oldPos.Length; i++)
                {
                    projectile.oldPos[i] += drawOffset;
                }
            }

            orig(self, projectileIndex);
        }
        finally
        {
            projectile.position = originalPosition;
            if (originalOldPositions != null)
            {
                for (int i = 0; i < originalOldPositions.Length; i++)
                {
                    projectile.oldPos[i] = originalOldPositions[i];
                }
            }
        }
    }

    public static bool TryResolvePetBestiaryProjectile(Projectile projectile, out Player owner, out string petKey, out PetDyeData dyeData, out string failReason)
    {
        owner = null;
        petKey = string.Empty;
        dyeData = null;
        failReason = string.Empty;

        if (!PetSpacingManager.TryResolvePetBestiaryProjectile(projectile, out owner, out petKey, out _, out failReason))
        {
            return false;
        }

        PetBestiaryPlayer petPlayer = owner.GetModPlayer<PetBestiaryPlayer>();
        if (!petPlayer.TryGetDye(petKey, out dyeData))
        {
            failReason = $"no assigned dye for pet key: {petKey}";
            return false;
        }

        return true;
    }

    private static bool TryDrawProjectileSprite(Projectile projectile, Color lightColor, Color forcedColor, int shaderId, Player owner, string petKey, DyeRendererDebugMode mode = DyeRendererDebugMode.ForceLimeProjectile, int dyeItemType = ItemID.None, bool logDrawFailures = true, Vector2 drawOffset = default)
    {
        if (projectile.type <= ProjectileID.None || projectile.type >= TextureAssets.Projectile.Length)
        {
            if (logDrawFailures)
            {
                LogDebug($"Pet dye diagnostic draw skipped for {petKey}: invalid projectile type {projectile.type}.");
            }

            return false;
        }

        try
        {
            Main.instance.LoadProjectile(projectile.type);
            Texture2D texture = TextureAssets.Projectile[projectile.type].Value;
            int frameCount = Main.projFrames[projectile.type] > 0 ? Main.projFrames[projectile.type] : 1;
            int frameHeight = texture.Height / frameCount;
            Rectangle source = new(0, projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = source.Size() / 2f;
            SpriteEffects effects = projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawPosition = projectile.Center - Main.screenPosition + new Vector2(0f, projectile.gfxOffY) + drawOffset;
            Color drawColor = shaderId > 0 ? lightColor : forcedColor;

            DrawData drawData = new(texture, drawPosition, source, drawColor, projectile.rotation, origin, projectile.scale, effects, 0f)
            {
                shader = shaderId
            };

            if (!IsArmorShaderMode(mode))
            {
                drawData.Draw(Main.spriteBatch);
                return true;
            }

            DrawShaderImmediate(projectile, owner, drawData, shaderId, mode, dyeItemType);
            return true;
        }
        catch (System.Exception exception)
        {
            if (logDrawFailures)
            {
                LogDebug($"Pet dye diagnostic draw skipped for {petKey}: {exception.GetType().Name}: {exception.Message}");
            }

            return false;
        }
    }

    private static bool DrawManualShaderOrVanilla(Projectile projectile, Color lightColor, Player owner, string petKey, PetDyeData dyeData, DyeRendererDebugMode mode, bool logDiagnostics)
    {
        if (!TryValidateShaderMode(mode, dyeData, out int shaderId, out string invalidReason))
        {
            if (logDiagnostics)
            {
                LogArmorShaderDiagnostic(petKey, projectile, dyeData, mode, ShaderPathName(mode), immediateMode: true, vanillaSuppressed: false, invalidReason);
            }

            return true;
        }

        bool drawn = TryDrawProjectileSprite(projectile, lightColor, Color.White, shaderId, owner, petKey, mode, dyeData.DyeItemType, logDiagnostics);
        if (logDiagnostics)
        {
            LogArmorShaderDiagnostic(petKey, projectile, dyeData, mode, ShaderPathName(mode), immediateMode: true, vanillaSuppressed: drawn, drawn ? "manual draw succeeded" : "manual draw failed");
        }

        return !drawn;
    }

    private static void DrawShaderImmediate(Projectile projectile, Player owner, DrawData drawData, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        bool immediateBegun = false;
        try
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.GameViewMatrix.TransformationMatrix);
            immediateBegun = true;

            ApplyShaderPath(projectile, owner, drawData, shaderId, mode, dyeItemType);
            drawData.Draw(Main.spriteBatch);
        }
        finally
        {
            if (immediateBegun)
            {
                Main.spriteBatch.End();
            }

            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.GameViewMatrix.TransformationMatrix);
        }
    }

    private static void ApplyShaderPath(Projectile projectile, Player owner, DrawData drawData, int shaderId, DyeRendererDebugMode mode, int dyeItemType)
    {
        switch (mode)
        {
            case DyeRendererDebugMode.ArmorShaderApplyByShaderId:
                GameShaders.Armor.Apply(shaderId, projectile, drawData);
                break;
            case DyeRendererDebugMode.ArmorShaderApplySecondaryByShaderId:
                GameShaders.Armor.ApplySecondary(shaderId, projectile, drawData);
                break;
            case DyeRendererDebugMode.ArmorShaderGetSecondaryShaderByShaderId:
                GameShaders.Armor.GetSecondaryShader(shaderId, owner).Apply(projectile, drawData);
                break;
            case DyeRendererDebugMode.ArmorShaderFromItemType:
                GameShaders.Armor.GetShaderFromItemId(dyeItemType).Apply(projectile, drawData);
                break;
        }
    }

    private static bool TryValidateShaderMode(DyeRendererDebugMode mode, PetDyeData dyeData, out int shaderId, out string invalidReason)
    {
        shaderId = 0;
        invalidReason = string.Empty;
        if (dyeData == null)
        {
            invalidReason = "missing dye data";
            return false;
        }

        shaderId = dyeData.DyeShaderId;
        if (shaderId <= 0)
        {
            invalidReason = $"invalid dye shader ID {shaderId}";
            return false;
        }

        if (mode == DyeRendererDebugMode.ArmorShaderFromItemType)
        {
            if (dyeData.DyeItemType <= ItemID.None)
            {
                invalidReason = $"invalid dye item type {dyeData.DyeItemType}";
                return false;
            }

            if (GameShaders.Armor.GetShaderFromItemId(dyeData.DyeItemType) == null)
            {
                invalidReason = $"GetShaderFromItemId returned null for item {dyeData.DyeItemType}";
                return false;
            }

            return true;
        }

        return true;
    }

    private static bool IsArmorShaderMode(DyeRendererDebugMode mode)
    {
        return mode == DyeRendererDebugMode.ArmorShaderApplyByShaderId
            || mode == DyeRendererDebugMode.ArmorShaderApplySecondaryByShaderId
            || mode == DyeRendererDebugMode.ArmorShaderGetSecondaryShaderByShaderId
            || mode == DyeRendererDebugMode.ArmorShaderFromItemType;
    }

    private static bool IsProjectileDiagnosticMode(DyeRendererDebugMode mode)
    {
        return mode == DyeRendererDebugMode.HideMatchedProjectile
            || mode == DyeRendererDebugMode.ForceLimeProjectile;
    }

    private static bool IsVanillaHookDiagnosticMode(DyeRendererDebugMode mode)
    {
        return mode == DyeRendererDebugMode.VanillaHookLogOnly
            || mode == DyeRendererDebugMode.VanillaHookForcePetField
            || mode == DyeRendererDebugMode.VanillaHookForceLightField
            || mode == DyeRendererDebugMode.VanillaHookForceBothFields;
    }

    private static bool IsDiagnosticOverrideMode(DyeRendererDebugMode mode)
    {
        return IsProjectileDiagnosticMode(mode)
            || IsArmorShaderMode(mode)
            || IsVanillaHookDiagnosticMode(mode)
            || mode == DyeRendererDebugMode.ForceLimePreview;
    }

    private static string ShaderPathName(DyeRendererDebugMode mode)
    {
        return mode switch
        {
            DyeRendererDebugMode.ArmorShaderApplyByShaderId => "GameShaders.Armor.Apply(dyeShaderId, projectile, drawData)",
            DyeRendererDebugMode.ArmorShaderApplySecondaryByShaderId => "GameShaders.Armor.ApplySecondary(dyeShaderId, projectile, drawData)",
            DyeRendererDebugMode.ArmorShaderGetSecondaryShaderByShaderId => "GameShaders.Armor.GetSecondaryShader(dyeShaderId, owner).Apply(projectile, drawData)",
            DyeRendererDebugMode.ArmorShaderFromItemType => "GameShaders.Armor.GetShaderFromItemId(dyeItemType).Apply(projectile, drawData)",
            _ => "None"
        };
    }

    private static void LogProjectileDiagnostic(Projectile projectile, DyeRendererDebugMode mode, bool matched, Player owner, string petKey, PetDyeData dyeData, string failReason)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        if (!config.DebugLogging || Main.GameUpdateCount < nextDiagnosticLogTick)
        {
            return;
        }

        bool shouldLog = matched;
        if (!shouldLog && projectile != null && projectile.active)
        {
            PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
            shouldLog = global.SpawnedByPetBestiary || !string.IsNullOrEmpty(global.PetKey);
        }

        if (!shouldLog)
        {
            return;
        }

        nextDiagnosticLogTick = Main.GameUpdateCount + DiagnosticLogIntervalTicks;
        string projectileName = projectile != null && projectile.type > ProjectileID.None && projectile.type < ProjectileID.Count
            ? Lang.GetProjectileName(projectile.type).Value
            : "Unknown";
        string dyeItemType = dyeData != null ? dyeData.DyeItemType.ToString() : "none";
        string dyeShaderId = dyeData != null ? dyeData.DyeShaderId.ToString() : "none";
        string ownerId = owner != null ? owner.whoAmI.ToString() : projectile?.owner.ToString() ?? "none";
        string result = matched ? $"matched petKey={petKey}" : $"failed: {failReason}";

        LogDebug($"Pet dye diagnostic: mode={mode}, projectileType={projectile?.type ?? -1}, projectileName={projectileName}, owner={ownerId}, {result}, dyeItemType={dyeItemType}, dyeShaderId={dyeShaderId}");
    }

    private static void LogArmorShaderDiagnostic(string petKey, Projectile projectile, PetDyeData dyeData, DyeRendererDebugMode mode, string shaderPath, bool immediateMode, bool vanillaSuppressed, string result)
    {
        if (!ModContent.GetInstance<PetBestiaryConfig>().DebugLogging)
        {
            return;
        }

        LogDebug($"Pet dye armor diagnostic: petKey={petKey}, projectileType={projectile?.type ?? -1}, dyeItemType={dyeData?.DyeItemType ?? 0}, dyeShaderId={dyeData?.DyeShaderId ?? 0}, mode={mode}, shaderPath={shaderPath}, immediateMode={immediateMode}, vanillaSuppressed={vanillaSuppressed}, result={result}");
    }

    private static void LogVanillaHookDiagnostic(Projectile projectile, DyeRendererDebugMode mode, bool matched, string petKey, PetDyeData dyeData, int shaderId, PetDefinition definition, string result)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        if (!config.DebugLogging || Main.GameUpdateCount < nextDiagnosticLogTick)
        {
            return;
        }

        nextDiagnosticLogTick = Main.GameUpdateCount + DiagnosticLogIntervalTicks;
        string projectileName = projectile != null && projectile.type > ProjectileID.None && projectile.type < ProjectileID.Count
            ? Lang.GetProjectileName(projectile.type).Value
            : "Unknown";
        string category = definition != null ? definition.Category.ToString() : "Unknown";
        string state = projectile != null
            ? $"frame={projectile.frame}, frameCounter={projectile.frameCounter}, ai=({projectile.ai[0]:0.###},{projectile.ai[1]:0.###}), localAI=({projectile.localAI[0]:0.###},{projectile.localAI[1]:0.###}), center=({projectile.Center.X:0.##},{projectile.Center.Y:0.##}), velocity=({projectile.velocity.X:0.###},{projectile.velocity.Y:0.###}), timeLeft={projectile.timeLeft}"
            : "projectile=null";

        LogDebug($"Pet dye vanilla hook diagnostic: mode={mode}, matched={matched}, petKey={petKey}, projectileType={projectile?.type ?? -1}, projectileName={projectileName}, category={category}, dyeItemType={dyeData?.DyeItemType ?? 0}, dyeShaderId={dyeData?.DyeShaderId ?? 0}, appliedShaderId={shaderId}, result={result}, {state}");
    }

    private static void LogCategoryFallbackDiagnostic(PetCategory category, string result)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        if (!config.DebugLogging || Main.GameUpdateCount < nextDiagnosticLogTick)
        {
            return;
        }

        nextDiagnosticLogTick = Main.GameUpdateCount + DiagnosticLogIntervalTicks;
        LogDebug($"Pet dye category fallback: category={category}, result={result}");
    }

    private static void LogThiefsDimeDiagnostic(Player player, string thiefPetKey, string thiefDisplayName, string result)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        if (!config.DebugLogging || Main.GameUpdateCount < nextDiagnosticLogTick)
        {
            return;
        }

        nextDiagnosticLogTick = Main.GameUpdateCount + DiagnosticLogIntervalTicks;
        LogDebug($"Pet dye Thief's Dime diagnostic: player={player?.whoAmI ?? -1}, thiefPetKey={thiefPetKey}, thiefName={thiefDisplayName}, result={result}");
    }

    private static void LogThiefsDimeProjectileDiagnostic(Projectile projectile, Player owner, string petKey, PetDefinition definition, PetDyeData dyeData, int shaderId, bool validShader, bool setPetField, bool setLightField, int originalPetShader, int originalLightShader, Vector2 drawOffset)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        if (!config.DebugLogging || projectile == null || owner == null)
        {
            return;
        }

        string logKey = $"{owner.whoAmI}:{petKey}:{projectile.type}";
        if (nextThiefsDimeProjectileLogTicks.TryGetValue(logKey, out ulong nextLogTick) && Main.GameUpdateCount < nextLogTick)
        {
            return;
        }

        nextThiefsDimeProjectileLogTicks[logKey] = Main.GameUpdateCount + DiagnosticLogIntervalTicks;
        string projectileName = projectile.type > ProjectileID.None && projectile.type < ProjectileID.Count
            ? Lang.GetProjectileName(projectile.type).Value
            : "ModdedOrUnknown";
        bool vanillaPetSet = projectile.type >= ProjectileID.None && projectile.type < Main.projPet.Length && Main.projPet[projectile.type];
        bool lightPetSet = projectile.type >= ProjectileID.None && projectile.type < ProjectileID.Sets.LightPet.Length && ProjectileID.Sets.LightPet[projectile.type];
        PetBestiaryPlayer petPlayer = owner.GetModPlayer<PetBestiaryPlayer>();

        LogDebug($"Pet dye Thief's Dime projectile trace: owner={owner.whoAmI}, petKey={petKey}, displayName={definition.DisplayName}, category={definition.Category}, projectileType={projectile.type}, projectileName={projectileName}, vanillaPetSet={vanillaPetSet}, lightPetSet={lightPetSet}, dyeItemType={dyeData?.DyeItemType ?? 0}, dyeShaderId={dyeData?.DyeShaderId ?? 0}, appliedShaderId={shaderId}, validShader={validShader}, setPet={setPetField}, setLight={setLightField}, originalPet={originalPetShader}, originalLight={originalLightShader}, currentPet={owner.cPet}, currentLight={owner.cLight}, drawOffset=({drawOffset.X:0.##},{drawOffset.Y:0.##}), frame={projectile.frame}, frameCounter={projectile.frameCounter}, ai=({projectile.ai[0]:0.###},{projectile.ai[1]:0.###}), localAI=({projectile.localAI[0]:0.###},{projectile.localAI[1]:0.###}), oldPosLength={projectile.oldPos?.Length ?? 0}, activeNormal={petPlayer.ActiveNormalPets.Count}, activeLight={petPlayer.ActiveLightPets.Count}");
    }

    private static bool IsTaggedPetBestiaryProjectile(Projectile projectile)
    {
        if (projectile == null || !projectile.active)
        {
            return false;
        }

        PetBestiaryGlobalProjectile global = projectile.GetGlobalProjectile<PetBestiaryGlobalProjectile>();
        return global.SpawnedByPetBestiary || !string.IsNullOrEmpty(global.PetKey);
    }

    public static string GetItemKey(Item item)
    {
        ModItem modItem = item.ModItem;
        if (modItem != null)
        {
            return $"{modItem.Mod.Name}/{modItem.Name}";
        }

        return $"Terraria/{ItemID.Search.GetName(item.type)}";
    }

    public static bool TryResolveItemKey(string itemKey, out int itemType)
    {
        itemType = 0;
        if (string.IsNullOrWhiteSpace(itemKey))
        {
            return false;
        }

        string[] parts = itemKey.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[0] == "Terraria")
        {
            return ItemID.Search.TryGetId(parts[1], out itemType);
        }

        if (ModLoader.TryGetMod(parts[0], out Mod mod) && mod.TryFind(parts[1], out ModItem modItem))
        {
            itemType = modItem.Type;
            return true;
        }

        return false;
    }

    private static void LogDebug(string message)
    {
        if (ModContent.GetInstance<PetBestiaryConfig>().DebugLogging)
        {
            ModContent.GetInstance<PetBestiary>().Logger.Info(message);
        }
    }
}

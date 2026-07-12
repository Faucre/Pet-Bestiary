using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace PetBestiary.Common.Configs;

public sealed class PetBestiaryConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [DefaultValue(1)]
    [Range(0, 9999)]
    public int MaxNormalPets { get; set; }

    [DefaultValue(1)]
    [Range(0, 9999)]
    public int MaxLightPets { get; set; }

    [DefaultValue(false)]
    public bool EnableProgressionMode { get; set; }

    [DefaultValue(true)]
    public bool ProgressionAffectsNormalPets { get; set; }

    [DefaultValue(true)]
    public bool ProgressionAffectsLightPets { get; set; }

    [DefaultValue(1)]
    [Range(0, 9999)]
    public int BaseNormalPetSlots { get; set; }

    [DefaultValue(1)]
    [Range(0, 9999)]
    public int BaseLightPetSlots { get; set; }

    [DefaultValue(true)]
    public bool EnableModdedPetDiscovery { get; set; }

    [DefaultValue(true)]
    // Master switch for per-pet dye rendering. When false, Pet Bestiary never
    // replaces pet projectile drawing for dyes.
    public bool EnablePerPetDye { get; set; }

    [DefaultValue(true)]
    public bool UseConservativeDyeRendering { get; set; }

    [DefaultValue(DyeRenderMode.VanillaPreserving)]
    // VanillaPreserving temporarily applies the dye to Player.cPet/cLight while
    // vanilla draws the projectile. ManualShaderExperimental replaces vanilla
    // drawing and can break special pet animation or custom draw layers.
    public DyeRenderMode DyeRenderMode { get; set; }

    [DefaultValue(DyeRendererDebugMode.Off)]
    // Off means no diagnostic override. Other values are for testing projectile
    // matching, vanilla dye fields, manual drawing, and shader APIs. Non-Off
    // diagnostics run even when EnablePerPetDye is false.
    public DyeRendererDebugMode DyeRendererDebugMode { get; set; }

    [DefaultValue(68)]
    [Range(0, 200)]
    [Slider]
    [Increment(4)]
    // Cosmetic draw-only spacing for Pet Bestiary projectiles. This never moves
    // projectile state outside the scoped draw call.
    public int PetSpacingPixels { get; set; }

    [DefaultValue(true)]
    public bool SeparateNormalAndLightPetSpacingGroups { get; set; }

    [DefaultValue(true)]
    public bool PetSpacingAvoidSolidTiles { get; set; }

    [DefaultValue(PetRuntimeDebugMode.Off)]
    // Debug-only pet runtime experiments. These can intentionally violate the
    // normal no-buff strategy to isolate animation/state bugs.
    public PetRuntimeDebugMode PetRuntimeDebugMode { get; set; }

    [DefaultValue(false)]
    public bool DebugLogging { get; set; }

    [DefaultValue(false)]
    public bool DebugMode { get; set; }
}

public enum DyeRenderMode
{
    Off,
    VanillaPreserving,
    ManualShaderExperimental
}

public enum DyeRendererDebugMode
{
    // Normal production path selected by DyeRenderMode.
    Off,
    HideMatchedProjectile,
    ForceLimeProjectile,
    ForceLimePreview,
    VanillaHookLogOnly,
    VanillaHookForcePetField,
    VanillaHookForceLightField,
    VanillaHookForceBothFields,
    ArmorShaderApplyByShaderId,
    ArmorShaderApplySecondaryByShaderId,
    ArmorShaderGetSecondaryShaderByShaderId,
    ArmorShaderFromItemType
}

public enum PetRuntimeDebugMode
{
    Off,
    MaintainActivePetBuffs
}

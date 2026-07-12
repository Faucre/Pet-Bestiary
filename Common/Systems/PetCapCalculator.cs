using System;
using PetBestiary.Common.Configs;
using PetBestiary.Content;
using Terraria;
using Terraria.ModLoader;

namespace PetBestiary.Common.Systems;

public static class PetCapCalculator
{
    public static int GetCap(PetCategory category)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        int configuredMax = GetConfiguredMax(config, category);

        if (!config.EnableProgressionMode || !ProgressionAffectsCategory(config, category))
        {
            return Math.Max(0, configuredMax);
        }

        int progressedCap = category == PetCategory.Light
            ? GetProgressedLightCap(config)
            : GetProgressedNormalCap(config);

        return Math.Clamp(progressedCap, 0, Math.Max(0, configuredMax));
    }

    public static bool IsProgressionModeEnabledFor(PetCategory category)
    {
        PetBestiaryConfig config = ModContent.GetInstance<PetBestiaryConfig>();
        return config.EnableProgressionMode && ProgressionAffectsCategory(config, category);
    }

    private static int GetConfiguredMax(PetBestiaryConfig config, PetCategory category)
    {
        return category == PetCategory.Light ? config.MaxLightPets : config.MaxNormalPets;
    }

    private static bool ProgressionAffectsCategory(PetBestiaryConfig config, PetCategory category)
    {
        return category == PetCategory.Light
            ? config.ProgressionAffectsLightPets
            : config.ProgressionAffectsNormalPets;
    }

    private static int GetProgressedNormalCap(PetBestiaryConfig config)
    {
        int slots = Math.Max(0, config.BaseNormalPetSlots);

        if (NPC.downedBoss1)
        {
            slots++;
        }

        if (NPC.downedBoss2)
        {
            slots++;
        }

        if (NPC.downedBoss3)
        {
            slots++;
        }

        if (Main.hardMode)
        {
            slots++;
        }

        if (AnyMechanicalBossDefeated())
        {
            slots++;
        }

        if (AllMechanicalBossesDefeated())
        {
            slots++;
        }

        if (NPC.downedPlantBoss)
        {
            slots++;
        }

        if (NPC.downedGolemBoss)
        {
            slots++;
        }

        if (NPC.downedAncientCultist)
        {
            slots++;
        }

        if (NPC.downedMoonlord)
        {
            return Math.Max(0, config.MaxNormalPets);
        }

        return slots;
    }

    private static int GetProgressedLightCap(PetBestiaryConfig config)
    {
        int slots = Math.Max(0, config.BaseLightPetSlots);

        if (NPC.downedBoss3)
        {
            slots++;
        }

        if (Main.hardMode)
        {
            slots++;
        }

        if (AnyMechanicalBossDefeated())
        {
            slots++;
        }

        if (NPC.downedPlantBoss)
        {
            slots++;
        }

        if (NPC.downedMoonlord)
        {
            return Math.Max(0, config.MaxLightPets);
        }

        return slots;
    }

    private static bool AnyMechanicalBossDefeated()
    {
        return NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3;
    }

    private static bool AllMechanicalBossesDefeated()
    {
        return NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
    }
}

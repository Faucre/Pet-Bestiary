using Terraria.ModLoader;

namespace PetBestiary.Common.Systems;

public sealed class PetDyeRenderSystem : ModSystem
{
    public override void Load()
    {
        PetDyeManager.LoadHooks();
    }

    public override void Unload()
    {
        PetDyeManager.UnloadHooks();
    }
}

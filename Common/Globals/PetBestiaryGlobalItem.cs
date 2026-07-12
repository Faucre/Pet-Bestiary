using PetBestiary.Common.Players;
using PetBestiary.Content;
using Terraria;
using Terraria.ModLoader;

namespace PetBestiary.Common.Globals;

public sealed class PetBestiaryGlobalItem : GlobalItem
{
    public override bool CanUseItem(Item item, Player player)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null || item == null || item.IsAir || !registry.TryGetByItemType(item.type, out _))
        {
            return base.CanUseItem(item, player);
        }

        player.GetModPlayer<PetBestiaryPlayer>().TryUsePetItem(item, true);

        // Once a pet is known by Pet Bestiary, item use is routed to virtual state so vanilla
        // cannot create a hidden pet buff/projectile that blocks bestiary toggles.
        return false;
    }
}

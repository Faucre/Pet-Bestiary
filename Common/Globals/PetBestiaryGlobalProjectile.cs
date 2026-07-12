using System.IO;
using Microsoft.Xna.Framework;
using PetBestiary.Common.Systems;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PetBestiary.Common.Globals;

public sealed class PetBestiaryGlobalProjectile : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    public bool SpawnedByPetBestiary { get; set; }

    public string PetKey { get; set; } = string.Empty;

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        bitWriter.WriteBit(SpawnedByPetBestiary);
        if (SpawnedByPetBestiary)
        {
            binaryWriter.Write(PetKey ?? string.Empty);
        }
    }

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
    {
        SpawnedByPetBestiary = bitReader.ReadBit();
        PetKey = SpawnedByPetBestiary ? binaryReader.ReadString() : string.Empty;
    }

    public override bool PreDraw(Projectile projectile, ref Color lightColor)
    {
        return PetDyeManager.PreDrawPetProjectile(projectile, lightColor);
    }
}

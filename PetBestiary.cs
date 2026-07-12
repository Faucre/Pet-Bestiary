using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using PetBestiary.Common.Configs;
using PetBestiary.Common.Players;
using PetBestiary.Common.Systems;
using PetBestiary.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PetBestiary;

public class PetBestiary : Mod
{
    private const int MaxSyncedEntries = 4096;

    internal static ModKeybind ToggleBestiaryKeybind;

    private enum PetBestiaryPacketType : byte
    {
        ActivePetState = 1
    }

    public override void Load()
    {
        ToggleBestiaryKeybind = KeybindLoader.RegisterKeybind(this, "TogglePetBestiary", Keys.P);
    }

    public override void Unload()
    {
        ToggleBestiaryKeybind = null;
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        PetBestiaryPacketType packetType = (PetBestiaryPacketType)reader.ReadByte();
        if (packetType == PetBestiaryPacketType.ActivePetState)
        {
            ReceiveActivePetState(reader, whoAmI);
        }
    }

    internal static void SendActivePetState(Player player, int toClient = -1, int ignoreClient = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer || player == null || !player.active)
        {
            return;
        }

        PetBestiaryPlayer petPlayer = player.GetModPlayer<PetBestiaryPlayer>();
        ModPacket packet = ModContent.GetInstance<PetBestiary>().GetPacket();
        packet.Write((byte)PetBestiaryPacketType.ActivePetState);
        packet.Write((byte)player.whoAmI);
        WriteStringList(packet, petPlayer.ActiveNormalPets);
        WriteStringList(packet, petPlayer.ActiveLightPets);
        WriteStringList(packet, petPlayer.LockedPets);
        WriteDyes(packet, petPlayer.PetDyes);
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            packet.Send();
            return;
        }

        packet.Send(toClient, ignoreClient);
    }

    private static void ReceiveActivePetState(BinaryReader reader, int whoAmI)
    {
        int playerId = reader.ReadByte();
        List<string> normalPets = ReadStringList(reader);
        List<string> lightPets = ReadStringList(reader);
        List<string> lockedPets = ReadStringList(reader);
        Dictionary<string, PetDyeData> dyes = ReadDyes(reader);

        if (Main.netMode == NetmodeID.Server)
        {
            // Clients are only allowed to update their own player state.
            playerId = whoAmI;
        }

        if (playerId < 0 || playerId >= Main.maxPlayers || !Main.player[playerId].active)
        {
            return;
        }

        Player player = Main.player[playerId];
        PetBestiaryPlayer petPlayer = player.GetModPlayer<PetBestiaryPlayer>();
        petPlayer.ApplySyncedActivePetState(normalPets, lightPets, lockedPets, dyes);
        LogDebug($"Received active pet state for player {playerId}: normal={petPlayer.ActiveNormalPets.Count}, light={petPlayer.ActiveLightPets.Count}, dyes={petPlayer.PetDyes.Count}, netMode={Main.netMode}");

        if (Main.netMode == NetmodeID.Server)
        {
            PetSpawnManager.MaintainPlayerPets(player, petPlayer);
            SendActivePetState(player, ignoreClient: whoAmI);
        }
    }

    private static void WriteStringList(BinaryWriter writer, IEnumerable<string> values)
    {
        List<string> safeValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .Take(MaxSyncedEntries)
            .ToList();

        writer.Write(safeValues.Count);
        foreach (string value in safeValues)
        {
            writer.Write(value);
        }
    }

    private static List<string> ReadStringList(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        List<string> values = new();
        for (int i = 0; i < count; i++)
        {
            string value = reader.ReadString();
            if (i < MaxSyncedEntries && !string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static void WriteDyes(BinaryWriter writer, IReadOnlyDictionary<string, PetDyeData> dyes)
    {
        List<KeyValuePair<string, PetDyeData>> safeDyes = dyes
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value?.HasShader == true)
            .Take(MaxSyncedEntries)
            .ToList();

        writer.Write(safeDyes.Count);
        foreach ((string petKey, PetDyeData dyeData) in safeDyes)
        {
            writer.Write(petKey);
            writer.Write(dyeData.DyeItemType);
            writer.Write(dyeData.DyeShaderId);
            writer.Write(dyeData.DyeItemKey ?? string.Empty);
            writer.Write(dyeData.DisplayName ?? string.Empty);
        }
    }

    private static Dictionary<string, PetDyeData> ReadDyes(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        Dictionary<string, PetDyeData> dyes = new();
        for (int i = 0; i < count; i++)
        {
            string petKey = reader.ReadString();
            PetDyeData dyeData = new()
            {
                DyeItemType = reader.ReadInt32(),
                DyeShaderId = reader.ReadInt32(),
                DyeItemKey = reader.ReadString(),
                DisplayName = reader.ReadString()
            };

            if (i < MaxSyncedEntries && !string.IsNullOrWhiteSpace(petKey) && dyeData.HasShader)
            {
                dyes[petKey] = dyeData;
            }
        }

        return dyes;
    }

    private static void LogDebug(string message)
    {
        if (ModContent.GetInstance<PetBestiaryConfig>().DebugLogging)
        {
            ModContent.GetInstance<PetBestiary>().Logger.Info(message);
        }
    }
}

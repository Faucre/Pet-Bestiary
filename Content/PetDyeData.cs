using Terraria.ModLoader.IO;

namespace PetBestiary.Content;

public sealed class PetDyeData
{
    public int DyeItemType { get; set; }

    public int DyeShaderId { get; set; }

    public string DyeItemKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool HasShader => DyeShaderId > 0;

    public TagCompound Save()
    {
        return new TagCompound
        {
            ["DyeItemType"] = DyeItemType,
            ["DyeShaderId"] = DyeShaderId,
            ["DyeItemKey"] = DyeItemKey,
            ["DisplayName"] = DisplayName
        };
    }

    public static PetDyeData Load(TagCompound tag)
    {
        return new PetDyeData
        {
            DyeItemType = tag.ContainsKey("DyeItemType") ? tag.GetInt("DyeItemType") : 0,
            DyeShaderId = tag.ContainsKey("DyeShaderId") ? tag.GetInt("DyeShaderId") : 0,
            DyeItemKey = tag.ContainsKey("DyeItemKey") ? tag.GetString("DyeItemKey") : string.Empty,
            DisplayName = tag.ContainsKey("DisplayName") ? tag.GetString("DisplayName") : string.Empty
        };
    }
}

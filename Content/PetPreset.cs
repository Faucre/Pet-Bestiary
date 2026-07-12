using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;

namespace PetBestiary.Content;

public sealed class PetPreset
{
    public PetPreset()
    {
    }

    public PetPreset(string name, IEnumerable<string> normalPets, IEnumerable<string> lightPets, IEnumerable<string> lockedPets)
        : this(name, normalPets, lightPets, lockedPets, new Dictionary<string, PetDyeData>())
    {
    }

    public PetPreset(string name, IEnumerable<string> normalPets, IEnumerable<string> lightPets, IEnumerable<string> lockedPets, IDictionary<string, PetDyeData> dyes)
    {
        Name = name;
        NormalPets = normalPets.Distinct().ToList();
        LightPets = lightPets.Distinct().ToList();
        LockedPets = lockedPets.Distinct().ToList();
        PetDyes = dyes
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value?.HasShader == true)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public string Name { get; set; } = "Preset";

    public List<string> NormalPets { get; set; } = new();

    public List<string> LightPets { get; set; } = new();

    public List<string> LockedPets { get; set; } = new();

    public Dictionary<string, PetDyeData> PetDyes { get; set; } = new();

    public TagCompound Save()
    {
        return new TagCompound
        {
            ["Name"] = Name,
            ["NormalPets"] = NormalPets.ToList(),
            ["LightPets"] = LightPets.ToList(),
            ["LockedPets"] = LockedPets.ToList(),
            ["PetDyes"] = PetDyes
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value?.HasShader == true)
                .Select(pair => new TagCompound
                {
                    ["PetKey"] = pair.Key,
                    ["Dye"] = pair.Value.Save()
                })
                .ToList()
        };
    }

    public static PetPreset Load(TagCompound tag)
    {
        return new PetPreset
        {
            Name = tag.ContainsKey("Name") ? tag.Get<string>("Name") : "Preset",
            NormalPets = ReadStringList(tag, "NormalPets").ToList(),
            LightPets = ReadStringList(tag, "LightPets").ToList(),
            LockedPets = ReadStringList(tag, "LockedPets").ToList(),
            PetDyes = LoadDyes(tag)
        };
    }

    private static IList<string> ReadStringList(TagCompound tag, string key)
    {
        return tag.ContainsKey(key) ? tag.GetList<string>(key) : new List<string>();
    }

    private static Dictionary<string, PetDyeData> LoadDyes(TagCompound tag)
    {
        Dictionary<string, PetDyeData> result = new();
        if (!tag.ContainsKey("PetDyes"))
        {
            return result;
        }

        foreach (TagCompound entry in tag.GetList<TagCompound>("PetDyes"))
        {
            string petKey = entry.ContainsKey("PetKey") ? entry.GetString("PetKey") : string.Empty;
            if (string.IsNullOrWhiteSpace(petKey) || !entry.ContainsKey("Dye"))
            {
                continue;
            }

            PetDyeData dyeData = PetDyeData.Load(entry.GetCompound("Dye"));
            if (dyeData.HasShader)
            {
                result[petKey] = dyeData;
            }
        }

        return result;
    }
}

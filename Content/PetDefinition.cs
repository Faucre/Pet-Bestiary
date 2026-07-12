namespace PetBestiary.Content;

public enum PetCategory
{
    Normal,
    Light
}

public sealed class PetDefinition
{
    public PetDefinition(
        string key,
        int itemType,
        int projectileType,
        int buffType,
        string displayName,
        string sourceMod,
        PetCategory category,
        string unlockHint)
    {
        Key = key;
        ItemType = itemType;
        ProjectileType = projectileType;
        BuffType = buffType;
        DisplayName = displayName;
        SourceMod = sourceMod;
        Category = category;
        UnlockHint = unlockHint;
    }

    public string Key { get; }

    public int ItemType { get; }

    public int ProjectileType { get; }

    public int BuffType { get; }

    public string DisplayName { get; }

    public string SourceMod { get; }

    public PetCategory Category { get; }

    public string UnlockHint { get; }
}

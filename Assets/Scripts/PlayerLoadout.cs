using UnityEngine;

// PlayerPrefs-backed loadout from the character customization screen.
// Per-slot index 0..17 maps to (type = idx/3 + 1, color = idx%3 + 1).
public static class PlayerLoadout
{
    public const int SlotCount = 6; // Head, Chest, Arm, Belt, Legs, Feet
    public const int WeaponCount = 9;

    private const string SavedKey = "Loadout_Saved";
    private const string ArmorKeyPrefix = "Loadout_Armor_";
    private const string WeaponKey = "Loadout_Weapon";

    public static bool HasSavedLoadout => PlayerPrefs.GetInt(SavedKey, 0) == 1;

    public static int GetArmorIndex(int slot)
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(ArmorKeyPrefix + slot, 0), 0, 17);
    }

    public static int GetWeaponIndex()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(WeaponKey, 0), 0, WeaponCount - 1);
    }

    public static void SetArmorIndex(int slot, int index)
    {
        PlayerPrefs.SetInt(ArmorKeyPrefix + slot, Mathf.Clamp(index, 0, 17));
        PlayerPrefs.SetInt(SavedKey, 1);
    }

    public static void SetWeaponIndex(int index)
    {
        PlayerPrefs.SetInt(WeaponKey, Mathf.Clamp(index, 0, WeaponCount - 1));
        PlayerPrefs.SetInt(SavedKey, 1);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(SavedKey, 1);
        PlayerPrefs.Save();
    }

    public static (int type, int color) IndexToTypeColor(int idx)
    {
        idx = Mathf.Clamp(idx, 0, 17);
        return (idx / 3 + 1, idx % 3 + 1);
    }
}

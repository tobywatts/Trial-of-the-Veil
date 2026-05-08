using System.Collections.Generic;
using UnityEngine;

public class ModularCharacterCustomizer : MonoBehaviour
{
    public enum ArmorSlot { Head, Chest, Arm, Belt, Legs, Feet }
    public enum FaceSlot { Hair, Eyebrow, FaceHair, Eyes, Nose, Ears }

    [System.Serializable]
    public class ArmorChoice
    {
        public ArmorSlot slot;
        [Range(0, 6)] public int type = 0;
        [Range(1, 3)] public int color = 1;
    }

    [System.Serializable]
    public class FaceChoice
    {
        public FaceSlot slot;
        [Range(0, 5)] public int type = 0;
        [Range(1, 5)] public int color = 1;
    }

    [Header("Armor (type 0 = none, 1-6; color 1-3)")]
    public List<ArmorChoice> armor = new()
    {
        new() { slot = ArmorSlot.Head, type = 0, color = 1 },
        new() { slot = ArmorSlot.Chest, type = 1, color = 1 },
        new() { slot = ArmorSlot.Arm, type = 1, color = 1 },
        new() { slot = ArmorSlot.Belt, type = 1, color = 1 },
        new() { slot = ArmorSlot.Legs, type = 1, color = 1 },
        new() { slot = ArmorSlot.Feet, type = 1, color = 1 },
    };

    [Header("Face (type 0 = none, else 1-5)")]
    public List<FaceChoice> face = new()
    {
        new() { slot = FaceSlot.Hair, type = 1, color = 1 },
        new() { slot = FaceSlot.Eyebrow, type = 1, color = 1 },
        new() { slot = FaceSlot.FaceHair, type = 0, color = 1 },
        new() { slot = FaceSlot.Eyes, type = 1, color = 1 },
        new() { slot = FaceSlot.Nose, type = 1, color = 1 },
        new() { slot = FaceSlot.Ears, type = 1, color = 1 },
    };

    [Header("Materials")]
    public bool fixURPMaterials = true;

    private readonly Dictionary<string, SkinnedMeshRenderer> rendererByName = new();

    private void Awake()
    {
        Refresh();
    }

    [ContextMenu("Refresh (Apply Armor + Fix Materials)")]
    public void Refresh()
    {
        IndexRenderers();
        if (fixURPMaterials) FixURPMaterials();
        ApplyAll();
    }

    private void IndexRenderers()
    {
        rendererByName.Clear();
        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            rendererByName[r.gameObject.name] = r;
    }

    public void ApplyAll()
    {
        foreach (var a in armor) Apply(SlotToBaseName(a.slot), a.type, a.color);
        foreach (var f in face) Apply(FaceSlotToBaseName(f.slot), f.type, f.color, hasColor: f.slot != FaceSlot.Nose && f.slot != FaceSlot.Ears);
    }

    public void EquipArmor(ArmorSlot slot, int type, int color)
    {
        Apply(SlotToBaseName(slot), type, color);
    }

    public void EquipFace(FaceSlot slot, int type, int color)
    {
        bool hasColor = slot != FaceSlot.Nose && slot != FaceSlot.Ears;
        Apply(FaceSlotToBaseName(slot), type, color, hasColor);
    }

    private void Apply(string baseName, int type, int color, bool hasColor = true)
    {
        foreach (var kv in rendererByName)
        {
            if (!kv.Key.StartsWith(baseName, System.StringComparison.OrdinalIgnoreCase)) continue;
            kv.Value.enabled = false;
        }
        if (type <= 0) return;

        string target = hasColor
            ? $"{baseName} Type {type} Color {color}"
            : $"{baseName} Type {type}";

        if (rendererByName.TryGetValue(target, out var r)) r.enabled = true;
        else Debug.LogWarning($"Renderer not found: '{target}'");
    }

    private static string SlotToBaseName(ArmorSlot slot) => slot switch
    {
        ArmorSlot.Head => "Head Armor",
        ArmorSlot.Chest => "Chest Armor",
        ArmorSlot.Arm => "Arm Armor",
        ArmorSlot.Belt => "Belt Armor",
        ArmorSlot.Legs => "Legs Armor",
        ArmorSlot.Feet => "Feet Armor",
        _ => ""
    };

    private static string FaceSlotToBaseName(FaceSlot slot) => slot switch
    {
        FaceSlot.Hair => "Hair",
        FaceSlot.Eyebrow => "Eyebrow",
        FaceSlot.FaceHair => "Face Hair",
        FaceSlot.Eyes => "Eyes",
        FaceSlot.Nose => "Nose",
        FaceSlot.Ears => "Ears",
        _ => ""
    };

    private void FixURPMaterials()
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) return;

        Material cached = null;
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        foreach (var r in all)
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                if (m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/")) continue;

                if (cached == null)
                {
                    cached = new Material(urp) { name = "Base Palette (URP runtime)" };
                    if (cached.HasProperty("_BaseMap")) cached.SetTexture("_BaseMap", m.mainTexture);
                    if (cached.HasProperty("_BaseColor")) cached.SetColor("_BaseColor", Color.white);
                    cached.mainTexture = m.mainTexture;
                }
                mats[i] = cached;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }
}

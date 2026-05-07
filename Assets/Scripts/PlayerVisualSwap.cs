using UnityEngine;

public class PlayerVisualSwap : MonoBehaviour
{
    [Tooltip("GanzSe modular character prefab")]
    public GameObject characterPrefab;

    public Vector3 localOffset = Vector3.zero;
    public Vector3 localEulerOffset = Vector3.zero;
    public float scale = 1f;

    [Tooltip("Hide the player's own renderers so only the new visual shows")]
    public bool hideOriginalVisuals = true;

    [Tooltip("Copy the spawned Avatar onto the player's Animator (needs GanzSe rig set to Humanoid)")]
    public bool retargetAnimatorAvatar = true;

    private void Start()
    {
        if (!Application.isPlaying) return;
        EnsureSpawned(applyAvatarSwap: retargetAnimatorAvatar);
    }

    [ContextMenu("Spawn Visual In Scene (Edit Mode)")]
    public void SpawnInEditMode()
    {
        EnsureSpawned(applyAvatarSwap: false);
    }

    [ContextMenu("Remove Spawned Visual")]
    public void RemoveSpawnedVisual()
    {
        Transform existing = FindExistingVisualChild();
        if (existing != null) DestroyImmediate(existing.gameObject);

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.enabled = true;
        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
            mr.enabled = true;
    }

    private void EnsureSpawned(bool applyAvatarSwap)
    {
        if (characterPrefab == null)
            characterPrefab = Resources.Load<GameObject>("GanzSeCharacter");

        if (characterPrefab == null)
        {
            Debug.LogWarning("Character Prefab not set and Resources/GanzSeCharacter.prefab not found.");
            return;
        }

        Transform existing = FindExistingVisualChild();
        GameObject visual;
        if (existing != null)
        {
            visual = existing.gameObject;
        }
        else
        {
            visual = Instantiate(characterPrefab, transform);
            visual.transform.localPosition = localOffset;
            visual.transform.localRotation = Quaternion.Euler(localEulerOffset);
            visual.transform.localScale = Vector3.one * scale;
        }

        if (hideOriginalVisuals)
        {
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!smr.transform.IsChildOf(visual.transform))
                    smr.enabled = false;
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                if (!mr.transform.IsChildOf(visual.transform) && mr.GetComponent<PlayerWeapon>() == null)
                    mr.enabled = false;
        }

        var customizer = visual.GetComponent<ModularCharacterCustomizer>();
        if (customizer == null) customizer = visual.AddComponent<ModularCharacterCustomizer>();
        customizer.Refresh();
        ApplySavedLoadoutArmor(customizer);

        if (applyAvatarSwap)
        {
            Animator playerAnim = GetComponent<Animator>();
            Animator newAnim = visual.GetComponentInChildren<Animator>();
            if (playerAnim != null && newAnim != null && newAnim.avatar != null)
            {
                playerAnim.avatar = newAnim.avatar;
                newAnim.enabled = false;
            }
            else if (playerAnim != null && (newAnim == null || newAnim.avatar == null))
            {
                Debug.LogWarning("No Humanoid Avatar on the GanzSe prefab. Set the GanzSe FBX Rig to Humanoid and apply.");
            }
        }
    }

    private static void ApplySavedLoadoutArmor(ModularCharacterCustomizer customizer)
    {
        if (!PlayerLoadout.HasSavedLoadout) return;
        for (int slot = 0; slot < PlayerLoadout.SlotCount; slot++)
        {
            var (type, color) = PlayerLoadout.IndexToTypeColor(PlayerLoadout.GetArmorIndex(slot));
            customizer.EquipArmor((ModularCharacterCustomizer.ArmorSlot)slot, type, color);
        }
    }

    private Transform FindExistingVisualChild()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c.name.Contains("GanzSe") || c.name.Contains("Modular Character") || c.name == "GanzSeCharacter") return c;
        }
        return null;
    }
}

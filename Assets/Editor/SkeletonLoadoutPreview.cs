using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// In-editor workflow for hand-tuning where the sword and shield sit in the skeleton enemy's hands.
// Spawn Preview drops a posed skeleton at the origin; adjust the sword/shield children in hand-local
// space; Save Pose writes those transforms back to the level2 Skeleton EnemyVariant; Clear Preview
// removes it.
public static class SkeletonLoadoutPreview
{
    private const string Level2Path = "Assets/Scenes/level2.unity";
    private const string SkeletonPath = "Assets/Mini Simple Characters Skeleton Demo/Prefabs/Characters/mini simple skeleton demo.prefab";
    private const string SwordPath = "Assets/Mini Simple Characters Skeleton Demo/Prefabs/Props/sword_wood.prefab";
    private const string ShieldPath = "Assets/Mini Simple Characters Skeleton Demo/Prefabs/Props/shield_wood.prefab";
    private const string PreviewRootName = "SkeletonLoadoutPreview";
    private const string SwordChildName = "Sword (Preview)";
    private const string ShieldChildName = "Shield (Preview)";
    private const float PreviewScale = 1.6f; // Match the variant's spawn scale so units match the game.

    [MenuItem("Tools/Risk of Tob/Skeleton Loadout/Spawn Preview")]
    public static void SpawnPreview()
    {
        GameObject existing = GameObject.Find(PreviewRootName);
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        GameObject skelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
        GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
        GameObject shieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPath);
        if (skelPrefab == null || swordPrefab == null || shieldPrefab == null)
        {
            Debug.LogError("[SkeletonLoadout] One or more prefabs missing - check asset paths.");
            return;
        }

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(skelPrefab);
        Undo.RegisterCreatedObjectUndo(root, "Spawn Skeleton Loadout Preview");
        root.name = PreviewRootName;
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * PreviewScale;

        Animator anim = root.GetComponentInChildren<Animator>();
        if (anim == null || !anim.isHuman)
        {
            Debug.LogError("[SkeletonLoadout] Skeleton root has no humanoid Animator - can't resolve hand bones.");
            Object.DestroyImmediate(root);
            return;
        }

        Transform rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
        Transform lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        if (rh == null || lh == null)
        {
            Debug.LogError("[SkeletonLoadout] Hand bones not found on humanoid avatar.");
            Object.DestroyImmediate(root);
            return;
        }

        AttachAsChild(swordPrefab, rh, SwordChildName);
        AttachAsChild(shieldPrefab, lh, ShieldChildName);

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[SkeletonLoadout] Preview spawned. Adjust 'Sword (Preview)' and 'Shield (Preview)' in the Hierarchy, then run 'Save Pose to Level2 Spawner'.");
    }

    private static GameObject AttachAsChild(GameObject prefab, Transform parent, string childName)
    {
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.name = childName;
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        UrpMaterialUpgrader.Convert(inst);
        return inst;
    }

    [MenuItem("Tools/Risk of Tob/Skeleton Loadout/Save Pose to Level2 Spawner")]
    public static void SavePoseToLevel2Spawner()
    {
        GameObject preview = GameObject.Find(PreviewRootName);
        if (preview == null) { Debug.LogError("[SkeletonLoadout] No preview in any open scene - spawn it first."); return; }

        Transform sword = FindDescendant(preview.transform, SwordChildName);
        Transform shield = FindDescendant(preview.transform, ShieldChildName);
        if (sword == null || shield == null)
        {
            Debug.LogError("[SkeletonLoadout] Preview is missing Sword/Shield children - re-spawn it.");
            return;
        }

        Vector3 swordPos = sword.localPosition;
        Vector3 swordRot = sword.localRotation.eulerAngles;
        float swordScale = sword.localScale.x;
        Vector3 shieldPos = shield.localPosition;
        Vector3 shieldRot = shield.localRotation.eulerAngles;
        float shieldScale = shield.localScale.x;

        // Make sure level2 is the active scene (the spawner we want to write to lives there).
        var active = EditorSceneManager.GetActiveScene();
        bool needLoad = active.path != Level2Path;
        if (needLoad)
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Debug.LogWarning("[SkeletonLoadout] Aborted: current scene has unsaved changes."); return; }
            }
            EditorSceneManager.OpenScene(Level2Path);
        }

        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner == null) { Debug.LogError("[SkeletonLoadout] No EnemySpawner in level2."); return; }

        EnemySpawner.EnemyVariant skel = null;
        if (spawner.enemyVariants != null)
        {
            for (int i = 0; i < spawner.enemyVariants.Count; i++)
            {
                var v = spawner.enemyVariants[i];
                if (v != null && v.displayName == "Skeleton") { skel = v; break; }
            }
        }
        if (skel == null) { Debug.LogError("[SkeletonLoadout] Skeleton variant not found on the level2 spawner."); return; }

        skel.rightHandLocalPosition = swordPos;
        skel.rightHandLocalRotationEuler = swordRot;
        skel.rightHandAttachmentScale = swordScale;
        skel.leftHandLocalPosition = shieldPos;
        skel.leftHandLocalRotationEuler = shieldRot;
        skel.leftHandAttachmentScale = shieldScale;

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[SkeletonLoadout] Saved -> sword pos={swordPos} rot={swordRot} scale={swordScale:F3}; shield pos={shieldPos} rot={shieldRot} scale={shieldScale:F3}");
    }

    [MenuItem("Tools/Risk of Tob/Skeleton Loadout/Clear Preview")]
    public static void ClearPreview()
    {
        GameObject existing = GameObject.Find(PreviewRootName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            Debug.Log("[SkeletonLoadout] Preview cleared.");
        }
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}

using UnityEditor;
using UnityEngine;

// Editor utility: bakes the runtime-built UI scripts into editor-editable .prefab assets.
// Each menu item builds a temp GameObject, calls the component's BuildHierarchy(), and saves
// the result to Assets/Resources/UI/<Name>.prefab.
public static class BuildUIPrefabs
{
    private const string OutputDir = "Assets/Resources/UI";

    [MenuItem("Tools/Risk of Tob/UI/Build All UI Prefabs")]
    public static void BuildAll()
    {
        BuildGameHUD();
        BuildPauseMenu();
        BuildMainMenu();
    }

    [MenuItem("Tools/Risk of Tob/UI/Build GameHUD Prefab")]
    public static void BuildGameHUD()
    {
        EnsureOutputDir();

        GameHUD existing = Object.FindFirstObjectByType<GameHUD>(FindObjectsInactive.Include);

        GameObject temp = new GameObject("GameHUD");
        try
        {
            GameHUD hud = temp.AddComponent<GameHUD>();
            if (existing != null) CopySerializedFields(existing, hud);
            // Ensure icon sprites are populated (the scene copy may not have them wired);
            // BuildHierarchy uses these to assign Image.sprite on each child Image.
            ApplyIconRefs(hud);

            hud.BuildHierarchy();

            string prefabPath = $"{OutputDir}/GameHUD.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            if (saved == null)
            {
                Debug.LogError($"[BuildUIPrefabs] Failed to save GameHUD prefab at '{prefabPath}'.");
                return;
            }

            Debug.Log($"[BuildUIPrefabs] GameHUD prefab saved -> '{prefabPath}'" +
                      (existing != null ? " (sprite/style fields copied from scene's existing GameHUD)" : "") +
                      ". Drag it into level1/level2 Hierarchies and delete the old script-only GameHUD.");

            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    // Reflection-based copy of public fields from source to dest.
    // Skips Object refs to scene-instance objects (they'd point at the source's children, not the
    // prefab's); asset refs (Sprite, Font, Texture, ScriptableObject) are safe and get copied.
    private static void CopySerializedFields(Component source, Component dest)
    {
        System.Type t = source.GetType();
        var fields = t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var field in fields)
        {
            object value = field.GetValue(source);
            if (value == null) continue;

            if (value is UnityEngine.Object obj)
            {
                if (!AssetDatabase.Contains(obj)) continue; // skip scene refs
                field.SetValue(dest, obj);
            }
            else if (field.FieldType.IsValueType || field.FieldType == typeof(string))
            {
                field.SetValue(dest, value);
            }
        }
    }

    [MenuItem("Tools/Risk of Tob/UI/Build PauseMenu Prefab")]
    public static void BuildPauseMenu()
    {
        EnsureOutputDir();
        PauseMenu existing = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);

        GameObject temp = new GameObject("PauseMenu");
        try
        {
            PauseMenu pm = temp.AddComponent<PauseMenu>();
            if (existing != null) CopySerializedFields(existing, pm);
            pm.BuildHierarchy();

            string prefabPath = $"{OutputDir}/PauseMenu.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            if (saved == null) { Debug.LogError($"[BuildUIPrefabs] Failed to save PauseMenu prefab."); return; }

            Debug.Log($"[BuildUIPrefabs] PauseMenu prefab saved -> '{prefabPath}'. Button click handlers re-attach at runtime in PauseMenu.Awake (via the serialized button refs).");
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    [MenuItem("Tools/Risk of Tob/UI/Build MainMenu Prefab")]
    public static void BuildMainMenu()
    {
        EnsureOutputDir();
        MainMenu existing = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);

        GameObject temp = new GameObject("MainMenu");
        try
        {
            MainMenu mm = temp.AddComponent<MainMenu>();
            if (existing != null) CopySerializedFields(existing, mm);
            mm.BuildHierarchy();

            string prefabPath = $"{OutputDir}/MainMenu.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            if (saved == null) { Debug.LogError($"[BuildUIPrefabs] Failed to save MainMenu prefab."); return; }

            Debug.Log($"[BuildUIPrefabs] MainMenu prefab saved -> '{prefabPath}'. Drag into the MainMenu scene and delete the script-only GameObject.");
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    // Re-applies the sprite/icon references on the GameHUD prefab: pulls the 8 item icons off
    // MainMenu.prefab and the spell/coin icons from their asset GUIDs.
    [MenuItem("Tools/Risk of Tob/UI/Restore GameHUD Icon Refs")]
    public static void RestoreGameHUDIconRefs()
    {
        const string GameHUDPath = "Assets/Resources/UI/GameHUD.prefab";
        const string MainMenuPath = "Assets/Resources/UI/MainMenu.prefab";

        GameObject gameHUDPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameHUDPath);
        GameObject mainMenuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPath);
        if (gameHUDPrefab == null || mainMenuPrefab == null)
        {
            Debug.LogError("[BuildUIPrefabs] GameHUD or MainMenu prefab missing.");
            return;
        }

        GameHUD hud = gameHUDPrefab.GetComponent<GameHUD>();
        MainMenu mm = mainMenuPrefab.GetComponent<MainMenu>();
        if (hud == null || mm == null) { Debug.LogError("[BuildUIPrefabs] Components missing on prefabs."); return; }

        // Edits to a prefab asset must go through LoadPrefabContents + SaveAsPrefabAsset;
        // mutating it directly via GetComponent isn't tracked by Unity.
        GameObject hudRoot = PrefabUtility.LoadPrefabContents(GameHUDPath);
        try
        {
            GameHUD hudInst = hudRoot.GetComponent<GameHUD>();
            // Copy item icons off the MainMenu component fields (same 8 items in both UIs).
            hudInst.maxHealthIcon         = mm.maxHealthIcon;
            hudInst.doubleJumpIcon        = mm.doubleJumpIcon;
            hudInst.damageVsHighHpIcon    = mm.damageVsHighHpIcon;
            hudInst.attackSpeedIcon       = mm.attackSpeedIcon;
            hudInst.moveSpeedIcon         = mm.moveSpeedIcon;
            hudInst.healBuffIcon          = mm.healBuffIcon;
            hudInst.multishotIcon         = mm.multishotIcon;
            hudInst.cooldownReductionIcon = mm.cooldownReductionIcon;

            // Spell + coin icons: pulled from their asset GUIDs.
            hudInst.fireSpellIcon = LoadSpriteByGuid("dd3885098198d104888439be4c590aba");
            hudInst.iceSpellIcon  = LoadSpriteByGuid("2bccd332882fcd14b9e4c377e432c587");
            hudInst.healSpellIcon = LoadSpriteByGuid("c3aab7237df394c4890e647a4ae580ea");
            hudInst.coinIcon      = LoadSpriteByGuid("394899dbf2938874ca7d1025f7598406");

            PrefabUtility.SaveAsPrefabAsset(hudRoot, GameHUDPath);
            Debug.Log("[BuildUIPrefabs] GameHUD icon refs restored.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hudRoot);
        }
    }

    // Fills the GameHUD's public Sprite refs (item icons, spell icons, coin icon) before
    // BuildHierarchy runs. Item icons come from MainMenu.prefab; spell + coin icons from asset GUIDs.
    private static void ApplyIconRefs(GameHUD hud)
    {
        const string MainMenuPath = "Assets/Resources/UI/MainMenu.prefab";
        GameObject mmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPath);
        MainMenu mm = mmPrefab != null ? mmPrefab.GetComponent<MainMenu>() : null;
        if (mm != null)
        {
            if (hud.maxHealthIcon == null)         hud.maxHealthIcon         = mm.maxHealthIcon;
            if (hud.doubleJumpIcon == null)        hud.doubleJumpIcon        = mm.doubleJumpIcon;
            if (hud.damageVsHighHpIcon == null)    hud.damageVsHighHpIcon    = mm.damageVsHighHpIcon;
            if (hud.attackSpeedIcon == null)       hud.attackSpeedIcon       = mm.attackSpeedIcon;
            if (hud.moveSpeedIcon == null)         hud.moveSpeedIcon         = mm.moveSpeedIcon;
            if (hud.healBuffIcon == null)          hud.healBuffIcon          = mm.healBuffIcon;
            if (hud.multishotIcon == null)         hud.multishotIcon         = mm.multishotIcon;
            if (hud.cooldownReductionIcon == null) hud.cooldownReductionIcon = mm.cooldownReductionIcon;
        }
        if (hud.fireSpellIcon == null) hud.fireSpellIcon = LoadSpriteByGuid("dd3885098198d104888439be4c590aba");
        if (hud.iceSpellIcon == null)  hud.iceSpellIcon  = LoadSpriteByGuid("2bccd332882fcd14b9e4c377e432c587");
        if (hud.healSpellIcon == null) hud.healSpellIcon = LoadSpriteByGuid("c3aab7237df394c4890e647a4ae580ea");
        if (hud.coinIcon == null)      hud.coinIcon      = LoadSpriteByGuid("394899dbf2938874ca7d1025f7598406");
    }

    private static Sprite LoadSpriteByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) { Debug.LogWarning($"[BuildUIPrefabs] No asset for guid '{guid}'."); return null; }
        // Asset may be a Sprite directly or a Texture with a Sprite subasset.
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) return s;
        return null;
    }

    [MenuItem("Tools/Risk of Tob/UI/Wire Prefabs Into All Scenes")]
    public static void WirePrefabsIntoScenes()
    {
        ReplaceInScene<GameHUD>("Assets/Scenes/level1.unity", "Assets/Resources/UI/GameHUD.prefab", rewireGameManagerHud: true);
        ReplaceInScene<GameHUD>("Assets/Scenes/level2.unity", "Assets/Resources/UI/GameHUD.prefab", rewireGameManagerHud: true);
        ReplaceInScene<MainMenu>("Assets/Scenes/MainMenu.unity", "Assets/Resources/UI/MainMenu.prefab", rewireGameManagerHud: false);

        Debug.Log("[BuildUIPrefabs] All scenes wired to UI prefabs.");
    }

    // Replaces a scene's script-only UI GameObject with an instance of the corresponding prefab.
    // For GameHUD also re-wires GameManager.hud to point at the new instance.
    private static void ReplaceInScene<T>(string scenePath, string prefabPath, bool rewireGameManagerHud) where T : Component
    {
        var active = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (active.isDirty && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning($"[BuildUIPrefabs] Skipping '{scenePath}': current scene has unsaved changes.");
            return;
        }

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogError($"[BuildUIPrefabs] Prefab missing at '{prefabPath}'."); return; }

        // Delete every existing script-only instance; defensive in case one scene somehow has multiple.
        T[] existing = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var e in existing)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(e.gameObject)) continue; // already a prefab instance, leave it
            Object.DestroyImmediate(e.gameObject);
        }

        T newInstance = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (newInstance == null)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            newInstance = inst.GetComponent<T>();
        }

        if (rewireGameManagerHud && newInstance is GameHUD newHud)
        {
            GameManager gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm != null)
            {
                gm.hud = newHud;
                EditorUtility.SetDirty(gm);
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BuildUIPrefabs] Wired '{prefabPath}' into '{scenePath}'.");
    }

    private static void EnsureOutputDir()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(OutputDir))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
    }
}

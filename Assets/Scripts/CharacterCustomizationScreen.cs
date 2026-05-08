using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[ExecuteAlways]
public class CharacterCustomizationScreen : MonoBehaviour
{
    [Header("Scene Names")]
    public string nextScene = "level1";
    public string backScene = "MainMenu";

    [Header("Prefabs")]
    public GameObject characterPrefab;
    public RuntimeAnimatorController battleStanceController;

    [Header("Background")]
    public Sprite backgroundSprite;
    [Range(0, 32)] public int backgroundBlurPasses = 7;
    [Tooltip("UV units per second")]
    public float backgroundScrollSpeed = 0.02f;
    [Range(0f, 1f)] public float vignetteIntensity = 0.75f;
    [Range(0.1f, 1f)] public float vignetteInnerRadius = 0.55f;

    [Header("Colors")]
    public Color overlayColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    public Color panelColor = new Color(0.18f, 0.18f, 0.18f, 0.92f);
    public Color buttonColor = Color.white;
    public Color buttonHoverColor = new Color(0.66f, 0.66f, 0.66f, 1f);
    public Color buttonTextColor = Color.black;
    public Color titleColor = Color.white;
    public Color modelBackgroundColor = new Color(0.22f, 0.24f, 0.30f, 1f);
    public float keyLightIntensity = 1.9f;
    public float fillLightIntensity = 0.9f;
    public float rimLightIntensity = 0.6f;

    [Header("Main Model Camera")]
    public Vector3 mainCamLocalPos = new Vector3(0f, 1.05f, 3.0f);
    public Vector3 mainCamLookOffset = new Vector3(0f, 1.05f, 0f);
    public float mainCamFov = 38f;

    [Header("Layout")]
    public float modelSpinDegPerSec = 35f;
    [Tooltip("World units to raise the model in frame")]
    public float modelHeightOffset = 0.2f;
    public Vector2 mainModelSize = new Vector2(820, 980);
    public Vector2 mainModelOffset = new Vector2(0, 140);
    public Vector2 outfitButtonSize = new Vector2(720, 110);
    public float outfitBottomMargin = 50f;
    public Vector2 actionButtonSize = new Vector2(340, 100);
    public float actionButtonMargin = 30f;
    public int panelCornerRadius = 10;
    public int buttonCornerRadius = 6;

    // 18 outfits: type 1..6, color 1..3
    private const int OutfitCount = 18;
    private int outfitIndex;

    private Transform mainModelPivot;
    private ModularCharacterCustomizer mainCustomizer;
    private Animator mainAnimator;

    private Text outfitLabel;

    private Camera mainCam;
    private Light keyLight, fillLight, rimLight;
    private Material backdropMat;
    private Texture2D backdropTex;

    private readonly List<GameObject> spawnedRoots = new();
    private bool built;

    private void OnEnable() { Build(); }
    private void OnDisable() { Clear(); }

    [ContextMenu("Rebuild Preview")]
    public void Rebuild() { Clear(); Build(); }

    private void Build()
    {
        if (built) return;
        if (Application.isPlaying)
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ScrubBakedPhantomObjects();
        }

        try
        {
            EnsureEventSystem();
            EnsureAudioListener();
            // Preview Camera renders to screen directly, no RT.
            SpawnGlobalLights();
            SpawnBackdrop();
            BuildUI();
            SpawnMainModel();
            ApplyInitialSelection();
            if (!Application.isPlaying)
            {
                foreach (var root in spawnedRoots)
                    if (root != null) MarkDontSaveRecursive(root);
            }
            built = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Build failed: {e}");
            Clear();
        }
    }

    private void Clear()
    {
        for (int i = spawnedRoots.Count - 1; i >= 0; i--)
            if (spawnedRoots[i] != null) SafeDestroy(spawnedRoots[i]);
        spawnedRoots.Clear();

        if (backdropMat != null) { SafeDestroy(backdropMat); backdropMat = null; }
        if (backdropTex != null) { SafeDestroy(backdropTex); backdropTex = null; }

        mainCam = null;
        keyLight = fillLight = rimLight = null;
        mainModelPivot = null;
        mainCustomizer = null;
        mainAnimator = null;
        outfitLabel = null;
        canvasRoot = null;

        built = false;
    }

    private GameObject NewSpawnedRoot(string name)
    {
        var go = new GameObject(name);
        if (!Application.isPlaying) MarkDontSaveRecursive(go);
        spawnedRoots.Add(go);
        return go;
    }

    private static void MarkDontSaveRecursive(GameObject go)
    {
        // DontSaveInBuild also keeps edit-time spawns out of the built scene.
        go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        foreach (Transform child in go.transform) MarkDontSaveRecursive(child.gameObject);
    }

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void Update()
    {
        // Keep the cursor unlocked; another script may try to re-lock it.
        if (Application.isPlaying && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (mainModelPivot != null)
        {
            Vector3 lp = mainModelPivot.localPosition;
            if (!Mathf.Approximately(lp.y, modelHeightOffset))
                mainModelPivot.localPosition = new Vector3(lp.x, modelHeightOffset, lp.z);

            float dt = Application.isPlaying ? Time.deltaTime : Time.unscaledDeltaTime;
            mainModelPivot.Rotate(0f, modelSpinDegPerSec * dt, 0f, Space.World);
        }
        ApplyLiveCameraTuning();
        ApplyLiveLightTuning();
        ApplyBackdropScroll();
    }

    private void ApplyLiveCameraTuning()
    {
        if (mainCam != null) ApplyCamera(mainCam, mainCamLocalPos, mainCamLookOffset, mainCamFov);
    }

    private static void ApplyCamera(Camera cam, Vector3 localPos, Vector3 localLookOffset, float fov)
    {
        var t = cam.transform;
        t.localPosition = localPos;
        var parent = t.parent;
        if (parent != null) t.LookAt(parent.TransformPoint(localLookOffset));
        cam.fieldOfView = fov;
    }

    private void ApplyLiveLightTuning()
    {
        if (keyLight != null) keyLight.intensity = keyLightIntensity;
        if (fillLight != null) fillLight.intensity = fillLightIntensity;
        if (rimLight != null) rimLight.intensity = rimLightIntensity;
    }

    private void ApplyBackdropScroll()
    {
        if (backdropMat == null || Mathf.Abs(backgroundScrollSpeed) < 0.00001f) return;
        string prop = backdropMat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        Vector2 off = backdropMat.GetTextureOffset(prop);
        off.x = (off.x + backgroundScrollSpeed * Time.unscaledDeltaTime) % 1f;
        backdropMat.SetTextureOffset(prop, off);
    }

    private void OnDestroy() { Clear(); }

    public void Play()
    {
        PlayerLoadout.Save();
        SceneManager.LoadScene(nextScene);
    }

    public void Back()
    {
        SceneManager.LoadScene(backScene);
    }

    private void ApplyInitialSelection()
    {
        if (PlayerLoadout.HasSavedLoadout)
        {
            outfitIndex = PlayerLoadout.GetArmorIndex(0);
        }
        outfitIndex = ((outfitIndex % OutfitCount) + OutfitCount) % OutfitCount;
        ApplyOutfitToMain();
        UpdateLabels();
    }

    private void CycleOutfit(int dir)
    {
        outfitIndex = (outfitIndex + dir + OutfitCount) % OutfitCount;
        for (int i = 0; i < 6; i++) PlayerLoadout.SetArmorIndex(i, outfitIndex);
        ApplyOutfitToMain();
        UpdateLabels();
    }

    private static (int type, int color) IndexToTypeColor(int idx)
    {
        int type = (idx / 3) + 1;
        int color = (idx % 3) + 1;
        return (type, color);
    }

    private void ApplyOutfitToMain()
    {
        if (mainCustomizer == null) return;
        var (type, color) = IndexToTypeColor(outfitIndex);
        for (int slot = 0; slot < 6; slot++)
            mainCustomizer.EquipArmor((ModularCharacterCustomizer.ArmorSlot)slot, type, color);
    }

    private void UpdateLabels()
    {
        if (outfitLabel != null)
        {
            var (type, color) = IndexToTypeColor(outfitIndex);
            outfitLabel.text = $"{SkinName(type)} {color}";
        }
    }

    private static string SkinName(int type)
    {
        switch (type)
        {
            case 1: return "Brawler";
            case 2: return "Fighter";
            case 3: return "Maestro";
            case 4: return "Gladiator";
            case 5: return "Rogue";
            case 6: return "Paladin";
            default: return $"Set {type}";
        }
    }

    private void SpawnMainModel()
    {
        if (characterPrefab == null) characterPrefab = Resources.Load<GameObject>("GanzSeCharacter");
        if (characterPrefab == null)
        {
            Debug.LogWarning("characterPrefab not set and Resources/GanzSeCharacter.prefab not found.");
            return;
        }
        var stage = NewSpawnedRoot("MainModelStage");
        stage.transform.position = Vector3.zero;

        mainModelPivot = new GameObject("Pivot").transform;
        mainModelPivot.SetParent(stage.transform, false);
        mainModelPivot.localPosition = new Vector3(0f, modelHeightOffset, 0f);

        var go = Instantiate(characterPrefab, mainModelPivot);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        mainCustomizer = go.GetComponent<ModularCharacterCustomizer>();
        if (mainCustomizer == null) mainCustomizer = go.AddComponent<ModularCharacterCustomizer>();

        mainAnimator = go.GetComponentInChildren<Animator>();
        if (mainAnimator != null)
        {
            if (battleStanceController != null) mainAnimator.runtimeAnimatorController = battleStanceController;
            mainAnimator.applyRootMotion = false;
        }

        // Renders straight to screen; the clear color is the backdrop, UI overlays on top.
        mainCam = CreateStageCamera(stage.transform, mainCamLocalPos, mainCamLookOffset, mainCamFov, null, overlayColor);
    }

    private static Camera CreateStageCamera(Transform parent, Vector3 localPos, Vector3 localLookOffset, float fov, RenderTexture rt, Color? bg)
    {
        var cgo = new GameObject(rt != null ? "Preview Camera" : "Main Camera");
        cgo.transform.SetParent(parent, false);
        cgo.transform.localPosition = localPos;
        cgo.transform.LookAt(parent.TransformPoint(localLookOffset));

        var cam = cgo.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = bg ?? new Color(0.08f, 0.09f, 0.12f, 1f);
        if (rt != null) cam.targetTexture = rt;
        return cam;
    }

    private void EnsureAudioListener()
    {
        if (FindFirstObjectByType<AudioListener>() != null) return;
        var go = NewSpawnedRoot("AudioListener");
        go.AddComponent<AudioListener>();
    }

    private void SpawnGlobalLights()
    {
        var root = NewSpawnedRoot("GlobalLights").transform;
        keyLight = AddLight(root, "KeyLight", new Vector3(40f, -25f, 0f), new Color(1f, 0.97f, 0.92f), keyLightIntensity);
        fillLight = AddLight(root, "FillLight", new Vector3(15f, 150f, 0f), new Color(0.78f, 0.85f, 1f), fillLightIntensity);
        rimLight = AddLight(root, "RimLight", new Vector3(-20f, 200f, 0f), new Color(1f, 0.95f, 0.85f), rimLightIntensity);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.60f, 0.62f, 0.66f, 1f);
        RenderSettings.ambientIntensity = 1.5f;
    }

    private static Light AddLight(Transform parent, string name, Vector3 euler, Color color, float intensity)
    {
        var lgo = new GameObject(name);
        lgo.transform.SetParent(parent, false);
        lgo.transform.localRotation = Quaternion.Euler(euler);
        var light = lgo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    // Unlit quad backdrop behind the model; a UI image would draw on top of the character.
    private void SpawnBackdrop()
    {
        if (backgroundSprite == null || backgroundSprite.texture == null) return;

        Texture2D tex = backgroundSprite.texture;
        if (backgroundBlurPasses > 0 && tex.isReadable)
        {
            backdropTex = MenuBackground.BoxBlur(tex, backgroundBlurPasses);
            tex = backdropTex;
        }
        if (tex != null) tex.wrapMode = TextureWrapMode.Repeat;

        var root = NewSpawnedRoot("ModelBackdrop");
        root.transform.position = new Vector3(0f, 1f, -6f);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "BackdropQuad";
        quad.transform.SetParent(root.transform, false);
        // Quad faces -Z; camera views from +Z, so flip it to face the camera.
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        // Oversized to fill the frame at any aspect ratio or FOV.
        quad.transform.localScale = new Vector3(40f, 24f, 1f);

        var col = quad.GetComponent<Collider>();
        if (col != null) SafeDestroy(col);

        var mr = quad.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        backdropMat = new Material(shader);
        if (backdropMat.HasProperty("_BaseMap")) backdropMat.SetTexture("_BaseMap", tex);
        if (backdropMat.HasProperty("_MainTex")) backdropMat.SetTexture("_MainTex", tex);
        if (backdropMat.HasProperty("_Cull")) backdropMat.SetFloat("_Cull", 0f); // double-sided
        mr.sharedMaterial = backdropMat;
    }

    private GameObject canvasRoot;

    private void BuildUI()
    {
        canvasRoot = new GameObject("CustomizationCanvas");
        canvasRoot.transform.SetParent(transform, false);
        spawnedRoots.Add(canvasRoot);

        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        Sprite panelSprite = UiBuilder.MakeRoundedSprite(panelCornerRadius);
        Sprite buttonSprite = UiBuilder.MakeRoundedSprite(buttonCornerRadius);

        // The vignette is screen-space: over the character but behind the buttons.
        if (vignetteIntensity > 0f)
        {
            var vig = UiBuilder.NewChildImage(canvasRoot.transform, "Vignette",
                MenuBackground.MakeVignetteSprite(256, vignetteInnerRadius, vignetteIntensity));
            vig.raycastTarget = false;
            UiBuilder.Stretch(vig.rectTransform);
            vig.transform.SetAsFirstSibling();
        }

        BuildOutfitSelector(canvasRoot.transform, panelSprite, buttonSprite);
        BuildCornerButtons(canvasRoot.transform, buttonSprite);
    }

    private void BuildOutfitSelector(Transform parent, Sprite panelSprite, Sprite buttonSprite)
    {
        var panel = UiBuilder.NewChildImage(parent, "OutfitSelector", panelSprite);
        panel.type = Image.Type.Sliced;
        panel.color = panelColor;
        var rt = panel.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = outfitButtonSize;
        rt.anchoredPosition = new Vector2(0, outfitBottomMargin);

        const float pad = 10f;
        float arrowSize = outfitButtonSize.y - 2f * pad;

        var label = UiBuilder.NewChildText(panel.transform, "Label", "Outfit", 38, FontStyle.Bold);
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        var lRT = label.rectTransform;
        lRT.anchorMin = new Vector2(0, 0);
        lRT.anchorMax = new Vector2(1, 1);
        lRT.offsetMin = new Vector2(arrowSize + 2f * pad, 0);
        lRT.offsetMax = new Vector2(-(arrowSize + 2f * pad), 0);
        outfitLabel = label;

        CreateFixedArrowButton(panel.transform, buttonSprite, "<", arrowSize,
            new Vector2(0, 0.5f), new Vector2(pad, -arrowSize * 0.5f), () => CycleOutfit(-1));
        CreateFixedArrowButton(panel.transform, buttonSprite, ">", arrowSize,
            new Vector2(1, 0.5f), new Vector2(-pad - arrowSize, -arrowSize * 0.5f), () => CycleOutfit(+1));
    }

    private void CreateFixedArrowButton(Transform parent, Sprite sprite, string label, float size, Vector2 anchor, Vector2 anchoredPos, System.Action onClick)
    {
        var img = UiBuilder.NewChildImage(parent, label == "<" ? "LeftBtn" : "RightBtn", sprite);
        img.type = Image.Type.Sliced;
        img.color = buttonColor;
        var rt = img.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPos;

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.normalColor = buttonColor;
        c.highlightedColor = buttonHoverColor;
        c.pressedColor = buttonHoverColor;
        c.selectedColor = buttonColor;
        c.disabledColor = buttonColor;
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.05f;
        btn.colors = c;
        btn.onClick.AddListener(() => onClick());

        int arrowFontSize = Mathf.Max(20, Mathf.RoundToInt(size * 0.5f));
        var t = UiBuilder.NewChildText(img.transform, "Text", label, arrowFontSize, FontStyle.Bold);
        t.color = buttonTextColor;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        UiBuilder.Stretch(t.rectTransform);
    }

    private void BuildCornerButtons(Transform parent, Sprite buttonSprite)
    {
        // Top-left: Back
        CreateActionButton(parent, buttonSprite, "Back",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(actionButtonMargin, -actionButtonMargin), Back);

        // Top-right: Start Game
        CreateActionButton(parent, buttonSprite, "Start Game",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-actionButtonMargin, -actionButtonMargin), Play);
    }

    private void CreateActionButton(Transform parent, Sprite sprite, string label, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, System.Action onClick)
    {
        var img = UiBuilder.NewChildImage(parent, label + "Button", sprite);
        img.type = Image.Type.Sliced;
        img.color = buttonColor;
        var rt = img.rectTransform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.sizeDelta = actionButtonSize;
        rt.anchoredPosition = pos;

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.normalColor = buttonColor;
        c.highlightedColor = buttonHoverColor;
        c.pressedColor = buttonHoverColor;
        c.selectedColor = buttonColor;
        c.disabledColor = buttonColor;
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.05f;
        btn.colors = c;
        btn.onClick.AddListener(() => onClick());

        var text = UiBuilder.NewChildText(img.transform, "Text", label, 32, FontStyle.Bold);
        text.color = buttonTextColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        UiBuilder.Stretch(text.rectTransform);
    }

    // Wipe any leftover spawned roots/canvas from a previous run before spawning fresh ones.
    private void ScrubBakedPhantomObjects()
    {
        var scene = gameObject.scene;
        if (!scene.IsValid()) return;

        // Scene roots we spawn.
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == gameObject) continue;
            string n = root.name;
            bool match = n.Contains("GanzSe") ||
                         n == "MainModelStage" || n == "Display Camera" || n == "Preview Camera" ||
                         n == "Main Camera" ||
                         n == "GlobalLights" || n == "AudioListener" ||
                         n == "EventSystem" || n == "CustomizationCanvas" ||
                         n == "MenuBackground" || n == "ModelBackdrop";
            if (match)
            {
                Debug.Log("Scrubbing phantom root: " + n);
                DestroyImmediate(root);
            }
        }

        // Loop backwards: we're removing children while iterating.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (child.name == "CustomizationCanvas" || child.name == "MenuBackground")
            {
                Debug.Log("Scrubbing phantom child: " + child.name);
                DestroyImmediate(child);
            }
        }
    }

    private void EnsureEventSystem()
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>();
        GameObject esGO;
        if (existing != null)
        {
            esGO = existing.gameObject;
        }
        else
        {
            esGO = NewSpawnedRoot("EventSystem");
            esGO.AddComponent<EventSystem>();
        }

        // A stale EventSystem without an input module means clicks won't work.
        if (esGO.GetComponent<BaseInputModule>() == null)
        {
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}

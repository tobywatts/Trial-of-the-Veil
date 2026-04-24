using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenu : MonoBehaviour
{
    [Header("Scene Names")]
    public string mainMenuScene = "MainMenu";

    [Header("Colors")]
    public Color overlayColor = new Color(0.1f, 0.1f, 0.1f, 0.65f);
    public Color panelColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    public Color buttonColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    public Color buttonHoverColor = new Color(0.66f, 0.66f, 0.66f, 1f);
    public Color buttonTextColor = Color.black;
    public Color titleColor = Color.white;

    [Header("Layout")]
    public Vector2 panelSize = new Vector2(560, 540);
    public Vector2 buttonSize = new Vector2(320, 64);
    public float buttonSpacing = 18f;
    public int panelCornerRadius = 12;
    public int buttonCornerRadius = 6;

    // Serialized so the layout survives prefab serialization; Awake skips re-building when set.
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Transform overlayTransform;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button itemLogButton;
    [SerializeField] private Button mainMenuButton;

    private bool isPaused;
    private float prevTimeScale = 1f;
    // Sub-panels built on demand the first time they're opened, not part of the prefab.
    private GameObject optionsPanel;
    private GameObject itemLogPanel;
    // Rounded sprites generated at runtime each spawn; re-populated in RegenerateDynamicSprites.
    private Sprite cachedPanelSprite;
    private Sprite cachedButtonSprite;

    private void Awake()
    {
        // Prefab instance: children already wired, skip. Script-only GameObject: build now.
        if (transform.childCount == 0)
            BuildHierarchy();

        // Runs every Awake: runtime sprites and onClick delegates aren't serialized, so both need re-doing.
        RegenerateDynamicSprites();
        WireButtonHandlers();
        SetVisible(false);
        EnsureEventSystem();
    }

    private void Update()
    {
        if (TogglePressed())
        {
            if (GameManager.Instance != null && GameManager.Instance.GameOver) return;
            if (isPaused) Resume();
            else Pause();
        }
    }

    private static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public void Pause()
    {
        if (isPaused) return;
        isPaused = true;
        prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetVisible(true);
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;
        Time.timeScale = prevTimeScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetVisible(false);
    }

    public void Exit()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isPaused = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    private void SetVisible(bool v)
    {
        if (root != null) root.SetActive(v);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    // Builds the static hierarchy and caches button refs. Public so the Editor utility can save a
    // prefab. onClick wiring lives in WireButtonHandlers so prefab instances re-attach delegates.
    public void BuildHierarchy()
    {
        root = new GameObject("PauseMenuCanvas");
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        Sprite panelSprite = UiBuilder.MakeRoundedSprite(panelCornerRadius);
        Sprite buttonSprite = UiBuilder.MakeRoundedSprite(buttonCornerRadius);
        cachedPanelSprite = panelSprite;
        cachedButtonSprite = buttonSprite;

        var overlay = UiBuilder.NewChildImage(root.transform, "Overlay", null);
        overlay.color = overlayColor;
        UiBuilder.Stretch(overlay.rectTransform);
        overlayTransform = overlay.transform;

        var panel = UiBuilder.NewChildImage(overlay.transform, "Panel", panelSprite);
        mainPanel = panel.gameObject;
        panel.type = Image.Type.Sliced;
        panel.color = panelColor;
        var panelRT = panel.rectTransform;
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = panelSize;
        panelRT.anchoredPosition = Vector2.zero;

        var title = UiBuilder.NewChildText(panel.transform, "Title", "Paused", 56, FontStyle.Bold);
        title.color = titleColor;
        title.alignment = TextAnchor.MiddleCenter;
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, -32);
        titleRT.sizeDelta = new Vector2(0, 80);

        float topPadding = 140f;
        float startY = (panelSize.y * 0.5f) - topPadding - (buttonSize.y * 0.5f);

        resumeButton = CreateButton(panel.transform, buttonSprite, "Resume", new Vector2(0, startY));
        optionsButton = CreateButton(panel.transform, buttonSprite, "Options", new Vector2(0, startY - (buttonSize.y + buttonSpacing)));
        itemLogButton = CreateButton(panel.transform, buttonSprite, "Item Log", new Vector2(0, startY - 2 * (buttonSize.y + buttonSpacing)));
        mainMenuButton = CreateButton(panel.transform, buttonSprite, "Main Menu", new Vector2(0, startY - 3 * (buttonSize.y + buttonSpacing)));
    }

    // Rebuilds the rounded sprites and re-assigns them; the runtime Texture2Ds don't serialize into a prefab.
    private void RegenerateDynamicSprites()
    {
        cachedPanelSprite = UiBuilder.MakeRoundedSprite(panelCornerRadius);
        cachedButtonSprite = UiBuilder.MakeRoundedSprite(buttonCornerRadius);

        if (mainPanel != null)
        {
            var img = mainPanel.GetComponent<Image>();
            if (img != null) img.sprite = cachedPanelSprite;
        }
        AssignButtonSprite(resumeButton);
        AssignButtonSprite(optionsButton);
        AssignButtonSprite(itemLogButton);
        AssignButtonSprite(mainMenuButton);
    }

    private void AssignButtonSprite(Button btn)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.sprite = cachedButtonSprite;
    }

    private void WireButtonHandlers()
    {
        if (resumeButton != null) { resumeButton.onClick.RemoveAllListeners(); resumeButton.onClick.AddListener(Resume); }
        if (optionsButton != null) { optionsButton.onClick.RemoveAllListeners(); optionsButton.onClick.AddListener(OpenOptions); }
        if (itemLogButton != null) { itemLogButton.onClick.RemoveAllListeners(); itemLogButton.onClick.AddListener(OpenItemLog); }
        if (mainMenuButton != null) { mainMenuButton.onClick.RemoveAllListeners(); mainMenuButton.onClick.AddListener(Exit); }
    }

    private void OpenItemLog()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (itemLogPanel == null)
        {
            Color logPanelColor = new Color(panelColor.r, panelColor.g, panelColor.b, 0.92f);
            var style = new ItemLogPanelBuilder.Style
            {
                panelSprite = cachedPanelSprite,
                buttonSprite = cachedButtonSprite,
                panelColor = logPanelColor,
                titleColor = titleColor,
                buttonColor = buttonColor,
                buttonHoverColor = buttonHoverColor,
                buttonTextColor = buttonTextColor,
                panelSize = new Vector2(620, 720),
                buttonSize = new Vector2(260, 64),
            };
            GameHUD hud = FindFirstObjectByType<GameHUD>();
            System.Func<Item.Type, Sprite> lookup = hud != null ? new System.Func<Item.Type, Sprite>(hud.IconFor) : null;
            itemLogPanel = ItemLogPanelBuilder.Build(overlayTransform, style, lookup, CloseItemLog);
        }
        else
        {
            itemLogPanel.SetActive(true);
        }
    }

    private void CloseItemLog()
    {
        if (itemLogPanel != null) itemLogPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    private void OpenOptions()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (optionsPanel == null)
        {
            Color pauseOptionsPanel = new Color(panelColor.r, panelColor.g, panelColor.b, 0.7f);
            var style = new OptionsPanelBuilder.Style
            {
                panelSprite = cachedPanelSprite,
                buttonSprite = cachedButtonSprite,
                panelColor = pauseOptionsPanel,
                titleColor = titleColor,
                buttonColor = buttonColor,
                buttonHoverColor = buttonHoverColor,
                buttonTextColor = buttonTextColor,
                panelSize = new Vector2(620, 720),
                buttonSize = new Vector2(260, 64),
            };
            optionsPanel = OptionsPanelBuilder.Build(overlayTransform, style, CloseOptions);
        }
        else
        {
            optionsPanel.SetActive(true);
        }
    }

    private void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    // Visual construction only; onClick wiring is deferred to WireButtonHandlers.
    private Button CreateButton(Transform parent, Sprite sprite, string label, Vector2 anchoredPos)
    {
        var img = UiBuilder.NewChildImage(parent, label + "Button", sprite);
        img.type = Image.Type.Sliced;
        img.color = buttonColor;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = buttonSize;
        rt.anchoredPosition = anchoredPos;

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = buttonHoverColor;
        colors.selectedColor = buttonColor;
        colors.disabledColor = buttonColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        btn.colors = colors;

        var text = UiBuilder.NewChildText(img.transform, "Text", label, 28, FontStyle.Bold);
        text.color = buttonTextColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        UiBuilder.Stretch(text.rectTransform);
        return btn;
    }
}

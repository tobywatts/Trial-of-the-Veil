// Made with Claude (Opus 4.6) — prompt: "add an options panel and how to play panel to the menu"

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scene Names")]
    public string characterCustomizationScene = "CharacterCustomization";

    [Header("Colors")]
    public Color overlayColor = new Color(0.1f, 0.1f, 0.1f, 0.65f);
    public Color panelColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    public Color buttonColor = Color.white;
    public Color buttonHoverColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    public Color buttonTextColor = Color.black;
    public Color titleColor = Color.white;

    [Header("Background")]
    public Sprite backgroundSprite;
    [Range(0, 12)] public int backgroundBlurPasses = 7;
    [Tooltip("UV units/sec. 0.02 = ~50s per full cycle.")]
    public float backgroundScrollSpeed = 0.02f;
    [Range(0f, 1f)] public float vignetteIntensity = 0.75f;
    [Range(0.1f, 1f)] public float vignetteInnerRadius = 0.55f;

    [Header("Layout")]
    public string titleText = "The Trial of the Veil";
    public int titleFontSize = 110;
    public float titleTopMargin = 70f;
    public Vector2 panelSize = new Vector2(520, 720);
    public Vector2 buttonSize = new Vector2(520, 120);
    public float buttonSpacing = 32f;
    public int panelCornerRadius = 12;
    public int buttonCornerRadius = 6;

    [Header("Item Log Icons")]
    public Sprite maxHealthIcon;
    public Sprite doubleJumpIcon;
    public Sprite damageVsHighHpIcon;
    public Sprite attackSpeedIcon;
    public Sprite moveSpeedIcon;
    public Sprite healBuffIcon;
    public Sprite multishotIcon;
    public Sprite cooldownReductionIcon;

    // Set when built from a prefab; Awake then skips BuildHierarchy.
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Transform overlayTransform;
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button itemLogButton;
    [SerializeField] private Button exitButton;
    private Button howToPlayButton;

    // Sub-panels built on demand, not part of the prefab.
    private GameObject optionsPanel;
    private GameObject itemLogPanel;
    private GameObject howToPlayPanel;
    // Procedural rounded sprites, regenerated each spawn.
    private Sprite cachedPanelSprite;
    private Sprite cachedButtonSprite;

    private Sprite IconFor(Item.Type t)
    {
        switch (t)
        {
            case Item.Type.MaxHealth: return maxHealthIcon;
            case Item.Type.DoubleJump: return doubleJumpIcon;
            case Item.Type.DamageVsHighHp: return damageVsHighHpIcon;
            case Item.Type.AttackSpeed: return attackSpeedIcon;
            case Item.Type.MoveSpeed: return moveSpeedIcon;
            case Item.Type.HealBuff: return healBuffIcon;
            case Item.Type.Multishot: return multishotIcon;
            case Item.Type.CooldownReduction: return cooldownReductionIcon;
        }
        return null;
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SoundFx.PlayMusic(SoundFx.MenuMusic);
        EnsureEventSystem();

        if (transform.childCount == 0)
            BuildHierarchy();
        // Populates cachedButtonSprite, needed by EnsureHowToPlayButton.
        RegenerateDynamicSprites();
        EnsureHowToPlayButton();
        LiftMainPanelForFiveButtons();
        WireButtonHandlers();
    }

    private void LiftMainPanelForFiveButtons()
    {
        if (mainPanel == null) return;
        RectTransform rt = mainPanel.GetComponent<RectTransform>();
        if (rt == null) return;
        // Already lifted, nothing to do.
        if (rt.anchoredPosition.y >= 100f) return;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 110f);
    }

    private void EnsureHowToPlayButton()
    {
        if (howToPlayButton != null) return;
        if (itemLogButton == null) return; // prefab unwired, bail safely

        // Slot the new button below Item Log; Exit steps down one slot to keep its spacing.
        float slot = buttonSize.y + buttonSpacing;
        Vector2 itemPos = itemLogButton.GetComponent<RectTransform>().anchoredPosition;
        Vector2 newPos = new Vector2(itemPos.x, itemPos.y - slot);

        howToPlayButton = CreateButton(itemLogButton.transform.parent, cachedButtonSprite, "How to Play", newPos);

        if (exitButton != null)
        {
            RectTransform exitRT = exitButton.GetComponent<RectTransform>();
            exitRT.anchoredPosition = new Vector2(exitRT.anchoredPosition.x, exitRT.anchoredPosition.y - slot);
        }
    }

    public void Play()
    {
        if (string.IsNullOrEmpty(characterCustomizationScene))
        {
            Debug.LogWarning("characterCustomizationScene is not set.");
            return;
        }
        SceneManager.LoadScene(characterCustomizationScene);
    }

    public void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

    public void BuildHierarchy()
    {
        root = new GameObject("MainMenuCanvas");
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

        BuildBackground();

        var overlay = UiBuilder.NewChildImage(root.transform, "Overlay", null);
        overlay.color = overlayColor;
        UiBuilder.Stretch(overlay.rectTransform);
        overlayTransform = overlay.transform;

        var panel = UiBuilder.NewChildImage(overlay.transform, "Panel", panelSprite);
        mainPanel = panel.gameObject;
        panel.type = Image.Type.Sliced;
        // No central box; the panel is just a layout anchor for the buttons.
        panel.color = new Color(0, 0, 0, 0);
        panel.raycastTarget = false;
        var panelRT = panel.rectTransform;
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = panelSize;
        panelRT.anchoredPosition = Vector2.zero;

        var title = UiBuilder.NewChildText(overlay.transform, "Title", titleText, titleFontSize, FontStyle.Bold);
        title.color = titleColor;
        title.alignment = TextAnchor.MiddleCenter;
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, -titleTopMargin);
        titleRT.sizeDelta = new Vector2(0, titleFontSize + 40);
        var titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0, 0, 0, 0.85f);
        titleShadow.effectDistance = new Vector2(3, -3);

        float topPadding = 60f;
        float startY = (panelSize.y * 0.5f) - topPadding - (buttonSize.y * 0.5f);

        float slot = buttonSize.y + buttonSpacing;
        playButton = CreateButton(panel.transform, buttonSprite, "Play", new Vector2(0, startY));
        optionsButton = CreateButton(panel.transform, buttonSprite, "Options", new Vector2(0, startY - 1 * slot));
        itemLogButton = CreateButton(panel.transform, buttonSprite, "Item Log", new Vector2(0, startY - 2 * slot));
        howToPlayButton = CreateButton(panel.transform, buttonSprite, "How to Play", new Vector2(0, startY - 3 * slot));
        exitButton = CreateButton(panel.transform, buttonSprite, "Exit", new Vector2(0, startY - 4 * slot));
    }

    // Rebuild the rounded sprites; their Texture2Ds don't survive prefab serialization.
    private void RegenerateDynamicSprites()
    {
        cachedPanelSprite = UiBuilder.MakeRoundedSprite(panelCornerRadius);
        cachedButtonSprite = UiBuilder.MakeRoundedSprite(buttonCornerRadius);

        if (mainPanel != null)
        {
            var img = mainPanel.GetComponent<Image>();
            if (img != null) img.sprite = cachedPanelSprite;
        }
        AssignButtonSprite(playButton);
        AssignButtonSprite(optionsButton);
        AssignButtonSprite(howToPlayButton);
        AssignButtonSprite(itemLogButton);
        AssignButtonSprite(exitButton);
    }

    private void AssignButtonSprite(Button btn)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.sprite = cachedButtonSprite;
    }

    private void WireButtonHandlers()
    {
        if (playButton != null) { playButton.onClick.RemoveAllListeners(); playButton.onClick.AddListener(Play); }
        if (optionsButton != null) { optionsButton.onClick.RemoveAllListeners(); optionsButton.onClick.AddListener(OpenOptions); }
        if (howToPlayButton != null) { howToPlayButton.onClick.RemoveAllListeners(); howToPlayButton.onClick.AddListener(OpenHowToPlay); }
        if (itemLogButton != null) { itemLogButton.onClick.RemoveAllListeners(); itemLogButton.onClick.AddListener(OpenItemLog); }
        if (exitButton != null) { exitButton.onClick.RemoveAllListeners(); exitButton.onClick.AddListener(Exit); }
    }

    private void OpenHowToPlay()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (howToPlayPanel == null)
        {
            Color solidPanel = new Color(panelColor.r, panelColor.g, panelColor.b, 1f);
            var style = new HowToPlayPanelBuilder.Style
            {
                panelSprite = cachedPanelSprite,
                buttonSprite = cachedButtonSprite,
                panelColor = solidPanel,
                titleColor = titleColor,
                buttonColor = buttonColor,
                buttonHoverColor = buttonHoverColor,
                buttonTextColor = buttonTextColor,
                panelSize = new Vector2(720, 760),
                buttonSize = new Vector2(280, 70),
                panelOffset = new Vector2(0f, -120f),
            };
            howToPlayPanel = HowToPlayPanelBuilder.Build(overlayTransform, style, CloseHowToPlay);
        }
        else
        {
            howToPlayPanel.SetActive(true);
        }
    }

    private void CloseHowToPlay()
    {
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    private void OpenItemLog()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (itemLogPanel == null)
        {
            Color logPanelColor = new Color(panelColor.r, panelColor.g, panelColor.b, 1f);
            var style = new ItemLogPanelBuilder.Style
            {
                panelSprite = cachedPanelSprite,
                buttonSprite = cachedButtonSprite,
                panelColor = logPanelColor,
                titleColor = titleColor,
                buttonColor = buttonColor,
                buttonHoverColor = buttonHoverColor,
                buttonTextColor = buttonTextColor,
                panelSize = new Vector2(700, 760),
                buttonSize = new Vector2(280, 70),
                panelOffset = new Vector2(0f, -120f),
            };
            itemLogPanel = ItemLogPanelBuilder.Build(overlayTransform, style, IconFor, CloseItemLog);
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

    private void BuildBackground()
    {
        MenuBackground.Create(root.transform, backgroundSprite, backgroundBlurPasses, backgroundScrollSpeed, vignetteIntensity, vignetteInnerRadius);
    }

    private void OpenOptions()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (optionsPanel == null)
        {
            // The options panel needs a solid backdrop, so force alpha to 1.
            Color optionsPanelColor = new Color(panelColor.r, panelColor.g, panelColor.b, 1f);
            var style = new OptionsPanelBuilder.Style
            {
                panelSprite = cachedPanelSprite,
                buttonSprite = cachedButtonSprite,
                panelColor = optionsPanelColor,
                titleColor = titleColor,
                buttonColor = buttonColor,
                buttonHoverColor = buttonHoverColor,
                buttonTextColor = buttonTextColor,
                panelSize = new Vector2(700, 760),
                buttonSize = new Vector2(280, 70),
                panelOffset = new Vector2(0f, -120f),
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

        var text = UiBuilder.NewChildText(img.transform, "Text", label, 58, FontStyle.Bold);
        text.color = buttonTextColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        UiBuilder.Stretch(text.rectTransform);
        return btn;
    }
}

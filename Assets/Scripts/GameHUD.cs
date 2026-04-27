// Made with Claude (Sonnet 4.5) — prompt: "build out the hud, health bar xp bar and spell slots with cooldowns"

using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    [Header("Health Bar")]
    public Image healthBarFill;
    public Image healthBarBackground;
    public Image healthBarBorder;
    public Text healthText;

    [Header("XP Bar")]
    public Image xpBarFill;
    public Image xpBarBackground;
    public Image xpBarBorder;
    public Text xpText;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public Text gameOverText;

    [Header("Crosshair")]
    public Image crosshair;

    [Header("Info Panel")]
    public Text timeText;
    public Text stageText;
    public Text stageProgressText;
    public Text enemyLevelText;

    [Header("Spell Bar")]
    public Sprite fireSpellIcon;
    public Sprite iceSpellIcon;
    public Sprite healSpellIcon;
    public PlayerWeapon playerWeapon;
    public HealAbility healAbility;

    [Header("Coins")]
    public Sprite coinIcon;
    public PlayerCoins playerCoins;
    [SerializeField] private Text coinCountText;

    [Header("Inventory")]
    public PlayerInventory playerInventory;
    [Tooltip("Square size in HUD pixels per inventory entry.")]
    public float inventoryEntrySize = 64f;
    [Header("Inventory Icons")]
    public Sprite maxHealthIcon;
    public Sprite doubleJumpIcon;
    public Sprite damageVsHighHpIcon;
    public Sprite attackSpeedIcon;
    public Sprite moveSpeedIcon;
    public Sprite healBuffIcon;
    public Sprite multishotIcon;
    public Sprite cooldownReductionIcon;

    // [SerializeField] so these refs survive prefab serialization.
    [SerializeField] private RectTransform inventoryBar;
    private readonly System.Collections.Generic.Dictionary<Item.Type, Text> inventoryCountTexts = new System.Collections.Generic.Dictionary<Item.Type, Text>();
    private readonly System.Collections.Generic.Dictionary<Item.Type, GameObject> inventoryEntries = new System.Collections.Generic.Dictionary<Item.Type, GameObject>();

    public Sprite IconFor(Item.Type t)
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

    private PlayerHealth playerHealth;
    private PlayerLevel playerLevel;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform healthBarFillRect;
    [SerializeField] private RectTransform xpBarFillRect;

    private float displayedHealth = 1f;
    private float targetHealth = 1f;
    private float displayedXP;
    private float targetXP;

    // [Serializable] so Unity persists the struct's Image/Text refs in the prefab.
    [System.Serializable]
    private struct SpellSlot
    {
        public Image iconImage;
        public Image cooldownOverlay;
        public Text cooldownText;
    }

    [SerializeField] private SpellSlot fireSlot;
    [SerializeField] private SpellSlot iceSlot;
    [SerializeField] private SpellSlot healSlot;

    private void Awake()
    {
        // Script-only GameObject has no children: build at runtime. A prefab instance skips this.
        if (transform.childCount == 0)
            BuildHierarchy();
    }

    // Constructs the full HUD hierarchy. Public so the Editor utility can bake it into a prefab.
    public void BuildHierarchy()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Reference resolution scaled down by 1.3 so every HUD element renders 30% larger.
        scaler.referenceResolution = new Vector2(1920f / 1.3f, 1080f / 1.3f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        BuildHealthBar();
        BuildXPBar();
        BuildCrosshair();
        BuildInfoPanel();
        BuildSpellBar();
        BuildCoinCounter();
        BuildInventoryBar();
        BuildGameOverPanel();
    }

    private void Start()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.OnHealthChanged += UpdateHealthBar;

        playerLevel = FindFirstObjectByType<PlayerLevel>();
        if (playerLevel != null)
        {
            playerLevel.OnXPChanged += UpdateXPBar;
            UpdateXPBar(playerLevel.CurrentXP, playerLevel.XPToNextLevel);
        }

        if (playerWeapon == null) playerWeapon = FindFirstObjectByType<PlayerWeapon>();
        if (healAbility == null) healAbility = FindFirstObjectByType<HealAbility>();
        if (playerCoins == null) playerCoins = FindFirstObjectByType<PlayerCoins>();
        if (playerCoins != null)
        {
            playerCoins.OnCoinsChanged += UpdateCoinCounter;
            UpdateCoinCounter(playerCoins.Coins);
        }

        if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (playerInventory != null)
            playerInventory.OnItemAdded += UpdateInventoryEntry;
    }

    private void UpdateCoinCounter(int amount)
    {
        if (coinCountText != null) coinCountText.text = amount.ToString();
    }

    private void Update()
    {
        if (healthBarFillRect != null)
        {
            displayedHealth = Mathf.Lerp(displayedHealth, targetHealth, Time.deltaTime * 8f);
            healthBarFillRect.anchorMax = new Vector2(displayedHealth, 1f);
        }

        if (xpBarFillRect != null)
        {
            displayedXP = Mathf.Lerp(displayedXP, targetXP, Time.deltaTime * 10f);
            xpBarFillRect.anchorMax = new Vector2(displayedXP, 1f);
        }

        if (GameManager.Instance != null)
        {
            float t = GameManager.Instance.TimeElapsed;
            int mins = Mathf.FloorToInt(t / 60f);
            int secs = Mathf.FloorToInt(t % 60f);
            if (timeText != null)
                timeText.text = $"Time: {mins:00}:{secs:00}";

            int stage = GameManager.Instance.CurrentStage;
            int percent = Mathf.FloorToInt(GameManager.Instance.StageProgress * 100f);
            if (stageText != null) stageText.text = $"Stage {stage}";
            if (stageProgressText != null) stageProgressText.text = $"{percent}%";
            if (enemyLevelText != null) enemyLevelText.text = $"Enemy Lv: {GameManager.Instance.EnemyLevel}";
        }

        UpdateSpellSlots();
    }

    private void UpdateSpellSlots()
    {
        UpdateSpellSlot(fireSlot, 0f, 0f);

        if (playerWeapon != null)
            UpdateSpellSlot(iceSlot, playerWeapon.IceSpellCooldownRemaining, playerWeapon.iceSpellCooldown);
        else
            UpdateSpellSlot(iceSlot, 0f, 0f);

        if (healAbility != null)
            UpdateSpellSlot(healSlot, healAbility.CooldownRemaining, healAbility.cooldown);
        else
            UpdateSpellSlot(healSlot, 0f, 0f);
    }

    private static void UpdateSpellSlot(SpellSlot slot, float remaining, float totalCooldown)
    {
        if (slot.iconImage == null) return;
        if (remaining > 0.01f && totalCooldown > 0.01f)
        {
            slot.cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / totalCooldown);
            slot.cooldownText.text = Mathf.CeilToInt(remaining).ToString();
            slot.iconImage.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        }
        else
        {
            slot.cooldownOverlay.fillAmount = 0f;
            slot.cooldownText.text = string.Empty;
            slot.iconImage.color = Color.white;
        }
    }

    private void BuildHealthBar()
    {
        GameObject barBorder = CreateUIElement("HealthBarBorder", transform);
        RectTransform borderRect = barBorder.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0, 0);
        borderRect.anchorMax = new Vector2(0, 0);
        borderRect.pivot = new Vector2(0, 0);
        borderRect.anchoredPosition = new Vector2(20, 22);
        borderRect.sizeDelta = new Vector2(360, 44);
        healthBarBorder = barBorder.AddComponent<Image>();
        healthBarBorder.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        GameObject barBg = CreateUIElement("HealthBarBG", barBorder.transform);
        RectTransform bgRect = barBg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(3, 3);
        bgRect.offsetMax = new Vector2(-3, -3);
        healthBarBackground = barBg.AddComponent<Image>();
        healthBarBackground.color = new Color(0.15f, 0.05f, 0.05f, 0.9f);

        GameObject barFill = CreateUIElement("HealthBarFill", barBg.transform);
        RectTransform fillRect = barFill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);
        healthBarFill = barFill.AddComponent<Image>();
        healthBarFill.color = new Color(0.8f, 0.1f, 0.1f, 1f);
        healthBarFillRect = fillRect;

        GameObject hpText = CreateUIElement("HealthText", barBorder.transform);
        RectTransform hpRect = hpText.GetComponent<RectTransform>();
        hpRect.anchorMin = Vector2.zero;
        hpRect.anchorMax = Vector2.one;
        hpRect.offsetMin = Vector2.zero;
        hpRect.offsetMax = Vector2.zero;
        healthText = hpText.AddComponent<Text>();
        healthText.text = "100 / 100";
        healthText.font = UiFont.Get();
        healthText.fontSize = UiFont.Scale(24);
        healthText.horizontalOverflow = HorizontalWrapMode.Overflow;
        healthText.verticalOverflow = VerticalWrapMode.Overflow;
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.color = Color.white;
        Shadow hpShadow = hpText.AddComponent<Shadow>();
        hpShadow.effectColor = new Color(0, 0, 0, 0.9f);
        hpShadow.effectDistance = new Vector2(1, -1);
    }

    private void BuildXPBar()
    {
        GameObject barBorder = CreateUIElement("XPBarBorder", transform);
        RectTransform borderRect = barBorder.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0, 0);
        borderRect.anchorMax = new Vector2(0, 0);
        borderRect.pivot = new Vector2(0, 0);
        borderRect.anchoredPosition = new Vector2(20, 70);
        borderRect.sizeDelta = new Vector2(360, 36);
        xpBarBorder = barBorder.AddComponent<Image>();
        xpBarBorder.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        GameObject barBg = CreateUIElement("XPBarBG", barBorder.transform);
        RectTransform bgRect = barBg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(3, 3);
        bgRect.offsetMax = new Vector2(-3, -3);
        xpBarBackground = barBg.AddComponent<Image>();
        xpBarBackground.color = new Color(0.05f, 0.07f, 0.12f, 0.9f);

        GameObject barFill = CreateUIElement("XPBarFill", barBg.transform);
        RectTransform fillRect = barFill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);
        xpBarFill = barFill.AddComponent<Image>();
        xpBarFill.color = new Color(0.25f, 0.55f, 1f, 1f);
        xpBarFillRect = fillRect;

        GameObject xpLabel = CreateUIElement("XPText", barBorder.transform);
        RectTransform xpRect = xpLabel.GetComponent<RectTransform>();
        xpRect.anchorMin = Vector2.zero;
        xpRect.anchorMax = Vector2.one;
        xpRect.offsetMin = Vector2.zero;
        xpRect.offsetMax = Vector2.zero;
        xpText = xpLabel.AddComponent<Text>();
        xpText.text = "Lv 1   0 / 100";
        xpText.font = UiFont.Get();
        xpText.fontSize = UiFont.Scale(20);
        xpText.alignment = TextAnchor.MiddleCenter;
        xpText.color = Color.white;
        xpText.horizontalOverflow = HorizontalWrapMode.Overflow;
        xpText.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow xpShadow = xpLabel.AddComponent<Shadow>();
        xpShadow.effectColor = new Color(0, 0, 0, 0.9f);
        xpShadow.effectDistance = new Vector2(1, -1);
    }

    private void BuildInfoPanel()
    {
        GameObject panel = CreateUIElement("InfoPanel", transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = new Vector2(-20, -20);
        int rowStage = UiFont.Scale(22);
        int rowSmall = UiFont.Scale(16);
        int totalRows = rowStage + rowStage + rowSmall + rowSmall;
        panelRect.sizeDelta = new Vector2(280, totalRows + 36);
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.6f);

        int y = -8;
        stageText = CreateInfoText("StageText", panel.transform, new Vector2(0, y), "Stage 1", 22, FontStyle.Bold);
        y -= rowStage + 4;
        stageProgressText = CreateInfoText("StageProgressText", panel.transform, new Vector2(0, y), "0%", 22, FontStyle.Bold);
        y -= rowStage + 6;
        timeText = CreateInfoText("TimeText", panel.transform, new Vector2(0, y), "Time: 00:00", 16, FontStyle.Normal);
        y -= rowSmall + 4;
        enemyLevelText = CreateInfoText("EnemyLevelText", panel.transform, new Vector2(0, y), "Enemy Lv: 1", 16, FontStyle.Normal);
    }

    private Text CreateInfoText(string name, Transform parent, Vector2 position, string defaultText, int fontSize = 24, FontStyle style = FontStyle.Normal)
    {
        GameObject obj = CreateUIElement(name, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = position;
        int scaledSize = UiFont.Scale(fontSize);
        rect.sizeDelta = new Vector2(0, scaledSize + 8);
        Text text = obj.AddComponent<Text>();
        text.text = defaultText;
        text.font = UiFont.Get();
        text.fontSize = scaledSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow shadow = obj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);
        return text;
    }

    private void BuildCrosshair()
    {
        GameObject ch = CreateUIElement("Crosshair", transform);
        RectTransform rect = ch.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(24, 24);
        crosshair = ch.AddComponent<Image>();
        crosshair.color = new Color(0, 0, 0, 0);

        Color armColor = new Color(1f, 1f, 1f, 0.85f);
        Color outline = new Color(0, 0, 0, 0.7f);
        float armLength = 10f;
        float armThickness = 2f;
        float gap = 4f;

        BuildCrosshairArm(ch.transform, new Vector2(0, gap + armLength * 0.5f), new Vector2(armThickness, armLength), armColor, outline);
        BuildCrosshairArm(ch.transform, new Vector2(0, -(gap + armLength * 0.5f)), new Vector2(armThickness, armLength), armColor, outline);
        BuildCrosshairArm(ch.transform, new Vector2(gap + armLength * 0.5f, 0), new Vector2(armLength, armThickness), armColor, outline);
        BuildCrosshairArm(ch.transform, new Vector2(-(gap + armLength * 0.5f), 0), new Vector2(armLength, armThickness), armColor, outline);

        GameObject dot = CreateUIElement("CrosshairDot", ch.transform);
        RectTransform dotRect = dot.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(2, 2);
        Image dotImg = dot.AddComponent<Image>();
        dotImg.color = armColor;
    }

    private void BuildCrosshairArm(Transform parent, Vector2 anchoredPos, Vector2 size, Color fill, Color outline)
    {
        GameObject arm = CreateUIElement("Arm", parent);
        RectTransform rect = arm.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        Image img = arm.AddComponent<Image>();
        img.color = fill;
        Outline o = arm.AddComponent<Outline>();
        o.effectColor = outline;
        o.effectDistance = new Vector2(1, -1);
    }

    private GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    private void BuildInventoryBar()
    {
        GameObject bar = CreateUIElement("InventoryBar", transform);
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -16);
        rect.sizeDelta = new Vector2(0, 56);
        inventoryBar = rect;

        var layout = bar.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = bar.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    }

    private void UpdateInventoryEntry(Item.Type type, int count)
    {
        if (inventoryBar == null) return;
        if (!inventoryEntries.TryGetValue(type, out GameObject entry))
        {
            entry = CreateInventoryEntry(type);
            inventoryEntries[type] = entry;
        }
        if (inventoryCountTexts.TryGetValue(type, out Text countText))
            countText.text = $"x{count}";
    }

    private GameObject CreateInventoryEntry(Item.Type type)
    {
        Color tint = Item.ColorForType(type);
        float size = Mathf.Max(32f, inventoryEntrySize);

        GameObject entry = CreateUIElement($"Item_{type}", inventoryBar);
        RectTransform entryRect = entry.GetComponent<RectTransform>();
        entryRect.sizeDelta = new Vector2(size, size);
        Image bg = entry.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        // Border in the item's color, thicker than the default outline so it reads as a frame.
        var entryOutline = entry.AddComponent<Outline>();
        entryOutline.effectColor = new Color(tint.r, tint.g, tint.b, 1f);
        entryOutline.effectDistance = new Vector2(2.5f, -2.5f);

        GameObject iconObj = CreateUIElement("Icon", entry.transform);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(4, 4);
        iconRect.offsetMax = new Vector2(-4, -4);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = IconFor(type);
        iconImg.preserveAspect = true;
        iconImg.color = iconImg.sprite != null ? Color.white : tint;

        GameObject countObj = CreateUIElement("Count", entry.transform);
        RectTransform countRect = countObj.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(1, 0);
        countRect.anchorMax = new Vector2(1, 0);
        countRect.pivot = new Vector2(1, 0);
        countRect.anchoredPosition = new Vector2(-3, 2);
        countRect.sizeDelta = new Vector2(size - 6, UiFont.Scale(20) + 4);
        Text count = countObj.AddComponent<Text>();
        count.text = "x1";
        count.font = UiFont.Get();
        count.fontSize = UiFont.Scale(20);
        count.fontStyle = FontStyle.Bold;
        count.alignment = TextAnchor.LowerRight;
        count.color = Color.white;
        count.horizontalOverflow = HorizontalWrapMode.Overflow;
        count.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow countShadow = countObj.AddComponent<Shadow>();
        countShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        countShadow.effectDistance = new Vector2(1, -1);
        inventoryCountTexts[type] = count;

        return entry;
    }

    private void BuildCoinCounter()
    {
        GameObject panel = CreateUIElement("CoinCounter", transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = new Vector2(-20, -160);
        panelRect.sizeDelta = new Vector2(190, 60);
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.6f);

        if (coinIcon != null)
        {
            GameObject icon = CreateUIElement("Icon", panel.transform);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(8, 0);
            iconRect.sizeDelta = new Vector2(44, 44);
            Image iconImg = icon.AddComponent<Image>();
            iconImg.sprite = coinIcon;
            iconImg.preserveAspect = true;
        }

        GameObject textObj = CreateUIElement("CountText", panel.transform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(60, 0);
        textRect.offsetMax = new Vector2(-8, 0);
        coinCountText = textObj.AddComponent<Text>();
        coinCountText.text = "0";
        coinCountText.font = UiFont.Get();
        coinCountText.fontSize = UiFont.Scale(34);
        coinCountText.fontStyle = FontStyle.Bold;
        coinCountText.alignment = TextAnchor.MiddleLeft;
        coinCountText.color = Color.white;
        coinCountText.horizontalOverflow = HorizontalWrapMode.Overflow;
        coinCountText.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);
    }

    private void BuildSpellBar()
    {
        const float slotSize = 80f;
        const float slotPadding = 8f;
        const float labelHeight = 32f;
        const float labelGap = 4f;

        float barWidth = 3f * slotSize + 2f * slotPadding;
        float barHeight = slotSize + labelGap + labelHeight;

        GameObject container = CreateUIElement("SpellBar", transform);
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(1, 0);
        containerRect.anchorMax = new Vector2(1, 0);
        containerRect.pivot = new Vector2(1, 0);
        containerRect.anchoredPosition = new Vector2(-20, 20);
        containerRect.sizeDelta = new Vector2(barWidth, barHeight);

        fireSlot = BuildSpellSlot(container.transform, fireSpellIcon, "LMB", 0, slotSize, slotPadding, labelHeight, labelGap);
        iceSlot = BuildSpellSlot(container.transform, iceSpellIcon, "Q", 1, slotSize, slotPadding, labelHeight, labelGap);
        healSlot = BuildSpellSlot(container.transform, healSpellIcon, "E", 2, slotSize, slotPadding, labelHeight, labelGap);
    }

    private SpellSlot BuildSpellSlot(Transform parent, Sprite icon, string inputLabel, int columnIndex, float slotSize, float slotPadding, float labelHeight, float labelGap)
    {
        SpellSlot slot = new SpellSlot();
        float xPos = columnIndex * (slotSize + slotPadding);

        GameObject box = CreateUIElement($"SpellSlot_{columnIndex}", parent);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0, 1);
        boxRect.anchorMax = new Vector2(0, 1);
        boxRect.pivot = new Vector2(0, 1);
        boxRect.anchoredPosition = new Vector2(xPos, 0);
        boxRect.sizeDelta = new Vector2(slotSize, slotSize);
        Image boxBg = box.AddComponent<Image>();
        // Less opaque, slightly lighter bg so the spell icons aren't fighting a near-black backdrop.
        boxBg.color = new Color(0.08f, 0.08f, 0.1f, 0.55f);
        Outline boxOutline = box.AddComponent<Outline>();
        boxOutline.effectColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        boxOutline.effectDistance = new Vector2(2, -2);

        GameObject iconObj = CreateUIElement("Icon", box.transform);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        // Tighter padding so the icon fills more of the slot.
        iconRect.offsetMin = new Vector2(2, 2);
        iconRect.offsetMax = new Vector2(-2, -2);
        slot.iconImage = iconObj.AddComponent<Image>();
        slot.iconImage.sprite = icon;
        slot.iconImage.preserveAspect = true;
        // Soft white outline behind the icon, reads as a subtle glow.
        Outline iconGlow = iconObj.AddComponent<Outline>();
        iconGlow.effectColor = new Color(1f, 1f, 1f, 0.6f);
        iconGlow.effectDistance = new Vector2(1, -1);

        GameObject overlayObj = CreateUIElement("CooldownOverlay", box.transform);
        RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        slot.cooldownOverlay = overlayObj.AddComponent<Image>();
        slot.cooldownOverlay.color = new Color(0, 0, 0, 0.7f);
        slot.cooldownOverlay.sprite = CreateWhiteSprite();
        slot.cooldownOverlay.type = Image.Type.Filled;
        slot.cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
        slot.cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
        slot.cooldownOverlay.fillClockwise = true;
        slot.cooldownOverlay.fillAmount = 0f;
        slot.cooldownOverlay.raycastTarget = false;

        GameObject cdTextObj = CreateUIElement("CooldownText", box.transform);
        RectTransform cdTextRect = cdTextObj.GetComponent<RectTransform>();
        cdTextRect.anchorMin = Vector2.zero;
        cdTextRect.anchorMax = Vector2.one;
        cdTextRect.offsetMin = Vector2.zero;
        cdTextRect.offsetMax = Vector2.zero;
        slot.cooldownText = cdTextObj.AddComponent<Text>();
        slot.cooldownText.text = string.Empty;
        slot.cooldownText.font = UiFont.Get();
        slot.cooldownText.fontSize = UiFont.Scale(36);
        slot.cooldownText.fontStyle = FontStyle.Bold;
        slot.cooldownText.alignment = TextAnchor.MiddleCenter;
        slot.cooldownText.color = Color.white;
        slot.cooldownText.raycastTarget = false;
        Shadow cdShadow = cdTextObj.AddComponent<Shadow>();
        cdShadow.effectColor = new Color(0, 0, 0, 0.9f);
        cdShadow.effectDistance = new Vector2(1, -1);

        GameObject labelObj = CreateUIElement("InputLabel", parent);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(0, 1);
        labelRect.pivot = new Vector2(0, 1);
        labelRect.anchoredPosition = new Vector2(xPos, -(slotSize + labelGap));
        labelRect.sizeDelta = new Vector2(slotSize, labelHeight);
        Text label = labelObj.AddComponent<Text>();
        label.text = inputLabel;
        label.font = UiFont.Get();
        label.fontSize = UiFont.Scale(20);
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow labelShadow = labelObj.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0, 0, 0, 0.9f);
        labelShadow.effectDistance = new Vector2(1, -1);

        return slot;
    }

    private static Sprite cachedWhiteSprite;
    private static Sprite CreateWhiteSprite()
    {
        if (cachedWhiteSprite != null) return cachedWhiteSprite;
        Texture2D tex = new Texture2D(2, 2);
        Color32[] pixels = new Color32[4];
        for (int i = 0; i < 4; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        return cachedWhiteSprite;
    }

    private void BuildGameOverPanel()
    {
        gameOverPanel = CreateUIElement("GameOverPanel", transform);
        RectTransform panelRect = gameOverPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelBg = gameOverPanel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.7f);

        // GAME OVER text, lifted above center so the button sits beneath it.
        GameObject goText = CreateUIElement("GameOverText", gameOverPanel.transform);
        RectTransform goRect = goText.GetComponent<RectTransform>();
        goRect.anchorMin = new Vector2(0.5f, 0.5f);
        goRect.anchorMax = new Vector2(0.5f, 0.5f);
        goRect.anchoredPosition = new Vector2(0, 80);
        goRect.sizeDelta = new Vector2(600, 120);
        gameOverText = goText.AddComponent<Text>();
        gameOverText.text = "GAME OVER";
        gameOverText.font = UiFont.Get();
        gameOverText.fontSize = UiFont.Scale(96);
        gameOverText.fontStyle = FontStyle.Bold;
        gameOverText.alignment = TextAnchor.MiddleCenter;
        gameOverText.color = Color.red;

        BuildReturnButton(gameOverPanel.transform, new Vector2(0, -60));

        gameOverPanel.SetActive(false);
    }

    private void BuildReturnButton(Transform parent, Vector2 anchoredPosition)
    {
        GameObject btnGO = CreateUIElement("ReturnButton", parent);
        RectTransform btnRect = btnGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = anchoredPosition;
        btnRect.sizeDelta = new Vector2(360, 80);
        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.onClick.AddListener(ReturnToMainMenu);

        GameObject btnTextGO = CreateUIElement("Text", btnGO.transform);
        RectTransform btnTextRect = btnTextGO.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;
        Text btnText = btnTextGO.AddComponent<Text>();
        btnText.text = "Return to Main Menu";
        btnText.font = UiFont.Get();
        btnText.fontSize = UiFont.Scale(32);
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.black;
        btnText.raycastTarget = false;
    }

    private void ReturnToMainMenu()
    {
        // Unfreeze before leaving, or MainMenu loads with timeScale = 0 and its UI/Awake logic stalls.
        // Clear run state so the next playthrough starts fresh instead of restoring the dead run.
        Time.timeScale = 1f;
        PlayerRunState.Clear();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private GameObject victoryPanel;

    public void ShowVictory()
    {
        if (victoryPanel == null) BuildVictoryPanel();
        victoryPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    private void BuildVictoryPanel()
    {
        victoryPanel = CreateUIElement("VictoryPanel", transform);
        RectTransform panelRect = victoryPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelBg = victoryPanel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.7f);

        // Large gold title, sits in the upper third.
        GameObject titleGO = CreateUIElement("VictoryTitle", victoryPanel.transform);
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0, 140);
        titleRect.sizeDelta = new Vector2(800, 160);
        Text title = titleGO.AddComponent<Text>();
        title.text = "Victory!";
        title.font = UiFont.Get();
        title.fontSize = UiFont.Scale(96);
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(1f, 0.85f, 0.25f, 1f);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject subGO = CreateUIElement("VictorySubtitle", victoryPanel.transform);
        RectTransform subRect = subGO.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.5f);
        subRect.anchorMax = new Vector2(0.5f, 0.5f);
        subRect.anchoredPosition = new Vector2(0, 30);
        subRect.sizeDelta = new Vector2(900, 80);
        Text sub = subGO.AddComponent<Text>();
        sub.text = "You have successfully defeated the Veil and freed the land from the shadows";
        sub.font = UiFont.Get();
        sub.fontSize = UiFont.Scale(28);
        sub.fontStyle = FontStyle.Normal;
        sub.alignment = TextAnchor.MiddleCenter;
        sub.color = Color.white;
        sub.horizontalOverflow = HorizontalWrapMode.Overflow;
        sub.verticalOverflow = VerticalWrapMode.Overflow;

        // Same style as the game-over button.
        BuildReturnButton(victoryPanel.transform, new Vector2(0, -90));

        victoryPanel.SetActive(false);
    }

    public void UpdateHealthBar(float current, float max)
    {
        targetHealth = Mathf.Clamp01(current / max);
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    public void UpdateXPBar(int currentXP, int xpToNext)
    {
        targetXP = xpToNext > 0 ? Mathf.Clamp01((float)currentXP / xpToNext) : 0f;
        if (xpText != null)
        {
            int level = playerLevel != null ? playerLevel.CurrentLevel : 1;
            xpText.text = $"Lv {level}   {currentXP} / {xpToNext}";
        }
    }

    public void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            // Older prefabs predate the return-to-menu button; patch it in at show-time.
            EnsureGameOverButton();
            gameOverPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // Freeze gameplay. The button still works since UI events run on unscaled time.
            Time.timeScale = 0f;
        }
    }

    private void EnsureGameOverButton()
    {
        if (gameOverPanel == null) return;
        if (gameOverPanel.transform.Find("ReturnButton") != null) return;

        // Lift the GAME OVER text so the button has room beneath it. Don't touch its size/font.
        if (gameOverText != null)
        {
            RectTransform tRect = gameOverText.rectTransform;
            tRect.anchoredPosition = new Vector2(0, 80);
            gameOverText.horizontalOverflow = HorizontalWrapMode.Overflow;
            gameOverText.verticalOverflow = VerticalWrapMode.Overflow;
            gameOverText.fontStyle = FontStyle.Bold;
        }

        BuildReturnButton(gameOverPanel.transform, new Vector2(0, -60));
    }
}

using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    [Header("Health Bar")]
    public Image healthBarFill;
    public Image healthBarBackground;
    public Image healthBarBorder;
    public Text healthText;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public Text gameOverText;

    [Header("Crosshair")]
    public Image crosshair;

    private PlayerHealth playerHealth;
    private Canvas canvas;
    private RectTransform healthBarFillRect;

    private float displayedHealth = 1f;
    private float targetHealth = 1f;

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        BuildHealthBar();
        BuildCrosshair();
        BuildGameOverPanel();
    }

    private void Start()
    {
        playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.OnHealthChanged += UpdateHealthBar;
    }

    private void Update()
    {
        if (healthBarFillRect != null)
        {
            displayedHealth = Mathf.Lerp(displayedHealth, targetHealth, Time.deltaTime * 8f);
            healthBarFillRect.anchorMax = new Vector2(displayedHealth, 1f);
        }
    }

    private void BuildHealthBar()
    {
        GameObject barBorder = CreateUIElement("HealthBarBorder", transform);
        RectTransform borderRect = barBorder.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0, 0);
        borderRect.anchorMax = new Vector2(0, 0);
        borderRect.pivot = new Vector2(0, 0);
        borderRect.anchoredPosition = new Vector2(20, 20);
        borderRect.sizeDelta = new Vector2(320, 32);
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
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        healthText.fontSize = 18;
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.color = Color.white;
        Shadow hpShadow = hpText.AddComponent<Shadow>();
        hpShadow.effectColor = new Color(0, 0, 0, 0.9f);
        hpShadow.effectDistance = new Vector2(1, -1);
    }

    private void BuildCrosshair()
    {
        GameObject ch = CreateUIElement("Crosshair", transform);
        RectTransform rect = ch.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(8, 8);
        crosshair = ch.AddComponent<Image>();
        crosshair.color = new Color(1f, 1f, 1f, 0.6f);
    }

    private GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
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

        GameObject goText = CreateUIElement("GameOverText", gameOverPanel.transform);
        RectTransform goRect = goText.GetComponent<RectTransform>();
        goRect.anchorMin = new Vector2(0.5f, 0.5f);
        goRect.anchorMax = new Vector2(0.5f, 0.5f);
        goRect.sizeDelta = new Vector2(400, 100);
        gameOverText = goText.AddComponent<Text>();
        gameOverText.text = "GAME OVER";
        gameOverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gameOverText.fontSize = 48;
        gameOverText.alignment = TextAnchor.MiddleCenter;
        gameOverText.color = Color.red;

        gameOverPanel.SetActive(false);
    }

    public void UpdateHealthBar(float current, float max)
    {
        targetHealth = Mathf.Clamp01(current / max);
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    public void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Enemy))]
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Visibility")]
    public float showDistance = 28f;
    public float hideAtFullThreshold = 0.999f;

    [Header("Placement")]
    public Vector3 localOffset = new Vector3(0f, 2.5f, 0f);
    public Vector2 worldSize = new Vector2(1.0f, 0.12f);

    [Header("Colors")]
    public Color borderColor = new Color(0f, 0f, 0f, 0.95f);
    public Color backgroundColor = new Color(0.15f, 0.04f, 0.04f, 0.85f);
    public Color fillColor = new Color(0.85f, 0.15f, 0.15f, 1f);

    private Enemy enemy;
    private Camera cam;
    private Transform playerTransform;
    private Canvas canvas;
    private Image fillImage;
    private RectTransform fillRect;
    private GameObject root;
    private const float CanvasUnitScale = 0.01f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) playerTransform = ph.transform;
        BuildBar();
    }

    private void BuildBar()
    {
        root = new GameObject("HealthBar");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = localOffset;

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRect = (RectTransform)canvas.transform;
        canvasRect.sizeDelta = new Vector2(worldSize.x / CanvasUnitScale, worldSize.y / CanvasUnitScale);
        canvasRect.localScale = Vector3.one * CanvasUnitScale;

        Image border = NewChildImage(root.transform, "Border");
        StretchFill(border.rectTransform, Vector2.zero, Vector2.zero);
        border.color = borderColor;

        Image bg = NewChildImage(border.transform, "BG");
        StretchFill(bg.rectTransform, new Vector2(2, 2), new Vector2(-2, -2));
        bg.color = backgroundColor;

        Image fill = NewChildImage(bg.transform, "Fill");
        var fr = fill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = new Vector2(1f, 1f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = Vector2.zero;
        fr.offsetMax = Vector2.zero;
        fill.color = fillColor;
        fillImage = fill;
        fillRect = fr;

        root.SetActive(false);
    }

    private static Image NewChildImage(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go.AddComponent<Image>();
    }

    private static void StretchFill(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public void ApplyOverrides(Vector3 offset, float showDist, Vector2 size)
    {
        localOffset = offset;
        showDistance = showDist;
        worldSize = size;
        if (root != null) root.transform.localPosition = localOffset;
        if (canvas != null)
        {
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(worldSize.x / CanvasUnitScale, worldSize.y / CanvasUnitScale);
        }
    }

    private void LateUpdate()
    {
        if (enemy == null || root == null) return;
        if (cam == null) cam = Camera.main;

        float maxHp = enemy.MaxHealth;
        float curHp = enemy.CurrentHealth;
        float ratio = maxHp > 0f ? Mathf.Clamp01(curHp / maxHp) : 0f;

        bool show = ratio > 0f && ratio < hideAtFullThreshold;
        if (show && playerTransform != null && (transform.position - playerTransform.position).sqrMagnitude > showDistance * showDistance)
            show = false;

        if (root.activeSelf != show) root.SetActive(show);
        if (!show || cam == null) return;

        if (fillRect != null)
            fillRect.anchorMax = new Vector2(ratio, 1f);

        Vector3 toCam = root.transform.position - cam.transform.position;
        if (toCam.sqrMagnitude > 0.0001f)
            root.transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
    }
}

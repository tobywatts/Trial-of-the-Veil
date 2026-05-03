using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class Chest : MonoBehaviour
{
    [Header("Item Drop")]
    public Vector3 itemSpawnOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Interaction")]
    public float interactRange = 3f;
    public Vector3 promptOffset = new Vector3(0f, 1.4f, 0f);

    [Header("Cost")]
    [Tooltip("Coins needed to open this chest.")]
    public int coinCost = 30;
    [Tooltip("Set by the ChestSpawner.")]
    public Sprite coinIcon;

    [Header("Idle Motion")]
    public float bobAmplitude = 0.06f;
    public float bobSpeed = 2f;

    [Header("Open Animation")]
    [Tooltip("Local-X rotation of the lid child when opened. The lid hinge is along its local X axis.")]
    public float openLidAngle = 80f;
    [Tooltip("Seconds for the lid to swing open.")]
    public float openAnimationDuration = 0.35f;

    private bool opened;
    private Transform player;
    private Transform lid;
    private Quaternion lidClosedRotation;
    private Quaternion lidOpenRotation;
    private float openAnimationT;
    private GameObject promptRoot;
    private Text promptText;
    private Camera cam;
    private Vector3 basePosition;
    private float bobPhase;
    private PlayerCoins wallet;
    private static readonly Color AffordColor = new Color(1f, 0.95f, 0.6f, 1f);
    private static readonly Color CantAffordColor = new Color(1f, 0.45f, 0.45f, 1f);

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;
        basePosition = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        wallet = FindFirstObjectByType<PlayerCoins>();
        FindLid();
        BuildPrompt();
    }

    private void FindLid()
    {
        // Both the prefab lid and the primitive fallback are named starting with "Top". First match wins.
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == transform) continue;
            if (children[i].name.StartsWith("Top"))
            {
                lid = children[i];
                lidClosedRotation = lid.localRotation;
                lidOpenRotation = lidClosedRotation * Quaternion.Euler(openLidAngle, 0f, 0f);
                return;
            }
        }
    }

    private void BuildPrompt()
    {
        promptRoot = new GameObject("ChestPrompt");
        promptRoot.transform.SetParent(transform, false);
        promptRoot.transform.localPosition = promptOffset;

        var canvas = promptRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)canvas.transform;
        rect.sizeDelta = new Vector2(260, 60);
        rect.localScale = Vector3.one * 0.01f;

        var go = new GameObject("Text");
        go.transform.SetParent(promptRoot.transform, false);
        var tr = go.AddComponent<RectTransform>();
        var t = go.AddComponent<Text>();
        t.text = $"[F] {coinCost}";
        t.font = UiFont.Get();
        t.fontSize = UiFont.Scale(32);
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleRight;
        t.color = AffordColor;
        promptText = t;
        tr.anchorMin = new Vector2(0, 0);
        tr.anchorMax = new Vector2(0.65f, 1);
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.95f);
        shadow.effectDistance = new Vector2(2, -2);

        if (coinIcon != null)
        {
            var iconGO = new GameObject("CoinIcon");
            iconGO.transform.SetParent(promptRoot.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.65f, 0.5f);
            iconRT.anchorMax = new Vector2(0.65f, 0.5f);
            iconRT.pivot = new Vector2(0, 0.5f);
            iconRT.anchoredPosition = new Vector2(6, 0);
            iconRT.sizeDelta = new Vector2(46, 46);
            var img = iconGO.AddComponent<Image>();
            img.sprite = coinIcon;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        promptRoot.SetActive(false);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver) return;

        if (opened)
        {
            UpdateOpenAnimation();
            return;
        }

        if (player == null) return;

        float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmplitude;
        transform.position = basePosition + Vector3.up * bob;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        bool inRange = toPlayer.sqrMagnitude <= interactRange * interactRange;

        if (promptRoot != null && promptRoot.activeSelf != inRange)
            promptRoot.SetActive(inRange);

        if (inRange)
        {
            BillboardPrompt();
            UpdatePromptText();
            if (InteractPressed() && wallet != null && wallet.TrySpend(coinCost)) Open();
        }
    }

    private void UpdateOpenAnimation()
    {
        if (lid == null) return;
        if (openAnimationT >= 1f) return;
        float dt = openAnimationDuration > 0.001f ? Time.deltaTime / openAnimationDuration : 1f;
        openAnimationT = Mathf.Min(1f, openAnimationT + dt);
        float eased = 1f - (1f - openAnimationT) * (1f - openAnimationT); // ease-out quad
        lid.localRotation = Quaternion.Slerp(lidClosedRotation, lidOpenRotation, eased);
    }

    private void UpdatePromptText()
    {
        if (promptText == null) return;
        int have = wallet != null ? wallet.Coins : 0;
        promptText.color = have >= coinCost ? AffordColor : CantAffordColor;
    }

    private void BillboardPrompt()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || promptRoot == null) return;
        Vector3 toCam = promptRoot.transform.position - cam.transform.position;
        if (toCam.sqrMagnitude > 0.0001f)
            promptRoot.transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
    }

    private static bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }

    private void Open()
    {
        opened = true;
        if (promptRoot != null) promptRoot.SetActive(false);
        // Snap chest to its rest position so the lid animates cleanly without bobbing under it.
        transform.position = basePosition;
        Item.Spawn(transform.position + itemSpawnOffset, Item.RandomType());
    }
}

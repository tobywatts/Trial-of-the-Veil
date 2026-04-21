using UnityEngine;
using UnityEngine.UI;

// Builds a "How to Play" overlay: a one-line game pitch plus a three-column controls table.
public static class HowToPlayPanelBuilder
{
    public class Style
    {
        public Sprite panelSprite;
        public Sprite buttonSprite;
        public Color panelColor;
        public Color titleColor;
        public Color buttonColor;
        public Color buttonHoverColor;
        public Color buttonTextColor;
        public Vector2 panelSize = new Vector2(720, 760);
        public Vector2 buttonSize = new Vector2(220, 56);
        public Vector2 panelOffset = Vector2.zero;
    }

    private static readonly string[][] Controls = new string[][]
    {
        new[] { "Action", "Keyboard", "Controller" },
        new[] { "Move", "WASD", "LS / L3" },
        new[] { "Look", "Mouse", "RS / R3" },
        new[] { "Jump", "Space", "A / X" },
        new[] { "Sprint", "Shift", "LS / L3 (Click)" },
        new[] { "Attack", "Left Click", "RB / R1" },
        new[] { "Ice Spell", "Q", "RT / R2" },
        new[] { "Heal", "E", "LB / L1" },
        new[] { "Interact", "F", "A / X" },
        new[] { "Pause", "Esc", "Start" },
    };

    public static GameObject Build(Transform parent, Style s, System.Action onBack)
    {
        var panel = UiBuilder.NewChildImage(parent, "HowToPlayPanel", s.panelSprite);
        panel.type = Image.Type.Sliced;
        panel.color = s.panelColor;
        var rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = s.panelSize;
        rt.anchoredPosition = s.panelOffset;

        var title = UiBuilder.NewChildText(panel.transform, "Title", "How to Play", 70, FontStyle.Bold);
        title.color = s.titleColor;
        title.alignment = TextAnchor.MiddleCenter;
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, -10);
        titleRT.sizeDelta = new Vector2(0, 80);

        var desc = UiBuilder.NewChildText(panel.transform, "Description",
            "A roguelike adventure - defeat enemies and remove the veil as fast as you can.",
            26, FontStyle.Normal);
        desc.color = Color.white;
        desc.alignment = TextAnchor.MiddleCenter;
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.verticalOverflow = VerticalWrapMode.Overflow;
        var descRT = desc.rectTransform;
        descRT.anchorMin = new Vector2(0, 1);
        descRT.anchorMax = new Vector2(1, 1);
        descRT.pivot = new Vector2(0.5f, 1f);
        descRT.anchoredPosition = new Vector2(0, -100);
        descRT.sizeDelta = new Vector2(-60, 80); // 30px padding each side

        // Controls table: rows start below the description and step down 38px each.
        const float TableTop = -180f;
        const float RowHeight = 38f;
        for (int i = 0; i < Controls.Length; i++)
        {
            bool isHeader = i == 0;
            BuildRow(panel.transform, new Vector2(0, TableTop - i * RowHeight), new Vector2(s.panelSize.x - 60, RowHeight),
                     Controls[i][0], Controls[i][1], Controls[i][2], isHeader);
        }

        BuildBackButton(panel.transform, s, onBack);

        return panel.gameObject;
    }

    private static void BuildRow(Transform parent, Vector2 anchoredPos, Vector2 size,
                                 string action, string kb, string controller, bool isHeader)
    {
        var row = UiBuilder.NewChildContainer(parent, action + "Row", anchoredPos, size);
        var rt = row.GetComponent<RectTransform>();
        // Top-anchored: anchoredPos is measured down from the panel's top edge.
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;

        Color color = isHeader ? new Color(1f, 0.9f, 0.4f, 1f) : Color.white;
        FontStyle fs = isHeader ? FontStyle.Bold : FontStyle.Normal;
        int size_ = isHeader ? 26 : 24;

        AddCell(row.transform, action, new Vector2(0f, 0f), new Vector2(0.36f, 1f), TextAnchor.MiddleLeft, color, size_, fs);
        AddCell(row.transform, kb, new Vector2(0.36f, 0f), new Vector2(0.66f, 1f), TextAnchor.MiddleCenter, color, size_, fs);
        AddCell(row.transform, controller, new Vector2(0.66f, 0f), new Vector2(1f, 1f), TextAnchor.MiddleCenter, color, size_, fs);
    }

    private static void AddCell(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax,
                                TextAnchor align, Color color, int size, FontStyle style)
    {
        var cell = UiBuilder.NewChildText(parent, text + "Cell", text, size, style);
        cell.color = color;
        cell.alignment = align;
        cell.horizontalOverflow = HorizontalWrapMode.Overflow;
        cell.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = cell.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(12, 0);
        rt.offsetMax = new Vector2(-12, 0);
    }

    private static void BuildBackButton(Transform parent, Style s, System.Action onBack)
    {
        var img = UiBuilder.NewChildImage(parent, "BackButton", s.buttonSprite);
        img.type = Image.Type.Sliced;
        img.color = s.buttonColor;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = s.buttonSize;
        rt.anchoredPosition = new Vector2(0, 32);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = s.buttonColor;
        colors.highlightedColor = s.buttonHoverColor;
        colors.pressedColor = s.buttonHoverColor;
        colors.selectedColor = s.buttonColor;
        colors.disabledColor = s.buttonColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.onClick.AddListener(() => onBack?.Invoke());

        var text = UiBuilder.NewChildText(img.transform, "Text", "Back", 32, FontStyle.Bold);
        // Fall back to black if the caller passed an unset Color (alpha 0), else the text is invisible.
        Color tc = s.buttonTextColor;
        if (tc.a < 0.01f) tc = Color.black;
        text.color = tc;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

}

using UnityEngine;
using UnityEngine.UI;

public static class ItemLogPanelBuilder
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
        public Vector2 panelSize = new Vector2(720, 600);
        public Vector2 buttonSize = new Vector2(220, 56);
        public Vector2 panelOffset = Vector2.zero;
    }

    public static GameObject Build(Transform parent, Style s, System.Func<Item.Type, Sprite> iconLookup, System.Action onBack)
    {
        var panel = UiBuilder.NewChildImage(parent, "ItemLogPanel", s.panelSprite);
        panel.type = Image.Type.Sliced;
        panel.color = s.panelColor;
        var rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = s.panelSize;
        rt.anchoredPosition = s.panelOffset;

        var title = UiBuilder.NewChildText(panel.transform, "Title", "Item Log", 64, FontStyle.Bold);
        title.color = s.titleColor;
        title.alignment = TextAnchor.MiddleCenter;
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, -32);
        titleRT.sizeDelta = new Vector2(0, 80);

        BuildScrollList(panel.transform, s, iconLookup);
        BuildBackButton(panel.transform, s, onBack);

        return panel.gameObject;
    }

    private static void BuildScrollList(Transform parent, Style s, System.Func<Item.Type, Sprite> iconLookup)
    {
        const float topReserved = 130f;
        const float bottomReserved = 110f;
        const float horizontalPadding = 30f;
        const float scrollbarWidth = 22f;
        const float scrollbarGap = 8f;

        float listHeight = s.panelSize.y - topReserved - bottomReserved;
        float listAreaWidth = s.panelSize.x - 2f * horizontalPadding;
        float viewportWidth = Mathf.Max(40f, listAreaWidth - scrollbarWidth - scrollbarGap);
        float centerY = (bottomReserved - topReserved) * 0.5f;
        float viewportCenterX = -(scrollbarWidth + scrollbarGap) * 0.5f;
        float scrollbarCenterX = (viewportWidth + scrollbarGap) * 0.5f;

        var viewport = UiBuilder.NewChildContainer(parent, "Viewport", new Vector2(viewportCenterX, centerY), new Vector2(viewportWidth, listHeight));
        var viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0.25f);
        viewport.AddComponent<Mask>().showMaskGraphic = true;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewport.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        // A fresh RectTransform defaults sizeDelta to (100,100); with stretched X anchors that overflows the viewport.
        contentRT.sizeDelta = Vector2.zero;
        var layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = BuildScrollbar(parent, new Vector2(scrollbarCenterX, centerY), new Vector2(scrollbarWidth, listHeight));

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRT;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        var stickScroll = viewport.AddComponent<ControllerScroll>();
        stickScroll.scrollRect = scroll;

        var values = System.Enum.GetValues(typeof(Item.Type));
        for (int i = 0; i < values.Length; i++)
        {
            Item.Type t = (Item.Type)values.GetValue(i);
            BuildRow(contentGO.transform, t, iconLookup != null ? iconLookup(t) : null);
        }
    }

    private static Scrollbar BuildScrollbar(Transform parent, Vector2 pos, Vector2 size)
    {
        var bg = UiBuilder.NewChildImage(parent, "Scrollbar", null);
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        var bgRT = bg.rectTransform;
        bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.pivot = new Vector2(0.5f, 0.5f);
        bgRT.sizeDelta = size;
        bgRT.anchoredPosition = pos;

        var slidingArea = new GameObject("Sliding Area");
        slidingArea.transform.SetParent(bg.transform, false);
        var saRT = slidingArea.AddComponent<RectTransform>();
        saRT.anchorMin = Vector2.zero;
        saRT.anchorMax = Vector2.one;
        saRT.offsetMin = new Vector2(2, 2);
        saRT.offsetMax = new Vector2(-2, -2);

        var handle = UiBuilder.NewChildImage(slidingArea.transform, "Handle", null);
        handle.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var hRT = handle.rectTransform;
        hRT.anchorMin = Vector2.zero;
        hRT.anchorMax = Vector2.one;
        hRT.offsetMin = Vector2.zero;
        hRT.offsetMax = Vector2.zero;

        var sb = bg.gameObject.AddComponent<Scrollbar>();
        sb.targetGraphic = handle;
        sb.handleRect = hRT;
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.value = 1f;
        return sb;
    }

    private static void BuildRow(Transform parent, Item.Type t, Sprite icon)
    {
        const float rowHeight = 140f;
        const float iconSize = 96f;

        var row = new GameObject($"Row_{t}");
        row.transform.SetParent(parent, false);
        var rowRT = row.AddComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0, rowHeight);
        var rowBg = row.AddComponent<Image>();
        rowBg.color = new Color(0, 0, 0, 0.35f);

        Color tint = Item.ColorForType(t);

        // Icon square anchored to the left with colored border.
        var iconBox = UiBuilder.NewChildImage(row.transform, "IconBox", null);
        iconBox.color = new Color(0, 0, 0, 0.7f);
        var iconBoxRT = iconBox.rectTransform;
        iconBoxRT.anchorMin = new Vector2(0, 0.5f);
        iconBoxRT.anchorMax = new Vector2(0, 0.5f);
        iconBoxRT.pivot = new Vector2(0, 0.5f);
        iconBoxRT.anchoredPosition = new Vector2(10, 0);
        iconBoxRT.sizeDelta = new Vector2(iconSize, iconSize);
        var iconBorder = iconBox.gameObject.AddComponent<Outline>();
        iconBorder.effectColor = new Color(tint.r, tint.g, tint.b, 1f);
        iconBorder.effectDistance = new Vector2(3, -3);

        var iconImg = UiBuilder.NewChildImage(iconBox.transform, "Icon", icon);
        iconImg.preserveAspect = true;
        iconImg.color = icon != null ? Color.white : tint;
        var iconRT = iconImg.rectTransform;
        iconRT.anchorMin = Vector2.zero;
        iconRT.anchorMax = Vector2.one;
        iconRT.offsetMin = new Vector2(6, 6);
        iconRT.offsetMax = new Vector2(-6, -6);

        // Name on top, description underneath.
        var name = UiBuilder.NewChildText(row.transform, "Name", Item.FullNameFor(t), 30, FontStyle.Bold);
        name.color = new Color(tint.r * 0.9f + 0.3f, tint.g * 0.9f + 0.3f, tint.b * 0.9f + 0.3f, 1f);
        name.alignment = TextAnchor.LowerLeft;
        var nameRT = name.rectTransform;
        nameRT.anchorMin = new Vector2(0, 0.5f);
        nameRT.anchorMax = new Vector2(1, 1);
        nameRT.offsetMin = new Vector2(iconSize + 24, 0);
        nameRT.offsetMax = new Vector2(-12, -8);

        var desc = UiBuilder.NewChildText(row.transform, "Desc", Item.DescriptionFor(t), 22, FontStyle.Normal);
        desc.color = Color.white;
        desc.alignment = TextAnchor.UpperLeft;
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        var descRT = desc.rectTransform;
        descRT.anchorMin = new Vector2(0, 0);
        descRT.anchorMax = new Vector2(1, 0.5f);
        descRT.offsetMin = new Vector2(iconSize + 24, 8);
        descRT.offsetMax = new Vector2(-12, 0);
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
        rt.anchoredPosition = new Vector2(0, 28);

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

        var text = UiBuilder.NewChildText(img.transform, "Text", "Back", 40, FontStyle.Bold);
        text.color = s.buttonTextColor;
        text.alignment = TextAnchor.MiddleCenter;
        UiBuilder.Stretch(text.rectTransform);
    }
}

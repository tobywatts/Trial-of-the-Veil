using UnityEngine;
using UnityEngine.UI;

public static class OptionsPanelBuilder
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
        public Vector2 panelSize = new Vector2(560, 480);
        public Vector2 buttonSize = new Vector2(220, 56);
        public Vector2 panelOffset = Vector2.zero;
    }

    public static GameObject Build(Transform parent, Style s, System.Action onBack)
    {
        var panel = UiBuilder.NewChildImage(parent, "OptionsPanel", s.panelSprite);
        panel.type = Image.Type.Sliced;
        panel.color = s.panelColor;
        var rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = s.panelSize;
        rt.anchoredPosition = s.panelOffset;

        var title = UiBuilder.NewChildText(panel.transform, "Title", "Options", 70, FontStyle.Bold);
        title.color = s.titleColor;
        title.alignment = TextAnchor.MiddleCenter;
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, -56);
        titleRT.sizeDelta = new Vector2(0, 80);

        // Four rows: SFX Volume, Music Volume, Look Sensitivity, Toggle Sprint.
        BuildVolumeRow(panel.transform, s, new Vector2(0, 140), "Sound FX Volume",
            () => GameSettings.SfxVolume,
            v => GameSettings.SfxVolume = v);
        BuildVolumeRow(panel.transform, s, new Vector2(0, 70), "Music Volume",
            () => GameSettings.MusicVolume,
            v => GameSettings.MusicVolume = v);
        BuildSensitivityRow(panel.transform, s, new Vector2(0, 0));
        BuildToggleSprintRow(panel.transform, s, new Vector2(0, -70));

        BuildBackButton(panel.transform, s, onBack);

        return panel.gameObject;
    }

    private static void BuildVolumeRow(Transform parent, Style s, Vector2 pos, string labelText,
                                       System.Func<float> getter, System.Action<float> setter)
    {
        var row = UiBuilder.NewChildContainer(parent, labelText.Replace(" ", "") + "Row", pos, new Vector2(s.panelSize.x - 80, 60));

        var label = UiBuilder.NewChildText(row.transform, "Label", labelText, 30, FontStyle.Bold);
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        var labelRT = label.rectTransform;
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(0.45f, 1);
        labelRT.offsetMin = new Vector2(8, 0);
        labelRT.offsetMax = Vector2.zero;

        var valueText = UiBuilder.NewChildText(row.transform, "Value", $"{Mathf.RoundToInt(getter() * 100f)}%", 30, FontStyle.Bold);
        valueText.color = new Color(1f, 0.9f, 0.4f, 1f);
        valueText.alignment = TextAnchor.MiddleRight;
        var valueRT = valueText.rectTransform;
        valueRT.anchorMin = new Vector2(0.85f, 0);
        valueRT.anchorMax = new Vector2(1, 1);
        valueRT.offsetMin = Vector2.zero;
        valueRT.offsetMax = new Vector2(-8, 0);

        var sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(row.transform, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.56f, 0.22f);
        sliderRT.anchorMax = new Vector2(0.84f, 0.78f);
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        var bg = UiBuilder.NewChildImage(sliderGO.transform, "Background", null);
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        UiBuilder.Stretch(bg.rectTransform);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1, 0.75f);
        fillAreaRT.offsetMin = new Vector2(8, 0);
        fillAreaRT.offsetMax = new Vector2(-8, 0);

        var fill = UiBuilder.NewChildImage(fillArea.transform, "Fill", null);
        fill.color = new Color(0.5f, 0.85f, 1f, 1f);
        var fillRT = fill.rectTransform;
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1, 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderGO.transform, false);
        var handleAreaRT = handleArea.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(8, 0);
        handleAreaRT.offsetMax = new Vector2(-8, 0);

        var handle = UiBuilder.NewChildImage(handleArea.transform, "Handle", null);
        handle.color = Color.white;
        var handleRT = handle.rectTransform;
        handleRT.sizeDelta = new Vector2(18, 0);

        var slider = sliderGO.AddComponent<Slider>();
        slider.targetGraphic = handle;
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = getter();
        slider.onValueChanged.AddListener(v =>
        {
            setter(v);
            valueText.text = $"{Mathf.RoundToInt(v * 100f)}%";
        });
    }

    private static void BuildSensitivityRow(Transform parent, Style s, Vector2 pos)
    {
        var row = UiBuilder.NewChildContainer(parent, "Sensitivity", pos, new Vector2(s.panelSize.x - 80, 60));

        var label = UiBuilder.NewChildText(row.transform, "Label", "Look Sensitivity", 30, FontStyle.Bold);
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        var labelRT = label.rectTransform;
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(0.45f, 1);
        labelRT.offsetMin = new Vector2(8, 0);
        labelRT.offsetMax = Vector2.zero;

        var valueText = UiBuilder.NewChildText(row.transform, "Value", $"{GameSettings.Sensitivity:0.00}x", 30, FontStyle.Bold);
        valueText.color = new Color(1f, 0.9f, 0.4f, 1f);
        valueText.alignment = TextAnchor.MiddleRight;
        var valueRT = valueText.rectTransform;
        valueRT.anchorMin = new Vector2(0.85f, 0);
        valueRT.anchorMax = new Vector2(1, 1);
        valueRT.offsetMin = Vector2.zero;
        valueRT.offsetMax = new Vector2(-8, 0);

        var sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(row.transform, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.56f, 0.22f);
        sliderRT.anchorMax = new Vector2(0.84f, 0.78f);
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        var bg = UiBuilder.NewChildImage(sliderGO.transform, "Background", null);
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        UiBuilder.Stretch(bg.rectTransform);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1, 0.75f);
        fillAreaRT.offsetMin = new Vector2(8, 0);
        fillAreaRT.offsetMax = new Vector2(-8, 0);

        var fill = UiBuilder.NewChildImage(fillArea.transform, "Fill", null);
        fill.color = new Color(0.5f, 0.85f, 1f, 1f);
        var fillRT = fill.rectTransform;
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1, 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderGO.transform, false);
        var handleAreaRT = handleArea.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(8, 0);
        handleAreaRT.offsetMax = new Vector2(-8, 0);

        var handle = UiBuilder.NewChildImage(handleArea.transform, "Handle", null);
        handle.color = Color.white;
        var handleRT = handle.rectTransform;
        handleRT.sizeDelta = new Vector2(18, 0);

        var slider = sliderGO.AddComponent<Slider>();
        slider.targetGraphic = handle;
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.minValue = GameSettings.MinSensitivity;
        slider.maxValue = GameSettings.MaxSensitivity;
        slider.value = GameSettings.Sensitivity;
        slider.onValueChanged.AddListener(v =>
        {
            GameSettings.Sensitivity = v;
            valueText.text = $"{v:0.00}x";
        });
    }

    private static void BuildToggleSprintRow(Transform parent, Style s, Vector2 pos)
    {
        var row = UiBuilder.NewChildContainer(parent, "ToggleSprint", pos, new Vector2(s.panelSize.x - 80, 60));

        var label = UiBuilder.NewChildText(row.transform, "Label", "Toggle Sprint", 30, FontStyle.Bold);
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        var labelRT = label.rectTransform;
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(0.7f, 1);
        labelRT.offsetMin = new Vector2(8, 0);
        labelRT.offsetMax = Vector2.zero;

        // Checkbox: always-black outer box, centered yellow inner fill when on.
        var box = UiBuilder.NewChildImage(row.transform, "Box", null);
        box.color = Color.black;
        var boxRT = box.rectTransform;
        boxRT.anchorMin = new Vector2(1, 0.5f);
        boxRT.anchorMax = new Vector2(1, 0.5f);
        boxRT.pivot = new Vector2(1, 0.5f);
        boxRT.anchoredPosition = new Vector2(-8, 0);
        boxRT.sizeDelta = new Vector2(36, 36);

        var fill = UiBuilder.NewChildImage(box.transform, "Fill", null);
        fill.color = new Color(1f, 0.85f, 0.15f, 1f);
        var fillRT = fill.rectTransform;
        fillRT.anchorMin = new Vector2(0.5f, 0.5f);
        fillRT.anchorMax = new Vector2(0.5f, 0.5f);
        fillRT.pivot = new Vector2(0.5f, 0.5f);
        fillRT.anchoredPosition = Vector2.zero;
        fillRT.sizeDelta = new Vector2(22, 22);

        var toggle = box.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = fill;
        toggle.isOn = GameSettings.ToggleSprint;
        toggle.onValueChanged.AddListener(v => GameSettings.ToggleSprint = v);
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

        var text = UiBuilder.NewChildText(img.transform, "Text", "Back", 50, FontStyle.Bold);
        text.color = s.buttonTextColor;
        text.alignment = TextAnchor.MiddleCenter;
        UiBuilder.Stretch(text.rectTransform);
    }
}

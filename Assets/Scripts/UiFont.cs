using UnityEngine;

public static class UiFont
{
    private const string FontResourcePath = "MarkaziText-Regular";
    public const float GlobalFontScale = 1.3f;
    private static Font cached;

    public static int Scale(int baseSize) => Mathf.Max(1, Mathf.RoundToInt(baseSize * GlobalFontScale));

    public static Font Get()
    {
        if (cached != null) return cached;
        cached = Resources.Load<Font>(FontResourcePath);
        if (cached == null) cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cached;
    }
}

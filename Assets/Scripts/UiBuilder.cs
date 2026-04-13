// Made with Claude (Sonnet 4.5) — prompt: "make rounded sprites for the ui buttons"

using UnityEngine;
using UnityEngine.UI;

// Shared UI construction helpers used by the menu and panel builders.
public static class UiBuilder
{
    public static Image NewChildImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.raycastTarget = true;
        return img;
    }

    public static Text NewChildText(Transform parent, string name, string text, int fontSize, FontStyle style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = UiFont.Get();
        t.fontSize = UiFont.Scale(fontSize);
        t.fontStyle = style;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public static GameObject NewChildContainer(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return go;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Sprite MakeRoundedSprite(int cornerRadius)
    {
        int r = Mathf.Max(2, cornerRadius);
        int s = r * 2 + 4;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[s * s];
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 white = new Color32(255, 255, 255, 255);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                int cx = -1, cy = -1;
                if (x < r && y < r) { cx = r; cy = r; }
                else if (x >= s - r && y < r) { cx = s - r - 1; cy = r; }
                else if (x < r && y >= s - r) { cx = r; cy = s - r - 1; }
                else if (x >= s - r && y >= s - r) { cx = s - r - 1; cy = s - r - 1; }

                if (cx >= 0)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist >= r)
                    {
                        pixels[y * s + x] = clear;
                    }
                    else
                    {
                        float aa = Mathf.Clamp01(r - dist);
                        pixels[y * s + x] = new Color32(255, 255, 255, (byte)(aa * 255f));
                    }
                }
                else
                {
                    pixels[y * s + x] = white;
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        Vector4 border = new Vector4(r + 1, r + 1, r + 1, r + 1);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}

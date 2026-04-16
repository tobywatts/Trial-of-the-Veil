using UnityEngine;
using UnityEngine.UI;

// [ExecuteAlways] so the blurred background and vignette regenerate in the editor; their runtime
// textures don't survive prefab serialization.
[ExecuteAlways]
public class MenuBackground : MonoBehaviour
{
    public RawImage backgroundRaw;
    public Image vignetteImg;
    public Sprite sourceSprite;
    [Range(0, 12)] public int blurPasses = 7;
    [Range(0f, 1f)] public float vignetteIntensity = 0.75f;
    [Range(0.1f, 1f)] public float vignetteInnerRadius = 0.55f;
    public float scrollSpeed;

    private void OnEnable() { RegenerateTextures(); }
    private void OnValidate() { if (isActiveAndEnabled) RegenerateTextures(); }

    // Rebuilds the blurred background and vignette from sourceSprite + params. Cheap enough for every OnEnable.
    public void RegenerateTextures()
    {
        if (backgroundRaw != null && sourceSprite != null && sourceSprite.texture != null)
        {
            Texture2D src = sourceSprite.texture;
            Texture2D bg = src;
            if (blurPasses > 0 && src.isReadable) bg = BoxBlur(src, blurPasses);
            if (bg != null) bg.wrapMode = TextureWrapMode.Repeat;
            backgroundRaw.texture = bg;
        }
        if (vignetteImg != null && vignetteIntensity > 0f)
        {
            vignetteImg.sprite = MakeVignetteSprite(256, vignetteInnerRadius, vignetteIntensity);
        }
    }

    public static MenuBackground Create(Transform root, Sprite sprite, int blurPasses, float scrollSpeed, float vignetteIntensity, float vignetteInnerRadius)
    {
        if (sprite == null) return null;

        var bgGO = new GameObject("MenuBackground");
        bgGO.transform.SetParent(root, false);
        bgGO.AddComponent<RectTransform>();
        var raw = bgGO.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;
        raw.uvRect = new Rect(0f, 0f, 1f, 1f);
        Stretch(raw.rectTransform);
        bgGO.transform.SetAsFirstSibling();

        Image vimg = null;
        if (vignetteIntensity > 0f)
        {
            var vignetteGO = new GameObject("MenuVignette");
            vignetteGO.transform.SetParent(root, false);
            vignetteGO.AddComponent<RectTransform>();
            vimg = vignetteGO.AddComponent<Image>();
            vimg.color = Color.white;
            vimg.raycastTarget = false;
            Stretch(vimg.rectTransform);
            vignetteGO.transform.SetSiblingIndex(1);
        }

        // Store the params so OnEnable can regenerate the textures; the runtime Texture2Ds don't serialize.
        var mb = bgGO.AddComponent<MenuBackground>();
        mb.backgroundRaw = raw;
        mb.vignetteImg = vimg;
        mb.sourceSprite = sprite;
        mb.blurPasses = blurPasses;
        mb.vignetteIntensity = vignetteIntensity;
        mb.vignetteInnerRadius = vignetteInnerRadius;
        mb.scrollSpeed = scrollSpeed;
        // Force an initial bake; OnEnable won't fire when the component is added mid-script.
        mb.RegenerateTextures();
        return mb;
    }

    private void Update()
    {
        if (backgroundRaw == null || Mathf.Abs(scrollSpeed) < 0.00001f) return;
        Rect r = backgroundRaw.uvRect;
        r.x = (r.x + scrollSpeed * Time.unscaledDeltaTime) % 1f;
        backgroundRaw.uvRect = r;
    }

    public static Texture2D BoxBlur(Texture2D src, int passes)
    {
        int w = src.width;
        int h = src.height;
        Color32[] a = src.GetPixels32();
        Color32[] b = new Color32[a.Length];
        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int r = 0, g = 0, bb = 0, cnt = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w) continue;
                            Color32 c = a[yy * w + xx];
                            r += c.r; g += c.g; bb += c.b; cnt++;
                        }
                    }
                    b[y * w + x] = new Color32((byte)(r / cnt), (byte)(g / cnt), (byte)(bb / cnt), 255);
                }
            }
            Color32[] tmp = a; a = b; b = tmp;
        }
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.wrapMode = TextureWrapMode.Repeat;
        dst.SetPixels32(a);
        dst.Apply();
        return dst;
    }

    public static Sprite MakeVignetteSprite(int size, float innerRadius, float intensity)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float maxDist = Mathf.Sqrt(cx * cx + cy * cy);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / maxDist;
                float dy = (y - cy) / maxDist;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.InverseLerp(innerRadius, 1f, dist);
                byte alpha = (byte)(Mathf.Clamp01(t * intensity) * 255f);
                px[y * size + x] = new Color32(0, 0, 0, alpha);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

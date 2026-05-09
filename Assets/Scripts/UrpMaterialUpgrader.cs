using UnityEngine;

// Rebuilds Built-in Standard materials (which render pink under URP) against URP/Lit, keeping the
// albedo/normal/metallic/occlusion bindings. Used on asset-pack imports.
public static class UrpMaterialUpgrader
{
    public static void Convert(GameObject root)
    {
        if (root == null) return;
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            // Iterate every material slot, not just slot 0, so multi-submesh renderers fully convert.
            Material[] shared = renderers[i].sharedMaterials;
            if (shared == null || shared.Length == 0) continue;

            Material[] upgraded = new Material[shared.Length];
            bool anyChanged = false;
            for (int s = 0; s < shared.Length; s++)
            {
                Material src = shared[s];
                if (src == null) { upgraded[s] = null; continue; }
                if (src.shader == urpLit) { upgraded[s] = src; continue; }

                Texture albedo = src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
                if (albedo == null && src.HasProperty("_BaseMap")) albedo = src.GetTexture("_BaseMap");
                Texture bump = src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
                Texture metallic = src.HasProperty("_MetallicGlossMap") ? src.GetTexture("_MetallicGlossMap") : null;
                Texture occlusion = src.HasProperty("_OcclusionMap") ? src.GetTexture("_OcclusionMap") : null;
                Color baseColor = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;

                Material m = new Material(urpLit);
                m.name = src.name + " (URP)";
                if (albedo != null)
                {
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedo);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", albedo);
                }
                if (bump != null && m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", bump);
                if (metallic != null && m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", metallic);
                if (occlusion != null && m.HasProperty("_OcclusionMap")) m.SetTexture("_OcclusionMap", occlusion);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
                if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);

                upgraded[s] = m;
                anyChanged = true;
            }
            if (anyChanged) renderers[i].materials = upgraded;
        }
    }

    // Assigns a fresh solid-color URP/Lit (or Standard fallback) material. Emission is applied only
    // when emissionStrength > 0. Used by Item and ChestSpawner for procedural primitives.
    public static void ApplySolidColor(Renderer r, Color color, float emissionStrength)
    {
        if (r == null) return;
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = new Material(s);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        if (emissionStrength > 0f && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emissionStrength);
        }
        r.material = m;
    }
}

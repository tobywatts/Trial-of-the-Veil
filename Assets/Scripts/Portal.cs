using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour
{
    [Tooltip("Scene to load on entry. Must be in Build Settings.")]
    public string targetScene = "level2";
    public bool overrideSpawnPosition = false;
    [Tooltip("Only used when overrideSpawnPosition is true.")]
    public Vector3 spawnPositionInTargetScene = Vector3.zero;
    [Tooltip("Degrees/sec.")]
    public float spinSpeed = 90f;

    private bool triggered;

    public static GameObject CreateDefault(Vector3 worldPos)
    {
        GameObject root = new GameObject("Portal");
        root.transform.position = worldPos;

        // Runtime-loaded so no inspector reference is needed; falls back to a placeholder rig if missing.
        GameObject visualPrefab = Resources.Load<GameObject>("Portals/Procedural Portal Effect");
        if (visualPrefab != null)
        {
            GameObject visual = Instantiate(visualPrefab, root.transform);
            visual.name = "PortalVisual";
            // Root is already lifted 1.5m by SpawnPortal, so zero the prefab's own offset.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(3f, 3f, 3f);
        }
        else
        {
            Debug.LogWarning("Procedural Portal Effect prefab missing from Resources/Portals, falling back to placeholder visual.");
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PortalRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localScale = new Vector3(3f, 2f, 3f);
            Object.Destroy(ring.GetComponent<Collider>());
            TintRenderer(ring.GetComponent<Renderer>(), new Color(0.25f, 0.7f, 1f, 0.55f), new Color(0.4f, 1.4f, 2.2f, 1f));

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            core.name = "PortalCore";
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = new Vector3(1.5f, 2.05f, 1.5f);
            Object.Destroy(core.GetComponent<Collider>());
            TintRenderer(core.GetComponent<Renderer>(), new Color(0.8f, 0.95f, 1f, 0.8f), new Color(1.6f, 2.4f, 3.6f, 1f));
        }

        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 3f;
        trigger.center = new Vector3(0f, 1f, 0f);

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.4f, 0.8f, 1f, 1f);
        light.range = 15f;
        light.intensity = 4f;

        return root;
    }

    private static void TintRenderer(Renderer r, Color baseColor, Color emission)
    {
        if (r == null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
        }
        // URP transparency setup so the cylinders render as a glow, not a solid pillar.
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        r.material = m;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (other == null) return;
        if (other.GetComponentInParent<PlayerHealth>() == null) return;
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning("targetScene is empty, staying on current scene.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"Scene '{targetScene}' is not in Build Settings. Add it via File > Build Settings to enable transit.");
            return;
        }

        triggered = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // stageAdvance=1: the next level resumes one stage past the boss just defeated.
        PlayerRunState.Capture(GameManager.Instance, stageAdvance: 1);
        if (overrideSpawnPosition)
        {
            PlayerRunState.HasSpawnPosition = true;
            PlayerRunState.SpawnPosition = spawnPositionInTargetScene;
        }

        SceneManager.LoadScene(targetScene);
    }
}

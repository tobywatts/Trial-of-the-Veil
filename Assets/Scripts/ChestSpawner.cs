using System.Collections.Generic;
using UnityEngine;

public class ChestSpawner : MonoBehaviour
{
    [Header("Count")]
    public int minChests = 5;
    public int maxChests = 10;

    [Header("Placement")]
    public float minDistanceFromPlayer = 10f;
    public float maxDistanceFromPlayer = 45f;
    public float minDistanceBetweenChests = 4f;
    [Tooltip("Lift above the ground hit-point. Pivot is at the chest's bottom, so a small offset keeps the bob animation off the floor.")]
    public float spawnHeightOffset = 0.08f;
    public LayerMask groundMask = ~0;
    public int placementAttemptsPerChest = 25;
    [Tooltip("Scenes where chests skip the ground raycast and spawn at the player's Y (flat platform levels).")]
    public string[] flatYScenes = new string[] { "level2" };

    [Header("Visual")]
    [Tooltip("Should contain a 'Bottom_*' body child and a 'Top_*' lid child.")]
    public GameObject chestPrefab;
    [Tooltip("Default tuned for Chest_1_material_1.prefab.")]
    public float chestScale = 0.85f;
    [Tooltip("Fallback primitive visuals, used only when chestPrefab is empty.")]
    public Color bodyColor = new Color(0.55f, 0.32f, 0.08f, 1f);
    public Color trimColor = new Color(1f, 0.78f, 0.2f, 1f);
    public Vector3 chestSize = new Vector3(0.9f, 0.7f, 0.6f);

    [Header("Prompt")]
    public Sprite coinIcon;

    private void Start()
    {
        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph == null)
        {
            Debug.LogWarning("No PlayerHealth found, skipping chest spawn.");
            return;
        }

        Transform player = ph.transform;

        int target = Random.Range(minChests, maxChests + 1);
        var placed = new List<Vector3>(target);

        for (int i = 0; i < target; i++)
        {
            if (TryPickPosition(player.position, placed, out Vector3 pos))
            {
                SpawnChest(pos, player);
                placed.Add(pos);
            }
        }

        Debug.Log($"Placed {placed.Count}/{target} chests.");
    }

    private bool TryPickPosition(Vector3 origin, List<Vector3> existing, out Vector3 pos)
    {
        bool hasArea = SpawnArea.TryGetBounds(out Bounds area);
        bool useFlatY = IsFlatYScene();
        float minDistSqr = minDistanceFromPlayer * minDistanceFromPlayer;
        float maxDistSqr = maxDistanceFromPlayer * maxDistanceFromPlayer;
        float minBetweenSqr = minDistanceBetweenChests * minDistanceBetweenChests;

        for (int attempt = 0; attempt < placementAttemptsPerChest; attempt++)
        {
            Vector3 candidate;
            if (hasArea)
            {
                // Uniform-in-rectangle so chests scatter instead of ringing the player; distance still enforced below.
                candidate = SpawnArea.RandomPointXZ(area, origin.y);
                Vector3 toPlayer = candidate - origin; toPlayer.y = 0f;
                float dSqr = toPlayer.sqrMagnitude;
                if (dSqr < minDistSqr || dSqr > maxDistSqr) continue;
            }
            else
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
                candidate = origin + new Vector3(r.x, 0f, r.y);
            }

            if (useFlatY)
            {
                // Flat platform: sit flush at the player's Y. spawnHeightOffset would leave a visible gap.
                candidate.y = origin.y;
            }
            else
            {
                Vector3 rayStart = new Vector3(candidate.x, origin.y + 50f, candidate.z);
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
                    candidate = hit.point + Vector3.up * spawnHeightOffset;
                else
                    candidate = new Vector3(candidate.x, origin.y + spawnHeightOffset, candidate.z);
            }

            bool tooClose = false;
            foreach (var p in existing)
            {
                if ((p - candidate).sqrMagnitude < minBetweenSqr) { tooClose = true; break; }
            }
            if (tooClose) continue;

            pos = candidate;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    private bool IsFlatYScene()
    {
        if (flatYScenes == null) return false;
        string active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        for (int i = 0; i < flatYScenes.Length; i++)
            if (flatYScenes[i] == active) return true;
        return false;
    }

    private void SpawnChest(Vector3 pos, Transform player)
    {
        GameObject root;
        if (chestPrefab != null)
        {
            root = Instantiate(chestPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            root.name = "Chest";
            float s = Mathf.Max(0.05f, chestScale);
            root.transform.localScale = new Vector3(s, s, s);
            // Asset pack ships Built-in Standard materials, which URP renders as pink.
            UrpMaterialUpgrader.Convert(root);
        }
        else
        {
            root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Chest";
            root.transform.position = pos;
            root.transform.localScale = chestSize;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            UrpMaterialUpgrader.ApplySolidColor(root.GetComponent<Renderer>(), bodyColor, emissionStrength: 0.0f);

            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Top";
            lid.transform.SetParent(root.transform, worldPositionStays: false);
            lid.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            lid.transform.localScale = new Vector3(1.05f, 0.25f, 1.05f);
            Object.Destroy(lid.GetComponent<BoxCollider>());
            UrpMaterialUpgrader.ApplySolidColor(lid.GetComponent<Renderer>(), trimColor, emissionStrength: 0.35f);
        }

        Chest chest = root.AddComponent<Chest>();
        chest.coinIcon = coinIcon;
        chest.Initialize(player);
    }

}

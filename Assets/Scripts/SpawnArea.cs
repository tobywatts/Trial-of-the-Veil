using UnityEngine;

// Returns the AABB enclosed by the level's "border*" wall colliders, used to keep spawns inside
// the play area. With no border objects, callers fall back to free world-space spawning.
public static class SpawnArea
{
    public const string Prefix = "border";

    public static bool IsBorder(Collider c)
    {
        if (c == null) return false;
        string n = c.gameObject.name;
        return !string.IsNullOrEmpty(n) && n.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetBounds(out Bounds bounds, float wallInset = 5.0f)
    {
        bounds = new Bounds();
        bool found = false;
        Collider[] all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i].gameObject;
            string n = go.name;
            if (string.IsNullOrEmpty(n)) continue;
            if (!n.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (!found) { bounds = all[i].bounds; found = true; }
            else bounds.Encapsulate(all[i].bounds);
        }
        if (!found) return false;
        // Shrink XZ by the wall thickness so spawned actors don't clip into the borders.
        bounds.Expand(new Vector3(-2f * wallInset, 0f, -2f * wallInset));
        return true;
    }

    public static bool Contains(Bounds b, Vector3 worldXZ)
    {
        return worldXZ.x >= b.min.x && worldXZ.x <= b.max.x &&
               worldXZ.z >= b.min.z && worldXZ.z <= b.max.z;
    }

    public static Vector3 RandomPointXZ(Bounds b, float y)
    {
        return new Vector3(Random.Range(b.min.x, b.max.x), y, Random.Range(b.min.z, b.max.z));
    }
}

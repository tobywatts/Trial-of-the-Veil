using UnityEngine;

public class Coin : MonoBehaviour
{
    public float pickupRange = 1f;
    [Tooltip("Speed while magnetizing toward the player.")]
    public float magnetSpeed = 14f;
    [Tooltip("Set by the spawner.")]
    public int value = 1;
    [Tooltip("Degrees/sec around Y.")]
    public float spinSpeed = 180f;
    [Tooltip("Scale multiplier at spawn. <1 shrinks it.")]
    public float spawnScale = 0.4f;

    private Transform player;
    private PlayerCoins wallet;

    public static GameObject Spawn(GameObject prefab, Vector3 position, int value = 1)
    {
        if (prefab == null) return null;
        GameObject go = Instantiate(prefab, position, Quaternion.identity);

        // Disable any animation/float scripts shipped on the prefab so this Coin component controls movement.
        MonoBehaviour[] behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is Coin) continue;
            behaviours[i].enabled = false;
        }

        Coin coin = go.GetComponent<Coin>();
        if (coin == null) coin = go.AddComponent<Coin>();
        coin.value = Mathf.Max(1, value);
        if (coin.spawnScale > 0f && Mathf.Abs(coin.spawnScale - 1f) > 0.0001f)
            go.transform.localScale = go.transform.localScale * coin.spawnScale;
        coin.transform.position = position;
        return go;
    }

    private void Start()
    {
        wallet = FindFirstObjectByType<PlayerCoins>();
        if (wallet != null) player = wallet.transform;
    }

    private void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        if (player == null)
        {
            wallet = FindFirstObjectByType<PlayerCoins>();
            if (wallet != null) player = wallet.transform;
        }
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        if (dist < pickupRange)
        {
            if (wallet != null) wallet.Add(value);
            SoundFx.PlayAt(SoundFx.CoinPickup, transform.position);
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, player.position, magnetSpeed * Time.deltaTime);
    }
}

using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    private Vector3 velocity;
    private float lifetime;
    private float damage;
    private PlayerHealth playerHealth;
    private Transform playerTransform;
    private float hitRadius = 0.9f;

    public static EnemyProjectile Spawn(Vector3 position, Vector3 direction, float speed, float life, float dmg, PlayerHealth target)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyProjectile";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.35f;

        Object.Destroy(go.GetComponent<Collider>());

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.color = new Color(1f, 0.35f, 0.1f, 1f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1.5f, 0.6f, 0.2f, 1f));
            }
            r.material = m;
        }

        EnemyProjectile p = go.AddComponent<EnemyProjectile>();
        p.velocity = direction.normalized * speed;
        p.lifetime = life;
        p.damage = dmg;
        p.playerHealth = target;
        p.playerTransform = target != null ? target.transform : null;
        return p;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += velocity * Time.deltaTime;

        if (playerTransform == null || playerHealth == null) return;

        Vector3 hitCenter = playerTransform.position + Vector3.up * 1.0f;
        if ((transform.position - hitCenter).sqrMagnitude <= hitRadius * hitRadius)
        {
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}

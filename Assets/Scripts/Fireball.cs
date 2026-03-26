using UnityEngine;

public class Fireball : MonoBehaviour
{
    private Vector3 velocity;
    private float remainingLife;
    private float damage;
    private float hitRadius;
    private LayerMask hitMask;
    private Transform owner;
    private GameObject hitVfxPrefab;
    private float hitVfxSpeedMultiplier = 1f;
    private float hitVfxHoldDuration = 0.5f;
    private float hitVfxShrinkDuration = 0.5f;
    private float hitVfxScale = 1f;
    private bool firedByEnemy;

    public static Fireball Spawn(GameObject prefab, Vector3 position, Vector3 direction, float speed, float damage, float lifetime, float radius, float scale, LayerMask hitMask, Transform owner, GameObject hitVfxPrefab = null, float hitVfxSpeedMultiplier = 1f, float hitVfxHoldDuration = 0.5f, float hitVfxShrinkDuration = 0.5f, float hitVfxScale = 1f)
    {
        if (prefab == null) return null;
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        GameObject go = Instantiate(prefab, position, Quaternion.LookRotation(dir));
        if (scale > 0f && Mathf.Abs(scale - 1f) > 0.0001f)
            go.transform.localScale = go.transform.localScale * scale;
        Fireball f = go.GetComponent<Fireball>();
        if (f == null) f = go.AddComponent<Fireball>();
        f.velocity = dir * speed;
        f.damage = damage;
        f.remainingLife = lifetime;
        f.hitRadius = radius;
        f.hitMask = hitMask;
        f.owner = owner;
        f.hitVfxPrefab = hitVfxPrefab;
        f.hitVfxSpeedMultiplier = hitVfxSpeedMultiplier;
        f.hitVfxHoldDuration = hitVfxHoldDuration;
        f.hitVfxShrinkDuration = hitVfxShrinkDuration;
        f.hitVfxScale = hitVfxScale;
        f.firedByEnemy = owner != null && owner.GetComponent<Enemy>() != null;

        // Enemy fireballs are quieter and fall off faster so a distant caster doesn't drown out close combat.
        if (f.firedByEnemy)
            SoundFx.PlayAtSpatial(SoundFx.FireballCast, position, 2f, 22f);
        else
            SoundFx.PlayAtSpatial(SoundFx.FireballCast, position, 4f, 35f);
        return f;
    }

    private void Update()
    {
        remainingLife -= Time.deltaTime;
        if (remainingLife <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 step = velocity * Time.deltaTime;
        float stepDist = step.magnitude;
        if (stepDist <= 0f) return;

        RaycastHit[] hits = Physics.SphereCastAll(transform.position, hitRadius, velocity.normalized, stepDist, hitMask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                if (owner != null && c.transform.IsChildOf(owner)) continue;

                if (firedByEnemy)
                {
                    Enemy alliedEnemy = c.GetComponentInParent<Enemy>();
                    if (alliedEnemy != null) continue; // no friendly fire

                    transform.position = hits[i].point;
                    PlayerHealth ph = c.GetComponentInParent<PlayerHealth>();
                    if (ph != null) ph.TakeDamage(damage);
                    Consume();
                    return;
                }

                transform.position = hits[i].point;
                Enemy enemy = c.GetComponentInParent<Enemy>();
                if (enemy != null)
                {
                    float dmg = damage;
                    if (owner != null)
                    {
                        PlayerWeapon pw = owner.GetComponent<PlayerWeapon>();
                        if (pw != null) dmg = pw.ApplyDamageConditionals(damage, enemy);
                    }
                    enemy.TakeDamage(dmg, transform.position);
                }
                Consume();
                return;
            }
        }

        transform.position += step;
    }

    private void Consume()
    {
        SpawnHitVfx(transform.position, -velocity);
        Destroy(gameObject);
    }

    private void SpawnHitVfx(Vector3 position, Vector3 facing)
    {
        if (hitVfxPrefab == null) return;
        Quaternion rot = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing.normalized) : Quaternion.identity;
        GameObject vfx = Instantiate(hitVfxPrefab, position, rot);
        if (hitVfxScale > 0f && Mathf.Abs(hitVfxScale - 1f) > 0.0001f)
        {
            vfx.transform.localScale = vfx.transform.localScale * hitVfxScale;
        }
        bool tweakSpeed = hitVfxSpeedMultiplier > 0f && Mathf.Abs(hitVfxSpeedMultiplier - 1f) > 0.0001f;
        bool tweakSize = hitVfxScale > 0f && Mathf.Abs(hitVfxScale - 1f) > 0.0001f;
        ParticleSystem[] systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            if (tweakSize) main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (tweakSpeed) main.simulationSpeed = main.simulationSpeed * hitVfxSpeedMultiplier;
        }

        HitVfxShrink shrinker = vfx.AddComponent<HitVfxShrink>();
        shrinker.Configure(Mathf.Max(0f, hitVfxHoldDuration), Mathf.Max(0.001f, hitVfxShrinkDuration));
    }
}

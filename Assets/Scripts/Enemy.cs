// Made with Claude (Opus 4.6) — prompt: "make the enemies chase the player and not walk through walls"

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class Enemy : MonoBehaviour
{
    public enum State { Wander, Chase }

    [Header("Stats (scale with time and stage)")]
    public float baseMaxHealth = 30f;
    public float healthPerStage = 15f;
    public float baseDamage = 8f;
    public float damagePerStage = 3f;

    [Header("Movement")]
    public float baseMoveSpeed = 3.5f;
    public float moveSpeedPerStage = 0.25f;
    public float wanderRadius = 8f;
    public float wanderRepathInterval = 4f;

    [Header("Targeting")]
    public float chaseRadius = 38f;
    public float disengageRadius = 50f;
    [Tooltip("Within this distance the enemy stops closing and fires from range.")]
    public float preferredAttackDistance = 20f;

    [Header("Attack")]
    public float attackInterval = 2.92f;
    public float attackCooldownJitter = 0.6f;
    public float projectileSpeed = 30f;
    public float projectileLifetime = 4f;
    public float aimHeightOffset = 1.2f;

    [Header("Fireball Projectile")]
    public GameObject fireballPrefab;
    public GameObject fireballHitVfxPrefab;
    public float fireballRadius = 0.25f;
    public float fireballScale = 0.35f;
    public float fireballHitVfxScale = 0.45f;
    public float fireballHitVfxHoldDuration = 0.4f;
    public float fireballHitVfxShrinkDuration = 0.4f;
    public LayerMask fireballHitMask = ~0;

    [Header("XP Drop (scales with stage)")]
    public int baseXpReward = 10;
    public int xpRewardPerStage = 8;

    [Tooltip("Boss death routes through RegisterBossKill instead of RegisterEnemyKill.")]
    public bool isBoss = false;

    [Header("Coin Drop")]
    public GameObject coinDropPrefab;
    public int minCoinValue = 3;
    public int maxCoinValue = 15;

    [Header("Knockback")]
    public float knockbackForce = 8f;
    public float knockbackUpward = 2f;
    public float knockbackStunDuration = 0.25f;

    [Header("Obstacle Avoidance")]
    [Tooltip("Side-step around obstacles while chasing. Off for open levels, on for tile dungeons (level3).")]
    public bool avoidObstacles = false;
    public float obstacleProbeDistance = 1.6f;
    [Tooltip("Match to the enemy's scaled collider radius.")]
    public float obstacleProbeRadius = 0.45f;
    [Tooltip("Probe cast height: clear the floor but stay below head height.")]
    public float obstacleProbeYOffset = 0.6f;

    [Header("Melee")]
    [Tooltip("Chase into melee range and strike via animation instead of firing a projectile.")]
    public bool isMelee = false;
    [Tooltip("Stop-closing radius for melee chasers; plants and swings inside it.")]
    public float meleeRange = 2.0f;
    [Tooltip("Seconds from animation start to the damage tick. Match to the swing's contact frame.")]
    public float meleeDamageDelay = 0.35f;
    [Tooltip("Slack added to meleeRange at the strike moment so a strike still lands if the player drifted out.")]
    public float meleeStrikeTolerance = 0.6f;
    [Tooltip("Clip played at swing start. Null = silent.")]
    public AudioClip attackSoundClip;
    [Range(0f, 1f)] public float attackSoundVolume = 0.7f;

    private float knockbackEndTime;
    private float pendingMeleeStrikeTime;

    [Header("Animation State Names")]
    public string animIdle = "Idle01";
    public string animRun = "BattleRunForward";
    public string animAttack = "Attack01";
    public string animDie = "Die";
    [Tooltip("Idle clip used while wandering. Falls back to animIdle if empty.")]
    public string animFarIdle = "";
    [Tooltip("Strafe walk states. If empty, animRun covers any direction.")]
    public string animWalkBack = "";
    public string animWalkLeft = "";
    public string animWalkRight = "";
    [Tooltip("Hit-reaction clip played when TakeDamage doesn't kill. Empty disables.")]
    public string animGetHit = "";
    public float attackAnimationDuration = 0.7f;
    [Tooltip("Lock window where TakeDamage won't retrigger GetHit, so chain hits don't flicker the animator.")]
    public float getHitAnimationDuration = 0.4f;
    [Tooltip("Animator float param driven by movement state (1 moving, 0 idle). For blend-tree locomotion like the zombie's MoveSpeed-driven Walk.")]
    public string locomotionFloatParam = "";

    private Animator animator;
    private bool hasLocomotionParam;
    private int locomotionParamHash;
    private string lastAnimatedClip = "";
    private bool isAnimatedMoving;
    private Vector3 lastMoveDir;
    private bool dying;
    private float attackAnimationEndTime;
    private float getHitAnimationEndTime;

    private float currentHealth;
    private float maxHealth;
    private float scaledDamage;
    private float moveSpeed;
    private int xpReward;
    private PlayerLevel playerLevel;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody rb;
    private State state = State.Wander;

    private Vector3 wanderTarget;
    private float wanderTimer;
    private float attackTimer;
    private Vector3 spawnOrigin;

    public void Initialize(Transform playerTransform, PlayerHealth playerHp)
    {
        player = playerTransform;
        playerHealth = playerHp;

        int stage = GameManager.Instance != null ? GameManager.Instance.CurrentStage : 1;
        int stageStep = Mathf.Max(0, stage - 1);

        // Every 30s adds +5% to HP/damage/XP (not move speed).
        float elapsed = GameManager.Instance != null ? GameManager.Instance.TimeElapsed : 0f;
        int timeLevel = Mathf.FloorToInt(elapsed / 30f);
        float timeMultiplier = 1f + 0.05f * timeLevel;

        maxHealth = (baseMaxHealth + healthPerStage * stageStep) * timeMultiplier;
        currentHealth = maxHealth;
        scaledDamage = (baseDamage + damagePerStage * stageStep) * timeMultiplier;
        moveSpeed = baseMoveSpeed + moveSpeedPerStage * stageStep;
        xpReward = Mathf.RoundToInt((baseXpReward + xpRewardPerStage * stageStep) * timeMultiplier);

        if (player != null) playerLevel = player.GetComponent<PlayerLevel>();

        ConfigureAnimationFromVariantFields();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        spawnOrigin = transform.position;

        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();
    }

    // Not in Awake: EnemySpawner sets the anim fields after AddComponent<Enemy>, so Awake would read stale defaults.
    private void ConfigureAnimationFromVariantFields()
    {
        if (animator != null && !string.IsNullOrEmpty(locomotionFloatParam))
        {
            hasLocomotionParam = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == locomotionFloatParam && p.type == AnimatorControllerParameterType.Float)
                {
                    hasLocomotionParam = true;
                    locomotionParamHash = p.nameHash;
                    break;
                }
            }
        }
        attackTimer = Random.Range(0.5f, attackInterval);
        PickWanderTarget();
        PlayAnimation(SelectIdleClip());
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null || dying) return;
        if (string.IsNullOrEmpty(stateName)) return;
        // Dedup only if the animator is still in / heading to this state; exit-time auto-transitions
        // (e.g. the slime's Attack01 to Attack02) can drift it off, and then we re-issue the CrossFade.
        if (stateName == lastAnimatedClip)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            bool stillOnRequested = current.IsName(stateName);
            if (!stillOnRequested && animator.IsInTransition(0))
                stillOnRequested = animator.GetNextAnimatorStateInfo(0).IsName(stateName);
            if (stillOnRequested) return;
        }
        animator.CrossFade(stateName, 0.15f, 0, 0f);
        lastAnimatedClip = stateName;
    }

    private string SelectIdleClip()
    {
        if (state == State.Wander && !string.IsNullOrEmpty(animFarIdle)) return animFarIdle;
        return animIdle;
    }

    private string SelectMoveClip()
    {
        if (lastMoveDir.sqrMagnitude < 0.0001f) return animRun;
        // Pick a directional walk when overrides exist; otherwise use the single animRun.
        Vector3 local = transform.InverseTransformDirection(lastMoveDir.normalized);
        float ax = Mathf.Abs(local.x);
        float az = Mathf.Abs(local.z);
        if (az >= ax)
        {
            if (local.z < 0f && !string.IsNullOrEmpty(animWalkBack)) return animWalkBack;
            return animRun;
        }
        if (local.x > 0f && !string.IsNullOrEmpty(animWalkRight)) return animWalkRight;
        if (local.x < 0f && !string.IsNullOrEmpty(animWalkLeft)) return animWalkLeft;
        return animRun;
    }

    private void Update()
    {
        if (dying) return;
        if (player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.GameOver) return;

        // Stunned by recent knockback, let physics carry it.
        if (Time.time < knockbackEndTime) return;

        float distSqr = (player.position - transform.position).sqrMagnitude;

        if (state == State.Wander && distSqr < chaseRadius * chaseRadius)
            state = State.Chase;
        else if (state == State.Chase && distSqr > disengageRadius * disengageRadius)
            state = State.Wander;

        isAnimatedMoving = false;
        lastMoveDir = Vector3.zero;

        if (state == State.Chase) UpdateChase();
        else UpdateWander();

        bool playingAttack = Time.time < attackAnimationEndTime;
        bool playingGetHit = Time.time < getHitAnimationEndTime;

        string desired;
        if (playingAttack) desired = animAttack;
        else if (playingGetHit) desired = animGetHit;
        else desired = isAnimatedMoving ? SelectMoveClip() : SelectIdleClip();
        PlayAnimation(desired);

        // Drive the blend-tree locomotion param so blend-tree Walk states animate instead of freezing at idle.
        if (hasLocomotionParam)
            animator.SetFloat(locomotionParamHash, isAnimatedMoving ? 1f : 0f);
    }

    private void UpdateChase()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distSqr = toPlayer.sqrMagnitude;
        float attackDistance = isMelee ? meleeRange : preferredAttackDistance;

        if (distSqr > 0.01f)
        {
            Vector3 dir = toPlayer.normalized;
            // Outside range: close at full speed. Ranged inside range: walk slowly. Melee inside range: plant.
            float speedScale;
            if (distSqr > attackDistance * attackDistance) speedScale = 1f;
            else speedScale = isMelee ? 0f : 0.4f;
            MoveInDirection(dir * speedScale);
            FaceDirection(dir);
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f && (!isMelee || distSqr <= meleeRange * meleeRange))
        {
            if (isMelee)
            {
                PlayAnimation(animAttack);
                attackAnimationEndTime = Time.time + attackAnimationDuration;
                pendingMeleeStrikeTime = Time.time + meleeDamageDelay;
                // Variants set attackSoundClip; the boss falls back to whoosh if its variant has none.
                if (attackSoundClip != null)
                    AudioSource.PlayClipAtPoint(attackSoundClip, transform.position, attackSoundVolume);
                else if (isBoss)
                    SoundFx.PlayAt(SoundFx.SwingWhoosh, transform.position);
            }
            else
            {
                FireProjectile();
            }
            attackTimer = attackInterval + Random.Range(-attackCooldownJitter, attackCooldownJitter);
        }

        if (isMelee && pendingMeleeStrikeTime > 0f && Time.time >= pendingMeleeStrikeTime)
        {
            pendingMeleeStrikeTime = 0f;
            if (playerHealth != null)
            {
                Vector3 nowToPlayer = player.position - transform.position;
                nowToPlayer.y = 0f;
                float strikeRange = meleeRange + meleeStrikeTolerance;
                if (nowToPlayer.sqrMagnitude <= strikeRange * strikeRange)
                    playerHealth.TakeDamage(scaledDamage);
            }
        }
    }

    private void UpdateWander()
    {
        wanderTimer -= Time.deltaTime;
        Vector3 toTarget = wanderTarget - transform.position;
        toTarget.y = 0f;

        if (wanderTimer <= 0f || toTarget.sqrMagnitude < 1f)
        {
            PickWanderTarget();
            return;
        }

        Vector3 dir = toTarget.normalized;
        MoveInDirection(dir * 0.6f);
        FaceDirection(dir);
    }

    private void MoveInDirection(Vector3 dir)
    {
        Vector3 desired = dir * moveSpeed;
        if (avoidObstacles && desired.sqrMagnitude > 0.0001f)
            desired = SteerAroundObstacles(desired);

        Vector3 v = rb.linearVelocity;
        v.x = desired.x;
        v.z = desired.z;
        rb.linearVelocity = v;
        if (dir.sqrMagnitude > 0.0001f)
        {
            isAnimatedMoving = true;
            // Face the steered direction so the animation matches actual travel, not the blocked path.
            Vector3 steeredXZ = desired; steeredXZ.y = 0f;
            lastMoveDir = steeredXZ.sqrMagnitude > 0.0001f ? steeredXZ.normalized : dir;
        }
    }

    // Basic obstacle awareness for tile dungeons: probe ahead and slide along whichever perpendicular
    // side has clearance. No A*, just enough to round corners.
    private static readonly RaycastHit[] s_avoidanceHits = new RaycastHit[8];
    private Vector3 SteerAroundObstacles(Vector3 desired)
    {
        Vector3 dir = new Vector3(desired.x, 0f, desired.z);
        if (dir.sqrMagnitude < 0.0001f) return desired;
        dir.Normalize();
        Vector3 origin = transform.position + Vector3.up * obstacleProbeYOffset;
        float radius = Mathf.Max(0.1f, obstacleProbeRadius);
        float distance = Mathf.Max(0.1f, obstacleProbeDistance);

        if (!IsBlocked(origin, dir, radius, distance)) return desired;

        Vector3 right = Vector3.Cross(Vector3.up, dir);
        bool leftBlocked = IsBlocked(origin, -right, radius, distance);
        bool rightBlocked = IsBlocked(origin, right, radius, distance);
        float speed = new Vector2(desired.x, desired.z).magnitude;

        if (!leftBlocked && rightBlocked) return new Vector3(-right.x * speed, desired.y, -right.z * speed);
        if (!rightBlocked && leftBlocked) return new Vector3(right.x * speed, desired.y, right.z * speed);
        if (!leftBlocked && !rightBlocked)
        {
            // Both sides open: pick by instance-id parity so the enemy commits instead of oscillating.
            Vector3 chosen = (GetInstanceID() & 1) == 0 ? right : -right;
            return new Vector3(chosen.x * speed, desired.y, chosen.z * speed);
        }
        // Boxed in on all three sides: back up briefly and re-evaluate next frame.
        return new Vector3(-dir.x * speed * 0.3f, desired.y, -dir.z * speed * 0.3f);
    }

    private bool IsBlocked(Vector3 origin, Vector3 dir, float radius, float distance)
    {
        int hitCount = Physics.SphereCastNonAlloc(origin, radius, dir, s_avoidanceHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Transform t = s_avoidanceHits[i].collider.transform;
            // Skip our own hierarchy and the player (we want to reach them, not strafe around).
            if (t.IsChildOf(transform) || transform.IsChildOf(t)) continue;
            if (player != null && (t == player || t.IsChildOf(player))) continue;
            return true;
        }
        return false;
    }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 8f);
    }

    private void PickWanderTarget()
    {
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        wanderTarget = spawnOrigin + new Vector3(r.x, 0f, r.y);
        wanderTimer = wanderRepathInterval;
    }

    private void FireProjectile()
    {
        if (player == null || playerHealth == null) return;

        Vector3 origin = transform.position + Vector3.up * aimHeightOffset;
        Vector3 targetPoint = player.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPoint - origin).normalized;

        if (fireballPrefab != null)
        {
            Fireball.Spawn(fireballPrefab, origin, dir, projectileSpeed, scaledDamage, projectileLifetime, fireballRadius, fireballScale, fireballHitMask, transform, fireballHitVfxPrefab, 1f, fireballHitVfxHoldDuration, fireballHitVfxShrinkDuration, fireballHitVfxScale);
        }
        else
        {
            EnemyProjectile.Spawn(origin, dir, projectileSpeed, projectileLifetime, scaledDamage, playerHealth);
        }
        PlayAnimation(animAttack);
        attackAnimationEndTime = Time.time + attackAnimationDuration;
    }

    public void TakeDamage(float amount)
    {
        if (dying) return;
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            if (playerLevel != null) playerLevel.GrantXP(xpReward);
            DropCoins();
            if (GameManager.Instance != null)
            {
                if (isBoss) GameManager.Instance.RegisterBossKill(transform.position);
                else GameManager.Instance.RegisterEnemyKill();
            }
            BeginDying();
            return;
        }
        if (!string.IsNullOrEmpty(animGetHit) && Time.time >= getHitAnimationEndTime)
        {
            PlayAnimation(animGetHit);
            getHitAnimationEndTime = Time.time + getHitAnimationDuration;
        }
    }

    private void BeginDying()
    {
        dying = true;
        if (animator != null && !string.IsNullOrEmpty(animDie)) animator.CrossFade(animDie, 0.1f, 0, 0f);
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        Destroy(gameObject, 1.5f);
    }

    private void DropCoins()
    {
        if (coinDropPrefab == null) return;
        int value = Random.Range(minCoinValue, maxCoinValue + 1);
        Coin.Spawn(coinDropPrefab, transform.position + Vector3.up * 0.5f, value);
    }

    public void TakeDamage(float amount, Vector3 hitFromWorldPos)
    {
        TakeDamage(amount, hitFromWorldPos, 1f);
    }

    public void TakeDamage(float amount, Vector3 hitFromWorldPos, float knockbackMultiplier)
    {
        bool wasAlive = currentHealth > 0f && !dying;
        TakeDamage(amount);
        if (!wasAlive || currentHealth <= 0f || dying) return;

        Vector3 away = transform.position - hitFromWorldPos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
        away.Normalize();

        if (rb != null)
        {
            Vector3 v = away * (knockbackForce * knockbackMultiplier);
            v.y = knockbackUpward * knockbackMultiplier;
            rb.linearVelocity = v;
        }
        knockbackEndTime = Time.time + knockbackStunDuration;
    }
}

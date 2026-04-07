using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyVariant
    {
        public string displayName = "Enemy";
        public GameObject prefab;
        public float scale = 1f;
        [Tooltip("Replaces the prefab's controller. Lets one humanoid animator retarget onto another humanoid model.")]
        public RuntimeAnimatorController animatorOverride;
        [Tooltip("Convert to URP shaders on spawn. Needed when an asset pack ships Built-in shaders (render pink under URP).")]
        public bool upgradeMaterialsToUrp = true;

        [Header("Animation State Names")]
        public string animIdle = "Idle01";
        public string animRun = "BattleRunForward";
        public string animAttack = "Attack01";
        public string animDie = "Die";
        public string animFarIdle = "";
        public string animGetHit = "";
        public string animWalkBack = "";
        public string animWalkLeft = "";
        public string animWalkRight = "";
        [Tooltip("Animator float param driven by movement state (1 moving, 0 idle). For blend-tree locomotion controllers.")]
        public string locomotionFloatParam = "";

        [Header("Collider")]
        public Vector3 colliderCenter = new Vector3(0f, 1f, 0f);
        public float colliderHeight = 2f;
        public float colliderRadius = 0.4f;

        [Header("Combat")]
        public float aimHeightOffset = 1.4f;
        [Tooltip("If true, chases into melee range and swings instead of firing a fireball.")]
        public bool isMelee = false;
        public float meleeRange = 2.0f;
        public float meleeDamageDelay = 0.35f;
        [Tooltip("Multiplier on the prefab's baseMaxHealth and healthPerStage. 1 = prefab default.")]
        public float healthMultiplier = 1f;
        [Tooltip("Multiplier on the prefab's baseDamage and damagePerStage. 1 = prefab default.")]
        public float damageMultiplier = 1f;
        [Tooltip("If > 0, overrides Enemy.attackInterval (seconds between attacks). Bump for slow-windup bosses so attacks don't stack.")]
        public float attackIntervalOverride = 0f;
        [Tooltip("If > 0, overrides Enemy.attackAnimationDuration. Locks locomotion off the run blend while the swing plays.")]
        public float attackAnimationDurationOverride = 0f;
        [Tooltip("Multiplier on the prefab's baseMoveSpeed and moveSpeedPerStage. 1 = prefab default.")]
        public float moveSpeedMultiplier = 1f;

        [Tooltip("Clip swapped into the Attack01 state. Lets a melee variant play a swing instead of the wizard's cast clip.")]
        public AnimationClip attackClipOverride;
        [Tooltip("One-shot at the start of each attack swing, played at the enemy's position.")]
        public AudioClip attackSoundClip;
        [Range(0f, 1f)] public float attackSoundVolume = 0.7f;

        [Header("Equipment Attachments")]
        [Tooltip("Spawned as a child of the right-hand bone. Humanoid rigs only.")]
        public GameObject rightHandAttachment;
        public Vector3 rightHandLocalPosition = Vector3.zero;
        public Vector3 rightHandLocalRotationEuler = Vector3.zero;
        public float rightHandAttachmentScale = 1f;
        [Tooltip("Spawned as a child of the left-hand bone. Humanoid rigs only.")]
        public GameObject leftHandAttachment;
        public Vector3 leftHandLocalPosition = Vector3.zero;
        public Vector3 leftHandLocalRotationEuler = Vector3.zero;
        public float leftHandAttachmentScale = 1f;
    }

    [Header("Wave Spawning - Base Values")]
    [Tooltip("Wave size for the first stage of each level (stage 1, 4, 7...). Later levels add waveSizePerLevel.")]
    public int baseWaveSize = 4;
    [Tooltip("Spawn interval for the first stage of each level. Stages 2-3 shrink it via intraWaveMultipliers.")]
    public float baseIntraWaveSpawnInterval = 1.2f;
    [Tooltip("Wave rest seconds for the first stage of each level. Stages 2-3 shrink it via waveRestMultipliers.")]
    public float baseWaveRestSeconds = 10f;
    [Tooltip("Concurrent live-enemy cap for level 1. Later levels add concurrentCapPerLevel.")]
    public int baseConcurrentCap = 8;
    [Tooltip("Delay before the first wave after scene load.")]
    public float initialWaveDelay = 1.5f;

    [Header("Wave Spawning - Per-Stage Scaling")]
    [Tooltip("Per-stage-in-level multiplier on baseIntraWaveSpawnInterval. Lower = faster spawns.")]
    public float[] intraWaveMultipliers = new float[] { 1.0f, 0.65f, 0.42f };
    [Tooltip("Per-stage-in-level multiplier on baseWaveRestSeconds. Lower = shorter pause between waves.")]
    public float[] waveRestMultipliers = new float[] { 1.0f, 0.7f, 0.5f };
    [Tooltip("Wave size added per level beyond level 1.")]
    public int waveSizePerLevel = 2;
    [Tooltip("Concurrent cap added per level beyond level 1.")]
    public int concurrentCapPerLevel = 6;
    [Tooltip("Compounded multiplier on interval and rest, applied once per wave already spawned. <1 speeds up later waves.")]
    [Range(0.5f, 1f)] public float perWaveSpeedupMultiplier = 0.97f;
    [Tooltip("Floor on the compounded per-wave multiplier.")]
    [Range(0.1f, 1f)] public float perWaveSpeedupFloor = 0.45f;

    [Header("Spawn Placement")]
    public float minSpawnDistance = 22f;
    public float maxSpawnDistance = 35f;
    public float spawnHeightOffset = 1.2f;
    public LayerMask groundMask = ~0;
    [Tooltip("Stamp spawn Y to the player's Y instead of raycasting. For flat-tile dungeons where the raycast hits wall tops.")]
    public bool forceSpawnYToPlayer = false;
    [Tooltip("Enable obstacle-avoidance steering on spawned enemies. Needed for tile-based dungeons, not open terrain.")]
    public bool enemiesAvoidObstacles = false;
    [Tooltip("If > 0, boss spawns this many units in front of the player instead of via the ring picker.")]
    public float bossSpawnInFrontDistance = 0f;
    [Tooltip("Boss spawns at the player's level-start position instead of near where they currently are. Takes precedence over bossSpawnInFrontDistance.")]
    public bool bossSpawnsAtPlayerStart = false;

    [Header("Enemy Visual")]
    [Tooltip("Per-scene roster of enemy types. When non-empty, a variant is picked uniformly at random per spawn.")]
    public List<EnemyVariant> enemyVariants = new List<EnemyVariant>();
    [Tooltip("Legacy single-prefab path used when enemyVariants is empty. Children named 'staff' are disabled. Falls back to a tinted Capsule if null.")]
    public GameObject enemyVisualPrefab;
    public Color enemyColor = new Color(0.55f, 0.1f, 0.15f, 1f);

    [Header("Loot")]
    [Tooltip("Coin prefab passed to every spawned enemy for drop on death.")]
    public GameObject coinDropPrefab;

    [Header("Enemy Fireball Projectile")]
    [Tooltip("Fireball prefab spawned by enemies (reuses the player's fireball asset).")]
    public GameObject enemyFireballPrefab;
    [Tooltip("Impact VFX when an enemy's fireball hits.")]
    public GameObject enemyFireballHitVfxPrefab;

    [Header("Stage 1 Boss")]
    [Tooltip("Visual prefab for the stage 1 boss. Ignored when bossVariant.prefab is set.")]
    public GameObject bossVisualPrefab;
    [Tooltip("Scale for the boss visual root. Ignored when bossVariant.prefab is set (bossVariant.scale wins).")]
    public float bossScale = 5f;
    public float bossFireballScale = 1.05f;

    [Header("Boss Variant Override")]
    [Tooltip("When prefab is set, the boss is built from this EnemyVariant config instead of the legacy slime path.")]
    public EnemyVariant bossVariant;

    [Header("Enemy Cast Animation")]
    [Tooltip("Clip that replaces the wizard's Attack01 state at runtime. Leave empty to keep the prefab default.")]
    public AnimationClip enemyCastClipOverride;

    private readonly List<Enemy> liveEnemies = new();
    private Transform player;
    private PlayerHealth playerHealth;
    private int enemiesRemainingInWave;
    private float intraWaveTimer;
    private float waveRestTimer;
    private int wavesSpawned;
    private Vector3 playerStartPosition;
    private bool hasPlayerStartPosition;

    public int WavesSpawned => wavesSpawned;
    public int EnemiesRemainingInWave => enemiesRemainingInWave;
    public float WaveRestRemaining => waveRestTimer;

    private int CurrentStage => GameManager.Instance != null ? GameManager.Instance.CurrentStage : 1;
    // Levels group stages in threes: stages 1-3 are level 0, 4-6 level 1, etc.
    private int CurrentLevelIndex => Mathf.Max(0, (CurrentStage - 1) / 3);
    // Position within the level: stages 1/4/7 give 0, 2/5/8 give 1, 3/6/9 give 2.
    private int StageInLevel => Mathf.Max(0, (CurrentStage - 1) % 3);

    public int CurrentWaveSize() => Mathf.Max(1, baseWaveSize + CurrentLevelIndex * Mathf.Max(0, waveSizePerLevel));
    public int CurrentConcurrentCap() => Mathf.Max(1, baseConcurrentCap + CurrentLevelIndex * Mathf.Max(0, concurrentCapPerLevel));

    private float PerWaveSpeedup()
    {
        if (perWaveSpeedupMultiplier >= 1f || wavesSpawned <= 0) return 1f;
        float compounded = Mathf.Pow(perWaveSpeedupMultiplier, wavesSpawned);
        return Mathf.Max(perWaveSpeedupFloor, compounded);
    }

    public float CurrentIntraWaveInterval()
    {
        int sil = StageInLevel;
        float mul = intraWaveMultipliers != null && sil < intraWaveMultipliers.Length ? intraWaveMultipliers[sil] : 1f;
        return Mathf.Max(0.05f, baseIntraWaveSpawnInterval * mul * PerWaveSpeedup());
    }
    public float CurrentWaveRestSeconds()
    {
        int sil = StageInLevel;
        float mul = waveRestMultipliers != null && sil < waveRestMultipliers.Length ? waveRestMultipliers[sil] : 1f;
        return Mathf.Max(0.5f, baseWaveRestSeconds * mul * PerWaveSpeedup());
    }

    private void Start()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null) player = playerHealth.transform;
        waveRestTimer = Mathf.Max(0f, initialWaveDelay);
        enemiesRemainingInWave = 0;
        Debug.Log($"Start. player={player != null}, waveSize={CurrentWaveSize()}, rest={CurrentWaveRestSeconds():F1}s, cap={CurrentConcurrentCap()}");

        if (bossSpawnsAtPlayerStart) StartCoroutine(CapturePlayerStartPosition());
    }

    // Records where the player begins the level, for bossSpawnsAtPlayerStart. The two-frame wait lets
    // PlayerRunState.Apply run first: on a Portal entry it teleports the player a frame into the scene,
    // so reading the position any sooner would catch the pre-teleport spot.
    private IEnumerator CapturePlayerStartPosition()
    {
        yield return null;
        yield return null;
        if (player == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null) player = playerHealth.transform;
        }
        if (player == null) yield break;
        playerStartPosition = player.position;
        hasPlayerStartPosition = true;
    }

    private void Update()
    {
        if (player == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null) player = playerHealth.transform;
            else return;
        }

        if (GameManager.Instance != null && GameManager.Instance.GameOver) return;
        // No wave spawning during boss fights or after the run is complete.
        if (GameManager.Instance != null && GameManager.Instance.Phase != GameManager.StagePhase.Waves) return;

        liveEnemies.RemoveAll(e => e == null);

        if (liveEnemies.Count >= CurrentConcurrentCap()) return;

        if (enemiesRemainingInWave > 0)
        {
            intraWaveTimer -= Time.deltaTime;
            if (intraWaveTimer <= 0f)
            {
                SpawnOne();
                enemiesRemainingInWave--;
                intraWaveTimer = CurrentIntraWaveInterval();
                if (enemiesRemainingInWave == 0)
                    waveRestTimer = CurrentWaveRestSeconds();
            }
        }
        else
        {
            waveRestTimer -= Time.deltaTime;
            if (waveRestTimer <= 0f)
            {
                enemiesRemainingInWave = CurrentWaveSize();
                intraWaveTimer = 0f;
                wavesSpawned++;
            }
        }
    }

    private void SpawnOne()
    {
        Vector3 spawnPos = PickSpawnPoint();
        GameObject go;
        EnemyVariant variant = PickVariant();

        if (variant != null && variant.prefab != null)
        {
            go = Instantiate(variant.prefab, spawnPos, Quaternion.identity);
            go.name = string.IsNullOrEmpty(variant.displayName) ? "Enemy" : variant.displayName;
            float s = Mathf.Max(0.01f, variant.scale);
            go.transform.localScale = new Vector3(s, s, s);
            DisableStaffChildren(go);

            CapsuleCollider col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = go.AddComponent<CapsuleCollider>();
            col.center = variant.colliderCenter;
            col.height = variant.colliderHeight;
            col.radius = variant.colliderRadius;

            if (variant.upgradeMaterialsToUrp) UrpMaterialUpgrader.Convert(go);
        }
        else if (enemyVisualPrefab != null)
        {
            go = Instantiate(enemyVisualPrefab, spawnPos, Quaternion.identity);
            go.name = "Enemy";
            DisableStaffChildren(go);

            if (go.GetComponent<CapsuleCollider>() == null)
            {
                CapsuleCollider col = go.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1f, 0f);
                col.height = 2f;
                col.radius = 0.4f;
            }
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Enemy";
            go.transform.position = spawnPos;
            go.transform.localScale = new Vector3(1f, 1.1f, 1f);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.color = enemyColor;
                r.material = m;
            }
        }

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 2f;

        Animator anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            if (variant != null && variant.animatorOverride != null)
                anim.runtimeAnimatorController = variant.animatorOverride;
            // Variant Attack01 swap wins over the spawner-wide cast override so a melee variant gets its swing clip.
            AnimationClip attackClip = variant != null && variant.attackClipOverride != null ? variant.attackClipOverride : enemyCastClipOverride;
            if (attackClip != null && anim.runtimeAnimatorController != null)
            {
                var aoc = new AnimatorOverrideController(anim.runtimeAnimatorController);
                aoc["Attack01"] = attackClip;
                anim.runtimeAnimatorController = aoc;
            }
        }

        if (variant != null && anim != null && anim.isHuman)
        {
            if (variant.rightHandAttachment != null)
                AttachToBone(anim, HumanBodyBones.RightHand, variant.rightHandAttachment, variant.rightHandLocalPosition, variant.rightHandLocalRotationEuler, variant.rightHandAttachmentScale);
            if (variant.leftHandAttachment != null)
                AttachToBone(anim, HumanBodyBones.LeftHand, variant.leftHandAttachment, variant.leftHandLocalPosition, variant.leftHandLocalRotationEuler, variant.leftHandAttachmentScale);
        }

        Enemy enemy = go.AddComponent<Enemy>();
        enemy.coinDropPrefab = coinDropPrefab;
        enemy.fireballPrefab = enemyFireballPrefab;
        enemy.fireballHitVfxPrefab = enemyFireballHitVfxPrefab;
        if (variant != null)
        {
            ApplyVariantToEnemy(variant, enemy);
            enemy.aimHeightOffset = variant.aimHeightOffset;
            // Apply multipliers before Initialize computes final HP/damage from these.
            float hpMul = Mathf.Max(0.01f, variant.healthMultiplier);
            float dmgMul = Mathf.Max(0f, variant.damageMultiplier);
            enemy.baseMaxHealth *= hpMul;
            enemy.healthPerStage *= hpMul;
            enemy.baseDamage *= dmgMul;
            enemy.damagePerStage *= dmgMul;
            if (variant.attackIntervalOverride > 0f) enemy.attackInterval = variant.attackIntervalOverride;
            if (variant.attackAnimationDurationOverride > 0f) enemy.attackAnimationDuration = variant.attackAnimationDurationOverride;
            float spdMul = Mathf.Max(0.01f, variant.moveSpeedMultiplier);
            enemy.baseMoveSpeed *= spdMul;
            enemy.moveSpeedPerStage *= spdMul;
        }
        enemy.avoidObstacles = enemiesAvoidObstacles;
        // Scale the avoidance probe to the variant's collider so big enemies get a wider margin.
        if (variant != null)
        {
            float scaled = Mathf.Max(0.2f, variant.colliderRadius) * Mathf.Max(0.1f, variant.scale);
            enemy.obstacleProbeRadius = scaled + 0.05f;
            enemy.obstacleProbeDistance = Mathf.Max(1.0f, scaled * 3.5f);
            enemy.obstacleProbeYOffset = variant.colliderCenter.y * Mathf.Max(0.1f, variant.scale);
        }
        enemy.Initialize(player, playerHealth);
        liveEnemies.Add(enemy);
        int stage = GameManager.Instance != null ? GameManager.Instance.CurrentStage : 1;
        string label = variant != null ? variant.displayName : "wizard";
        Debug.Log($"Spawned {label} stage{stage} at {spawnPos} (live: {liveEnemies.Count}, wave remaining: {enemiesRemainingInWave})");
    }

    // Shared variant->Enemy field copy for SpawnOne and SpawnBoss. Only the fields both assign
    // identically; aimHeightOffset and the stat multipliers stay at the call sites.
    private static void ApplyVariantToEnemy(EnemyVariant variant, Enemy enemy)
    {
        enemy.animIdle = variant.animIdle;
        enemy.animRun = variant.animRun;
        enemy.animAttack = variant.animAttack;
        enemy.animDie = variant.animDie;
        enemy.animFarIdle = variant.animFarIdle;
        enemy.animGetHit = variant.animGetHit;
        enemy.animWalkBack = variant.animWalkBack;
        enemy.animWalkLeft = variant.animWalkLeft;
        enemy.animWalkRight = variant.animWalkRight;
        enemy.isMelee = variant.isMelee;
        enemy.meleeRange = variant.meleeRange;
        enemy.meleeDamageDelay = variant.meleeDamageDelay;
        enemy.locomotionFloatParam = variant.locomotionFloatParam;
        enemy.attackSoundClip = variant.attackSoundClip;
        enemy.attackSoundVolume = variant.attackSoundVolume;
    }

    private EnemyVariant PickVariant()
    {
        if (enemyVariants == null || enemyVariants.Count == 0) return null;
        // Skip null/empty entries so a partially-configured inspector list still works.
        int valid = 0;
        for (int i = 0; i < enemyVariants.Count; i++)
            if (enemyVariants[i] != null && enemyVariants[i].prefab != null) valid++;
        if (valid == 0) return null;

        int target = Random.Range(0, valid);
        int seen = 0;
        for (int i = 0; i < enemyVariants.Count; i++)
        {
            EnemyVariant v = enemyVariants[i];
            if (v == null || v.prefab == null) continue;
            if (seen == target) return v;
            seen++;
        }
        return null;
    }

    public Enemy SpawnBoss()
    {
        if (player == null)
        {
            if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null) player = playerHealth.transform;
        }
        if (player == null)
        {
            Debug.LogWarning("Cannot spawn boss: no player reference.");
            return null;
        }

        Vector3 spawnPos = ResolveBossSpawnPosition();
        bool variantBoss = bossVariant != null && bossVariant.prefab != null;
        bool slimeBoss = !variantBoss && bossVisualPrefab != null;

        GameObject go = CreateBossVisual(spawnPos, variantBoss, slimeBoss);

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 12f;

        ConfigureBossAnimator(go, variantBoss);

        Enemy boss = go.AddComponent<Enemy>();
        ConfigureBossStats(boss, variantBoss);
        ApplyBossArchetype(boss, variantBoss, slimeBoss);

        boss.Initialize(player, playerHealth);
        liveEnemies.Add(boss);
        string label = variantBoss ? bossVariant.displayName : (slimeBoss ? "slime" : "fallback");
        Debug.Log($"Boss spawned at {spawnPos} ({label})");
        return boss;
    }

    // Boss spawn placement, in priority order: the player's level-start position, then a fixed
    // distance in front of the player, then the standard ring picker.
    private Vector3 ResolveBossSpawnPosition()
    {
        if (bossSpawnsAtPlayerStart && hasPlayerStartPosition)
            return playerStartPosition;

        Vector3 spawnPos;
        if (bossSpawnInFrontDistance > 0f)
        {
            Vector3 fwd = player.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            spawnPos = player.position + fwd * bossSpawnInFrontDistance;
        }
        else
        {
            spawnPos = PickSpawnPoint();
        }
        return spawnPos;
    }

    // Instantiates the boss root and configures its scale, collider and materials.
    // Three paths: configured variant prefab, legacy slime prefab, or a tinted fallback capsule.
    private GameObject CreateBossVisual(Vector3 spawnPos, bool variantBoss, bool slimeBoss)
    {
        GameObject go;
        if (variantBoss)
        {
            go = Instantiate(bossVariant.prefab, spawnPos, Quaternion.identity);
            go.name = string.IsNullOrEmpty(bossVariant.displayName) ? "Boss" : bossVariant.displayName;
            float s = Mathf.Max(0.1f, bossVariant.scale);
            go.transform.localScale = new Vector3(s, s, s);

            CapsuleCollider col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = go.AddComponent<CapsuleCollider>();
            col.center = bossVariant.colliderCenter;
            col.height = bossVariant.colliderHeight;
            col.radius = bossVariant.colliderRadius;

            if (bossVariant.upgradeMaterialsToUrp) UrpMaterialUpgrader.Convert(go);
        }
        else if (slimeBoss)
        {
            go = Instantiate(bossVisualPrefab, spawnPos, Quaternion.identity);
            go.name = "Boss";
            float s = Mathf.Max(0.1f, bossScale);
            go.transform.localScale = new Vector3(s, s, s);

            CapsuleCollider col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = go.AddComponent<CapsuleCollider>();
            // Tight-fit capsule around the slime mesh in local space (AABB ~1.3m wide x 1.3m tall).
            col.center = new Vector3(0f, 0.7f, 0f);
            col.height = 1.4f;
            col.radius = 0.7f;

            // Slime materials target Built-in Standard (pink under URP). Rebuild against URP/Lit, keeping PBR texture bindings.
            UrpMaterialUpgrader.Convert(go);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Boss";
            go.transform.position = spawnPos;
            go.transform.localScale = new Vector3(3f, 3.5f, 3f);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.color = new Color(0.6f, 0.05f, 0.05f, 1f);
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.5f, 0.05f, 0.05f, 1f));
                }
                r.material = m;
            }
        }
        return go;
    }

    // Animator override, attack clip swap, hand attachments. Variant path only; the slime ships its own animator.
    private void ConfigureBossAnimator(GameObject go, bool variantBoss)
    {
        if (variantBoss)
        {
            Animator anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                if (bossVariant.animatorOverride != null) anim.runtimeAnimatorController = bossVariant.animatorOverride;
                if (bossVariant.attackClipOverride != null && anim.runtimeAnimatorController != null)
                {
                    var aoc = new AnimatorOverrideController(anim.runtimeAnimatorController);
                    aoc["Attack01"] = bossVariant.attackClipOverride;
                    anim.runtimeAnimatorController = aoc;
                }
                if (anim.isHuman)
                {
                    if (bossVariant.rightHandAttachment != null)
                        AttachToBone(anim, HumanBodyBones.RightHand, bossVariant.rightHandAttachment, bossVariant.rightHandLocalPosition, bossVariant.rightHandLocalRotationEuler, bossVariant.rightHandAttachmentScale);
                    if (bossVariant.leftHandAttachment != null)
                        AttachToBone(anim, HumanBodyBones.LeftHand, bossVariant.leftHandAttachment, bossVariant.leftHandLocalPosition, bossVariant.leftHandLocalRotationEuler, bossVariant.leftHandAttachmentScale);
                }
            }
        }
    }

    // Boss-wide stat baseline: HP/damage (with optional variant multipliers), attack timing,
    // engagement ranges, projectile refs and loot/XP rewards.
    private void ConfigureBossStats(Enemy boss, bool variantBoss)
    {
        boss.isBoss = true;
        boss.baseMaxHealth = 450f;
        boss.healthPerStage = 150f;
        boss.baseDamage = 24f;
        boss.damagePerStage = 6f;
        // Variant multipliers let a specific boss exceed the default 450 HP / 24 dmg without editing the prefab.
        if (variantBoss)
        {
            float hpMul = Mathf.Max(0.01f, bossVariant.healthMultiplier);
            float dmgMul = Mathf.Max(0f, bossVariant.damageMultiplier);
            boss.baseMaxHealth *= hpMul;
            boss.healthPerStage *= hpMul;
            boss.baseDamage *= dmgMul;
            boss.damagePerStage *= dmgMul;
        }
        boss.attackInterval = 0.8f;
        boss.attackCooldownJitter = 0.15f;
        boss.preferredAttackDistance = 26f;
        boss.chaseRadius = 80f;
        boss.disengageRadius = 100f;
        boss.fireballPrefab = enemyFireballPrefab;
        boss.fireballHitVfxPrefab = enemyFireballHitVfxPrefab;
        boss.fireballHitVfxScale = 0.8f;
        boss.coinDropPrefab = coinDropPrefab;
        boss.minCoinValue = 60;
        boss.maxCoinValue = 120;
        boss.baseXpReward = 80;
        boss.xpRewardPerStage = 40;
    }

    // Per-archetype boss setup: animation state names, aim height, fireball scale, attack/get-hit
    // durations and the health bar overrides. One branch each for variant, slime and fallback bosses.
    private void ApplyBossArchetype(Enemy boss, bool variantBoss, bool slimeBoss)
    {
        if (variantBoss)
        {
            ApplyVariantToEnemy(bossVariant, boss);
            // aimHeightOffset is pre-scale meters (model chest height). Multiply by scale so the strike origin tracks the giant body.
            boss.aimHeightOffset = bossVariant.aimHeightOffset * Mathf.Max(0.1f, bossVariant.scale);
            boss.fireballScale = bossFireballScale;
            boss.attackAnimationDuration = 1.0f;
            boss.getHitAnimationDuration = 0.5f;
            // Overrides come last so they beat the boss-path defaults. Slow-windup bosses need this or
            // attack #2 fires before #1 resolves.
            if (bossVariant.attackIntervalOverride > 0f)
            {
                boss.attackInterval = bossVariant.attackIntervalOverride;
                boss.attackCooldownJitter = Mathf.Min(boss.attackCooldownJitter, bossVariant.attackIntervalOverride * 0.2f);
            }
            if (bossVariant.attackAnimationDurationOverride > 0f)
                boss.attackAnimationDuration = bossVariant.attackAnimationDurationOverride;
            float bossSpdMul = Mathf.Max(0.01f, bossVariant.moveSpeedMultiplier);
            boss.baseMoveSpeed *= bossSpdMul;
            boss.moveSpeedPerStage *= bossSpdMul;

            // Health bar sits above the collider top. crownY is in local pre-scale meters; localOffset is scaled later by the transform.
            float crownY = bossVariant.colliderCenter.y + 0.5f * bossVariant.colliderHeight + 0.2f;
            EnemyHealthBar bar = boss.GetComponent<EnemyHealthBar>();
            if (bar != null) bar.ApplyOverrides(new Vector3(0f, crownY, 0f), 150f, new Vector2(1.0f, 0.15f));
        }
        else if (slimeBoss)
        {
            boss.fireballScale = bossFireballScale;
            // Slime mesh is ~1.3m tall in local space; aim from ~0.85 * bossScale above the origin.
            boss.aimHeightOffset = 0.85f * bossScale;
            boss.animIdle = "IdleBattle";
            boss.animFarIdle = "SenseSomethingRPT";
            boss.animRun = "WalkFWD";
            boss.animWalkBack = "WalkBWD";
            boss.animWalkLeft = "WalkLeft";
            boss.animWalkRight = "WalkRight";
            boss.animAttack = "Attack01";
            boss.animDie = "Die";
            boss.animGetHit = "GetHit";
            boss.attackAnimationDuration = 1.1f;
            boss.getHitAnimationDuration = 0.5f;

            // localOffset is scaled by bossScale, so y=0.95 sits just above the slime; worldSize is pre-scale meters.
            EnemyHealthBar bar = boss.GetComponent<EnemyHealthBar>();
            if (bar != null)
            {
                bar.ApplyOverrides(new Vector3(0f, 0.95f, 0f), 150f, new Vector2(1.0f, 0.15f));
            }
        }
        else
        {
            boss.fireballScale = 0.7f;
            boss.aimHeightOffset = 2.5f;
        }
    }

    private static void AttachToBone(Animator anim, HumanBodyBones bone, GameObject prefab, Vector3 localPos, Vector3 localEuler, float scale)
    {
        Transform bt = anim.GetBoneTransform(bone);
        if (bt == null) return;
        GameObject inst = Instantiate(prefab, bt);
        inst.transform.localPosition = localPos;
        inst.transform.localRotation = Quaternion.Euler(localEuler);
        float s = Mathf.Max(0.01f, scale);
        inst.transform.localScale = new Vector3(s, s, s);
        // Asset-pack props often ship Built-in shaders that render pink under URP. Convert in place.
        UrpMaterialUpgrader.Convert(inst);
    }

    private static void DisableStaffChildren(GameObject root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name;
            if (!string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains("staff"))
                all[i].gameObject.SetActive(false);
        }
    }

    private bool loggedSpawnArea;

    private Vector3 PickSpawnPoint()
    {
        bool hasArea = SpawnArea.TryGetBounds(out Bounds area);
        if (hasArea && !loggedSpawnArea)
        {
            loggedSpawnArea = true;
            Debug.Log($"Spawn area bounds (post-inset): min={area.min} max={area.max} size={area.size}");
        }

        Vector3 candidate = Vector3.zero;
        bool placed = false;

        // Prefer ring-around-player candidates that land inside the play area.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(minSpawnDistance, maxSpawnDistance);
            candidate = player.position + new Vector3(ring.x, 0f, ring.y);
            if (!hasArea || SpawnArea.Contains(area, candidate)) { placed = true; break; }
        }
        // All ring attempts fell outside the border (player hugging an edge). Sample uniformly inside.
        if (!placed && hasArea)
            candidate = SpawnArea.RandomPointXZ(area, player.position.y);

        // Final clamp before grounding, in case the loop above fell through.
        if (hasArea)
        {
            candidate.x = Mathf.Clamp(candidate.x, area.min.x, area.max.x);
            candidate.z = Mathf.Clamp(candidate.z, area.min.z, area.max.z);
        }

        return GroundedSpawnPoint(candidate);
    }

    // Raycasts down and returns the first hit that isn't a border wall. Tall thin border colliders
    // would otherwise be hit before the terrain when the candidate XZ grazes their footprint.
    private Vector3 GroundedSpawnPoint(Vector3 candidate)
    {
        // Flat-floor scenes can't trust the ground raycast (walls and ceilings intercept it). Stamp to the player's Y.
        if (forceSpawnYToPlayer)
            return new Vector3(candidate.x, player.position.y, candidate.z);

        Vector3 rayStart = new Vector3(candidate.x, candidate.y + 100f, candidate.z);
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 300f, groundMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (SpawnArea.IsBorder(hits[i].collider)) continue;
                return hits[i].point + Vector3.up * spawnHeightOffset;
            }
        }
        return new Vector3(candidate.x, player.position.y + spawnHeightOffset, candidate.z);
    }
}

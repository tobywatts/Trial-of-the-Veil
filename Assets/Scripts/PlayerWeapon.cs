using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerWeapon : MonoBehaviour
{
    [Header("Magic Attack")]
    public float damage = 14.375f;
    public LayerMask hitMask = ~0;
    [Tooltip("Extra damage vs targets above highHpThreshold. 0.15 = +15%.")]
    public float damageBonusVsHighHp = 0f;
    [Tooltip("HP fraction (0..1) above which the high-HP bonus applies.")]
    [Range(0f, 1f)] public float highHpThreshold = 0.5f;
    [Tooltip("Higher = faster recovery between casts.")]
    public float attackSpeedMultiplier = 1f;

    [Header("Fireball")]
    public GameObject fireballPrefab;
    [Tooltip("Units per second.")]
    public float fireballSpeed = 42f;
    [Tooltip("Lifetime if it never hits anything.")]
    public float fireballLifetime = 4f;
    [Tooltip("Sphere-cast radius for collision detection.")]
    public float fireballRadius = 0.2f;
    [Tooltip("Spawn offset forward of the hand bone, avoids clipping the hand.")]
    public float fireballForwardOffset = 0.15f;
    public float fireballScale = 0.35f;
    [Tooltip("Extra fireballs per cast, each spread up to multishotMaxSpreadDegrees.")]
    public int extraFireballs = 0;
    [Tooltip("Max yaw deviation in degrees for each extra fireball.")]
    public float multishotMaxSpreadDegrees = 15f;

    [Header("Fireball Impact VFX")]
    public GameObject fireballHitVfxPrefab;
    [Tooltip(">1 makes the effect resolve faster.")]
    public float fireballHitVfxSpeedMultiplier = 1f;
    [Tooltip("Seconds at full size before the shrink begins.")]
    public float fireballHitVfxHoldDuration = 0.6f;
    [Tooltip("Seconds spent shrinking down before destroy.")]
    public float fireballHitVfxShrinkDuration = 0.5f;
    public float fireballHitVfxScale = 0.5f;

    [Header("Ice Spell (Q / LB)")]
    public GameObject iceSpellVfxPrefab;
    [Tooltip("AoE damage to enemies within iceSpellRadius.")]
    public float iceSpellDamage = 23f;
    [Tooltip("Knockback multiplier on hit enemies. 1 = normal, 5 = 5x.")]
    public float iceSpellKnockbackMultiplier = 5f;
    [Tooltip("Damage radius around the player, in metres.")]
    public float iceSpellRadius = 4f;
    [Tooltip("Delay after cast start before VFX spawns and damage applies.")]
    public float iceSpellSpawnDelay = 0.35f;
    public float iceSpellCooldown = 1.5f;
    public float iceSpellVfxLifetime = 3f;
    [Tooltip(">1 makes the nova larger.")]
    public float iceSpellVfxScale = 3f;
    [Tooltip("Extra multiplier on the Y axis only. 1.5 = 50% taller.")]
    public float iceSpellVfxHeightMultiplier = 1f;
    [Tooltip("Negative values sink the VFX into the ground, hides the bottom hemisphere on slopes.")]
    public float iceSpellVfxYOffset = -1f;
    [Tooltip("Fallback distance ahead of the camera when aiming at empty sky.")]
    public float iceSpellForwardDistance = 8f;
    [Tooltip("Max downward search distance for the ground-snap raycast.")]
    public float iceSpellGroundProbeDistance = 6f;
    public LayerMask iceSpellGroundMask = ~0;

    [Header("Chain Behaviour")]
    [Tooltip("Idle time after a cast before the chain resets to the first (L) animation.")]
    public float chainResetWindow = 1.0f;
    public float crossFadeDuration = 0.05f;
    [Tooltip("Override of cast clip durations. 0 = auto-detect from animator clips.")]
    public float castLDurationOverride = 0f;
    public float castRDurationOverride = 0f;

    [Header("References")]
    public Camera attackCamera;

    private Animator playerAnimator;
    private PlayerCombatStance stance;
    private float castLDuration = 0.7f;
    private float castRDuration = 0.7f;
    private int upperBodyLayerIndex = -1;
    private const float LayerBlendOutDuration = 0.15f;

    private enum AttackPhase { Idle, Casting, ChainWindow }
    private AttackPhase phase = AttackPhase.Idle;
    public bool IsAttacking => phase == AttackPhase.Casting;
    private int nextCastIndex; // 0 = L, 1 = R
    private float phaseEndTime;

    private static readonly string[] CastStateNames = { "CastL", "CastR" };
    private const string CastLClipName = "HumanM@MagicAttackDirect1H01_L - Cast";
    private const string CastRClipName = "HumanM@MagicAttackDirect1H01_R";
    private const string CastIceStateName = "CastIce";
    private const string CastIceClipName = "HumanM@MagicAttackCall1H01_L - Cast";

    private bool iceCasting;
    private float iceCastEndTime;
    private float iceVfxSpawnTime;
    private bool iceVfxSpawned;
    private float lastIceCastStartTime = -999f;
    private float castIceDuration = 0.9f;

    public float IceSpellCooldownRemaining => Mathf.Max(0f, iceSpellCooldown - (Time.time - lastIceCastStartTime));
    public bool IceSpellReady => IceSpellCooldownRemaining <= 0f && !iceCasting;

    public float ApplyDamageConditionals(float baseDamage, Enemy target)
    {
        if (target == null || damageBonusVsHighHp <= 0f || target.MaxHealth <= 0f) return baseDamage;
        float frac = target.CurrentHealth / target.MaxHealth;
        if (frac > highHpThreshold) return baseDamage * (1f + damageBonusVsHighHp);
        return baseDamage;
    }

    private void Start()
    {
        if (attackCamera == null) attackCamera = Camera.main;
        playerAnimator = GetComponent<Animator>();
        stance = GetComponent<PlayerCombatStance>();
        CacheCastDurations();
        if (playerAnimator != null)
        {
            upperBodyLayerIndex = playerAnimator.GetLayerIndex("UpperBody");
            if (upperBodyLayerIndex < 0 && playerAnimator.layerCount > 1) upperBodyLayerIndex = 1;
            if (upperBodyLayerIndex > 0) playerAnimator.SetLayerWeight(upperBodyLayerIndex, 0f);
        }
    }

    private void CacheCastDurations()
    {
        castLDuration = 0.7f;
        castRDuration = 0.7f;
        castIceDuration = 0.9f;
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null) return;
        var clips = playerAnimator.runtimeAnimatorController.animationClips;
        foreach (var c in clips)
        {
            if (c == null) continue;
            if (c.name.Equals(CastLClipName, System.StringComparison.OrdinalIgnoreCase)) castLDuration = c.length;
            else if (c.name.Equals(CastRClipName, System.StringComparison.OrdinalIgnoreCase)) castRDuration = c.length;
            else if (c.name.Equals(CastIceClipName, System.StringComparison.OrdinalIgnoreCase)) castIceDuration = c.length;
        }
        if (castLDurationOverride > 0f) castLDuration = castLDurationOverride;
        if (castRDurationOverride > 0f) castRDuration = castRDurationOverride;
    }

    private void Update()
    {
        bool defending = stance != null && stance.IsDefending;

        if (!defending && AttackPressedThisFrame())
        {
            switch (phase)
            {
                case AttackPhase.Idle:
                    StartCast();
                    break;
                case AttackPhase.ChainWindow:
                    StartCast();
                    break;
            }
        }

        if (!defending && IceSpellPressedThisFrame() && !iceCasting && Time.time - lastIceCastStartTime >= iceSpellCooldown)
        {
            StartIceCast();
        }

        TickIceCast();
        TickPhase(defending);
        UpdateUpperBodyLayerWeight();
    }

    private void UpdateUpperBodyLayerWeight()
    {
        if (playerAnimator == null || upperBodyLayerIndex <= 0) return;
        float target = (phase == AttackPhase.Casting || iceCasting) ? 1f : 0f;
        float current = playerAnimator.GetLayerWeight(upperBodyLayerIndex);
        float speed = target > current ? 1f / 0.02f : 1f / LayerBlendOutDuration;
        float next = Mathf.MoveTowards(current, target, speed * Time.deltaTime);
        playerAnimator.SetLayerWeight(upperBodyLayerIndex, next);
    }

    private static bool AttackPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool AttackHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        if (Gamepad.current != null && Gamepad.current.rightShoulder.isPressed) return true;
        return false;
#else
        return Input.GetMouseButton(0);
#endif
    }

    private static bool IceSpellPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(KeyCode.Q);
#endif
    }

    private void TickPhase(bool defending)
    {
        if (phase == AttackPhase.Idle) return;
        if (Time.time < phaseEndTime) return;

        if (phase == AttackPhase.Casting)
        {
            if (!defending && AttackHeld())
            {
                StartCast();
            }
            else
            {
                phase = AttackPhase.ChainWindow;
                phaseEndTime = Time.time + chainResetWindow;
            }
        }
        else if (phase == AttackPhase.ChainWindow)
        {
            // Window expired without a new attack, next cast resets to L.
            phase = AttackPhase.Idle;
            nextCastIndex = 0;
        }
    }

    private void StartCast()
    {
        int idx = nextCastIndex;
        nextCastIndex = 1 - nextCastIndex;
        if (playerAnimator != null)
        {
            // Cast plays on the UpperBody layer so the legs keep running locomotion from the Base Layer.
            int layer = upperBodyLayerIndex > 0 ? upperBodyLayerIndex : (playerAnimator.layerCount > 1 ? 1 : 0);
            if (layer > 0) playerAnimator.SetLayerWeight(layer, 1f);
            playerAnimator.CrossFade(CastStateNames[idx], crossFadeDuration, layer, 0f);
        }

        phase = AttackPhase.Casting;
        float speed = Mathf.Max(0.1f, attackSpeedMultiplier);
        phaseEndTime = Time.time + (idx == 0 ? castLDuration : castRDuration) / speed;

        SpawnFireball(idx);
    }

    private void SpawnFireball(int castIndex)
    {
        if (fireballPrefab == null) return;

        HumanBodyBones handBone = castIndex == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        Transform hand = playerAnimator != null ? playerAnimator.GetBoneTransform(handBone) : null;

        Camera cam = attackCamera != null ? attackCamera : Camera.main;
        Vector3 spawnBase = hand != null ? hand.position : transform.position + Vector3.up * 1.2f;
        Vector3 aimPoint = ResolveAimPoint(cam, spawnBase);

        Vector3 aimDir = (aimPoint - spawnBase).normalized;
        if (aimDir.sqrMagnitude < 0.0001f) aimDir = (cam != null ? cam.transform.forward : transform.forward).normalized;

        Vector3 spawnPos = spawnBase + aimDir * fireballForwardOffset;
        Fireball.Spawn(fireballPrefab, spawnPos, aimDir, fireballSpeed, damage, fireballLifetime, fireballRadius, fireballScale, hitMask, transform, fireballHitVfxPrefab, fireballHitVfxSpeedMultiplier, fireballHitVfxHoldDuration, fireballHitVfxShrinkDuration, fireballHitVfxScale);

        for (int i = 0; i < extraFireballs; i++)
        {
            float yaw = Random.Range(-multishotMaxSpreadDegrees, multishotMaxSpreadDegrees);
            Vector3 spreadDir = Quaternion.AngleAxis(yaw, Vector3.up) * aimDir;
            Vector3 spreadSpawn = spawnBase + spreadDir * fireballForwardOffset;
            Fireball.Spawn(fireballPrefab, spreadSpawn, spreadDir, fireballSpeed, damage, fireballLifetime, fireballRadius, fireballScale, hitMask, transform, fireballHitVfxPrefab, fireballHitVfxSpeedMultiplier, fireballHitVfxHoldDuration, fireballHitVfxShrinkDuration, fireballHitVfxScale);
        }
    }

    private void StartIceCast()
    {
        lastIceCastStartTime = Time.time;
        iceCasting = true;
        iceVfxSpawned = false;
        iceVfxSpawnTime = Time.time + Mathf.Max(0f, iceSpellSpawnDelay);
        iceCastEndTime = Time.time + Mathf.Max(iceSpellSpawnDelay, castIceDuration);

        if (playerAnimator != null)
        {
            int layer = upperBodyLayerIndex > 0 ? upperBodyLayerIndex : (playerAnimator.layerCount > 1 ? 1 : 0);
            if (layer > 0) playerAnimator.SetLayerWeight(layer, 1f);
            playerAnimator.CrossFade(CastIceStateName, crossFadeDuration, layer, 0f);
        }
    }

    private void TickIceCast()
    {
        if (!iceCasting) return;
        if (!iceVfxSpawned && Time.time >= iceVfxSpawnTime)
        {
            SpawnIceVfx();
            iceVfxSpawned = true;
        }
        if (Time.time >= iceCastEndTime)
        {
            iceCasting = false;
        }
    }

    private void SpawnIceVfx()
    {
        Camera cam = attackCamera != null ? attackCamera : Camera.main;
        Vector3 aimPoint = ResolveAimPoint(cam, transform.position);

        Vector3 horizontalForward;
        if (cam != null) horizontalForward = cam.transform.forward;
        else horizontalForward = transform.forward;
        horizontalForward.y = 0f;
        if (horizontalForward.sqrMagnitude < 0.0001f) horizontalForward = transform.forward;
        horizontalForward.Normalize();

        Vector3 probeOrigin = aimPoint + Vector3.up * Mathf.Max(0.5f, iceSpellGroundProbeDistance * 0.5f);
        Vector3 spawnPos;
        if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit groundHit, iceSpellGroundProbeDistance, iceSpellGroundMask, QueryTriggerInteraction.Ignore))
        {
            spawnPos = groundHit.point + Vector3.up * iceSpellVfxYOffset;
        }
        else
        {
            spawnPos = aimPoint + Vector3.up * iceSpellVfxYOffset;
        }

        Quaternion spawnRot = Quaternion.LookRotation(horizontalForward, Vector3.up);

        // Seek 2s in to skip the slow charge-up so the impact "crack" lines up with the visual.
        SoundFx.PlayAtWithOffset(SoundFx.IceSpell, spawnPos, 2f);

        SpawnIceVfxObject(spawnPos, spawnRot);

        ApplyIceSpellDamage(spawnPos);
    }

    private void SpawnIceVfxObject(Vector3 spawnPos, Quaternion spawnRot)
    {
        if (iceSpellVfxPrefab != null)
        {
            GameObject vfx = Instantiate(iceSpellVfxPrefab, spawnPos, spawnRot);
            bool tweakScale = iceSpellVfxScale > 0f && Mathf.Abs(iceSpellVfxScale - 1f) > 0.0001f;
            bool tweakHeight = iceSpellVfxHeightMultiplier > 0f && Mathf.Abs(iceSpellVfxHeightMultiplier - 1f) > 0.0001f;
            if (tweakScale || tweakHeight)
            {
                Vector3 s = vfx.transform.localScale;
                if (tweakScale) s *= iceSpellVfxScale;
                if (tweakHeight) s.y *= iceSpellVfxHeightMultiplier;
                vfx.transform.localScale = s;

                FixUpIceVfxParticleScaling(vfx);
            }
            Destroy(vfx, Mathf.Max(0.1f, iceSpellVfxLifetime));
        }
    }

    private static void FixUpIceVfxParticleScaling(GameObject vfx)
    {
        ParticleSystem[] systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    private void ApplyIceSpellDamage(Vector3 center)
    {
        if (iceSpellDamage > 0f && iceSpellRadius > 0f)
        {
            Collider[] hits = Physics.OverlapSphere(center, iceSpellRadius, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c == null) continue;
                if (c.transform.IsChildOf(transform)) continue;
                Enemy enemy = c.GetComponentInParent<Enemy>();
                if (enemy != null)
                {
                    float dmg = ApplyDamageConditionals(iceSpellDamage, enemy);
                    enemy.TakeDamage(dmg, c.ClosestPoint(center), iceSpellKnockbackMultiplier);
                }
            }
        }
    }

    private Vector3 ResolveAimPoint(Camera cam, Vector3 spawnBase)
    {
        const float MaxAimDistance = 200f;
        if (cam == null) return spawnBase + transform.forward * MaxAimDistance;

        Vector3 camPos = cam.transform.position;
        Vector3 camForward = cam.transform.forward;
        RaycastHit[] hits = Physics.RaycastAll(camPos, camForward, MaxAimDistance, hitMask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                if (c.transform.IsChildOf(transform)) continue;
                return hits[i].point;
            }
        }
        return camPos + camForward * MaxAimDistance;
    }
}

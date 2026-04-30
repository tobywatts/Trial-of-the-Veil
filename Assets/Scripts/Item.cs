using UnityEngine;

public class Item : MonoBehaviour
{
    public enum Type
    {
        MaxHealth,
        DoubleJump,
        DamageVsHighHp,
        AttackSpeed,
        MoveSpeed,
        HealBuff,
        Multishot,
        CooldownReduction,
    }

    [Header("Pickup")]
    public float pickupRange = 1.6f;
    public Vector3 playerCenterOffset = new Vector3(0f, 1.0f, 0f);

    [Header("Buff Magnitudes")]
    [Tooltip("Fraction of current max health added, capped by statBoostFlatCap.")]
    [Range(0f, 1f)] public float maxHealthFraction = 0.15f;
    public float statBoostFlatCap = 100f;
    [Tooltip("Added to damageBonusVsHighHp. 0.15 = +15% damage vs enemies above 50% HP.")]
    public float damageBonusVsHighHpStep = 0.15f;
    [Tooltip("Multiplies attackSpeedMultiplier. 1.10 = +10% per pickup.")]
    public float attackSpeedStep = 1.10f;
    [Tooltip("Multiplies MoveSpeed and SprintSpeed.")]
    public float moveSpeedStep = 1.10f;
    [Tooltip("Multiplies HealAbility.healAmount.")]
    public float healBuffStep = 1.10f;
    public int multishotStep = 1;
    [Tooltip("Multiplies ice + heal cooldowns. 0.9 = -10%.")]
    public float cooldownReductionStep = 0.9f;

    [Header("Idle Motion")]
    public float bobAmplitude = 0.12f;
    public float bobSpeed = 2.5f;
    public float spinSpeed = 90f;

    private Type type;
    private Transform player;
    private Vector3 basePosition;
    private bool collected;

    private static readonly Color ColorMaxHealth = new Color(1f, 0.30f, 0.30f, 1f);
    private static readonly Color ColorDoubleJump = new Color(0.35f, 0.75f, 1f, 1f);
    private static readonly Color ColorDamageHighHp = new Color(1f, 0.55f, 0.15f, 1f);
    private static readonly Color ColorAttackSpeed = new Color(1f, 0.95f, 0.25f, 1f);
    private static readonly Color ColorMoveSpeed = new Color(0.40f, 0.95f, 0.45f, 1f);
    private static readonly Color ColorHealBuff = new Color(1f, 0.45f, 0.85f, 1f);
    private static readonly Color ColorMultishot = new Color(0.65f, 0.25f, 0.95f, 1f);
    private static readonly Color ColorCooldownRed = new Color(0.85f, 0.85f, 0.95f, 1f);

    public static Item Spawn(Vector3 position, Type itemType)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"Item ({itemType})";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.45f;
        Object.Destroy(go.GetComponent<SphereCollider>());

        UrpMaterialUpgrader.ApplySolidColor(go.GetComponent<Renderer>(), ColorFor(itemType), emissionStrength: 1.2f);

        var item = go.AddComponent<Item>();
        item.type = itemType;
        item.basePosition = position;
        return item;
    }

    [Tooltip("Chance a random item rolls Multishot. The rest is split equally across other types.")]
    public const float MultishotRollChance = 0.05f;

    public static Type RandomType()
    {
        if (Random.value < MultishotRollChance) return Type.Multishot;

        // Non-Multishot types share the remaining probability equally.
        var values = System.Enum.GetValues(typeof(Type));
        int count = values.Length;
        // Uniform index; if it lands on Multishot, remap to the next slot.
        int idx = Random.Range(0, count - 1);
        int multiIdx = (int)Type.Multishot;
        if (idx >= multiIdx) idx++;
        return (Type)idx;
    }

    private static Color ColorFor(Type t)
    {
        switch (t)
        {
            case Type.MaxHealth: return ColorMaxHealth;
            case Type.DoubleJump: return ColorDoubleJump;
            case Type.DamageVsHighHp: return ColorDamageHighHp;
            case Type.AttackSpeed: return ColorAttackSpeed;
            case Type.MoveSpeed: return ColorMoveSpeed;
            case Type.HealBuff: return ColorHealBuff;
            case Type.Multishot: return ColorMultishot;
            case Type.CooldownReduction: return ColorCooldownRed;
        }
        return Color.white;
    }

    private void Start()
    {
        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) player = ph.transform;
    }

    private void Update()
    {
        if (collected) return;
        if (GameManager.Instance != null && GameManager.Instance.GameOver) return;

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = basePosition + Vector3.up * bob;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        if (player == null) return;
        Vector3 playerCenter = player.position + playerCenterOffset;
        if ((playerCenter - transform.position).sqrMagnitude <= pickupRange * pickupRange)
            Collect();
    }

    private void Collect()
    {
        collected = true;
        SoundFx.PlayAt(SoundFx.ItemPickup, transform.position);
        if (player != null)
        {
            switch (type)
            {
                case Type.MaxHealth: ApplyMaxHealth(); break;
                case Type.DoubleJump: ApplyDoubleJump(); break;
                case Type.DamageVsHighHp: ApplyDamageVsHighHp(); break;
                case Type.AttackSpeed: ApplyAttackSpeed(); break;
                case Type.MoveSpeed: ApplyMoveSpeed(); break;
                case Type.HealBuff: ApplyHealBuff(); break;
                case Type.Multishot: ApplyMultishot(); break;
                case Type.CooldownReduction: ApplyCooldownReduction(); break;
            }
            var inv = player.GetComponent<PlayerInventory>();
            if (inv != null) inv.Add(type);
        }
        Destroy(gameObject);
    }

    public static string DisplayName(Type t)
    {
        switch (t)
        {
            case Type.MaxHealth: return "MaxHP";
            case Type.DoubleJump: return "DblJmp";
            case Type.DamageVsHighHp: return "Exec";
            case Type.AttackSpeed: return "ATKSpd";
            case Type.MoveSpeed: return "MvSpd";
            case Type.HealBuff: return "HealUp";
            case Type.Multishot: return "MShot";
            case Type.CooldownReduction: return "CDR";
        }
        return t.ToString();
    }

    public static string FullNameFor(Type t)
    {
        switch (t)
        {
            case Type.MaxHealth: return "Max Health";
            case Type.DoubleJump: return "Double Jump";
            case Type.DamageVsHighHp: return "Executioner";
            case Type.AttackSpeed: return "Attack Speed";
            case Type.MoveSpeed: return "Move Speed";
            case Type.HealBuff: return "Heal Up";
            case Type.Multishot: return "Multishot";
            case Type.CooldownReduction: return "Cooldown Reduction";
        }
        return t.ToString();
    }

    // Item-log copy. Numbers mirror the script-default magnitudes; keep them in sync.
    public static string DescriptionFor(Type t)
    {
        switch (t)
        {
            case Type.MaxHealth: return "Increases max HP by 15% (up to +100 flat) and instantly heals for the same amount.";
            case Type.DoubleJump: return "Grants one additional mid-air jump per pickup.";
            case Type.DamageVsHighHp: return "Deals +15% damage to enemies above 50% HP. Stacks additively.";
            case Type.AttackSpeed: return "Increases attack speed by 10% (multiplicative).";
            case Type.MoveSpeed: return "Increases walk and sprint speed by 10% (multiplicative).";
            case Type.HealBuff: return "Heal-ability potency +10% (multiplicative).";
            case Type.Multishot: return "Fires one additional fireball per cast.";
            case Type.CooldownReduction: return "Ice and heal cooldowns -10% (multiplicative).";
        }
        return "";
    }

    public static Color ColorForType(Type t) => ColorFor(t);

    private void ApplyMaxHealth()
    {
        var ph = player.GetComponent<PlayerHealth>();
        if (ph == null) return;
        float boost = Mathf.Min(ph.maxHealth * maxHealthFraction, statBoostFlatCap);
        ph.IncreaseMaxHealth(boost, healAmount: boost);
    }

    private void ApplyDoubleJump()
    {
        var tpc = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (tpc != null) tpc.ExtraJumps += 1;
    }

    private void ApplyDamageVsHighHp()
    {
        var pw = player.GetComponent<PlayerWeapon>();
        if (pw != null) pw.damageBonusVsHighHp += damageBonusVsHighHpStep;
    }

    private void ApplyAttackSpeed()
    {
        var pw = player.GetComponent<PlayerWeapon>();
        if (pw != null) pw.attackSpeedMultiplier *= attackSpeedStep;
    }

    private void ApplyMoveSpeed()
    {
        var tpc = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (tpc == null) return;
        tpc.MoveSpeed *= moveSpeedStep;
        tpc.SprintSpeed *= moveSpeedStep;
    }

    private void ApplyHealBuff()
    {
        var heal = player.GetComponent<HealAbility>();
        if (heal != null) heal.healPercent *= healBuffStep;
    }

    private void ApplyMultishot()
    {
        var pw = player.GetComponent<PlayerWeapon>();
        if (pw != null) pw.extraFireballs += multishotStep;
    }

    private void ApplyCooldownReduction()
    {
        var pw = player.GetComponent<PlayerWeapon>();
        if (pw != null) pw.iceSpellCooldown *= cooldownReductionStep;
        var heal = player.GetComponent<HealAbility>();
        if (heal != null) heal.cooldown *= cooldownReductionStep;
    }

}

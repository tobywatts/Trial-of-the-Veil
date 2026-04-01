using System.Collections.Generic;
using UnityEngine;
using StarterAssets;

// Carries the player's run state across a Portal scene transition. PlayerLoadout persists
// separately via PlayerPrefs, so it's not included here.
public static class PlayerRunState
{
    public static bool HasSnapshot { get; private set; }

    // GameManager
    public static float TimeElapsed;
    public static int CurrentStage;
    public static int CurrentStageKills;
    public static int BossesDefeated;
    public static GameManager.StagePhase Phase;

    // PlayerHealth
    public static float MaxHealth;
    public static float CurrentHealth;

    // PlayerLevel
    public static int Level;
    public static int CurrentXP;
    public static int XPToNextLevel;

    // PlayerCoins
    public static int Coins;

    // PlayerInventory
    public static readonly List<KeyValuePair<Item.Type, int>> Inventory = new List<KeyValuePair<Item.Type, int>>();

    // PlayerWeapon
    public static float WeaponDamage;
    public static float DamageBonusVsHighHp;
    public static float AttackSpeedMultiplier;
    public static int ExtraFireballs;
    public static float IceSpellCooldown;

    // HealAbility
    public static float HealPercent;
    public static float HealCooldown;

    // ThirdPersonController
    public static float MoveSpeed;
    public static float SprintSpeed;
    public static int ExtraJumps;

    // Spawn position override. Portal writes these to teleport the player to a specific spot in the next scene.
    public static bool HasSpawnPosition;
    public static Vector3 SpawnPosition;

    public static void Capture(GameManager gm, int stageAdvance)
    {
        if (gm == null) return;

        TimeElapsed = gm.TimeElapsed;
        CurrentStage = gm.CurrentStage + Mathf.Max(0, stageAdvance);
        CurrentStageKills = 0;
        BossesDefeated = gm.BossesDefeated;
        // The next level always starts in a fresh wave phase.
        Phase = GameManager.StagePhase.Waves;

        PlayerHealth ph = gm.playerHealth != null ? gm.playerHealth : Object.FindFirstObjectByType<PlayerHealth>();
        if (ph != null)
        {
            MaxHealth = ph.maxHealth;
            CurrentHealth = ph.currentHealth;
        }

        if (ph != null)
        {
            PlayerLevel lvl = ph.GetComponent<PlayerLevel>();
            if (lvl != null) { Level = lvl.CurrentLevel; CurrentXP = lvl.CurrentXP; XPToNextLevel = lvl.XPToNextLevel; }

            PlayerCoins coins = ph.GetComponent<PlayerCoins>();
            if (coins != null) Coins = coins.Coins;

            PlayerInventory inv = ph.GetComponent<PlayerInventory>();
            Inventory.Clear();
            if (inv != null)
            {
                foreach (var kv in inv.Counts) Inventory.Add(kv);
            }

            PlayerWeapon pw = ph.GetComponent<PlayerWeapon>();
            if (pw != null)
            {
                WeaponDamage = pw.damage;
                DamageBonusVsHighHp = pw.damageBonusVsHighHp;
                AttackSpeedMultiplier = pw.attackSpeedMultiplier;
                ExtraFireballs = pw.extraFireballs;
                IceSpellCooldown = pw.iceSpellCooldown;
            }

            HealAbility heal = ph.GetComponent<HealAbility>();
            if (heal != null)
            {
                HealPercent = heal.healPercent;
                HealCooldown = heal.cooldown;
            }

            ThirdPersonController tpc = ph.GetComponent<ThirdPersonController>();
            if (tpc != null)
            {
                MoveSpeed = tpc.MoveSpeed;
                SprintSpeed = tpc.SprintSpeed;
                ExtraJumps = tpc.ExtraJumps;
            }
        }

        HasSnapshot = true;
        Debug.Log($"Captured run state: stage={CurrentStage}, time={TimeElapsed:F1}s, level={Level}, coins={Coins}, items={Inventory.Count}");
    }

    public static void Apply(GameManager gm)
    {
        if (!HasSnapshot) return;
        if (gm == null) return;

        gm.RestoreState(TimeElapsed, CurrentStage, CurrentStageKills, BossesDefeated, Phase);

        PlayerHealth ph = gm.playerHealth != null ? gm.playerHealth : Object.FindFirstObjectByType<PlayerHealth>();
        if (ph != null)
        {
            // Set maxHealth first, then currentHealth; IncreaseMaxHealth would only ever raise it.
            ph.maxHealth = Mathf.Max(1f, MaxHealth);
            ph.currentHealth = Mathf.Clamp(CurrentHealth, 0f, ph.maxHealth);

            PlayerLevel lvl = ph.GetComponent<PlayerLevel>();
            if (lvl != null) lvl.RestoreState(Level, CurrentXP, XPToNextLevel);

            // Coins don't carry across portals; each level starts at zero.
            PlayerCoins coins = ph.GetComponent<PlayerCoins>();
            if (coins != null) coins.SetCoins(0);

            PlayerInventory inv = ph.GetComponent<PlayerInventory>();
            if (inv != null) inv.RestoreCounts(Inventory);

            PlayerWeapon pw = ph.GetComponent<PlayerWeapon>();
            if (pw != null)
            {
                pw.damage = WeaponDamage > 0f ? WeaponDamage : pw.damage;
                pw.damageBonusVsHighHp = DamageBonusVsHighHp;
                pw.attackSpeedMultiplier = AttackSpeedMultiplier > 0f ? AttackSpeedMultiplier : pw.attackSpeedMultiplier;
                pw.extraFireballs = ExtraFireballs;
                if (IceSpellCooldown > 0f) pw.iceSpellCooldown = IceSpellCooldown;
            }

            HealAbility heal = ph.GetComponent<HealAbility>();
            if (heal != null)
            {
                if (HealPercent > 0f) heal.healPercent = HealPercent;
                if (HealCooldown > 0f) heal.cooldown = HealCooldown;
            }

            ThirdPersonController tpc = ph.GetComponent<ThirdPersonController>();
            if (tpc != null)
            {
                if (MoveSpeed > 0f) tpc.MoveSpeed = MoveSpeed;
                if (SprintSpeed > 0f) tpc.SprintSpeed = SprintSpeed;
                tpc.ExtraJumps = ExtraJumps;
            }

            // Re-broadcast restored health so the HUD's listeners pick up the new values.
            ph.RefreshHealthUI();

            if (HasSpawnPosition)
            {
                // CharacterController ignores transform writes while enabled, so disable around the snap.
                CharacterController cc = ph.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                ph.transform.position = SpawnPosition;
                if (cc != null) cc.enabled = true;
            }
        }

        Debug.Log($"Applied run state: stage={CurrentStage}, time={TimeElapsed:F1}s");
        Clear();
    }

    public static void Clear()
    {
        HasSnapshot = false;
        HasSpawnPosition = false;
        Inventory.Clear();
    }
}

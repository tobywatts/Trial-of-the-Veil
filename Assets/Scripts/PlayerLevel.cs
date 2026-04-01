using System;
using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    [Header("XP Curve")]
    public int baseXpToNextLevel = 100;
    public int xpToNextLevelPerLevel = 50;

    [Header("Stat Gains Per Level")]
    public float maxHealthGainPerLevel = 15f;
    public float damageGainPerLevel = 2.5f;

    public int CurrentLevel { get; private set; } = 1;
    public int CurrentXP { get; private set; }
    public int XPToNextLevel { get; private set; }

    public event Action<int, int> OnXPChanged;
    public event Action<int> OnLevelUp;

    private PlayerHealth playerHealth;
    private PlayerWeapon playerWeapon;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerWeapon = GetComponent<PlayerWeapon>();
        XPToNextLevel = baseXpToNextLevel;
    }

    private void Start()
    {
        OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
    }

    public void GrantXP(int amount)
    {
        if (amount <= 0) return;
        CurrentXP += amount;
        while (CurrentXP >= XPToNextLevel)
        {
            CurrentXP -= XPToNextLevel;
            CurrentLevel++;
            XPToNextLevel = baseXpToNextLevel + (CurrentLevel - 1) * xpToNextLevelPerLevel;
            ApplyLevelUpBoosts();
            OnLevelUp?.Invoke(CurrentLevel);
        }
        OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
    }

    // Restores state across a scene transition. Doesn't re-apply per-level boosts; health/damage are restored directly.
    public void RestoreState(int level, int xp, int xpToNext)
    {
        CurrentLevel = Mathf.Max(1, level);
        CurrentXP = Mathf.Max(0, xp);
        XPToNextLevel = xpToNext > 0 ? xpToNext : baseXpToNextLevel + (CurrentLevel - 1) * xpToNextLevelPerLevel;
        OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
    }

    private void ApplyLevelUpBoosts()
    {
        if (playerHealth != null)
            playerHealth.IncreaseMaxHealth(maxHealthGainPerLevel, healAmount: maxHealthGainPerLevel);

        if (playerWeapon != null)
            playerWeapon.damage += damageGainPerLevel;
    }
}

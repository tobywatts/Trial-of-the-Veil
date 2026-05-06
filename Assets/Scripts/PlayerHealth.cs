using UnityEngine;
using System;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Passive Regeneration")]
    [Tooltip("Seconds without taking damage before regen starts.")]
    public float regenDelay = 5f;
    [Tooltip("Health per second once regen is active.")]
    public float regenRatePerSecond = 4f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnPlayerDied;

    private bool isDead;
    private float lastDamageTime = -999f;

    private void Awake()
    {
        if (CameraShake.Instance == null) gameObject.AddComponent<CameraShake>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Update()
    {
        if (isDead) return;
        if (currentHealth >= maxHealth) return;
        if (Time.time - lastDamageTime < regenDelay) return;

        float prev = currentHealth;
        currentHealth = Mathf.Min(currentHealth + regenRatePerSecond * Time.deltaTime, maxHealth);
        if (currentHealth != prev) OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        lastDamageTime = Time.time;
        if (CameraShake.Instance != null) CameraShake.Instance.ShakeOnHit();
        SoundFx.PlayAt(SoundFx.PlayerHurt, transform.position);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0f)
        {
            isDead = true;
            SoundFx.PlayAt(SoundFx.PlayerDeath, transform.position);
            OnPlayerDied?.Invoke();
        }
    }

    public void IncreaseMaxHealth(float amount, float healAmount = 0f)
    {
        if (amount <= 0f) return;
        maxHealth += amount;
        currentHealth = Mathf.Min(currentHealth + Mathf.Max(0f, healAmount), maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void RefreshHealthUI()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}

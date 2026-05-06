using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HealAbility : MonoBehaviour
{
    [Header("Heal (E / LB)")]
    [Tooltip("Fraction of max HP restored. 0.25 = 25%.")]
    public float healPercent = 0.25f;
    public float cooldown = 15f;
    [Tooltip("Spawned on the player when healing fires, destroyed after vfxLifetime.")]
    public GameObject healVfxPrefab;
    public float vfxLifetime = 2f;

    private PlayerHealth health;
    private float lastUseTime = -999f;

    public float CooldownRemaining => Mathf.Max(0f, cooldown - (Time.time - lastUseTime));
    public bool IsReady => CooldownRemaining <= 0f;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (!HealPressedThisFrame()) return;
        if (!IsReady) return;
        if (health == null || health.currentHealth >= health.maxHealth) return;

        health.Heal(health.maxHealth * healPercent);
        lastUseTime = Time.time;
        SoundFx.PlayAt(SoundFx.HealSpell, transform.position);

        if (healVfxPrefab != null)
        {
            GameObject vfx = Instantiate(healVfxPrefab, transform.position, Quaternion.identity, transform);
            Destroy(vfx, Mathf.Max(0.1f, vfxLifetime));
        }
    }

    private static bool HealPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

public class PlayerCombatStance : MonoBehaviour
{
    [Header("Health")]
    [Range(0f, 1f)] public float damagedHealthFraction = 0.25f;

    private Animator animator;
    private PlayerHealth playerHealth;
    private StarterAssetsInputs inputs;

    private static readonly int HashSprinting = Animator.StringToHash("Sprinting");
    private static readonly int HashLowHealth = Animator.StringToHash("LowHealth");
    private static readonly int HashDefending = Animator.StringToHash("Defending");

    private bool isDefending;
    public bool IsDefending => isDefending;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerHealth = GetComponent<PlayerHealth>();
        inputs = GetComponent<StarterAssetsInputs>();
    }

    private void Update()
    {
        if (animator == null) return;

        bool sprinting = inputs != null && inputs.sprint;

        bool lowHealth = false;
        if (playerHealth != null && playerHealth.maxHealth > 0f)
            lowHealth = (playerHealth.currentHealth / playerHealth.maxHealth) <= damagedHealthFraction;

        isDefending = ReadDefendInput();

        animator.SetBool(HashSprinting, sprinting);
        animator.SetBool(HashLowHealth, lowHealth);
        animator.SetBool(HashDefending, isDefending);
    }

    private static bool ReadDefendInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.isPressed) return true;
        if (Gamepad.current != null && Gamepad.current.leftTrigger.isPressed) return true;
        return false;
#else
        return Input.GetMouseButton(1);
#endif
    }
}

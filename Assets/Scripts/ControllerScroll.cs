using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Drives a ScrollRect from the right analog stick so menus stay navigable while paused
// (Time.timeScale == 0). Uses unscaled time.
public class ControllerScroll : MonoBehaviour
{
    public ScrollRect scrollRect;
    [Tooltip("Pixels per second at full stick deflection")]
    public float stickSpeed = 1600f;
    [Range(0f, 1f)] public float deadzone = 0.18f;

    private void Update()
    {
        if (scrollRect == null) return;
        float stickY = ReadStickY();
        if (Mathf.Abs(stickY) < deadzone) return;

        float contentH = scrollRect.content != null ? scrollRect.content.rect.height : 0f;
        float viewH = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
        float scrollable = contentH - viewH;
        if (scrollable <= 0.01f) return;

        float deltaPx = stickY * stickSpeed * Time.unscaledDeltaTime;
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
            scrollRect.verticalNormalizedPosition + deltaPx / scrollable);
    }

    private static float ReadStickY()
    {
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current == null) return 0f;
        return Gamepad.current.rightStick.ReadValue().y;
#else
        // Legacy axis; only works if "RightStickVertical" is configured in Input Manager.
        try { return Input.GetAxis("RightStickVertical"); }
        catch { return 0f; }
#endif
    }
}

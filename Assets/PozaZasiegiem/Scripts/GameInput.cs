using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Jedno miejsce na sterowanie. Działa z nowym Input System i ze starym Input Managerem.
/// </summary>
public static class GameInput
{
#if ENABLE_INPUT_SYSTEM
    static Keyboard K => Keyboard.current;
    static Mouse M => Mouse.current;
    static bool Held(Key k) => K != null && K[k].isPressed;
    static bool Down(Key k) => K != null && K[k].wasPressedThisFrame;

    public static Vector2 Move => new Vector2(
        (Held(Key.D) ? 1f : 0f) - (Held(Key.A) ? 1f : 0f),
        (Held(Key.W) ? 1f : 0f) - (Held(Key.S) ? 1f : 0f));
    public static Vector2 Look => M == null ? Vector2.zero : M.delta.ReadValue() * 0.1f;
    public static bool Run => Held(Key.LeftShift);
    public static bool Crouch => Held(Key.LeftCtrl) || Held(Key.C);
    public static bool InteractDown => Down(Key.E);
    public static bool FlashlightDown => Down(Key.F);
    public static bool TrackerHeld => Held(Key.Tab) || Held(Key.Q);
    public static bool FireDown => M != null && M.leftButton.wasPressedThisFrame;
    public static bool RestartDown => Down(Key.R);
    public static bool EscapeDown => Down(Key.Escape);
    public static bool DebugToggleDown => Down(Key.F1);
    public static bool HoldBreath => Held(Key.Space);
    public static bool CameraToggleDown => M != null && M.rightButton.wasPressedThisFrame;
    public static bool NightVisionDown => Down(Key.N);
    public static bool SkipDown => Down(Key.Space) || Down(Key.Enter);
    public static float ScrollY
    {
        get
        {
            if (M == null) return 0f;
            float y = M.scroll.ReadValue().y;
            if (Mathf.Abs(y) > 20f) y /= 120f;
            return Mathf.Clamp(y, -3f, 3f);
        }
    }
#else
    public static Vector2 Move => new Vector2(
        (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
        (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
    public static Vector2 Look => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
    public static bool Run => Input.GetKey(KeyCode.LeftShift);
    public static bool Crouch => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
    public static bool InteractDown => Input.GetKeyDown(KeyCode.E);
    public static bool FlashlightDown => Input.GetKeyDown(KeyCode.F);
    public static bool TrackerHeld => Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.Q);
    public static bool FireDown => Input.GetMouseButtonDown(0);
    public static bool RestartDown => Input.GetKeyDown(KeyCode.R);
    public static bool EscapeDown => Input.GetKeyDown(KeyCode.Escape);
    public static bool DebugToggleDown => Input.GetKeyDown(KeyCode.F1);
    public static bool HoldBreath => Input.GetKey(KeyCode.Space);
    public static bool CameraToggleDown => Input.GetMouseButtonDown(1);
    public static bool NightVisionDown => Input.GetKeyDown(KeyCode.N);
    public static bool SkipDown => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
    public static float ScrollY => Mathf.Clamp(Input.mouseScrollDelta.y, -3f, 3f);
#endif
}

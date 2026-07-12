using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace KoG.MiniMvp.InputUtil
{
    /// <summary>
    /// Input System helpers — ProjectSettings activeInputHandler=InputSystemOnly.
    /// Legacy UnityEngine.Input throws InvalidOperationException on device builds.
    /// </summary>
    public static class PointerInputUtil
    {
        public static bool TryGetScreenPosition(out Vector2 screenPos)
        {
            screenPos = default;
            var pointer = Pointer.current;
            if (pointer == null) return false;
            screenPos = pointer.position.ReadValue();
            return true;
        }

        public static bool WasPressedThisFrame()
        {
            var pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        public static bool WasReleasedThisFrame()
        {
            var pointer = Pointer.current;
            return pointer != null && pointer.press.wasReleasedThisFrame;
        }

        public static bool IsPressed()
        {
            var pointer = Pointer.current;
            return pointer != null && pointer.press.isPressed;
        }

        /// <summary>Finger-aware UI hit test (touch id when available).</summary>
        public static bool IsPointerOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var pointerId = -1;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                pointerId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            return es.IsPointerOverGameObject(pointerId);
        }
    }
}

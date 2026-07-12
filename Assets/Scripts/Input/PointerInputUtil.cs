using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KoG.MiniMvp.InputUtil
{
    /// <summary>
    /// Input System helpers — ProjectSettings activeInputHandler=InputSystemOnly.
    /// Legacy UnityEngine.Input throws InvalidOperationException on device builds.
    /// </summary>
    public static class PointerInputUtil
    {
        static readonly List<RaycastResult> RaycastScratch = new List<RaycastResult>(16);

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

        /// <summary>
        /// True only for Canvas UI (GraphicRaycaster). Ignores PhysicsRaycaster hits on buildings —
        /// otherwise EventSystem.IsPointerOverGameObject blocks all world drag.
        /// </summary>
        public static bool IsPointerOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (!TryGetScreenPosition(out var screenPos)) return false;

            var ped = new PointerEventData(es) { position = screenPos };
            RaycastScratch.Clear();
            es.RaycastAll(ped, RaycastScratch);
            for (var i = 0; i < RaycastScratch.Count; i++)
            {
                if (RaycastScratch[i].module is GraphicRaycaster)
                    return true;
            }

            return false;
        }
    }
}

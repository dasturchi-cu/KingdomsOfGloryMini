using KoG.MiniMvp.InputUtil;
using UnityEngine;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>
    /// Drives placement preview from touch/mouse raycasts onto the ground plane.
    /// Uses Input System only (matches ProjectSettings activeInputHandler=2).
    /// </summary>
    public sealed class PlacementInputDriver : MonoBehaviour
    {
        BuildingSystem _system;
        UnityEngine.Camera _cam;
        bool _dragging;

        public void Bind(BuildingSystem system, UnityEngine.Camera cam)
        {
            _system = system;
            _cam = cam;
        }

        void Update()
        {
            if (_system == null || !_system.IsPlacing || _system.IsBusy) return;
            if (_cam == null) _cam = UnityEngine.Camera.main;
            if (_cam == null) return;

            if (PointerInputUtil.IsPointerOverUi())
                return;

            if (PointerInputUtil.WasPressedThisFrame())
                _dragging = true;

            if (PointerInputUtil.WasReleasedThisFrame())
                _dragging = false;

            if (!_dragging && !PointerInputUtil.IsPressed()) return;
            if (!PointerInputUtil.TryGetScreenPosition(out var screenPos)) return;

            var ray = _cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var enter)) return;
            _system.MovePreviewToWorld(ray.GetPoint(enter));
        }
    }
}

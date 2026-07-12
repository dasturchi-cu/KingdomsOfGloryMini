using UnityEngine;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>
    /// Drives placement preview from touch/mouse raycasts onto the ground plane.
    /// Ignores UI hits when EventSystem reports them.
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

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (Input.GetMouseButtonDown(0))
                _dragging = true;

            if (Input.GetMouseButtonUp(0))
                _dragging = false;

            if (!_dragging && !Input.GetMouseButton(0)) return;

            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var enter)) return;
            _system.MovePreviewToWorld(ray.GetPoint(enter));
        }
    }
}

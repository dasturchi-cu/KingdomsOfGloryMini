using KoG.MiniMvp.App;
using KoG.MiniMvp.InputUtil;
using UnityEngine;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>
    /// Touch/mouse placement + relocate driver.
    /// Placement: finger follows in real time with grid snap (BuildingSystem + PlacePreviewFx).
    /// Relocate: press building → drag → release to confirm. Zero alloc on hot path.
    /// </summary>
    public sealed class PlacementInputDriver : MonoBehaviour
    {
        const float DragThresholdPx = 8f;
        const int RayBufferSize = 24;

        static readonly RaycastHit[] RayHits = new RaycastHit[RayBufferSize];

        BuildingSystem _system;
        UnityEngine.Camera _cam;
        System.Func<string> _selectedBuildingId;
        bool _placingDrag;
        bool _relocateArmed;
        string _pressBuildingId;
        Vector2 _pressScreen;

        /// <summary>Camera must not pan while placing, relocating, or press-held on a building.</summary>
        public bool BlocksCameraPan =>
            _placingDrag ||
            _relocateArmed ||
            !string.IsNullOrEmpty(_pressBuildingId) ||
            (_system != null && _system.BlocksCameraPan);

        public void Bind(BuildingSystem system, UnityEngine.Camera cam, System.Func<string> selectedBuildingId = null)
        {
            _system = system;
            _cam = cam;
            _selectedBuildingId = selectedBuildingId;
        }

        void Update()
        {
            if (_system == null || _system.IsBusy) return;
            if (_cam == null) _cam = UnityEngine.Camera.main;
            if (_cam == null) return;

            if (PointerInputUtil.IsPointerOverUi())
            {
                if (!_system.IsRelocating && !_placingDrag && string.IsNullOrEmpty(_pressBuildingId))
                    return;
            }

            if (_system.IsPlacing)
            {
                TickPlacementDrag();
                return;
            }

            if (_system.IsRelocating)
            {
                TickRelocateDrag();
                return;
            }

            TickBeginRelocate();
        }

        void TickPlacementDrag()
        {
            if (PointerInputUtil.WasPressedThisFrame())
            {
                if (PointerInputUtil.IsPointerOverUi()) return;
                _placingDrag = true;
            }

            if (PointerInputUtil.WasReleasedThisFrame())
                _placingDrag = false;

            if (!_placingDrag && !PointerInputUtil.IsPressed()) return;
            if (!PointerInputUtil.TryGetScreenPosition(out var screenPos)) return;
            if (PointerInputUtil.IsPointerOverUi() && !_placingDrag) return;
            if (!TryRayGround(screenPos, out var world)) return;
            _system.MovePreviewToWorld(world);
        }

        void TickBeginRelocate()
        {
            if (PointerInputUtil.WasPressedThisFrame())
            {
                _pressBuildingId = null;
                _relocateArmed = false;
                if (!PointerInputUtil.TryGetScreenPosition(out _pressScreen)) return;
                if (PointerInputUtil.IsPointerOverUi()) return;

                if (TryResolveRelocateTarget(_pressScreen, out var buildingId))
                    _pressBuildingId = buildingId;
            }

            if (string.IsNullOrEmpty(_pressBuildingId)) return;
            if (!PointerInputUtil.TryGetScreenPosition(out var screenPos)) return;

            if (!_relocateArmed && PointerInputUtil.IsPressed())
            {
                if ((screenPos - _pressScreen).sqrMagnitude >= DragThresholdPx * DragThresholdPx)
                {
                    if (_system.BeginRelocate(_pressBuildingId))
                    {
                        _relocateArmed = true;
                        BuildingClickRelay.SuppressClickFrames = Time.frameCount + 3;
                        if (TryRayGround(screenPos, out var world))
                            _system.MoveRelocateToWorld(world);
                    }
                    else
                    {
                        _pressBuildingId = null;
                    }
                }
            }

            if (PointerInputUtil.WasReleasedThisFrame())
            {
                _pressBuildingId = null;
                _relocateArmed = false;
            }
        }

        void TickRelocateDrag()
        {
            if (PointerInputUtil.TryGetScreenPosition(out var screenPos) &&
                PointerInputUtil.IsPressed() &&
                TryRayGround(screenPos, out var world))
            {
                _system.MoveRelocateToWorld(world);
            }

            if (PointerInputUtil.WasReleasedThisFrame())
            {
                BuildingClickRelay.SuppressClickFrames = Time.frameCount + 3;
                StartCoroutine(_system.ConfirmRelocate());
                _pressBuildingId = null;
                _relocateArmed = false;
            }
        }

        bool TryResolveRelocateTarget(Vector2 screenPos, out string buildingId)
        {
            buildingId = null;
            if (TryRayBuilding(screenPos, out var marker))
            {
                if (marker.buildingType == "castle") return false;
                buildingId = marker.buildingId;
                return !string.IsNullOrEmpty(buildingId);
            }

            var selectedId = _selectedBuildingId != null ? _selectedBuildingId() : null;
            if (string.IsNullOrEmpty(selectedId)) return false;
            if (!_system.TryGet(selectedId, out var inst)) return false;
            if (inst.Type == "castle") return false;

            var world = _system.AnchorToWorld(inst.Anchor);
            var screen = _cam.WorldToScreenPoint(world);
            if (screen.z < 0f) return false;
            if (((Vector2)screen - screenPos).sqrMagnitude > 120f * 120f) return false;
            buildingId = selectedId;
            return true;
        }

        bool TryRayGround(Vector2 screenPos, out Vector3 world)
        {
            world = default;
            var ray = _cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var enter)) return false;
            world = ray.GetPoint(enter);
            return true;
        }

        bool TryRayBuilding(Vector2 screenPos, out BuildingMarker marker)
        {
            marker = null;
            var ray = _cam.ScreenPointToRay(screenPos);
            var count = Physics.RaycastNonAlloc(ray, RayHits, 500f, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0) return false;

            var bestDist = float.MaxValue;
            BuildingMarker best = null;
            for (var i = 0; i < count; i++)
            {
                var hit = RayHits[i];
                var m = hit.collider.GetComponentInParent<BuildingMarker>();
                if (m == null || string.IsNullOrEmpty(m.buildingId)) continue;
                if (hit.distance >= bestDist) continue;
                bestDist = hit.distance;
                best = m;
            }

            marker = best;
            return marker != null;
        }
    }
}

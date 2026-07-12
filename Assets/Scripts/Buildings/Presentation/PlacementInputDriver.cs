using KoG.MiniMvp.App;
using KoG.MiniMvp.InputUtil;
using UnityEngine;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>
    /// Touch/mouse placement + relocate driver.
    /// Placement: finger follows in real time with grid snap (BuildingSystem + PlacePreviewFx).
    /// Relocate: press building → drag → release to confirm. Zero alloc on hot path.
    /// Empty ground tap clears selection via <see cref="OnEmptyTap"/>.
    /// </summary>
    public sealed class PlacementInputDriver : MonoBehaviour
    {
        const float DragThresholdPx = 8f;
        /// <summary>Lift aim point above fingertip so ghost is visible (CoC-style).</summary>
        const float FingerOffsetPx = 72f;
        const float TapMaxPx = 14f;
        const int RayBufferSize = 24;

        static readonly RaycastHit[] RayHits = new RaycastHit[RayBufferSize];

        BuildingSystem _system;
        UnityEngine.Camera _cam;
        System.Func<string> _selectedBuildingId;
        bool _placingDrag;
        bool _relocateArmed;
        string _pressBuildingId;
        Vector2 _pressScreen;
        bool _pressWasEmpty;
        bool _panLikely;
        float _pressTime;
        bool _longPressTriggered;

        /// <summary>Fired on short empty-ground tap (not a pan / not on a building).</summary>
        public System.Action OnEmptyTap;

        /// <summary>Camera must not pan while placing, relocating, or press-held on the selected building.</summary>
        public bool BlocksCameraPan
        {
            get
            {
                var selectedId = _selectedBuildingId != null ? _selectedBuildingId() : null;
                bool pressingSelected = !string.IsNullOrEmpty(_pressBuildingId) && _pressBuildingId == selectedId;

                return _placingDrag ||
                       _relocateArmed ||
                       pressingSelected ||
                       (_system != null && _system.BlocksCameraPan);
            }
        }

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
            TickEmptyTapDeselect();
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
            if (!TryRayGround(AimScreen(screenPos), out var world)) return;
            _system.MovePreviewToWorld(world);
        }

        void TriggerBuildingSelection(string buildingId)
        {
            if (TryRayBuilding(_pressScreen, out var marker) && marker.buildingId == buildingId)
            {
                var relay = marker.GetComponentInParent<BuildingClickRelay>();
                if (relay != null)
                {
                    relay.onClick?.Invoke();
                }
            }
        }

        float GetFingerOffsetPx()
        {
            // Scale offset dynamically based on screen resolution so it feels consistent physically
            return FingerOffsetPx * (Screen.height / 1080f);
        }

        Vector2 AimScreen(Vector2 finger)
        {
            // Offset toward screen top so the footprint sits above the fingertip.
            return new Vector2(finger.x, finger.y + GetFingerOffsetPx());
        }

        void TickBeginRelocate()
        {
            if (PointerInputUtil.WasPressedThisFrame())
            {
                _pressBuildingId = null;
                _relocateArmed = false;
                _pressWasEmpty = false;
                _panLikely = false;
                _pressTime = Time.unscaledTime;
                _longPressTriggered = false;
                if (!PointerInputUtil.TryGetScreenPosition(out _pressScreen)) return;
                if (PointerInputUtil.IsPointerOverUi()) return;

                if (TryResolveRelocateTarget(_pressScreen, out var buildingId))
                    _pressBuildingId = buildingId;
                else if (!TryRayBuilding(_pressScreen, out _))
                    _pressWasEmpty = true;
            }

            if (string.IsNullOrEmpty(_pressBuildingId)) return;
            if (!PointerInputUtil.TryGetScreenPosition(out var screenPos)) return;

            var selectedId = _selectedBuildingId != null ? _selectedBuildingId() : null;

            // Long press trigger for non-selected buildings
            if (_pressBuildingId != selectedId && !_longPressTriggered && !_relocateArmed)
            {
                if (PointerInputUtil.IsPressed())
                {
                    if (Time.unscaledTime - _pressTime >= 0.45f)
                    {
                        // Check if finger stayed within drag threshold
                        if ((screenPos - _pressScreen).sqrMagnitude < DragThresholdPx * DragThresholdPx)
                        {
                            _longPressTriggered = true;
                            TriggerBuildingSelection(_pressBuildingId);
                            
                            // Start relocation immediately
                            if (_system.BeginRelocate(_pressBuildingId))
                            {
                                _relocateArmed = true;
                                BuildingClickRelay.SuppressUntilTime = Time.unscaledTime + 0.12f;
                                if (TryRayGround(AimScreen(screenPos), out var world))
                                    _system.MoveRelocateToWorld(world);
                            }
                        }
                    }
                }
            }

            if (!_relocateArmed && PointerInputUtil.IsPressed())
            {
                if ((screenPos - _pressScreen).sqrMagnitude >= DragThresholdPx * DragThresholdPx)
                {
                    if (_pressBuildingId == selectedId)
                    {
                        // Selected building can be dragged immediately
                        if (_system.BeginRelocate(_pressBuildingId))
                        {
                            _relocateArmed = true;
                            BuildingClickRelay.SuppressUntilTime = Time.unscaledTime + 0.12f;
                            if (TryRayGround(AimScreen(screenPos), out var world))
                                _system.MoveRelocateToWorld(world);
                        }
                        else
                        {
                            _pressBuildingId = null;
                        }
                    }
                    else
                    {
                        // Dragging on non-selected building is ignored (lets camera pan)
                        _pressBuildingId = null;
                    }
                }
            }

            if (PointerInputUtil.WasReleasedThisFrame())
            {
                // Same-frame arm+release: confirm immediately so IsRelocating cannot soft-lock pan.
                if (_system.IsRelocating)
                {
                    BuildingClickRelay.SuppressUntilTime = Time.unscaledTime + 0.12f;
                    StartCoroutine(_system.ConfirmRelocate());
                }
                _pressBuildingId = null;
                _relocateArmed = false;
            }
        }

        void TickEmptyTapDeselect()
        {
            if (!_pressWasEmpty) return;
            if (!PointerInputUtil.TryGetScreenPosition(out var screenPos)) return;

            if (PointerInputUtil.IsPressed() &&
                (screenPos - _pressScreen).sqrMagnitude >= DragThresholdPx * DragThresholdPx)
                _panLikely = true;

            if (!PointerInputUtil.WasReleasedThisFrame()) return;

            var wasEmpty = _pressWasEmpty && !_panLikely;
            var shortTap = (screenPos - _pressScreen).sqrMagnitude <= TapMaxPx * TapMaxPx;
            _pressWasEmpty = false;
            _panLikely = false;
            if (!wasEmpty || !shortTap) return;
            if (PointerInputUtil.IsPointerOverUi()) return;
            OnEmptyTap?.Invoke();
        }

        void TickRelocateDrag()
        {
            if (PointerInputUtil.TryGetScreenPosition(out var screenPos) &&
                PointerInputUtil.IsPressed() &&
                TryRayGround(AimScreen(screenPos), out var world))
            {
                _system.MoveRelocateToWorld(world);
            }

            if (PointerInputUtil.WasReleasedThisFrame())
            {
                BuildingClickRelay.SuppressUntilTime = Time.unscaledTime + 0.12f;
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
                buildingId = marker.buildingId;
                return !string.IsNullOrEmpty(buildingId);
            }

            // Fallback: already-selected building (white ring) + press near it.
            var selectedId = _selectedBuildingId != null ? _selectedBuildingId() : null;
            if (string.IsNullOrEmpty(selectedId)) return false;
            if (!_system.TryGet(selectedId, out var inst)) return false;

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

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace KoG.MiniMvp.Camera
{
    /// <summary>
    /// Clash of Clans / Might &amp; Glory style orthographic base camera.
    /// Uses NEW Input System only (no UnityEngine.Input — avoids InvalidOperationException spam).
    /// </summary>
    public sealed class CoCCameraController : MonoBehaviour
    {
        const float PitchDeg = 52f;
        const float YawDeg = 45f;
        const float CamDistance = 44f;
        const float DragThresholdPx = 8f;
        const float MomentumDamping = 5.5f;
        const float PanSpeed = 0.024f;
        const float PinchZoomSpeed = 0.0045f;
        const float ScrollZoomSpeed = 1.35f;
        const float MinOrtho = 8f;
        const float MaxOrtho = 18f;
        const float UiBottomGuardPx = 190f;
        const float SoftClampStrength = 8f;
        const float EdgeRubber = 2.8f;
        const float FocusLerp = 7.5f;

        UnityEngine.Camera _cam;
        Vector3 _fieldCenter;
        Vector3 _focus;
        Vector3 _smoothTarget;
        bool _smoothing;
        float _halfExtent = 12f;
        float _boundsPadding = 2.5f;

        Vector2 _lastPointer;
        bool _dragging;
        bool _panArmed;
        Vector3 _velocity;
        int _activeFinger = -1;
        float _lastPinchDist = -1f;

        public Vector3 Focus => _focus;

        public float OrthoSize
        {
            get => _cam != null ? _cam.orthographicSize : 10f;
            set
            {
                if (_cam == null) return;
                _cam.orthographicSize = Mathf.Clamp(value, MinOrtho, MaxOrtho);
            }
        }

        void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            if (_cam == null) _cam = UnityEngine.Camera.main;
        }

        public void Configure(Vector3 fieldCenter, float fieldWorldSize, float boundsPadding = 2.5f)
        {
            _fieldCenter = new Vector3(fieldCenter.x, 0f, fieldCenter.z);
            _focus = _fieldCenter;
            _smoothTarget = _focus;
            _smoothing = false;
            _halfExtent = Mathf.Max(fieldWorldSize * 0.5f, 4f);
            _boundsPadding = boundsPadding;
            _velocity = Vector3.zero;
            EnsureCameraPose();
            ApplyTransform();
        }

        public void FocusBase(Vector3 focus, float orthoSize)
        {
            _focus = new Vector3(focus.x, 0f, focus.z);
            _smoothTarget = _focus;
            _smoothing = false;
            _velocity = Vector3.zero;
            OrthoSize = orthoSize;
            SoftClampFocus(hard: true);
            EnsureCameraPose();
            ApplyTransform();
        }

        public void FocusSmooth(Vector3 focus, float? orthoSize = null)
        {
            _smoothTarget = new Vector3(focus.x, 0f, focus.z);
            _smoothing = true;
            _velocity = Vector3.zero;
            if (orthoSize.HasValue) OrthoSize = orthoSize.Value;
        }

        void Update()
        {
            if (_cam == null) return;
            HandleTouch();
            HandleMouseEditor();
            ApplyMomentum();
            ApplySmoothFocus();
            SoftClampFocus(hard: false);
            ApplyTransform();
        }

        void EnsureCameraPose()
        {
            if (_cam == null) return;
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 200f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.52f, 0.74f, 0.92f);
            _cam.allowMSAA = true;
            _cam.transform.rotation = Quaternion.Euler(PitchDeg, YawDeg, 0f);
            OrthoSize = _cam.orthographicSize;
        }

        void ApplyTransform()
        {
            if (_cam == null) return;
            var rot = Quaternion.Euler(PitchDeg, YawDeg, 0f);
            _cam.transform.rotation = rot;
            _cam.transform.position = _focus - rot * Vector3.forward * CamDistance;
        }

        void SoftClampFocus(bool hard)
        {
            var lim = _halfExtent + _boundsPadding;
            var minX = _fieldCenter.x - lim;
            var maxX = _fieldCenter.x + lim;
            var minZ = _fieldCenter.z - lim;
            var maxZ = _fieldCenter.z + lim;

            if (hard)
            {
                _focus.x = Mathf.Clamp(_focus.x, minX, maxX);
                _focus.z = Mathf.Clamp(_focus.z, minZ, maxZ);
                _focus.y = 0f;
                return;
            }

            if (_focus.x < minX) _focus.x = Mathf.Lerp(_focus.x, minX, SoftClampStrength * Time.deltaTime);
            else if (_focus.x > maxX) _focus.x = Mathf.Lerp(_focus.x, maxX, SoftClampStrength * Time.deltaTime);
            if (_focus.z < minZ) _focus.z = Mathf.Lerp(_focus.z, minZ, SoftClampStrength * Time.deltaTime);
            else if (_focus.z > maxZ) _focus.z = Mathf.Lerp(_focus.z, maxZ, SoftClampStrength * Time.deltaTime);
            _focus.y = 0f;

            var hardLim = lim + EdgeRubber;
            _focus.x = Mathf.Clamp(_focus.x, _fieldCenter.x - hardLim, _fieldCenter.x + hardLim);
            _focus.z = Mathf.Clamp(_focus.z, _fieldCenter.z - hardLim, _fieldCenter.z + hardLim);
        }

        void ApplySmoothFocus()
        {
            if (!_smoothing) return;
            _focus = Vector3.Lerp(_focus, _smoothTarget, 1f - Mathf.Exp(-FocusLerp * Time.deltaTime));
            if ((_focus - _smoothTarget).sqrMagnitude < 0.0025f)
            {
                _focus = _smoothTarget;
                _smoothing = false;
            }
        }

        void HandleTouch()
        {
            var ts = Touchscreen.current;
            if (ts == null)
            {
                ResetTouchStateIfNeeded();
                return;
            }

            // Collect active touches (pressed this frame or held).
            TouchControl t0 = null;
            TouchControl t1 = null;
            int count = 0;
            foreach (var touch in ts.touches)
            {
                if (!touch.press.isPressed && !touch.press.wasPressedThisFrame)
                    continue;
                if (count == 0) t0 = touch;
                else if (count == 1) t1 = touch;
                count++;
                if (count >= 2) break;
            }

            if (count == 0)
            {
                ResetTouchStateIfNeeded();
                return;
            }

            if (count >= 2 && t0 != null && t1 != null)
            {
                var a = t0.position.ReadValue();
                var b = t1.position.ReadValue();
                var dist = Vector2.Distance(a, b);
                if (_lastPinchDist > 0f)
                {
                    OrthoSize -= (dist - _lastPinchDist) * PinchZoomSpeed;
                    _velocity = Vector3.zero;
                    _smoothing = false;
                }
                _lastPinchDist = dist;
                _dragging = false;
                _panArmed = false;
                return;
            }

            _lastPinchDist = -1f;
            if (t0 == null) return;

            var pos = t0.position.ReadValue();
            var fingerId = t0.touchId.ReadValue();
            if (IsOverUi(pos)) return;

            if (t0.press.wasPressedThisFrame)
            {
                _activeFinger = fingerId;
                _lastPointer = pos;
                _dragging = true;
                _panArmed = false;
                _velocity = Vector3.zero;
                _smoothing = false;
            }
            else if (fingerId == _activeFinger && t0.press.isPressed)
            {
                var delta = pos - _lastPointer;
                if (!_panArmed && delta.magnitude >= DragThresholdPx)
                    _panArmed = true;
                if (_panArmed)
                {
                    PanByScreenDelta(delta);
                    _lastPointer = pos;
                }
            }
            else if (fingerId == _activeFinger && t0.press.wasReleasedThisFrame)
            {
                _dragging = false;
                _panArmed = false;
                _activeFinger = -1;
            }
        }

        void ResetTouchStateIfNeeded()
        {
            if (_activeFinger < 0) return;
            _dragging = false;
            _panArmed = false;
            _activeFinger = -1;
            _lastPinchDist = -1f;
        }

        void HandleMouseEditor()
        {
            // Real touch active — skip mouse (avoids double-handling on some devices).
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.isPressed || touch.press.wasPressedThisFrame)
                        return;
                }
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            var scroll = mouse.scroll.ReadValue().y;
            // Input System scroll is often in pixels; normalize roughly to old mouseScrollDelta feel.
            if (Mathf.Abs(scroll) > 0.01f)
            {
                OrthoSize -= Mathf.Sign(scroll) * Mathf.Clamp(Mathf.Abs(scroll) * 0.01f, 0.1f, 3f) * ScrollZoomSpeed;
                _velocity = Vector3.zero;
                _smoothing = false;
            }

            var kb = Keyboard.current;
            bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
            bool lmb = mouse.leftButton.isPressed && !alt;
            bool panBtn = mouse.middleButton.isPressed || (mouse.leftButton.isPressed && alt);
            Vector2 mousePos = mouse.position.ReadValue();

            if (IsOverUi(mousePos) && !mouse.middleButton.isPressed) return;

            if (mouse.leftButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
            {
                _lastPointer = mousePos;
                _dragging = true;
                _panArmed = mouse.middleButton.wasPressedThisFrame;
                _velocity = Vector3.zero;
                _smoothing = false;
            }
            else if (_dragging && (lmb || panBtn || mouse.middleButton.isPressed))
            {
                var delta = mousePos - _lastPointer;
                if (!_panArmed && delta.magnitude >= DragThresholdPx)
                    _panArmed = true;
                if (_panArmed)
                {
                    PanByScreenDelta(delta);
                    _lastPointer = mousePos;
                }
            }
            else if (_dragging && !mouse.leftButton.isPressed && !mouse.middleButton.isPressed)
            {
                _dragging = false;
                _panArmed = false;
            }
        }

        void PanByScreenDelta(Vector2 screenDelta)
        {
            var rot = Quaternion.Euler(PitchDeg, YawDeg, 0f);
            var right = rot * Vector3.right;
            var forward = Vector3.ProjectOnPlane(rot * Vector3.up, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up).normalized;

            var zoomFactor = OrthoSize / 10f;
            var move = (-right * screenDelta.x - forward * screenDelta.y) * PanSpeed * zoomFactor;
            _focus += move;
            _velocity = move / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        void ApplyMomentum()
        {
            if (_dragging && _panArmed) return;
            if (_smoothing) return;
            if (_velocity.sqrMagnitude < 0.0001f)
            {
                _velocity = Vector3.zero;
                return;
            }

            _focus += _velocity * Time.deltaTime;
            _velocity = Vector3.Lerp(_velocity, Vector3.zero, MomentumDamping * Time.deltaTime);
        }

        static bool IsOverUi(Vector2 screenPos) => screenPos.y < UiBottomGuardPx;
    }
}

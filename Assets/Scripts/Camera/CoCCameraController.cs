using KoG.MiniMvp.InputUtil;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace KoG.MiniMvp.Camera
{
    /// <summary>
    /// Clash of Clans / Might &amp; Glory style orthographic base camera.
    /// Sole owner of Main Camera pose (pitch/yaw/distance/ortho). MiniMvpApp only calls Configure/FocusBase.
    /// Uses NEW Input System only (no UnityEngine.Input — avoids InvalidOperationException spam).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class CoCCameraController : MonoBehaviour
    {
        public const float PitchDeg = 52f;
        public const float YawDeg = 45f;
        public const float CamDistance = 44f;

        const float DragThresholdPx = 8f;
        const float MomentumDamping = 5.5f;
        const float PanSpeed = 0.024f;
        const float PinchZoomSpeed = 0.0045f;
        const float ScrollZoomSpeed = 1.35f;
        const float MinOrtho = 7f;
        const float MaxOrtho = 16f;
        const float DefaultOrtho = 10.5f;
        const float UiBottomGuardPx = 160f;
        const float SoftClampStrength = 8f;
        const float EdgeRubber = 2.8f;
        const float FocusLerp = 7.5f;

        UnityEngine.Camera _cam;
        Vector3 _fieldCenter;
        Vector3 _focus;
        Vector3 _smoothTarget;
        bool _smoothing;
        bool _configured;
        float _halfExtent = 12f;
        float _boundsPadding = 2.5f;

        Vector2 _lastPointer;
        bool _dragging;
        bool _panArmed;
        Vector3 _velocity;
        int _activeFinger = -1;
        float _lastPinchDist = -1f;

        float _punchOrtho;
        float _punchTime;
        float _punchDuration;
        float _punchAmount;
        Vector3 _punchOffset;
        float _orthoSize = DefaultOrtho;

        /// <summary>When true (placing / relocating building), skip pan — zoom still ok.</summary>
        public System.Func<bool> BlocksPan;

        public Vector3 Focus => _focus;
        public bool IsConfigured => _configured;

        public float OrthoSize
        {
            get => _orthoSize;
            set
            {
                _orthoSize = Mathf.Clamp(value, MinOrtho, MaxOrtho);
                if (_cam != null && _punchDuration <= 0f)
                    _cam.orthographicSize = _orthoSize;
            }
        }

        /// <summary>World position for an elevated CoC look-at <paramref name="focus"/>.</summary>
        public static Vector3 PosePosition(Vector3 focus)
        {
            var rot = Quaternion.Euler(PitchDeg, YawDeg, 0f);
            return focus - rot * Vector3.forward * CamDistance;
        }

        public static Quaternion PoseRotation() => Quaternion.Euler(PitchDeg, YawDeg, 0f);

        void Awake()
        {
            BindCamera();
            // Ortho + elevated rotation only — do NOT snap focus to (0,0,0) before MiniMvpApp.Configure.
            EnsureCameraPose(movePosition: false);
            if (!_configured)
                InferFocusFromCurrentPose();
        }

        void OnEnable()
        {
            BindCamera();
            EnsureCameraPose(movePosition: false);
            if (_configured)
                ApplyTransform();
        }

        public void Configure(Vector3 fieldCenter, float fieldWorldSize, float boundsPadding = 2.5f)
        {
            BindCamera();
            _fieldCenter = new Vector3(fieldCenter.x, 0f, fieldCenter.z);
            _focus = _fieldCenter;
            _smoothTarget = _focus;
            _smoothing = false;
            _halfExtent = Mathf.Max(fieldWorldSize * 0.5f, 4f);
            _boundsPadding = boundsPadding;
            _velocity = Vector3.zero;
            _configured = true;
            EnsureCameraPose(movePosition: true);
            ApplyTransform();
        }

        /// <summary>Ortho that fills the short screen axis with the square base (+margin).</summary>
        public static float FitOrthoForBase(float fieldWorldSize, float margin = 1.12f)
        {
            var half = Mathf.Max(fieldWorldSize * 0.5f, 4f) * margin;
            var aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            // ortho = half-height. Portrait needs larger ortho to fit width.
            var ortho = aspect < 1f ? half / Mathf.Max(aspect, 0.5f) : half;
            return Mathf.Clamp(ortho, MinOrtho, MaxOrtho);
        }

        public void FocusBase(Vector3 focus, float orthoSize)
        {
            BindCamera();
            _focus = new Vector3(focus.x, 0f, focus.z);
            _smoothTarget = _focus;
            _smoothing = false;
            _velocity = Vector3.zero;
            OrthoSize = orthoSize;
            SoftClampFocus(hard: true);
            _configured = true;
            EnsureCameraPose(movePosition: true);
            ApplyTransform();
        }

        public void FocusSmooth(Vector3 focus, float? orthoSize = null)
        {
            _smoothTarget = new Vector3(focus.x, 0f, focus.z);
            _smoothing = true;
            _velocity = Vector3.zero;
            if (orthoSize.HasValue) OrthoSize = orthoSize.Value;
        }

        /// <summary>Short ortho kick + lateral nudge for raid juice (presentation only).</summary>
        public void Punch(float orthoKick = 0.85f, float duration = 0.28f)
        {
            _punchAmount = Mathf.Clamp(orthoKick, 0.15f, 2.2f);
            _punchDuration = Mathf.Max(0.08f, duration);
            _punchTime = 0f;
            var yaw = PoseRotation() * Vector3.right;
            _punchOffset = yaw * (_punchAmount * 0.18f);
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            if (BlocksPan != null && BlocksPan())
            {
                // Drop any in-progress pan so building drag owns the pointer.
                _dragging = false;
                _panArmed = false;
                _activeFinger = -1;
                _velocity = Vector3.zero;
                HandleZoomOnly();
            }
            else
            {
                HandleTouch();
                HandleMouseEditor();
                ApplyMomentum();
            }

            ApplySmoothFocus();
            SoftClampFocus(hard: false);
            TickPunch();
            ApplyTransform();
        }

        void HandleZoomOnly()
        {
            var ts = Touchscreen.current;
            if (ts != null)
            {
                TouchControl t0 = null;
                TouchControl t1 = null;
                var count = 0;
                foreach (var touch in ts.touches)
                {
                    if (!touch.press.isPressed && !touch.press.wasPressedThisFrame)
                        continue;
                    if (count == 0) t0 = touch;
                    else if (count == 1) t1 = touch;
                    count++;
                    if (count >= 2) break;
                }

                if (count >= 2 && t0 != null && t1 != null)
                {
                    var a = t0.position.ReadValue();
                    var b = t1.position.ReadValue();
                    var dist = Vector2.Distance(a, b);
                    if (_lastPinchDist > 0f)
                        OrthoSize -= (dist - _lastPinchDist) * PinchZoomSpeed;
                    _lastPinchDist = dist;
                    return;
                }

                _lastPinchDist = -1f;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    OrthoSize -= scroll * 0.01f * ScrollZoomSpeed;
            }
        }

        void TickPunch()
        {
            if (_punchDuration <= 0f)
            {
                _punchOrtho = 0f;
                _punchOffset = Vector3.zero;
                return;
            }

            _punchTime += Time.deltaTime;
            var t = Mathf.Clamp01(_punchTime / _punchDuration);
            // Fast in, ease out.
            var envelope = 1f - t;
            envelope *= envelope;
            var kick = Mathf.Sin(t * Mathf.PI) * envelope;
            _punchOrtho = _punchAmount * kick;
            if (t >= 1f)
            {
                _punchDuration = 0f;
                _punchOrtho = 0f;
                _punchOffset = Vector3.zero;
            }
            else
            {
                var yaw = PoseRotation() * Vector3.right;
                _punchOffset = yaw * (_punchAmount * 0.22f * kick);
            }
        }

        void BindCamera()
        {
            if (_cam != null) return;
            _cam = GetComponent<UnityEngine.Camera>();
            if (_cam == null) _cam = UnityEngine.Camera.main;
        }

        /// <summary>
        /// Before Configure, keep the scene-authored elevated pose by recovering focus from
        /// position = focus - R * forward * distance.
        /// </summary>
        void InferFocusFromCurrentPose()
        {
            if (_cam == null) return;
            var rot = PoseRotation();
            _focus = _cam.transform.position + rot * Vector3.forward * CamDistance;
            _focus.y = 0f;
            _smoothTarget = _focus;
            _fieldCenter = _focus;
        }

        void EnsureCameraPose(bool movePosition)
        {
            if (_cam == null) return;
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 200f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            // Soft grass horizon — no empty blue letterbox feel on wide phones.
            _cam.backgroundColor = new Color(0.42f, 0.68f, 0.38f);
            _cam.allowMSAA = true;
            _cam.allowHDR = false;
            _cam.transform.rotation = PoseRotation();
            if (_cam.orthographicSize < MinOrtho || _cam.orthographicSize > MaxOrtho)
                OrthoSize = DefaultOrtho;
            else
                _orthoSize = Mathf.Clamp(_cam.orthographicSize, MinOrtho, MaxOrtho);
            if (movePosition)
                ApplyTransform();
        }

        void ApplyTransform()
        {
            if (_cam == null) return;
            var rot = PoseRotation();
            var focus = _focus + _punchOffset;
            // World-space pose — camera must stay a root (or under identity parent).
            _cam.transform.SetPositionAndRotation(PosePosition(focus), rot);
            _cam.orthographicSize = Mathf.Clamp(_orthoSize + _punchOrtho, MinOrtho, MaxOrtho + 1.5f);
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

            // Input System scroll is often large pixel deltas; ignore tiny noise that would auto-zoom.
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 2f)
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
            var rot = PoseRotation();
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

        static bool IsOverUi(Vector2 screenPos)
        {
            // Bottom HUD strip — never start a pan from here.
            if (screenPos.y < UiBottomGuardPx) return true;
            // Canvas UI only (not PhysicsRaycaster buildings).
            return PointerInputUtil.IsPointerOverUi();
        }
    }
}

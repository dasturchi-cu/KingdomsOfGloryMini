using UnityEngine;

namespace KoG.MiniMvp.Camera
{
    /// <summary>
    /// Clash of Clans / Might &amp; Glory style orthographic base camera.
    /// One-finger (or mouse) pan with momentum, pinch/scroll zoom, soft field bounds.
    /// </summary>
    public sealed class CoCCameraController : MonoBehaviour
    {
        const float PitchDeg = 55f;
        const float YawDeg = 45f;
        const float CamDistance = 42f;
        const float DragThresholdPx = 10f;
        const float MomentumDamping = 6.5f;
        const float PanSpeed = 0.022f;
        const float PinchZoomSpeed = 0.004f;
        const float ScrollZoomSpeed = 1.2f;
        const float MinOrtho = 6f;
        const float MaxOrtho = 18f;
        const float UiBottomGuardPx = 175f;

        UnityEngine.Camera _cam;
        Vector3 _fieldCenter;
        Vector3 _focus;
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

        /// <summary>Bind playable field for pan clamps and initial framing.</summary>
        public void Configure(Vector3 fieldCenter, float fieldWorldSize, float boundsPadding = 2.5f)
        {
            _fieldCenter = new Vector3(fieldCenter.x, 0f, fieldCenter.z);
            _focus = _fieldCenter;
            _halfExtent = Mathf.Max(fieldWorldSize * 0.5f, 4f);
            _boundsPadding = boundsPadding;
            _velocity = Vector3.zero;
            EnsureCameraPose();
            ApplyTransform();
        }

        /// <summary>Snap to a focus point (e.g. after base/castle load).</summary>
        public void FocusBase(Vector3 focus, float orthoSize)
        {
            _focus = new Vector3(focus.x, 0f, focus.z);
            _velocity = Vector3.zero;
            OrthoSize = orthoSize;
            ClampFocus();
            EnsureCameraPose();
            ApplyTransform();
        }

        void Update()
        {
            if (_cam == null) return;
            HandleTouch();
            HandleMouseEditor();
            ApplyMomentum();
            ClampFocus();
            ApplyTransform();
        }

        void EnsureCameraPose()
        {
            if (_cam == null) return;
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 200f;
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

        void ClampFocus()
        {
            var lim = _halfExtent + _boundsPadding;
            _focus.x = Mathf.Clamp(_focus.x, _fieldCenter.x - lim, _fieldCenter.x + lim);
            _focus.z = Mathf.Clamp(_focus.z, _fieldCenter.z - lim, _fieldCenter.z + lim);
            _focus.y = 0f;
        }

        void HandleTouch()
        {
            var count = Input.touchCount;
            if (count == 0)
            {
                if (_activeFinger >= 0)
                {
                    _dragging = false;
                    _panArmed = false;
                    _activeFinger = -1;
                    _lastPinchDist = -1f;
                }
                return;
            }

            if (count >= 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                var dist = Vector2.Distance(a.position, b.position);
                if (_lastPinchDist > 0f && (a.phase == TouchPhase.Moved || b.phase == TouchPhase.Moved))
                {
                    OrthoSize -= (dist - _lastPinchDist) * PinchZoomSpeed;
                    _velocity = Vector3.zero;
                }
                _lastPinchDist = dist;
                _dragging = false;
                _panArmed = false;
                return;
            }

            _lastPinchDist = -1f;
            var t = Input.GetTouch(0);
            if (IsOverUi(t.position)) return;

            if (t.phase == TouchPhase.Began)
            {
                _activeFinger = t.fingerId;
                _lastPointer = t.position;
                _dragging = true;
                _panArmed = false;
                _velocity = Vector3.zero;
            }
            else if (t.fingerId == _activeFinger && t.phase == TouchPhase.Moved)
            {
                var delta = t.position - _lastPointer;
                if (!_panArmed && delta.magnitude >= DragThresholdPx)
                    _panArmed = true;
                if (_panArmed)
                {
                    PanByScreenDelta(delta);
                    _lastPointer = t.position;
                }
            }
            else if (t.fingerId == _activeFinger && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
            {
                _dragging = false;
                _panArmed = false;
                _activeFinger = -1;
            }
        }

        void HandleMouseEditor()
        {
            if (Input.touchCount > 0) return;

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                OrthoSize -= scroll * ScrollZoomSpeed;
                _velocity = Vector3.zero;
            }

            if (IsOverUi(Input.mousePosition)) return;

            if (Input.GetMouseButtonDown(0))
            {
                _lastPointer = Input.mousePosition;
                _dragging = true;
                _panArmed = false;
                _velocity = Vector3.zero;
            }
            else if (_dragging && Input.GetMouseButton(0))
            {
                Vector2 pos = Input.mousePosition;
                var delta = pos - _lastPointer;
                if (!_panArmed && delta.magnitude >= DragThresholdPx)
                    _panArmed = true;
                if (_panArmed)
                {
                    PanByScreenDelta(delta);
                    _lastPointer = pos;
                }
            }
            else if (_dragging && Input.GetMouseButtonUp(0))
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
            ClampFocus();
        }

        void ApplyMomentum()
        {
            if (_dragging && _panArmed) return;
            if (_velocity.sqrMagnitude < 0.0001f)
            {
                _velocity = Vector3.zero;
                return;
            }

            _focus += _velocity * Time.deltaTime;
            _velocity = Vector3.Lerp(_velocity, Vector3.zero, MomentumDamping * Time.deltaTime);
            ClampFocus();
        }

        static bool IsOverUi(Vector2 screenPos) => screenPos.y < UiBottomGuardPx;
    }
}

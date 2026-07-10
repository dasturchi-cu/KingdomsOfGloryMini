using UnityEngine;

namespace KoG.MiniMvp.Camera
{
    /// <summary>
    /// Clash of Clans / Might &amp; Glory style orthographic base camera.
    /// Simulator-tuned: soft edge bounce, smooth focus, pinch/scroll zoom, momentum pan.
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
        const float SoftClampStrength = 10f;
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

        /// <summary>Smooth pan toward a building (selection / place) — simulator feel.</summary>
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

            // Elastic pull-back when past soft edge (CoC simulator feel).
            if (_focus.x < minX) _focus.x = Mathf.Lerp(_focus.x, minX, SoftClampStrength * Time.deltaTime);
            else if (_focus.x > maxX) _focus.x = Mathf.Lerp(_focus.x, maxX, SoftClampStrength * Time.deltaTime);
            if (_focus.z < minZ) _focus.z = Mathf.Lerp(_focus.z, minZ, SoftClampStrength * Time.deltaTime);
            else if (_focus.z > maxZ) _focus.z = Mathf.Lerp(_focus.z, maxZ, SoftClampStrength * Time.deltaTime);
            _focus.y = 0f;

            // Hard safety if dragged far.
            var hardLim = lim + 3.5f;
            _focus.x = Mathf.Clamp(_focus.x, _fieldCenter.x - hardLim, _fieldCenter.x + hardLim);
            _focus.z = Mathf.Clamp(_focus.z, _fieldCenter.z - hardLim, _fieldCenter.z + hardLim);
        }

        void ApplySmoothFocus()
        {
            if (!_smoothing) return;
            _focus = Vector3.Lerp(_focus, _smoothTarget, 1f - Mathf.Exp(-FocusLerp * Time.deltaTime));
            if (( _focus - _smoothTarget).sqrMagnitude < 0.0025f)
            {
                _focus = _smoothTarget;
                _smoothing = false;
            }
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
                    _smoothing = false;
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
                _smoothing = false;
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
                _smoothing = false;
            }

            // Middle-mouse or Alt+LMB also pans (editor simulator comfort).
            var panBtn = Input.GetMouseButton(2) || (Input.GetMouseButton(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)));
            var lmb = Input.GetMouseButton(0) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt);

            if (IsOverUi(Input.mousePosition) && !Input.GetMouseButton(2)) return;

            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2))
            {
                _lastPointer = Input.mousePosition;
                _dragging = true;
                _panArmed = Input.GetMouseButtonDown(2);
                _velocity = Vector3.zero;
                _smoothing = false;
            }
            else if (_dragging && (lmb || panBtn || Input.GetMouseButton(2)))
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
            else if (_dragging && !Input.GetMouseButton(0) && !Input.GetMouseButton(2))
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

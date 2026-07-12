using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// CoC-style placement preview: green/red footprint tiles + translucent ghost,
    /// smooth snap follow, reject pulse. Pooled — no per-frame allocations.
    /// </summary>
    public sealed class PlacePreviewFx : MonoBehaviour
    {
        const float FollowSmooth = 18f;
        const float PulseHz = 2.2f;
        const float GhostHeight = 1.65f;

        static PlacePreviewFx _instance;
        static Material _okMat;
        static Material _badMat;
        static Material _ghostOkMat;
        static Material _ghostBadMat;

        Transform _root;
        Transform _ghost;
        Renderer _ghostRend;
        readonly System.Collections.Generic.List<Renderer> _tiles =
            new System.Collections.Generic.List<Renderer>(8);

        Vector3 _targetPos;
        Vector3 _followVel;
        float _yaw;
        float _cellSize = 1f;
        int _width = 1;
        int _depth = 1;
        bool _canPlace = true;
        bool _visible;
        float _rejectUntil;
        float _rejectAmp;
        string _buildingType;

        public static void Show(Vector3 worldCellCenter, float cellSize, bool canPlace)
        {
            ShowFootprint(worldCellCenter, cellSize, 1, 1, 0f, canPlace, null);
        }

        public static void ShowFootprint(
            Vector3 anchorWorld,
            float cellSize,
            int width,
            int depth,
            float yawDegrees,
            bool canPlace)
        {
            ShowFootprint(anchorWorld, cellSize, width, depth, yawDegrees, canPlace, null);
        }

        public static void ShowFootprint(
            Vector3 anchorWorld,
            float cellSize,
            int width,
            int depth,
            float yawDegrees,
            bool canPlace,
            string buildingType)
        {
            Ensure();
            _instance.ShowInternal(
                anchorWorld,
                cellSize,
                Mathf.Max(1, width),
                Mathf.Max(1, depth),
                yawDegrees,
                canPlace,
                buildingType);
        }

        public static void Hide()
        {
            if (_instance != null) _instance.HideInternal();
        }

        /// <summary>Shake + flash when player confirms an invalid cell.</summary>
        public static void RejectPulse()
        {
            Ensure();
            _instance._rejectUntil = Time.unscaledTime + 0.35f;
            _instance._rejectAmp = 0.22f;
        }

        static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("PlacePreviewFx");
            _instance = go.AddComponent<PlacePreviewFx>();
            DontDestroyOnLoad(go);
        }

        void ShowInternal(
            Vector3 anchorWorld,
            float cellSize,
            int width,
            int depth,
            float yawDegrees,
            bool canPlace,
            string buildingType)
        {
            EnsureRoot();
            _cellSize = cellSize;
            _width = width;
            _depth = depth;
            _yaw = yawDegrees;
            _canPlace = canPlace;
            if (!string.IsNullOrEmpty(buildingType))
                _buildingType = buildingType;

            // Root stays on footprint anchor (corner); tiles/ghost use local offsets.
            _targetPos = new Vector3(anchorWorld.x, 0.04f, anchorWorld.z);

            if (!_visible)
            {
                _root.position = _targetPos;
                _followVel = Vector3.zero;
            }

            _root.rotation = Quaternion.Euler(0f, _yaw, 0f);
            LayoutTiles(canPlace);
            LayoutGhost(canPlace);
            _root.gameObject.SetActive(true);
            _visible = true;
        }

        void HideInternal()
        {
            if (_root != null) _root.gameObject.SetActive(false);
            _visible = false;
            _followVel = Vector3.zero;
            _rejectAmp = 0f;
            _rejectUntil = 0f;
        }

        void LateUpdate()
        {
            if (!_visible || _root == null) return;

            var pos = Vector3.SmoothDamp(
                _root.position,
                _targetPos,
                ref _followVel,
                1f / FollowSmooth,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (_rejectUntil > Time.unscaledTime)
            {
                var t = 1f - (_rejectUntil - Time.unscaledTime) / 0.35f;
                var shake = Mathf.Sin(t * Mathf.PI * 10f) * _rejectAmp * (1f - t);
                pos.x += shake;
            }
            else
            {
                _rejectAmp = 0f;
            }

            _root.position = pos;

            // Soft pulse — valid breathes, invalid flickers slightly.
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * PulseHz) *
                        (_canPlace ? 0.035f : 0.055f);
            if (_ghost != null)
                _ghost.localScale = new Vector3(
                    _width * _cellSize * 0.72f * pulse,
                    GhostHeight * pulse,
                    _depth * _cellSize * 0.72f * pulse);
        }

        void LayoutTiles(bool canPlace)
        {
            var needed = _width * _depth;
            while (_tiles.Count < needed)
                _tiles.Add(CreateTile());

            var mat = canPlace ? _okMat : _badMat;
            for (var i = 0; i < _tiles.Count; i++)
            {
                var on = i < needed;
                _tiles[i].gameObject.SetActive(on);
                if (!on) continue;

                var lx = i % _width;
                var lz = i / _width;
                _tiles[i].transform.localPosition = new Vector3(lx * _cellSize, 0f, lz * _cellSize);
                var s = _cellSize * 0.92f;
                _tiles[i].transform.localScale = new Vector3(s, s, 1f);
                _tiles[i].sharedMaterial = mat;
            }
        }

        void LayoutGhost(bool canPlace)
        {
            if (_ghost == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "PlaceGhost";
                go.transform.SetParent(_root, false);
                Object.Destroy(go.GetComponent<Collider>());
                _ghost = go.transform;
                _ghostRend = go.GetComponent<Renderer>();
                _ghostRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ghostRend.receiveShadows = false;
            }

            // Anchor is corner of footprint — ghost sits over footprint mid.
            _ghost.localPosition = new Vector3(
                (_width - 1) * _cellSize * 0.5f,
                GhostHeight * 0.5f,
                (_depth - 1) * _cellSize * 0.5f);
            _ghost.localScale = new Vector3(
                _width * _cellSize * 0.72f,
                GhostHeight,
                _depth * _cellSize * 0.72f);
            _ghostRend.sharedMaterial = canPlace ? _ghostOkMat : _ghostBadMat;
            TintGhostForType(_buildingType, canPlace);
            _ghost.gameObject.SetActive(true);
        }

        void TintGhostForType(string type, bool canPlace)
        {
            if (_ghostRend == null) return;
            var mat = canPlace ? _ghostOkMat : _ghostBadMat;
            if (mat == null) return;

            // Soft type cue on valid ghost only (invalid stays red).
            if (!canPlace || string.IsNullOrEmpty(type))
            {
                _ghostRend.sharedMaterial = mat;
                return;
            }

            Color c;
            if (type == "gold_mine") c = new Color(0.95f, 0.78f, 0.22f, 0.38f);
            else if (type == "barracks") c = new Color(0.35f, 0.55f, 0.95f, 0.38f);
            else c = new Color(0.55f, 0.95f, 0.65f, 0.35f);

            // Mutate instance once — ghost mats are private static clones.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            _ghostRend.sharedMaterial = mat;
        }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("PreviewRoot");
            go.transform.SetParent(transform, false);
            _root = go.transform;
            if (_okMat == null) _okMat = MakeMat(new Color(0.25f, 0.95f, 0.35f, 0.42f));
            if (_badMat == null) _badMat = MakeMat(new Color(0.95f, 0.22f, 0.18f, 0.48f));
            if (_ghostOkMat == null) _ghostOkMat = MakeMat(new Color(0.45f, 0.95f, 0.55f, 0.32f));
            if (_ghostBadMat == null) _ghostBadMat = MakeMat(new Color(0.95f, 0.28f, 0.22f, 0.40f));
        }

        Renderer CreateTile()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "PlaceTile";
            go.transform.SetParent(_root, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return rend;
        }

        static Material MakeMat(Color c)
        {
            var shader = UrpMaterialUtil.FindUnlitShader() ?? UrpMaterialUtil.FindLitShader();
            if (shader == null) return null;
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            SoftFieldLook.EnableAlphaPublic(m);
            m.enableInstancing = true;
            return m;
        }
    }
}

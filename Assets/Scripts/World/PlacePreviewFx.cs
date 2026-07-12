using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Soft green/red tile(s) shown while placing a building (CoC-style ghost cell).
    /// </summary>
    public sealed class PlacePreviewFx : MonoBehaviour
    {
        static PlacePreviewFx _instance;
        static Material _okMat;
        static Material _badMat;
        Transform _root;
        readonly System.Collections.Generic.List<Renderer> _tiles = new System.Collections.Generic.List<Renderer>(8);
        float _hideAt;
        float _yaw;

        public static void Show(Vector3 worldCellCenter, float cellSize, bool canPlace)
        {
            ShowFootprint(worldCellCenter, cellSize, 1, 1, 0f, canPlace);
        }

        public static void ShowFootprint(
            Vector3 anchorWorld,
            float cellSize,
            int width,
            int depth,
            float yawDegrees,
            bool canPlace)
        {
            Ensure();
            _instance.ShowInternal(anchorWorld, cellSize, Mathf.Max(1, width), Mathf.Max(1, depth), yawDegrees, canPlace);
        }

        public static void Hide()
        {
            if (_instance != null) _instance.HideInternal();
        }

        static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("PlacePreviewFx");
            _instance = go.AddComponent<PlacePreviewFx>();
            DontDestroyOnLoad(go);
        }

        void ShowInternal(Vector3 anchorWorld, float cellSize, int width, int depth, float yawDegrees, bool canPlace)
        {
            EnsureRoot();
            _yaw = yawDegrees;
            _root.position = new Vector3(anchorWorld.x, 0.04f, anchorWorld.z);
            _root.rotation = Quaternion.Euler(0f, _yaw, 0f);

            var needed = width * depth;
            while (_tiles.Count < needed)
                _tiles.Add(CreateTile());

            for (var i = 0; i < _tiles.Count; i++)
            {
                var on = i < needed;
                _tiles[i].gameObject.SetActive(on);
                if (!on) continue;

                var lx = i % width;
                var lz = i / width;
                var local = new Vector3(lx * cellSize, 0f, lz * cellSize);
                _tiles[i].transform.localPosition = local;
                var s = cellSize * 0.92f;
                _tiles[i].transform.localScale = new Vector3(s, s, 1f);
                _tiles[i].sharedMaterial = canPlace ? _okMat : _badMat;
            }

            _root.gameObject.SetActive(true);
            _hideAt = 0f; // sticky while placement session owns it
        }

        void HideInternal()
        {
            if (_root != null) _root.gameObject.SetActive(false);
            _hideAt = 0f;
        }

        void Update()
        {
            if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
                HideInternal();
        }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("PreviewRoot");
            go.transform.SetParent(transform, false);
            _root = go.transform;
            if (_okMat == null) _okMat = MakeMat(new Color(0.25f, 0.95f, 0.35f, 0.45f));
            if (_badMat == null) _badMat = MakeMat(new Color(0.95f, 0.22f, 0.18f, 0.45f));
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
            var shader = UrpMaterialUtil.FindUnlitShader();
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            SoftFieldLook.EnableAlphaPublic(m);
            m.enableInstancing = true;
            return m;
        }
    }
}

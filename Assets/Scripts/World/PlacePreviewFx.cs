using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Soft green/red tile shown while placing a building (CoC-style ghost cell).
    /// </summary>
    public sealed class PlacePreviewFx : MonoBehaviour
    {
        static PlacePreviewFx _instance;
        static Material _okMat;
        static Material _badMat;
        Transform _tile;
        Renderer _rend;
        float _hideAt;

        public static void Show(Vector3 worldCellCenter, float cellSize, bool canPlace)
        {
            Ensure();
            _instance.ShowInternal(worldCellCenter, cellSize, canPlace);
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

        void ShowInternal(Vector3 center, float cellSize, bool canPlace)
        {
            if (_tile == null) BuildTile();
            _tile.gameObject.SetActive(true);
            _tile.position = new Vector3(center.x, 0.04f, center.z);
            var s = cellSize * 0.92f;
            _tile.localScale = new Vector3(s, s, 1f);
            _rend.sharedMaterial = canPlace ? _okMat : _badMat;
            _hideAt = Time.unscaledTime + 1.35f;
        }

        void HideInternal()
        {
            if (_tile != null) _tile.gameObject.SetActive(false);
            _hideAt = 0f;
        }

        void Update()
        {
            if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
                HideInternal();
        }

        void BuildTile()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "PlaceTile";
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            _rend = go.GetComponent<Renderer>();
            _okMat = MakeMat(new Color(0.25f, 0.95f, 0.35f, 0.45f));
            _badMat = MakeMat(new Color(0.95f, 0.22f, 0.18f, 0.45f));
            _rend.sharedMaterial = _okMat;
            _rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rend.receiveShadows = false;
            _tile = go.transform;
            _tile.gameObject.SetActive(false);
        }

        static Material MakeMat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
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

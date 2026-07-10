using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// CoC-style selection ring under the active building (simulator / Play mode).
    /// </summary>
    public sealed class BuildingSelectFx : MonoBehaviour
    {
        static BuildingSelectFx _instance;
        static Material _ringMat;
        Transform _ring;
        float _radius = 1f;
        float _pulse;

        public static void Select(GameObject building)
        {
            Ensure();
            if (building == null)
            {
                _instance.Hide();
                return;
            }

            var pos = building.transform.position;
            var renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                var b = BuildingFitUtil.EncapsulateBounds(renderers);
                pos = new Vector3(b.center.x, 0.045f, b.center.z);
                _instance.Show(pos, Mathf.Max(b.size.x, b.size.z) * 0.58f + 0.2f);
            }
            else
            {
                _instance.Show(new Vector3(pos.x, 0.045f, pos.z), 0.9f);
            }
        }

        public static void Clear()
        {
            if (_instance != null) _instance.Hide();
        }

        static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("BuildingSelectFx");
            _instance = go.AddComponent<BuildingSelectFx>();
            DontDestroyOnLoad(go);
        }

        void Show(Vector3 worldPos, float radius)
        {
            if (_ring == null) BuildRing();
            enabled = true;
            _radius = Mathf.Max(radius, 0.5f);
            _ring.gameObject.SetActive(true);
            _ring.position = worldPos;
            ApplyScale(1f);
            _pulse = 0f;
        }

        void Hide()
        {
            if (_ring != null) _ring.gameObject.SetActive(false);
            enabled = false; // no Update cost when idle
        }

        void Update()
        {
            if (_ring == null || !_ring.gameObject.activeSelf) return;
            _pulse += Time.deltaTime * 3.5f;
            var breath = 1f + Mathf.Sin(_pulse) * 0.08f;
            ApplyScale(breath);
            if (_ringMat != null)
            {
                var glow = 0.75f + Mathf.Sin(_pulse) * 0.25f;
                var c = new Color(1f, 0.84f * glow, 0.12f, 1f);
                if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", c);
                if (_ringMat.HasProperty("_Color")) _ringMat.SetColor("_Color", c);
            }
        }

        void ApplyScale(float breath)
        {
            var d = _radius * 2f * breath;
            _ring.localScale = new Vector3(d, 0.035f, d);
        }

        void BuildRing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SelectRing";
            go.transform.SetParent(transform, false);
            Object.Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            if (_ringMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                _ringMat = new Material(shader);
                var c = new Color(1f, 0.82f, 0.12f, 1f);
                if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", c);
                if (_ringMat.HasProperty("_Color")) _ringMat.SetColor("_Color", c);
            }
            rend.sharedMaterial = _ringMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            _ring = go.transform;
            _ring.gameObject.SetActive(false);
        }
    }
}

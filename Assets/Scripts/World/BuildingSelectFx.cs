using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// CoC-style selection ring under the active building (simulator / Play mode).
    /// Pop-in + soft pulse + light Y-lift on the selected building; follows transform cheaply.
    /// </summary>
    public sealed class BuildingSelectFx : MonoBehaviour
    {
        const float LiftY = 0.12f;
        const float LiftSpeed = 14f;

        static BuildingSelectFx _instance;
        static Material _ringMat;
        Transform _ring;
        Transform _follow;
        float _radius = 1f;
        float _pulse;
        float _pop;
        float _baseY;
        float _liftT;
        bool _lifting;

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
                _instance.Show(building.transform, pos, Mathf.Max(b.size.x, b.size.z) * 0.58f + 0.2f);
            }
            else
            {
                _instance.Show(building.transform, new Vector3(pos.x, 0.045f, pos.z), 0.9f);
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

        void Show(Transform follow, Vector3 worldPos, float radius)
        {
            if (_ring == null) BuildRing();
            if (_ring == null) return;

            // Drop previous selection before attaching a new one.
            RestoreFollowY();

            enabled = true;
            _follow = follow;
            _baseY = follow != null ? follow.position.y : 0f;
            // If already slightly lifted from a prior select, prefer grounded base.
            if (_baseY > 0.05f && _baseY < LiftY * 2f)
                _baseY = 0f;
            _lifting = true;
            _liftT = 0f;
            _radius = Mathf.Max(radius, 0.5f);
            _ring.gameObject.SetActive(true);
            _ring.position = worldPos;
            _pop = 0f;
            _pulse = 0f;
            ApplyScale(0.72f);
        }

        void Hide()
        {
            RestoreFollowY();
            _follow = null;
            _lifting = false;
            if (_ring != null) _ring.gameObject.SetActive(false);
            enabled = false;
        }

        void RestoreFollowY()
        {
            if (_follow == null) return;
            var p = _follow.position;
            p.y = _baseY;
            _follow.position = p;
        }

        void Update()
        {
            if (_ring == null || !_ring.gameObject.activeSelf) return;

            if (_follow != null)
            {
                if (_lifting)
                {
                    _liftT = Mathf.Min(1f, _liftT + Time.unscaledDeltaTime * LiftSpeed);
                    var ease = 1f - (1f - _liftT) * (1f - _liftT);
                    var p = _follow.position;
                    p.y = Mathf.Lerp(_baseY, _baseY + LiftY, ease);
                    _follow.position = p;
                }

                var fp = _follow.position;
                _ring.position = new Vector3(fp.x, 0.045f, fp.z);
            }

            _pop = Mathf.Min(1f, _pop + Time.unscaledDeltaTime * 8f);
            var popEase = 1f - (1f - _pop) * (1f - _pop);
            _pulse += Time.unscaledDeltaTime * 3.8f;
            var breath = 1f + Mathf.Sin(_pulse) * 0.07f;
            ApplyScale(Mathf.Lerp(0.72f, 1f, popEase) * breath);

            if (_ringMat != null)
            {
                var glow = 0.78f + Mathf.Sin(_pulse) * 0.22f;
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
                var shader = UrpMaterialUtil.FindUnlitShader() ?? UrpMaterialUtil.FindLitShader();
                if (shader == null)
                {
                    Debug.LogWarning("[BuildingSelectFx] No shader — ring skipped");
                    return;
                }
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

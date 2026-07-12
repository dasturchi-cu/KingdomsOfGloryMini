using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Lightweight world-space feedback for simulator (place puff, +gold float).
    /// </summary>
    public sealed class WorldFeedback : MonoBehaviour
    {
        static WorldFeedback _instance;
        static Material _puffMat;
        static Material _textMat;

        public static void PlaceBurst(Vector3 worldPos)
        {
            Ensure();
            _instance.SpawnPuff(worldPos + Vector3.up * 0.2f);
        }

        /// <summary>Short bounce drop when a building lands after place confirm.</summary>
        public static void PlaceDrop(Transform building)
        {
            if (building == null) return;
            Ensure();
            _instance.StartCoroutine(_instance.AnimateDrop(building));
        }

        public static void FloatLabel(Vector3 worldPos, string text, Color color)
        {
            Ensure();
            _instance.SpawnFloat(worldPos + Vector3.up * 1.2f, text, color);
        }

        static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("WorldFeedback");
            _instance = go.AddComponent<WorldFeedback>();
            DontDestroyOnLoad(go);
        }

        void SpawnPuff(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "PlacePuff";
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.35f;
            Object.Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            if (_puffMat == null)
            {
                var shader = UrpMaterialUtil.FindUnlitShader() ?? UrpMaterialUtil.FindLitShader();
                if (shader == null)
                {
                    Debug.LogWarning("[WorldFeedback] No shader — puff skipped");
                    Object.Destroy(go);
                    return;
                }
                _puffMat = new Material(shader);
                var c = new Color(0.95f, 0.9f, 0.55f, 1f);
                if (_puffMat.HasProperty("_BaseColor")) _puffMat.SetColor("_BaseColor", c);
                if (_puffMat.HasProperty("_Color")) _puffMat.SetColor("_Color", c);
            }
            rend.sharedMaterial = _puffMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            StartCoroutine(AnimatePuff(go.transform));
        }

        void SpawnFloat(Vector3 pos, string text, Color color)
        {
            var go = new GameObject("FloatLabel");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;
            if (_textMat == null)
            {
                // TextMesh uses its own material; keep default.
            }
            StartCoroutine(AnimateFloat(go.transform, tm));
        }

        System.Collections.IEnumerator AnimateDrop(Transform building)
        {
            if (building == null) yield break;
            var start = building.localScale;
            if (start.sqrMagnitude < 0.0001f) start = Vector3.one;
            var t0 = 0f;
            const float life = 0.22f;
            while (t0 < life && building != null)
            {
                t0 += Time.deltaTime;
                var k = Mathf.Clamp01(t0 / life);
                var squash = 1f + Mathf.Sin(k * Mathf.PI) * 0.18f;
                var stretch = 1f - Mathf.Sin(k * Mathf.PI) * 0.12f;
                building.localScale = new Vector3(start.x * squash, start.y * stretch, start.z * squash);
                yield return null;
            }

            if (building != null) building.localScale = start;
            PlaceBurst(building != null ? building.position : Vector3.zero);
        }

        System.Collections.IEnumerator AnimatePuff(Transform t)
        {
            var life = 0.38f;
            var t0 = 0f;
            while (t0 < life && t != null)
            {
                t0 += Time.deltaTime;
                var k = t0 / life;
                t.localScale = Vector3.one * (0.35f + k * 1.4f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        System.Collections.IEnumerator AnimateFloat(Transform t, TextMesh tm)
        {
            var life = 0.95f;
            var t0 = 0f;
            var start = t.position;
            while (t0 < life && t != null)
            {
                t0 += Time.deltaTime;
                var k = t0 / life;
                t.position = start + Vector3.up * (k * 1.4f);
                var c = tm.color;
                c.a = 1f - k;
                tm.color = c;
                // Face camera.
                var cam = UnityEngine.Camera.main;
                if (cam != null)
                    t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }
}

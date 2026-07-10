using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// In-place nature quality polish. Never moves, respawns, or redesigns props —
    /// only materials, shadows, contact AO, and LOD on the existing hierarchy.
    /// </summary>
    public static class NatureVisualPolish
    {
        static Material _aoRingMat;
        static readonly Dictionary<string, Material> SharedByKey = new Dictionary<string, Material>(32);

        public static void Apply(GameObject natureBorder, float fieldWorldSize, Vector3 fieldCenter)
        {
            if (natureBorder == null) return;

            PolishExistingRenderers(natureBorder);
            AddContactAoRing(natureBorder.transform.parent != null
                ? natureBorder.transform.parent
                : natureBorder.transform, fieldWorldSize, fieldCenter);
            AddNatureDetailLod(natureBorder);
        }

        /// <summary>
        /// Shared-material palette + shadow flags. Positions / scales untouched.
        /// </summary>
        static void PolishExistingRenderers(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var n = r.gameObject.name.ToLowerInvariant();
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                var next = new Material[mats.Length];
                for (var i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) { next[i] = null; continue; }
                    next[i] = GetSharedPolished(src, n, i);
                }
                r.sharedMaterials = next;

                // Premium mobile read: soft shadows on volume, off on ground cover.
                var isGroundCover = n.Contains("grass") || n.Contains("flower") || n.Contains("plant");
                r.receiveShadows = true;
                r.shadowCastingMode = isGroundCover
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.On;
                r.allowOcclusionWhenDynamic = true;
                r.gameObject.isStatic = true;
            }
        }

        static Material GetSharedPolished(Material src, string objectName, int slot)
        {
            var category = Classify(objectName, src);
            var key = category + "|" + src.GetInstanceID() + "|" + slot;
            if (SharedByKey.TryGetValue(key, out var cached)) return cached;

            var m = new Material(src);
            m.name = src.name + "_KoG_" + category;
            var c = Color.white;
            if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) c = m.GetColor("_Color");

            // Color harmony — bright, stylized, readable. Same map, richer materials.
            switch (category)
            {
                case "grass":
                    c = Harmonize(c, new Color(0.40f, 0.70f, 0.30f), 0.45f);
                    break;
                case "canopy":
                    c = Harmonize(c, new Color(0.26f, 0.60f, 0.22f), 0.38f);
                    c = new Color(
                        Mathf.Clamp01(c.r * 0.96f),
                        Mathf.Clamp01(c.g * 1.08f),
                        Mathf.Clamp01(c.b * 0.92f),
                        1f);
                    break;
                case "autumn":
                    c = Harmonize(c, new Color(0.90f, 0.58f, 0.20f), 0.32f);
                    break;
                case "bush":
                    c = Harmonize(c, new Color(0.28f, 0.62f, 0.24f), 0.36f);
                    break;
                case "rock":
                    c = Harmonize(c, new Color(0.60f, 0.58f, 0.54f), 0.30f);
                    break;
                case "wood":
                    c = Harmonize(c, new Color(0.46f, 0.29f, 0.15f), 0.28f);
                    break;
                case "flower":
                    c.a = 1f;
                    if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = 2000;
                    break;
            }

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            UrpMaterialUtil.ApplyMobileSurface(m);
            // Slightly richer than flat plastic — still mobile-safe.
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", category == "rock" ? 0.14f : 0.07f);
            m.enableInstancing = true;

            SharedByKey[key] = m;
            return m;
        }

        static string Classify(string objectName, Material src)
        {
            var matName = (src != null ? src.name : string.Empty).ToLowerInvariant();
            if (objectName.Contains("autumn") || matName.Contains("autumn")) return "autumn";
            if (objectName.Contains("canopy") || matName.Contains("canopy") || matName.Contains("leaf")) return "canopy";
            if (objectName.Contains("bush") || matName.Contains("bush")) return "bush";
            if (objectName.Contains("rock") || matName.Contains("rock") || matName.Contains("stone")) return "rock";
            if (objectName.Contains("wood") || objectName.Contains("trunk") || objectName.Contains("bark") ||
                matName.Contains("wood") || matName.Contains("bark")) return "wood";
            if (objectName.Contains("flower") || matName.Contains("flower")) return "flower";
            if (objectName.Contains("grass") || matName.Contains("grass")) return "grass";
            // Heuristic from albedo if name is generic combined mesh.
            var c = Color.white;
            if (src != null)
            {
                if (src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
                else if (src.HasProperty("_Color")) c = src.GetColor("_Color");
            }
            if (c.r > c.g + 0.12f && c.g > 0.35f) return "autumn";
            if (c.g > c.r + 0.08f && c.g > c.b) return "canopy";
            if (Mathf.Abs(c.r - c.g) < 0.08f && c.g < 0.65f) return "rock";
            return "canopy";
        }

        static Color Harmonize(Color src, Color target, float amount)
        {
            var c = Color.Lerp(src, target, amount);
            // Lift value slightly for phone LCD readability — never muddy.
            var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (max < 0.35f)
            {
                var lift = 0.35f / Mathf.Max(max, 0.01f);
                c.r = Mathf.Clamp01(c.r * lift);
                c.g = Mathf.Clamp01(c.g * lift);
                c.b = Mathf.Clamp01(c.b * lift);
            }
            c.a = 1f;
            return c;
        }

        /// <summary>Soft contact AO under existing nature belt — grounds props without moving them.</summary>
        static void AddContactAoRing(Transform parent, float fieldWorldSize, Vector3 fieldCenter)
        {
            if (parent.Find("NatureContactAO") != null) return;

            if (_aoRingMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                _aoRingMat = new Material(shader);
                _aoRingMat.name = "NatureContactAO_Mat";
                var tex = CreateAoRingTexture(96);
                _aoRingMat.mainTexture = tex;
                if (_aoRingMat.HasProperty("_BaseMap")) _aoRingMat.SetTexture("_BaseMap", tex);
                if (_aoRingMat.HasProperty("_MainTex")) _aoRingMat.SetTexture("_MainTex", tex);
                if (_aoRingMat.HasProperty("_BaseColor")) _aoRingMat.SetColor("_BaseColor", Color.white);
                if (_aoRingMat.HasProperty("_Color")) _aoRingMat.SetColor("_Color", Color.white);
                SoftFieldLook.EnableAlphaPublic(_aoRingMat);
                _aoRingMat.renderQueue = 2900;
                _aoRingMat.enableInstancing = true;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "NatureContactAO";
            go.transform.SetParent(parent, false);
            go.transform.position = fieldCenter + new Vector3(0f, 0.018f, 0f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var s = fieldWorldSize * 1.30f;
            go.transform.localScale = new Vector3(s, s, 1f);
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _aoRingMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.isStatic = true;
        }

        static Texture2D CreateAoRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "NatureAORing";
            var cx = (size - 1) * 0.5f;
            var cy = (size - 1) * 0.5f;
            var inner = size * 0.355f;
            var outer = size * 0.485f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (d >= inner && d <= outer)
                    {
                        var t = Mathf.InverseLerp(inner, outer, d);
                        // Soft band — stronger mid for premium contact shadow feel.
                        a = Mathf.Sin(t * Mathf.PI) * 0.50f;
                    }
                    tex.SetPixel(x, y, new Color(0.06f, 0.10f, 0.05f, a));
                }
            }
            tex.Apply(false, true);
            return tex;
        }

        static void AddNatureDetailLod(GameObject border)
        {
            var lod = border.GetComponent<NatureDetailLod>();
            if (lod == null) lod = border.AddComponent<NatureDetailLod>();
            lod.Collect();
        }
    }

    /// <summary>Hides flower detail when zoomed out — saves overdraw. Does not move props.</summary>
    public sealed class NatureDetailLod : MonoBehaviour
    {
        Renderer[] _detail;
        bool _visible = true;
        float _nextCheck;

        public void Collect()
        {
            var list = new List<Renderer>(8);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("flower"))
                    list.Add(r);
            }
            _detail = list.ToArray();
        }

        void LateUpdate()
        {
            if (_detail == null || _detail.Length == 0) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.25f;

            var cam = UnityEngine.Camera.main;
            if (cam == null || !cam.orthographic) return;
            var want = cam.orthographicSize < 13.5f;
            if (want == _visible) return;
            _visible = want;
            for (var i = 0; i < _detail.Length; i++)
            {
                if (_detail[i] != null) _detail[i].enabled = want;
            }
        }
    }
}

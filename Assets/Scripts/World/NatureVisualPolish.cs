using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Second-pass nature polish: grass color match, warm canopy, contact AO ring, detail LOD.
    /// </summary>
    public static class NatureVisualPolish
    {
        static Material _blobMat;
        static Material _aoRingMat;

        public static void Apply(GameObject natureBorder, float fieldWorldSize, Vector3 fieldCenter)
        {
            if (natureBorder == null) return;

            TintNatureMaterials(natureBorder);
            AddContactAoRing(natureBorder.transform.parent != null
                ? natureBorder.transform.parent
                : natureBorder.transform, fieldWorldSize, fieldCenter);
            AddNatureDetailLod(natureBorder);
        }

        static void TintNatureMaterials(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var n = r.gameObject.name.ToLowerInvariant();
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                var next = new Material[mats.Length];
                for (var i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) { next[i] = null; continue; }
                    var m = new Material(src);
                    var c = Color.white;
                    if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
                    else if (m.HasProperty("_Color")) c = m.GetColor("_Color");

                    if (n.Contains("grass"))
                    {
                        // Match playable lime family (outer grass = same palette).
                        c = Color.Lerp(c, new Color(0.42f, 0.72f, 0.32f), 0.55f);
                    }
                    else if (n.Contains("canopy") || n.Contains("bush"))
                    {
                        // Warmer, richer green / autumn pop.
                        if (n.Contains("autumn"))
                            c = Color.Lerp(c, new Color(0.92f, 0.62f, 0.22f), 0.35f);
                        else
                            c = Color.Lerp(c, new Color(0.28f, 0.62f, 0.24f), 0.4f);
                    }
                    else if (n.Contains("rock"))
                    {
                        c = Color.Lerp(c, new Color(0.62f, 0.60f, 0.56f), 0.35f);
                    }
                    else if (n.Contains("wood"))
                    {
                        c = Color.Lerp(c, new Color(0.48f, 0.30f, 0.16f), 0.3f);
                    }
                    else if (n.Contains("flower"))
                    {
                        // Opaque flowers — less overdraw than transparent petals.
                        c.a = 1f;
                        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
                        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        m.SetOverrideTag("RenderType", "Opaque");
                        m.renderQueue = 2000;
                    }

                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                    UrpMaterialUtil.ApplyMobileSurface(m);
                    next[i] = m;
                }
                r.sharedMaterials = next;
                r.receiveShadows = true;
                if (n.Contains("grass") || n.Contains("flower"))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Soft dark ring just outside playable field — grounds the nature belt (contact AO).</summary>
        static void AddContactAoRing(Transform parent, float fieldWorldSize, Vector3 fieldCenter)
        {
            if (_aoRingMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                _aoRingMat = new Material(shader);
                _aoRingMat.name = "NatureContactAO_Mat";
                var tex = CreateAoRingTexture(64);
                _aoRingMat.mainTexture = tex;
                if (_aoRingMat.HasProperty("_BaseMap")) _aoRingMat.SetTexture("_BaseMap", tex);
                if (_aoRingMat.HasProperty("_MainTex")) _aoRingMat.SetTexture("_MainTex", tex);
                var col = new Color(1f, 1f, 1f, 1f);
                if (_aoRingMat.HasProperty("_BaseColor")) _aoRingMat.SetColor("_BaseColor", col);
                if (_aoRingMat.HasProperty("_Color")) _aoRingMat.SetColor("_Color", col);
                SoftFieldLook.EnableAlphaPublic(_aoRingMat);
                _aoRingMat.renderQueue = 2900;
                _aoRingMat.enableInstancing = true;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "NatureContactAO";
            go.transform.SetParent(parent, false);
            go.transform.position = fieldCenter + new Vector3(0f, 0.018f, 0f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var s = fieldWorldSize * 1.28f;
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
            var inner = size * 0.36f;
            var outer = size * 0.48f;
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
                        // Soft band peaking mid-ring.
                        a = Mathf.Sin(t * Mathf.PI) * 0.42f;
                    }
                    tex.SetPixel(x, y, new Color(0.08f, 0.12f, 0.06f, a));
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

    /// <summary>Hides flower/grass detail when zoomed out — saves overdraw on mid Android.</summary>
    public sealed class NatureDetailLod : MonoBehaviour
    {
        Renderer[] _detail;
        bool _visible = true;
        float _nextCheck;

        public void Collect()
        {
            var list = new System.Collections.Generic.List<Renderer>(8);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var n = r.gameObject.name.ToLowerInvariant();
                // Only flower detail — keep Grass mesh (outer floor) always on.
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

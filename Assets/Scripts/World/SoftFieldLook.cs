using UnityEngine;
using UnityEngine.Rendering;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// CoC-style lime checker on the playable BaseField.
    /// Never uses Unlit/Color (ignores textures → solid white).
    /// </summary>
    public static class SoftFieldLook
    {
        static Material _fieldMat;
        static Material _overlayMat;
        static int _cachedCells = -1;

        public static void Apply(Transform fieldRoot, int gridSize, float fieldWorldSize)
        {
            if (fieldRoot == null) return;
            EnsureMats(gridSize);

            // Paint authored / procedural CheckerGrass — this is the white diamond when broken.
            PaintUnderlay(fieldRoot);

            // Optional rim/checker overlay (textured transparent — not Unlit/Color).
            if (_overlayMat == null) return;
            var existing = fieldRoot.Find("SoftFieldOverlay");
            if (existing != null) Object.Destroy(existing.gameObject);

            var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "SoftFieldOverlay";
            overlay.transform.SetParent(fieldRoot, false);
            overlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            overlay.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            overlay.transform.localScale = new Vector3(fieldWorldSize, fieldWorldSize, 1f);
            Object.Destroy(overlay.GetComponent<Collider>());
            var or = overlay.GetComponent<Renderer>();
            or.sharedMaterial = _overlayMat;
            or.shadowCastingMode = ShadowCastingMode.Off;
            or.receiveShadows = false;
            overlay.isStatic = true;
        }

        public static void EnableAlphaPublic(Material m) => TryEnableAlpha(m);

        static void PaintUnderlay(Transform fieldRoot)
        {
            if (_fieldMat == null) return;
            foreach (var r in fieldRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var n = r.gameObject.name;
                // SoftFieldOverlay handled separately; horizon is outside BaseField.
                if (n == "SoftFieldOverlay" || n == "HorizonGrass" || n == "NatureContactAO")
                    continue;
                r.sharedMaterial = _fieldMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
        }

        static void EnsureMats(int cells)
        {
            if (_fieldMat != null && _cachedCells == cells) return;
            _cachedCells = cells;

            var opaqueTex = CreateCheckerTexture(cells, 12, opaque: true);
            var rimTex = CreateCombinedCheckerAndRim(cells, 12);

            var lit = UrpMaterialUtil.FindLitShader();
            var texShader = FindTexturedUnlit() ?? lit;
            if (texShader == null && lit == null)
            {
                Debug.LogWarning("[SoftFieldLook] No shader — cannot paint field");
                return;
            }

            // Opaque lime underlay — always visible even if overlay fails.
            _fieldMat = new Material(lit != null ? lit : texShader) { name = "KoG_FieldGrass" };
            BindTexture(_fieldMat, opaqueTex);
            SetColor(_fieldMat, Color.white);
            UrpMaterialUtil.ApplyMobileSurface(_fieldMat);
            _fieldMat.enableInstancing = true;

            if (texShader != null)
            {
                _overlayMat = new Material(texShader) { name = "KoG_SoftFieldOverlay" };
                BindTexture(_overlayMat, rimTex);
                SetColor(_overlayMat, Color.white);
                TryEnableAlpha(_overlayMat);
                _overlayMat.enableInstancing = true;
            }
            else
            {
                _overlayMat = null;
            }
        }

        /// <summary>Shaders that actually sample _MainTex / mainTexture (not Unlit/Color).</summary>
        static Shader FindTexturedUnlit()
        {
            var s = Shader.Find("Unlit/Transparent");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Texture");
            if (s == null) s = Shader.Find("UI/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            return s;
        }

        static void BindTexture(Material m, Texture2D tex)
        {
            if (m == null || tex == null) return;
            m.mainTexture = tex;
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        }

        static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static void TryEnableAlpha(Material m)
        {
            if (m == null) return;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }

        static Texture2D CreateCheckerTexture(int cells, int ppc, bool opaque)
        {
            var size = cells * ppc;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "KoG_FieldChecker_" + cells;

            byte a = opaque ? (byte)255 : (byte)220;
            var light = new Color32(148, 210, 88, a);
            var dark = new Color32(128, 188, 72, a);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x / ppc;
                    var cy = y / ppc;
                    tex.SetPixel(x, y, ((cx + cy) & 1) == 0 ? light : dark);
                }
            }

            tex.Apply(false, true);
            return tex;
        }

        static Texture2D CreateCombinedCheckerAndRim(int cells, int ppc)
        {
            var size = cells * ppc;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "SoftFieldCombined_" + cells;

            var a = new Color32(158, 220, 102, 90);
            var b = new Color32(142, 205, 92, 90);
            var rim = new Color32(72, 110, 52, 200);
            var rimInner = new Color32(88, 130, 60, 140);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x / ppc;
                    var cy = y / ppc;
                    var edge = cx == 0 || cy == 0 || cx == cells - 1 || cy == cells - 1;
                    var near = cx == 1 || cy == 1 || cx == cells - 2 || cy == cells - 2;
                    if (edge) tex.SetPixel(x, y, rim);
                    else if (near) tex.SetPixel(x, y, rimInner);
                    else tex.SetPixel(x, y, ((cx + cy) & 1) == 0 ? a : b);
                }
            }

            tex.Apply(false, true);
            return tex;
        }
    }
}

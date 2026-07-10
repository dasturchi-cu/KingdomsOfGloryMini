using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Soft CoC-style checker + dark playable rim in ONE transparent quad.
    /// Quality polish only — same layout identity (no new paths / rivers / redesign).
    /// </summary>
    public static class SoftFieldLook
    {
        static Material _combinedMat;
        static int _cachedCells = -1;

        public static void Apply(Transform fieldRoot, int gridSize, float fieldWorldSize)
        {
            if (fieldRoot == null) return;
            EnsureTextures(gridSize);

            var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "SoftFieldOverlay";
            overlay.transform.SetParent(fieldRoot, false);
            overlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            overlay.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            overlay.transform.localScale = new Vector3(fieldWorldSize, fieldWorldSize, 1f);
            Object.Destroy(overlay.GetComponent<Collider>());
            var or = overlay.GetComponent<Renderer>();
            or.sharedMaterial = _combinedMat;
            or.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            or.receiveShadows = false;
            overlay.isStatic = true;
        }

        public static void EnableAlphaPublic(Material m) => TryEnableAlpha(m);

        static void EnsureTextures(int cells)
        {
            if (_combinedMat != null && _cachedCells == cells) return;
            _cachedCells = cells;
            var tex = CreateCombinedCheckerAndRim(cells, 16);

            var shader = UrpMaterialUtil.FindUnlitShader();
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = UrpMaterialUtil.FindLitShader();

            _combinedMat = new Material(shader);
            _combinedMat.name = "SoftFieldCombined_Mat";
            _combinedMat.mainTexture = tex;
            if (_combinedMat.HasProperty("_BaseMap")) _combinedMat.SetTexture("_BaseMap", tex);
            if (_combinedMat.HasProperty("_MainTex")) _combinedMat.SetTexture("_MainTex", tex);
            var tint = Color.white;
            if (_combinedMat.HasProperty("_BaseColor")) _combinedMat.SetColor("_BaseColor", tint);
            if (_combinedMat.HasProperty("_Color")) _combinedMat.SetColor("_Color", tint);
            _combinedMat.enableInstancing = true;
            TryEnableAlpha(_combinedMat);
        }

        static void TryEnableAlpha(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }

        /// <summary>
        /// Same map identity: soft lime checker + dark rim.
        /// Polish: softer rim falloff + tiny grass micro-variation (no layout change).
        /// </summary>
        static Texture2D CreateCombinedCheckerAndRim(int cells, int ppc)
        {
            var size = cells * ppc;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "SoftFieldCombined_" + cells;

            // Bright, clean fantasy greens — never muddy.
            var a = new Color(0.620f, 0.863f, 0.400f, 0.48f);
            var b = new Color(0.557f, 0.804f, 0.361f, 0.48f);
            var rim = new Color(0.282f, 0.431f, 0.204f, 0.82f);
            var rimInner = new Color(0.345f, 0.510f, 0.235f, 0.58f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x / ppc;
                    var cy = y / ppc;
                    var fx = (x % ppc) / (float)ppc;
                    var fy = (y % ppc) / (float)ppc;

                    // Distance to nearest outer edge in cell units (smooth blend).
                    var edgeDist = Mathf.Min(Mathf.Min(cx, cells - 1 - cx), Mathf.Min(cy, cells - 1 - cy));
                    var edgeDistPx = edgeDist * ppc + Mathf.Min(Mathf.Min(x % ppc, ppc - 1 - (x % ppc)),
                        Mathf.Min(y % ppc, ppc - 1 - (y % ppc)));

                    Color c = ((cx + cy) & 1) == 0 ? a : b;

                    // Micro grass variation — same cells, richer read on phone.
                    var n = Hash01(x, y) * 0.04f - 0.02f;
                    c.r = Mathf.Clamp01(c.r + n * 0.5f);
                    c.g = Mathf.Clamp01(c.g + n);
                    c.b = Mathf.Clamp01(c.b + n * 0.3f);

                    // Soft cell-edge wear (terrain blending feel without new meshes).
                    var cellEdge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
                    if (cellEdge < 0.12f)
                    {
                        var wear = (1f - cellEdge / 0.12f) * 0.07f;
                        c = Color.Lerp(c, rimInner, wear);
                    }

                    // Soft rim falloff across ~1.5 cells — blends field into nature belt.
                    var rimW = 1f - Mathf.Clamp01(edgeDistPx / (ppc * 1.5f));
                    if (rimW > 0f)
                    {
                        rimW = rimW * rimW;
                        var rimCol = edgeDist <= 0 ? rim : rimInner;
                        c = Color.Lerp(c, rimCol, rimW * 0.92f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(false, true);
            return tex;
        }

        static float Hash01(int x, int y)
        {
            var n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}

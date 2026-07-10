using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Soft CoC-style checker + dark playable rim over BaseField.
    /// Visible grid lives in the grass texture — BuildingGrid stays invisible in Play.
    /// </summary>
    public static class SoftFieldLook
    {
        static Material _overlayMat;
        static Material _rimMat;
        static Texture2D _checkerTex;
        static int _cachedCells = -1;

        public static void Apply(Transform fieldRoot, int gridSize, float fieldWorldSize)
        {
            if (fieldRoot == null) return;
            EnsureTextures(gridSize);

            // Dark green rim first (under overlay) — only outer ring visible via alpha.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rim.name = "PlayableEdgeRim";
            rim.transform.SetParent(fieldRoot, false);
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rim.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            rim.transform.localScale = new Vector3(fieldWorldSize, fieldWorldSize, 1f);
            Object.Destroy(rim.GetComponent<Collider>());
            var rr = rim.GetComponent<Renderer>();
            rr.sharedMaterial = _rimMat;
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rr.receiveShadows = false;

            // Soft checker overlay — faint cells like the reference.
            var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "SoftCheckerOverlay";
            overlay.transform.SetParent(fieldRoot, false);
            overlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            overlay.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            overlay.transform.localScale = new Vector3(fieldWorldSize * 0.998f, fieldWorldSize * 0.998f, 1f);
            Object.Destroy(overlay.GetComponent<Collider>());
            var or = overlay.GetComponent<Renderer>();
            or.sharedMaterial = _overlayMat;
            or.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            or.receiveShadows = false;
        }

        static void EnsureTextures(int cells)
        {
            if (_overlayMat != null && _cachedCells == cells) return;
            _cachedCells = cells;
            _checkerTex = CreateSoftChecker(cells, 16);

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            _overlayMat = new Material(shader);
            _overlayMat.name = "SoftCheckerOverlay_Mat";
            _overlayMat.mainTexture = _checkerTex;
            if (_overlayMat.HasProperty("_BaseMap")) _overlayMat.SetTexture("_BaseMap", _checkerTex);
            if (_overlayMat.HasProperty("_MainTex")) _overlayMat.SetTexture("_MainTex", _checkerTex);
            var tint = new Color(1f, 1f, 1f, 0.5f);
            if (_overlayMat.HasProperty("_BaseColor")) _overlayMat.SetColor("_BaseColor", tint);
            if (_overlayMat.HasProperty("_Color")) _overlayMat.SetColor("_Color", tint);
            _overlayMat.enableInstancing = true;
            TryEnableAlpha(_overlayMat);

            _rimMat = BuildRimFrameMat(cells);
        }

        static Material BuildRimFrameMat(int cells)
        {
            var tex = CreateRimFrame(cells, 16);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader);
            m.name = "PlayableRim_Mat";
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            var c = new Color(1f, 1f, 1f, 0.85f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.renderQueue = 2990;
            m.enableInstancing = true;
            TryEnableAlpha(m);
            return m;
        }

        static void TryEnableAlpha(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }

        /// <summary>~10% contrast soft lime checker — reference kataklar.</summary>
        static Texture2D CreateSoftChecker(int cells, int ppc)
        {
            var size = cells * ppc;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "SoftChecker_" + cells;

            // Reference: very subtle — light lime vs slightly darker lime.
            var a = new Color32(158, 220, 102, 140);
            var b = new Color32(142, 205, 92, 140);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x / ppc;
                    var cy = y / ppc;
                    tex.SetPixel(x, y, ((cx + cy) & 1) == 0 ? a : b);
                }
            }

            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Only outer cell ring opaque dark green; center fully transparent.</summary>
        static Texture2D CreateRimFrame(int cells, int ppc)
        {
            var size = cells * ppc;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "PlayableRim_" + cells;

            var clear = new Color32(0, 0, 0, 0);
            var rim = new Color32(72, 110, 52, 200);
            var rimInner = new Color32(88, 130, 60, 120);

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
                    else tex.SetPixel(x, y, clear);
                }
            }

            tex.Apply(false, true);
            return tex;
        }
    }
}

using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Soft CoC-style checker + dark playable rim in ONE transparent quad (less overdraw).
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

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

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

        /// <summary>Soft checker in center + dark rim on outer 1–2 cells — single draw.</summary>
        static Texture2D CreateCombinedCheckerAndRim(int cells, int ppc)
        {
            var size = cells * ppc;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "SoftFieldCombined_" + cells;

            var a = new Color32(158, 220, 102, 128);
            var b = new Color32(142, 205, 92, 128);
            var rim = new Color32(72, 110, 52, 210);
            var rimInner = new Color32(88, 130, 60, 150);

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

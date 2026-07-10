using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Remaps Built-in Standard materials to URP Lit (pink-shader fix for imported FBX).
    /// Also applies mobile stylized polish: low smoothness, instancing.
    /// </summary>
    public static class UrpMaterialUtil
    {
        static Shader _urpLit;
        static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        public static void RemapToUrp(GameObject go)
        {
            if (_urpLit == null)
            {
                _urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (_urpLit == null) _urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (_urpLit == null) _urpLit = Shader.Find("Standard");
            }
            if (_urpLit == null) return;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;
                var next = new Material[shared.Length];
                var changed = false;
                for (var i = 0; i < shared.Length; i++)
                {
                    var src = shared[i];
                    if (src == null)
                    {
                        next[i] = null;
                        continue;
                    }

                    var alreadyUrp = src.shader != null && src.shader.name.Contains("Universal Render Pipeline");
                    if (alreadyUrp)
                    {
                        next[i] = src;
                        continue;
                    }

                    var key = src.GetInstanceID();
                    if (!Cache.TryGetValue(key, out var mapped))
                    {
                        var color = Color.white;
                        if (src.HasProperty("_BaseColor")) color = src.GetColor("_BaseColor");
                        else if (src.HasProperty("_Color")) color = src.GetColor("_Color");
                        mapped = new Material(_urpLit);
                        if (mapped.HasProperty("_BaseColor")) mapped.SetColor("_BaseColor", color);
                        if (mapped.HasProperty("_Color")) mapped.SetColor("_Color", color);
                        if (src.mainTexture != null)
                        {
                            mapped.mainTexture = src.mainTexture;
                            if (mapped.HasProperty("_BaseMap")) mapped.SetTexture("_BaseMap", src.mainTexture);
                        }
                        ApplyMobileSurface(mapped);
                        Cache[key] = mapped;
                    }
                    next[i] = mapped;
                    changed = true;
                }
                if (changed) r.sharedMaterials = next;
            }

            ForceMobileStylized(go);
        }

        /// <summary>Low gloss + GPU instancing — mutates shared mats via cache (no per-renderer clones).</summary>
        public static void ForceMobileStylized(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;
                var next = new Material[shared.Length];
                var changed = false;
                for (var i = 0; i < shared.Length; i++)
                {
                    var src = shared[i];
                    if (src == null) { next[i] = null; continue; }
                    var key = src.GetInstanceID() ^ 0x5A5A0000;
                    if (!Cache.TryGetValue(key, out var polished))
                    {
                        polished = new Material(src);
                        ApplyMobileSurface(polished);
                        Cache[key] = polished;
                    }
                    next[i] = polished;
                    if (!ReferenceEquals(polished, src)) changed = true;
                }
                if (changed) r.sharedMaterials = next;
            }
        }

        public static void ApplyMobileSurface(Material m)
        {
            if (m == null) return;
            m.enableInstancing = true;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
        }
    }
}

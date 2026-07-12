using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Material helper for Built-in Standard (Mini project RP).
    /// Prefer Standard / Unlit — URP shaders only as last resort for imported FBX leftovers.
    /// </summary>
    public static class UrpMaterialUtil
    {
        static Shader _lit;
        static Shader _unlit;
        static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        /// <summary>Built-in Lit first (Standard), then URP fallbacks.</summary>
        public static Shader FindLitShader()
        {
            if (_lit != null) return _lit;
            _lit = Shader.Find("Standard");
            if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Simple Lit");
            return _lit;
        }

        /// <summary>Built-in Unlit first, then URP Unlit.</summary>
        public static Shader FindUnlitShader()
        {
            if (_unlit != null) return _unlit;
            _unlit = Shader.Find("Unlit/Color");
            if (_unlit == null) _unlit = Shader.Find("Unlit/Transparent");
            if (_unlit == null) _unlit = Shader.Find("Sprites/Default");
            if (_unlit == null) _unlit = Shader.Find("Universal Render Pipeline/Unlit");
            return _unlit;
        }

        /// <summary>
        /// Remap foreign shaders onto the project lit shader (Standard on Built-in).
        /// Name kept for call-site compatibility.
        /// </summary>
        public static void RemapToUrp(GameObject go)
        {
            var lit = FindLitShader();
            if (lit == null || go == null) return;

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

                    var name = src.shader != null ? src.shader.name : "";
                    var isProjectLit = name == "Standard" || name.Contains("Universal Render Pipeline");
                    if (isProjectLit && name == "Standard")
                    {
                        next[i] = src;
                        continue;
                    }

                    // Remap URP / unknown → Standard for Built-in pipeline.
                    if (name == "Standard")
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
                        mapped = new Material(lit);
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

        /// <summary>Low gloss + GPU instancing — mutates via cache (no per-renderer .materials clones).</summary>
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

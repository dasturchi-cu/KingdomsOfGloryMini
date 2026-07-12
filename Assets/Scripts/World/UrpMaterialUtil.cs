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
        /// Remap foreign / missing shaders onto Built-in Standard.
        /// Null material slots (broken Soft-GO prefab bake) → colored Standard — no pink.
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
                        next[i] = MakeSolid(lit, GuessColor(r.name), "null_" + r.name);
                        changed = true;
                        continue;
                    }

                    var name = src.shader != null ? src.shader.name : "";
                    // Missing / error shader → solid Standard (pink fix).
                    if (string.IsNullOrEmpty(name)
                        || name.Contains("InternalError")
                        || name.Contains("Hidden/InternalError"))
                    {
                        next[i] = MakeSolid(lit, GuessColor(r.name), "err_" + r.name);
                        changed = true;
                        continue;
                    }

                    if (name == "Standard")
                    {
                        next[i] = src;
                        continue;
                    }

                    // Remap URP / unknown → Standard for Built-in pipeline.
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

        static Material MakeSolid(Shader lit, Color color, string cacheKey)
        {
            var key = cacheKey.GetHashCode();
            if (Cache.TryGetValue(key, out var existing) && existing != null)
                return existing;
            var m = new Material(lit) { name = cacheKey };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            ApplyMobileSurface(m);
            Cache[key] = m;
            return m;
        }

        /// <summary>Soft-GO part name → palette (matches BuildingVisualFactory).</summary>
        public static Color GuessColor(string partName)
        {
            if (string.IsNullOrEmpty(partName)) return new Color(0.62f, 0.60f, 0.56f);
            var n = partName.ToLowerInvariant();
            if (n.Contains("shadow")) return new Color(0.12f, 0.14f, 0.10f, 0.55f);
            if (n.Contains("roof") || n.Contains("ridge") || n.Contains("peak")) return new Color(0.12f, 0.42f, 0.78f);
            if (n.Contains("banner") || n.Contains("flag")) return new Color(0.82f, 0.12f, 0.12f);
            if (n.Contains("ore") || n.Contains("gold") || n.Contains("finial")) return new Color(0.95f, 0.78f, 0.18f);
            if (n.Contains("wood") || n.Contains("door") || n.Contains("beam") || n.Contains("cross")
                || n.Contains("crate") || n.Contains("pole") || n.Contains("trim"))
                return new Color(0.42f, 0.28f, 0.14f);
            if (n.Contains("window")) return new Color(0.35f, 0.55f, 0.75f);
            if (n.Contains("yard") || n.Contains("grass")) return new Color(0.35f, 0.55f, 0.28f);
            if (n.Contains("keep") || n.Contains("walls") || n.Contains("cream")) return new Color(0.92f, 0.88f, 0.78f);
            if (n.Contains("merlon") || n.Contains("boulder") || n.Contains("gate") || n.Contains("dark"))
                return new Color(0.42f, 0.40f, 0.38f);
            if (n.Contains("lvl")) return new Color(0.15f, 0.16f, 0.2f);
            return new Color(0.62f, 0.60f, 0.56f);
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

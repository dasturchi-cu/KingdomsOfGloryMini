using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Built-in Standard pipeline material helper.
    /// Keeps Polygon/FBX materials on Standard (no URP remap — project is Built-in).
    /// </summary>
    public static class UrpMaterialUtil
    {
        static Shader _standard;
        static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        /// <summary>Ensures renderers use Built-in Standard (fixes pink if mats were URP).</summary>
        public static void RemapToUrp(GameObject go)
        {
            if (_standard == null)
            {
                _standard = Shader.Find("Standard");
                if (_standard == null) _standard = Shader.Find("Mobile/Diffuse");
            }
            if (_standard == null || go == null) return;

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
                    var isBuiltIn = name == "Standard"
                                    || name.StartsWith("Mobile/")
                                    || name.StartsWith("Legacy Shaders/")
                                    || name == "Unlit/Color"
                                    || name == "Unlit/Texture";
                    if (isBuiltIn)
                    {
                        next[i] = src;
                        continue;
                    }

                    // URP / missing shader → remap to Standard
                    var key = src.GetInstanceID();
                    if (!Cache.TryGetValue(key, out var mapped))
                    {
                        var color = Color.white;
                        if (src.HasProperty("_Color")) color = src.GetColor("_Color");
                        else if (src.HasProperty("_BaseColor")) color = src.GetColor("_BaseColor");
                        mapped = new Material(_standard);
                        if (mapped.HasProperty("_Color")) mapped.SetColor("_Color", color);
                        if (src.mainTexture != null) mapped.mainTexture = src.mainTexture;
                        else if (src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null)
                            mapped.mainTexture = src.GetTexture("_BaseMap");
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

        public static void ForceMobileStylized(GameObject go)
        {
            if (go == null) return;
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
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        }

        /// <summary>Preferred Built-in shader for runtime-created mats.</summary>
        public static Shader FindLitShader()
        {
            return Shader.Find("Standard")
                   ?? Shader.Find("Mobile/Diffuse")
                   ?? Shader.Find("Diffuse");
        }

        public static Shader FindUnlitShader()
        {
            return Shader.Find("Unlit/Color")
                   ?? Shader.Find("Unlit/Texture")
                   ?? Shader.Find("Sprites/Default")
                   ?? FindLitShader();
        }
    }
}

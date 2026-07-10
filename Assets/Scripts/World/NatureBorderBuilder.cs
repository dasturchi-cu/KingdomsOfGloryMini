using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// CoC-style nature + outer world fill.
    /// Primary: solid URP procedural trees (never pink).
    /// Optional Nature2D sprites only with Sprites/Default (safe cutout).
    /// </summary>
    public static class NatureBorderBuilder
    {
        static Material _bark;
        static Material _canopyA;
        static Material _canopyB;
        static Material _canopyC;
        static Material _rock;
        static Material _bush;
        static Material _outerGrass;
        static Material _midGrass;
        static Material _shadowMat;
        static Mesh _quad;
        static readonly Dictionary<int, Material> SpriteMats = new Dictionary<int, Material>(8);

        static readonly string[] TreeNames = { "tree_large", "tree_medium" };
        static readonly string[] RockNames = { "rock_tall", "rock_small" };
        static readonly string[] BushNames = { "bush_patch" };
        static Texture2D[] _spriteTrees;
        static Texture2D[] _spriteRocks;
        static Texture2D[] _spriteBushes;

        public static void Build(Transform fieldRoot, float fieldWorldSize)
        {
            EnsureMats();
            BuildOuterWorld(fieldRoot, fieldWorldSize);
            BuildTreeRing(fieldRoot, fieldWorldSize);
        }

        /// <summary>Huge grass disc so portrait phone never shows empty blue void.</summary>
        static void BuildOuterWorld(Transform fieldRoot, float fieldWorldSize)
        {
            var half = fieldWorldSize * 0.5f;

            // Far outer — fills entire Game view on phone.
            var outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outer.name = "OuterWorld";
            outer.transform.SetParent(fieldRoot, false);
            Object.Destroy(outer.GetComponent<Collider>());
            outer.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            var outerR = Mathf.Max(half * 4.2f, 38f);
            outer.transform.localScale = new Vector3(outerR * 2f, 0.05f, outerR * 2f);
            var or = outer.GetComponent<Renderer>();
            or.sharedMaterial = _outerGrass;
            or.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            or.receiveShadows = true;

            // Mid ring — darker forest floor around the diamond.
            var mid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mid.name = "ForestFloor";
            mid.transform.SetParent(fieldRoot, false);
            Object.Destroy(mid.GetComponent<Collider>());
            mid.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            var midR = half * 1.85f;
            mid.transform.localScale = new Vector3(midR * 2f, 0.04f, midR * 2f);
            var mr = mid.GetComponent<Renderer>();
            mr.sharedMaterial = _midGrass;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        static void BuildTreeRing(Transform fieldRoot, float fieldWorldSize)
        {
            var border = new GameObject("NatureBorder");
            border.transform.SetParent(fieldRoot, false);

            var half = fieldWorldSize * 0.5f;
            var radius = half + 1.85f;
            var placed = new List<Vector3>(40);

            // Dense individual 3D trees — CoC perimeter.
            for (var i = 0; i < 18; i++)
            {
                var ang = (i / 18f) * Mathf.PI * 2f + 0.08f;
                var jitter = ((i * 17) % 5) * 0.22f - 0.3f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius + jitter), 0f, Mathf.Sin(ang) * (radius + jitter));
                if (!FarEnough(placed, pos, 1.95f)) continue;
                SpawnProcTree(border.transform, pos, 0.95f + (i % 5) * 0.1f, i);
                placed.Add(pos);
            }

            // Second outer ring — taller trees for depth.
            for (var i = 0; i < 12; i++)
            {
                var ang = (i / 12f) * Mathf.PI * 2f + 0.31f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius + 1.6f), 0f, Mathf.Sin(ang) * (radius + 1.6f));
                if (!FarEnough(placed, pos, 2.2f)) continue;
                SpawnProcTree(border.transform, pos, 1.15f + (i % 4) * 0.12f, i + 40);
                placed.Add(pos);
            }

            // Rocks / bushes between trees.
            for (var i = 0; i < 10; i++)
            {
                var ang = (i / 10f) * Mathf.PI * 2f + 0.5f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius - 0.35f), 0f, Mathf.Sin(ang) * (radius - 0.35f));
                if (!FarEnough(placed, pos, 1.3f)) continue;
                if (i % 2 == 0) SpawnProcRock(border.transform, pos, 0.55f + (i % 3) * 0.1f);
                else SpawnProcBush(border.transform, pos);
                placed.Add(pos);
            }

            // Optional painted sprites on top of a few slots (only if cutout works).
            TryScatterSprites(border.transform, placed, radius);

            Debug.Log("[MiniMvp] Nature border — " + border.transform.childCount + " props + outer world fill");
        }

        static void TryScatterSprites(Transform parent, List<Vector3> placed, float radius)
        {
            EnsureSprites();
            if (_spriteTrees == null || _spriteTrees.Length == 0) return;

            for (var i = 0; i < 6; i++)
            {
                var ang = (i / 6f) * Mathf.PI * 2f + 0.15f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius + 0.9f), 0f, Mathf.Sin(ang) * (radius + 0.9f));
                if (!FarEnough(placed, pos, 1.8f)) continue;
                var tex = _spriteTrees[i % _spriteTrees.Length];
                SpawnSafeSprite(parent, tex, pos, 2.6f + (i % 3) * 0.25f, i);
                placed.Add(pos);
            }
        }

        static void SpawnSafeSprite(Transform parent, Texture2D tex, Vector3 localPos, float height, int seed)
        {
            var root = new GameObject("Sprite_" + tex.name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            SpawnShadow(root.transform, height * 0.22f);

            var go = new GameObject("Billboard");
            go.transform.SetParent(root.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedQuad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SpriteMat(tex);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var aspect = tex.width / (float)Mathf.Max(tex.height, 1);
            var w = height * aspect;
            go.transform.localScale = new Vector3(w, height, 1f);
            go.transform.localPosition = new Vector3(0f, height * 0.48f, 0f);
            go.transform.localRotation = Quaternion.Euler(35f, 45f + ((seed % 7) - 3) * 4f, 0f);
        }

        static Material SpriteMat(Texture2D tex)
        {
            var id = tex.GetInstanceID();
            if (SpriteMats.TryGetValue(id, out var cached) && cached != null) return cached;

            // Sprites/Default never pinks out under URP.
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader);
            mat.mainTexture = tex;
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            mat.renderQueue = 3000;
            SpriteMats[id] = mat;
            return mat;
        }

        static void EnsureSprites()
        {
            if (_spriteTrees != null) return;
            _spriteTrees = LoadPunched(TreeNames);
            _spriteRocks = LoadPunched(RockNames);
            _spriteBushes = LoadPunched(BushNames);
        }

        static Texture2D[] LoadPunched(string[] names)
        {
            var list = new List<Texture2D>(names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var src = Resources.Load<Texture2D>("Environment/Nature2D/" + names[i]);
                if (src == null) continue;
                var punched = PunchCreamBackground(src);
                if (punched != null) list.Add(punched);
            }
            return list.ToArray();
        }

        static Texture2D PunchCreamBackground(Texture2D src)
        {
            Texture2D readable = src;
            if (!src.isReadable)
            {
                var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                readable = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                readable.Apply(false, false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            var w = readable.width;
            var h = readable.height;
            var pixels = readable.GetPixels32();
            var key = pixels[0];
            var minX = w;
            var minY = h;
            var maxX = 0;
            var maxY = 0;
            var kept = 0;

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    var p = pixels[i];
                    var dr = p.r - key.r;
                    var dg = p.g - key.g;
                    var db = p.b - key.b;
                    var bg = p.a < 16 || (dr * dr + dg * dg + db * db) < 55 * 55 || (p.r > 235 && p.g > 230 && p.b > 210);
                    if (bg)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                    }
                    else
                    {
                        kept++;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (kept < 64) return null;
            minX = Mathf.Max(0, minX - 1);
            minY = Mathf.Max(0, minY - 1);
            maxX = Mathf.Min(w - 1, maxX + 1);
            maxY = Mathf.Min(h - 1, maxY + 1);
            var cw = maxX - minX + 1;
            var ch = maxY - minY + 1;
            var cropped = new Color32[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = pixels[(minY + y) * w + (minX + x)];

            var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            outTex.name = src.name + "_cut";
            outTex.filterMode = FilterMode.Bilinear;
            outTex.SetPixels32(cropped);
            outTex.Apply(false, true);
            return outTex;
        }

        static Mesh SharedQuad()
        {
            if (_quad != null) return _quad;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            return _quad;
        }

        static bool FarEnough(List<Vector3> placed, Vector3 pos, float minDist)
        {
            for (var i = 0; i < placed.Count; i++)
            {
                var d = placed[i] - pos;
                d.y = 0f;
                if (d.sqrMagnitude < minDist * minDist) return false;
            }
            return true;
        }

        static void EnsureMats()
        {
            if (_bark != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            _bark = NewMat(shader, new Color(0.40f, 0.26f, 0.14f));
            _canopyA = NewMat(shader, new Color(0.18f, 0.48f, 0.18f));
            _canopyB = NewMat(shader, new Color(0.28f, 0.58f, 0.22f));
            _canopyC = NewMat(shader, new Color(0.22f, 0.52f, 0.28f));
            _rock = NewMat(shader, new Color(0.52f, 0.52f, 0.48f));
            _bush = NewMat(shader, new Color(0.26f, 0.55f, 0.22f));
            _outerGrass = NewMat(shader, new Color(0.28f, 0.48f, 0.22f));
            _midGrass = NewMat(shader, new Color(0.34f, 0.55f, 0.26f));
            _shadowMat = NewMat(Shader.Find("Universal Render Pipeline/Unlit") ?? shader, new Color(0.1f, 0.12f, 0.08f));
        }

        static Material NewMat(Shader shader, Color color)
        {
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            m.enableInstancing = true;
            return m;
        }

        static void SpawnProcTree(Transform parent, Vector3 localPos, float scale, int seed)
        {
            var tree = new GameObject("Tree_" + seed);
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = localPos;
            tree.transform.localRotation = Quaternion.Euler(0f, seed * 37.3f, 0f);

            SpawnShadow(tree.transform, 0.75f * scale);

            var trunk = Prim(tree.transform, PrimitiveType.Cylinder, "Trunk",
                new Vector3(0f, 0.72f * scale, 0f),
                new Vector3(0.24f, 0.72f, 0.24f) * scale, _bark);

            // 3 canopy clumps = one readable tree silhouette.
            var mats = new[] { _canopyA, _canopyB, _canopyC };
            Prim(tree.transform, PrimitiveType.Sphere, "Canopy0",
                new Vector3(0f, 1.7f * scale, 0f),
                new Vector3(1.4f, 1.2f, 1.4f) * scale, mats[seed % 3]);
            Prim(tree.transform, PrimitiveType.Sphere, "Canopy1",
                new Vector3(0.4f * scale, 1.95f * scale, 0.2f * scale),
                new Vector3(1.0f, 0.9f, 1.0f) * scale, mats[(seed + 1) % 3]);
            Prim(tree.transform, PrimitiveType.Sphere, "Canopy2",
                new Vector3(-0.35f * scale, 2.0f * scale, -0.25f * scale),
                new Vector3(0.9f, 0.8f, 0.9f) * scale, mats[(seed + 2) % 3]);
            _ = trunk;
        }

        static void SpawnProcRock(Transform parent, Vector3 localPos, float scale)
        {
            Prim(parent, PrimitiveType.Sphere, "Rock",
                localPos + Vector3.up * (0.22f * scale),
                new Vector3(1.15f, 0.5f, 0.95f) * scale, _rock);
        }

        static void SpawnProcBush(Transform parent, Vector3 localPos)
        {
            Prim(parent, PrimitiveType.Sphere, "Bush",
                localPos + Vector3.up * 0.3f,
                new Vector3(0.9f, 0.55f, 0.9f), _bush);
        }

        static void SpawnShadow(Transform parent, float radius)
        {
            Prim(parent, PrimitiveType.Cylinder, "Shadow",
                new Vector3(0f, 0.015f, 0f),
                new Vector3(radius * 2f, 0.015f, radius * 2f), _shadowMat, castShadows: false);
        }

        static GameObject Prim(Transform parent, PrimitiveType kind, string name, Vector3 localPos, Vector3 scale,
            Material mat, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(kind);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// CoC-style nature ring: each tree/bush/rock placed individually with spacing.
    /// Nature2D sprites get cream-background punched to alpha at load (art sheets are opaque).
    /// </summary>
    public static class NatureBorderBuilder
    {
        static readonly string[] TreeNames = { "tree_large", "tree_medium" };
        static readonly string[] RockNames = { "rock_tall", "rock_small" };
        static readonly string[] StumpNames = { "stump_hollow" };
        static readonly string[] BushNames = { "bush_patch" };

        static Texture2D[] _trees;
        static Texture2D[] _rocks;
        static Texture2D[] _stumps;
        static Texture2D[] _bushes;
        static Mesh _quad;
        static readonly Dictionary<int, Material> Mats = new Dictionary<int, Material>(16);
        static Material _shadowMat;

        // Procedural fallback mats.
        static Material _bark;
        static Material _canopy;
        static Material _canopyB;
        static Material _rock;
        static Material _bush;

        public static void Build(Transform fieldRoot, float fieldWorldSize)
        {
            if (TryBuildNature2D(fieldRoot, fieldWorldSize))
                return;

            BuildProcedural(fieldRoot, fieldWorldSize);
            Debug.Log("[MiniMvp] Procedural nature border (Nature2D missing)");
        }

        static bool TryBuildNature2D(Transform fieldRoot, float fieldWorldSize)
        {
            EnsureTextures();
            if (_trees == null || _trees.Length == 0) return false;

            var border = new GameObject("NatureBorder");
            border.transform.SetParent(fieldRoot, false);
            border.transform.localPosition = Vector3.zero;

            var half = fieldWorldSize * 0.5f;
            var inner = half + 1.35f;
            var outer = half + 3.1f;
            var placed = new List<Vector3>(48);

            // Outer tree ring — mostly trees, one-by-one with min spacing.
            PlaceRing(border.transform, placed, count: 14, radius: outer, kindBias: PropKind.Tree, seed: 11);
            // Inner mixed props — bushes/rocks/stumps between trees.
            PlaceRing(border.transform, placed, count: 12, radius: inner + 0.35f, kindBias: PropKind.Mixed, seed: 29);
            // Corner hero trees (CoC landmark feel).
            TryPlace(border.transform, placed, new Vector3(0f, 0f, outer + 0.4f), PropKind.Tree, 3.2f, 3);
            TryPlace(border.transform, placed, new Vector3(outer + 0.25f, 0f, 0f), PropKind.Tree, 2.7f, 5);
            TryPlace(border.transform, placed, new Vector3(-outer - 0.2f, 0f, 0.2f), PropKind.Tree, 2.9f, 7);
            TryPlace(border.transform, placed, new Vector3(0f, 0f, -outer - 0.15f), PropKind.Bush, 1.3f, 9);

            Debug.Log("[MiniMvp] Nature2D border — " + border.transform.childCount + " individual props");
            return border.transform.childCount > 0;
        }

        enum PropKind { Tree, Rock, Stump, Bush, Mixed }

        static void PlaceRing(Transform parent, List<Vector3> placed, int count, float radius, PropKind kindBias, int seed)
        {
            for (var i = 0; i < count; i++)
            {
                var ang = (i / (float)count) * Mathf.PI * 2f + (seed % 7) * 0.07f;
                var jitterR = ((i * 17 + seed) % 5) * 0.18f - 0.25f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius + jitterR), 0f, Mathf.Sin(ang) * (radius + jitterR));
                var kind = kindBias == PropKind.Mixed ? PickMixed(i + seed) : kindBias;
                var h = HeightFor(kind, i + seed);
                TryPlace(parent, placed, pos, kind, h, i + seed);
            }
        }

        static PropKind PickMixed(int seed)
        {
            var r = Mathf.Abs(seed) % 10;
            if (r < 5) return PropKind.Tree;
            if (r < 7) return PropKind.Bush;
            if (r < 9) return PropKind.Rock;
            return PropKind.Stump;
        }

        static float HeightFor(PropKind kind, int seed)
        {
            var v = (Mathf.Abs(seed) % 5) * 0.12f;
            switch (kind)
            {
                case PropKind.Tree: return 2.35f + v + (seed % 2) * 0.45f;
                case PropKind.Rock: return 0.85f + v * 0.5f;
                case PropKind.Stump: return 1.15f + v * 0.3f;
                default: return 1.05f + v * 0.4f;
            }
        }

        static void TryPlace(Transform parent, List<Vector3> placed, Vector3 pos, PropKind kind, float height, int seed)
        {
            var minDist = kind == PropKind.Tree ? 2.05f : 1.35f;
            for (var i = 0; i < placed.Count; i++)
            {
                var d = placed[i] - pos;
                d.y = 0f;
                if (d.sqrMagnitude < minDist * minDist) return;
            }

            var tex = TexFor(kind, seed);
            if (tex == null) return;

            SpawnBillboard(parent, tex, pos, height, seed);
            placed.Add(pos);
        }

        static Texture2D TexFor(PropKind kind, int seed)
        {
            switch (kind)
            {
                case PropKind.Tree: return Pick(_trees, seed);
                case PropKind.Rock: return Pick(_rocks, seed);
                case PropKind.Stump: return Pick(_stumps, seed);
                default: return Pick(_bushes, seed);
            }
        }

        static Texture2D Pick(Texture2D[] set, int seed)
        {
            if (set == null || set.Length == 0) return null;
            return set[Mathf.Abs(seed) % set.Length];
        }

        static void SpawnBillboard(Transform parent, Texture2D tex, Vector3 localPos, float targetHeight, int seed)
        {
            var root = new GameObject(tex.name + "_" + seed);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            // Soft ground shadow (reads as planted, CoC-like).
            SpawnShadow(root.transform, Mathf.Clamp(targetHeight * 0.28f, 0.45f, 1.1f));

            var go = new GameObject("Sprite");
            go.transform.SetParent(root.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedQuad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetMat(tex);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var aspect = tex.width / (float)Mathf.Max(tex.height, 1);
            var h = targetHeight;
            var w = h * aspect;
            // Slight per-tree scale variance.
            var s = 0.92f + (Mathf.Abs(seed) % 7) * 0.03f;
            go.transform.localScale = new Vector3(w * s, h * s, 1f);
            go.transform.localPosition = new Vector3(0f, h * s * 0.48f, 0f);

            // Match CoC cam (pitch 55 / yaw 45) — iso art already drawn for this view.
            // Small yaw jitter so trees don't look stamped.
            var yawJitter = ((seed % 9) - 4) * 3.5f;
            go.transform.localRotation = Quaternion.Euler(35f, 45f + yawJitter, 0f);
        }

        static void SpawnShadow(Transform parent, float radius)
        {
            var sh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sh.name = "Shadow";
            sh.transform.SetParent(parent, false);
            Object.Destroy(sh.GetComponent<Collider>());
            sh.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            sh.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            var r = sh.GetComponent<Renderer>();
            r.sharedMaterial = ShadowMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static Material ShadowMat()
        {
            if (_shadowMat != null) return _shadowMat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            _shadowMat = new Material(shader);
            var c = new Color(0.08f, 0.1f, 0.06f, 1f);
            if (_shadowMat.HasProperty("_BaseColor")) _shadowMat.SetColor("_BaseColor", c);
            if (_shadowMat.HasProperty("_Color")) _shadowMat.SetColor("_Color", c);
            _shadowMat.enableInstancing = true;
            return _shadowMat;
        }

        static Mesh SharedQuad()
        {
            if (_quad != null) return _quad;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            return _quad;
        }

        static Material GetMat(Texture2D tex)
        {
            var id = tex.GetInstanceID();
            if (Mats.TryGetValue(id, out var cached) && cached != null) return cached;

            // URP Lit cutout — reliable alpha kill on mobile/editor.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
            if (shader == null) shader = Shader.Find("Unlit/Transparent Cutout");

            var mat = new Material(shader);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.42f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;
            mat.enableInstancing = true;
            Mats[id] = mat;
            return mat;
        }

        static void EnsureTextures()
        {
            if (_trees != null) return;
            _trees = LoadSet(TreeNames);
            _rocks = LoadSet(RockNames);
            _stumps = LoadSet(StumpNames);
            _bushes = LoadSet(BushNames);
        }

        static Texture2D[] LoadSet(string[] names)
        {
            var list = new List<Texture2D>(names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var src = Resources.Load<Texture2D>("Environment/Nature2D/" + names[i]);
                if (src == null || src.width < 8) continue;
                var punched = PunchCreamBackground(src);
                if (punched != null) list.Add(punched);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Nature2D sheets ship with opaque cream/white fill. Punch near-background to alpha and crop.
        /// </summary>
        static Texture2D PunchCreamBackground(Texture2D src)
        {
            Texture2D readable = src;
            if (!src.isReadable)
            {
                // GPU blit → readable copy (works even when import isReadable=false).
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

            // Sample corner median as background key.
            var corners = new[]
            {
                pixels[0], pixels[w - 1], pixels[(h - 1) * w], pixels[h * w - 1],
                pixels[2 + 2 * w], pixels[w - 3 + 2 * w]
            };
            var key = Average(corners);

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
                    if (IsBackground(p, key))
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

            // Pad crop.
            minX = Mathf.Max(0, minX - 2);
            minY = Mathf.Max(0, minY - 2);
            maxX = Mathf.Min(w - 1, maxX + 2);
            maxY = Mathf.Min(h - 1, maxY + 2);
            var cw = maxX - minX + 1;
            var ch = maxY - minY + 1;
            var cropped = new Color32[cw * ch];
            for (var y = 0; y < ch; y++)
            {
                for (var x = 0; x < cw; x++)
                    cropped[y * cw + x] = pixels[(minY + y) * w + (minX + x)];
            }

            var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            outTex.name = src.name + "_cut";
            outTex.filterMode = FilterMode.Bilinear;
            outTex.wrapMode = TextureWrapMode.Clamp;
            outTex.SetPixels32(cropped);
            outTex.Apply(false, true);
            return outTex;
        }

        static Color32 Average(Color32[] c)
        {
            int r = 0, g = 0, b = 0;
            for (var i = 0; i < c.Length; i++)
            {
                r += c[i].r;
                g += c[i].g;
                b += c[i].b;
            }
            return new Color32((byte)(r / c.Length), (byte)(g / c.Length), (byte)(b / c.Length), 255);
        }

        static bool IsBackground(Color32 p, Color32 key)
        {
            // Already transparent.
            if (p.a < 16) return true;
            var dr = p.r - key.r;
            var dg = p.g - key.g;
            var db = p.b - key.b;
            var dist = dr * dr + dg * dg + db * db;
            // Cream / near-white sheets.
            if (dist < 48 * 48) return true;
            if (p.r > 235 && p.g > 230 && p.b > 210) return true;
            // Soft edge: near-key and bright.
            if (dist < 70 * 70 && (p.r + p.g + p.b) > 600) return true;
            return false;
        }

        // -------- Procedural 3D fallback (individual trees) --------

        static void BuildProcedural(Transform fieldRoot, float fieldWorldSize)
        {
            EnsureProcMats();
            var border = new GameObject("NatureBorder");
            border.transform.SetParent(fieldRoot, false);
            var half = fieldWorldSize * 0.5f;
            var radius = half + 1.7f;
            var placed = new List<Vector3>(32);

            for (var i = 0; i < 16; i++)
            {
                var ang = (i / 16f) * Mathf.PI * 2f;
                var jitter = ((i * 13) % 5) * 0.2f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius + jitter), 0f, Mathf.Sin(ang) * (radius + jitter));
                if (!FarEnough(placed, pos, 2.1f)) continue;
                SpawnProcTree(border.transform, pos, 0.85f + (i % 4) * 0.12f, i % 2 == 1);
                placed.Add(pos);
            }

            for (var i = 0; i < 8; i++)
            {
                var ang = (i / 8f) * Mathf.PI * 2f + 0.2f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius - 0.6f), 0f, Mathf.Sin(ang) * (radius - 0.6f));
                if (!FarEnough(placed, pos, 1.4f)) continue;
                if (i % 2 == 0) SpawnProcRock(border.transform, pos, 0.5f + (i % 3) * 0.1f);
                else SpawnProcBush(border.transform, pos);
                placed.Add(pos);
            }
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

        static void EnsureProcMats()
        {
            if (_bark != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _bark = NewMat(shader, new Color(0.42f, 0.28f, 0.16f));
            _canopy = NewMat(shader, new Color(0.22f, 0.55f, 0.20f));
            _canopyB = NewMat(shader, new Color(0.32f, 0.62f, 0.22f));
            _rock = NewMat(shader, new Color(0.55f, 0.55f, 0.52f));
            _bush = NewMat(shader, new Color(0.28f, 0.58f, 0.24f));
        }

        static Material NewMat(Shader shader, Color color)
        {
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.enableInstancing = true;
            return m;
        }

        static void SpawnProcTree(Transform parent, Vector3 localPos, float scale, bool autumn)
        {
            var tree = new GameObject("Tree3D");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = localPos;
            tree.transform.localRotation = Quaternion.Euler(0f, localPos.x * 35f + localPos.z * 17f, 0f);

            SpawnShadow(tree.transform, 0.7f * scale);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localScale = new Vector3(0.26f, 0.75f, 0.26f) * scale;
            trunk.transform.localPosition = new Vector3(0f, 0.75f * scale, 0f);
            Apply(trunk, _bark);

            // Layered canopy — reads as one tree, not a blob.
            AddCanopy(tree.transform, new Vector3(0f, 1.65f * scale, 0f), new Vector3(1.35f, 1.15f, 1.35f) * scale, autumn ? _canopyB : _canopy);
            AddCanopy(tree.transform, new Vector3(0.35f * scale, 1.85f * scale, 0.15f * scale), new Vector3(0.95f, 0.85f, 0.95f) * scale, autumn ? _canopy : _canopyB);
            AddCanopy(tree.transform, new Vector3(-0.3f * scale, 1.9f * scale, -0.2f * scale), new Vector3(0.85f, 0.75f, 0.85f) * scale, autumn ? _canopyB : _canopy);
        }

        static void AddCanopy(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Canopy";
            canopy.transform.SetParent(parent, false);
            canopy.transform.localPosition = pos;
            canopy.transform.localScale = scale;
            Apply(canopy, mat);
        }

        static void SpawnProcRock(Transform parent, Vector3 localPos, float scale)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(parent, false);
            rock.transform.localPosition = localPos + Vector3.up * (0.22f * scale);
            rock.transform.localScale = new Vector3(1.1f, 0.55f, 0.9f) * scale;
            rock.transform.localRotation = Quaternion.Euler(0f, localPos.x * 40f, 12f);
            Apply(rock, _rock);
        }

        static void SpawnProcBush(Transform parent, Vector3 localPos)
        {
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Bush";
            bush.transform.SetParent(parent, false);
            bush.transform.localPosition = localPos + Vector3.up * 0.28f;
            bush.transform.localScale = new Vector3(0.85f, 0.55f, 0.85f);
            Apply(bush, _bush);
        }

        static void Apply(GameObject go, Material mat)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }
    }
}

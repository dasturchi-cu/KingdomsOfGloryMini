using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Checkerboard base field + CoC-style nature ring (Nature2D → AI prefab → procedural).
    /// </summary>
    public static class FieldVisualBuilder
    {
        static Material _sharedFieldMat;
        static Texture2D _sharedCheckerTex;

        static readonly string[] Nature2DTrees = { "tree_large", "tree_medium" };
        static readonly string[] Nature2DRocks = { "rock_tall", "rock_small" };
        static readonly string[] Nature2DStumps = { "stump_hollow" };
        static readonly string[] Nature2DBushes = { "bush_patch" };

        static Texture2D[] _n2dTrees;
        static Texture2D[] _n2dRocks;
        static Texture2D[] _n2dStumps;
        static Texture2D[] _n2dBushes;
        static Mesh _sharedQuadMesh;
        static Quaternion _n2dBillboardRot;
        static readonly Dictionary<int, Material> _n2dMatByTex = new Dictionary<int, Material>(8);

        static Material _matBark;
        static Material _matCanopy;
        static Material _matCanopyAutumn;
        static Material _matRock;
        static Material _matBush;

        /// <summary>
        /// Clears stale Ground/Plane/BaseField roots and builds a fresh field at world center.
        /// </summary>
        public static Transform Build(int gridSize, float cellSize)
        {
            ClearStaleGround();
            var fieldWorldSize = gridSize * cellSize;
            var fieldCenter = new Vector3((gridSize - 1) * cellSize * 0.5f, 0f, (gridSize - 1) * cellSize * 0.5f);
            return BuildCheckerboardField(gridSize, fieldWorldSize, fieldCenter).transform;
        }

        static void ClearStaleGround()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                if (go.name == "Ground" || go.name == "Plane" || go.name == "BaseField")
                    Object.Destroy(go);
            }
        }

        static GameObject BuildCheckerboardField(int gridSize, float fieldWorldSize, Vector3 fieldCenter)
        {
            var fieldPrefab = Resources.Load<GameObject>("Environment/BaseField_L1");
            if (fieldPrefab != null)
            {
                var root = new GameObject("BaseField");
                root.transform.position = fieldCenter;

                var field = Object.Instantiate(fieldPrefab, root.transform);
                field.name = "CheckerGrass";
                field.transform.localPosition = Vector3.zero;
                field.transform.localRotation = Quaternion.identity;
                FitEnvPrefabToSize(field, fieldWorldSize);
                StripColliders(field);

                BuildNatureBorder(root.transform, fieldWorldSize);
                return root;
            }

            var procedural = new GameObject("BaseField");
            procedural.transform.position = fieldCenter;

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "CheckerGrass";
            plane.transform.SetParent(procedural.transform, false);
            var s = fieldWorldSize / 10f;
            plane.transform.localPosition = Vector3.zero;
            plane.transform.localRotation = Quaternion.identity;
            plane.transform.localScale = new Vector3(s, 1f, s);

            var col = plane.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            if (_sharedFieldMat == null)
            {
                _sharedCheckerTex = CreateCheckerTexture(gridSize, 8);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _sharedFieldMat = new Material(shader);
                _sharedFieldMat.mainTexture = _sharedCheckerTex;
                if (_sharedFieldMat.HasProperty("_BaseMap"))
                    _sharedFieldMat.SetTexture("_BaseMap", _sharedCheckerTex);
                if (_sharedFieldMat.HasProperty("_MainTex"))
                    _sharedFieldMat.SetTexture("_MainTex", _sharedCheckerTex);
                if (_sharedFieldMat.HasProperty("_BaseColor"))
                    _sharedFieldMat.SetColor("_BaseColor", Color.white);
                if (_sharedFieldMat.HasProperty("_Color"))
                    _sharedFieldMat.SetColor("_Color", Color.white);
                _sharedFieldMat.enableInstancing = true;
            }

            var rend = plane.GetComponent<Renderer>();
            rend.sharedMaterial = _sharedFieldMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = true;

            BuildNatureBorder(procedural.transform, fieldWorldSize);
            return procedural;
        }

        static void FitEnvPrefabToSize(GameObject go, float targetSize)
        {
            go.transform.localScale = Vector3.one;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            var bounds = EncapsulateBounds(renderers);
            var sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            var scale = Mathf.Clamp(targetSize / sizeXZ, 0.05f, 50f);
            go.transform.localScale = Vector3.one * scale;

            bounds = EncapsulateBounds(renderers);
            var localCenter = go.transform.parent != null
                ? go.transform.parent.InverseTransformPoint(bounds.center)
                : bounds.center;
            var localMinY = go.transform.parent != null
                ? go.transform.parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y
                : bounds.min.y;
            var lp = go.transform.localPosition;
            go.transform.localPosition = new Vector3(lp.x - localCenter.x, lp.y - localMinY, lp.z - localCenter.z);
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);
        }

        static void BuildNatureBorder(Transform fieldRoot, float fieldWorldSize)
        {
            if (TryBuildNature2DBorder(fieldRoot, fieldWorldSize))
                return;

            var borderPrefab = Resources.Load<GameObject>("Environment/NatureBorder_L1");
            if (borderPrefab != null)
            {
                var border = Object.Instantiate(borderPrefab, fieldRoot);
                border.name = "NatureBorder";
                border.transform.localPosition = Vector3.zero;
                border.transform.localRotation = Quaternion.identity;
                FitEnvPrefabToSize(border, fieldWorldSize * 1.45f);
                StripColliders(border);
                Debug.Log("[MiniMvp] AI NatureBorder fallback");
                return;
            }

            BuildProceduralNatureBorder(fieldRoot, fieldWorldSize);
            Debug.Log("[MiniMvp] Procedural nature border fallback");
        }

        static bool TryBuildNature2DBorder(Transform fieldRoot, float fieldWorldSize)
        {
            EnsureNature2DTextures();
            if (_n2dTrees == null || _n2dTrees.Length == 0) return false;

            var border = new GameObject("NatureBorder");
            border.transform.SetParent(fieldRoot, false);
            border.transform.localPosition = Vector3.zero;

            var cam = UnityEngine.Camera.main;
            _n2dBillboardRot = cam != null
                ? Quaternion.LookRotation(cam.transform.forward, Vector3.up)
                : Quaternion.Euler(35f, 45f, 0f);

            var half = fieldWorldSize * 0.5f;
            var radius = half + 1.55f;
            const int slots = 26; // denser ring for Game/Simulator view

            for (var i = 0; i < slots; i++)
            {
                var t = (i / (float)slots) * Mathf.PI * 2f;
                var jitter = 0.15f + (i % 3) * 0.12f;
                var pos = new Vector3(Mathf.Cos(t) * (radius + jitter), 0f, Mathf.Sin(t) * (radius + jitter));
                var kind = i % 5;
                if (kind == 0 || kind == 1)
                    SpawnNature2DBillboard(border.transform, PickTex(_n2dTrees, i), pos, 2.4f + (i % 3) * 0.35f);
                else if (kind == 2)
                    SpawnNature2DBillboard(border.transform, PickTex(_n2dRocks, i), pos, 0.95f + (i % 3) * 0.2f);
                else if (kind == 3)
                    SpawnNature2DBillboard(border.transform, PickTex(_n2dStumps, i), pos, 1.35f);
                else
                    SpawnNature2DBillboard(border.transform, PickTex(_n2dBushes, i), pos, 1.15f);
            }

            SpawnNature2DBillboard(border.transform, PickTex(_n2dTrees, 0), new Vector3(0f, 0f, radius + 0.7f), 3.1f);
            SpawnNature2DBillboard(border.transform, PickTex(_n2dStumps, 0), new Vector3(radius + 0.55f, 0f, 0f), 1.65f);
            SpawnNature2DBillboard(border.transform, PickTex(_n2dRocks, 0), new Vector3(-radius - 0.45f, 0f, 0.15f), 1.25f);

            StaticBatchingUtility.Combine(border);
            Debug.Log("[MiniMvp] CoC Nature2D border (" + border.transform.childCount + " props)");
            return true;
        }

        static void EnsureNature2DTextures()
        {
            if (_n2dTrees != null) return;
            _n2dTrees = LoadNature2DSet(Nature2DTrees);
            _n2dRocks = LoadNature2DSet(Nature2DRocks);
            _n2dStumps = LoadNature2DSet(Nature2DStumps);
            _n2dBushes = LoadNature2DSet(Nature2DBushes);
        }

        static Texture2D[] LoadNature2DSet(string[] names)
        {
            var list = new List<Texture2D>(names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var tex = Resources.Load<Texture2D>("Environment/Nature2D/" + names[i]);
                if (tex != null && tex.width > 8 && tex.height > 8) list.Add(tex);
            }
            return list.ToArray();
        }

        static Texture2D PickTex(Texture2D[] set, int seed)
        {
            if (set == null || set.Length == 0) return null;
            return set[Mathf.Abs(seed) % set.Length];
        }

        static Mesh SharedQuadMesh()
        {
            if (_sharedQuadMesh != null) return _sharedQuadMesh;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _sharedQuadMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            return _sharedQuadMesh;
        }

        static Material GetNature2DMaterial(Texture2D tex)
        {
            var id = tex.GetInstanceID();
            if (_n2dMatByTex.TryGetValue(id, out var cached) && cached != null) return cached;

            var shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
            if (shader == null) shader = Shader.Find("Unlit/Transparent Cutout");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.35f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;
            mat.enableInstancing = true;
            _n2dMatByTex[id] = mat;
            return mat;
        }

        static void SpawnNature2DBillboard(Transform parent, Texture2D tex, Vector3 localPos, float targetHeight)
        {
            if (tex == null) return;

            var go = new GameObject(tex.name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedQuadMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetNature2DMaterial(tex);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var aspect = tex.width / (float)Mathf.Max(tex.height, 1);
            var h = targetHeight;
            var w = h * aspect;
            go.transform.localScale = new Vector3(w, h, 1f);
            go.transform.localPosition = localPos + Vector3.up * (h * 0.48f);
            go.transform.rotation = _n2dBillboardRot;
        }

        static void BuildProceduralNatureBorder(Transform fieldRoot, float fieldWorldSize)
        {
            var border = new GameObject("NatureBorder");
            border.transform.SetParent(fieldRoot, false);
            border.transform.localPosition = Vector3.zero;

            EnsureBorderMaterials();

            var half = fieldWorldSize * 0.5f;
            var radius = half + 1.35f;
            const int slots = 28;

            for (var i = 0; i < slots; i++)
            {
                var t = (i / (float)slots) * Mathf.PI * 2f;
                var jitter = 0.35f + (i % 3) * 0.15f;
                var x = Mathf.Cos(t) * (radius + jitter * 0.4f);
                var z = Mathf.Sin(t) * (radius + jitter * 0.4f);
                var pos = new Vector3(x, 0f, z);

                var kind = i % 5;
                if (kind == 0 || kind == 1)
                    SpawnBorderTree(border.transform, pos, 0.85f + (i % 4) * 0.12f, kind == 1);
                else if (kind == 2)
                    SpawnBorderRock(border.transform, pos, 0.45f + (i % 3) * 0.12f);
                else if (kind == 3)
                    SpawnBorderStump(border.transform, pos);
                else
                    SpawnBorderBush(border.transform, pos);
            }

            SpawnBorderStump(border.transform, new Vector3(0f, 0f, radius + 0.6f), true);
            SpawnBorderRock(border.transform, new Vector3(radius + 0.4f, 0f, 0f), 0.9f);
            SpawnBorderRock(border.transform, new Vector3(-radius - 0.3f, 0f, 0.2f), 0.75f);
        }

        static void EnsureBorderMaterials()
        {
            if (_matBark != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            _matBark = NewMat(shader, new Color(0.42f, 0.28f, 0.16f));
            _matCanopy = NewMat(shader, new Color(0.22f, 0.55f, 0.20f));
            _matCanopyAutumn = NewMat(shader, new Color(0.72f, 0.55f, 0.18f));
            _matRock = NewMat(shader, new Color(0.55f, 0.55f, 0.52f));
            _matBush = NewMat(shader, new Color(0.28f, 0.58f, 0.24f));
        }

        static Material NewMat(Shader shader, Color color)
        {
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.enableInstancing = true;
            return m;
        }

        static void SpawnBorderTree(Transform parent, Vector3 localPos, float scale, bool autumn)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = localPos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localScale = new Vector3(0.28f, 0.7f, 0.28f) * scale;
            trunk.transform.localPosition = new Vector3(0f, 0.7f * scale, 0f);
            ApplyBorderMat(trunk, _matBark);

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Canopy";
            canopy.transform.SetParent(tree.transform, false);
            canopy.transform.localScale = new Vector3(1.4f, 1.2f, 1.4f) * scale;
            canopy.transform.localPosition = new Vector3(0f, 1.55f * scale, 0f);
            ApplyBorderMat(canopy, autumn ? _matCanopyAutumn : _matCanopy);
        }

        static void SpawnBorderRock(Transform parent, Vector3 localPos, float scale)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(parent, false);
            rock.transform.localPosition = localPos + Vector3.up * (0.25f * scale);
            rock.transform.localScale = new Vector3(1.1f, 0.55f, 0.9f) * scale;
            rock.transform.localRotation = Quaternion.Euler(0f, localPos.x * 40f, 12f);
            ApplyBorderMat(rock, _matRock);
        }

        static void SpawnBorderStump(Transform parent, Vector3 localPos, bool big = false)
        {
            var stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stump.name = big ? "StumpBig" : "Stump";
            stump.transform.SetParent(parent, false);
            var s = big ? 1.1f : 0.7f;
            stump.transform.localPosition = localPos + Vector3.up * (0.25f * s);
            stump.transform.localScale = new Vector3(0.7f, 0.25f, 0.7f) * s;
            ApplyBorderMat(stump, _matBark);
        }

        static void SpawnBorderBush(Transform parent, Vector3 localPos)
        {
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Bush";
            bush.transform.SetParent(parent, false);
            bush.transform.localPosition = localPos + Vector3.up * 0.25f;
            bush.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f);
            ApplyBorderMat(bush, _matBush);
        }

        static void ApplyBorderMat(GameObject go, Material mat)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        static Texture2D CreateCheckerTexture(int cells, int pixelsPerCell)
        {
            var size = cells * pixelsPerCell;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "FieldChecker";

            var light = new Color(0.58f, 0.82f, 0.38f);
            var dark = new Color(0.40f, 0.62f, 0.30f);
            var edge = new Color(0.32f, 0.48f, 0.24f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x / pixelsPerCell;
                    var cy = y / pixelsPerCell;
                    var c = ((cx + cy) & 1) == 0 ? light : dark;
                    if (cx == 0 || cy == 0 || cx == cells - 1 || cy == cells - 1)
                        c = Color.Lerp(c, edge, 0.45f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(false, true);
            return tex;
        }

        static Bounds EncapsulateBounds(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}

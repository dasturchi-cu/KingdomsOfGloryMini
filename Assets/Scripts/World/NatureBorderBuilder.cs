using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// CoC-smooth nature border with Might &amp; Glory bright fantasy palette.
    /// Prefers Quaternius CC0 low-poly FBX from Resources; falls back to solid URP primitives.
    /// </summary>
    public static class NatureBorderBuilder
    {
        const string Nature3DPath = "Environment/Nature3D/";

        static Material _bark;
        static Material _canopyA;
        static Material _canopyB;
        static Material _canopyC;
        static Material _rock;
        static Material _bush;
        static Material _outerGrass;
        static Material _midGrass;
        static Material _shadowMat;
        static Material _flower;
        static Material _brightGrass;

        static GameObject[] _treePrefabs;
        static GameObject[] _bushPrefabs;
        static GameObject[] _rockPrefabs;
        static GameObject[] _plantPrefabs;
        static bool _prefabsLoaded;

        static readonly string[] TreeNames =
        {
            "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5",
            "BirchTree_1", "BirchTree_2", "BirchTree_3",
            "PineTree_1", "PineTree_2", "PineTree_3", "PineTree_4", "PineTree_5"
        };

        static readonly string[] BushNames =
        {
            "Bush_1", "Bush_2", "BushBerries_1", "BushBerries_2"
        };

        static readonly string[] RockNames =
        {
            "Rock_1", "Rock_2", "Rock_3", "Rock_4", "Rock_5", "Rock_6", "Rock_7",
            "Rock_Moss_1", "Rock_Moss_2", "Rock_Moss_3"
        };

        static readonly string[] PlantNames =
        {
            "Plant_1", "Plant_2", "Plant_3", "Plant_4", "Plant_5",
            "Flowers", "Grass", "Grass_2", "Grass_Short"
        };

        public static void Build(Transform fieldRoot, float fieldWorldSize)
        {
            EnsureMats();
            EnsurePrefabs();
            BuildOuterWorld(fieldRoot, fieldWorldSize);
            BuildTreeRing(fieldRoot, fieldWorldSize);
        }

        static void BuildOuterWorld(Transform fieldRoot, float fieldWorldSize)
        {
            var half = fieldWorldSize * 0.5f;

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
            MarkStaticHierarchy(outer);

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
            MarkStaticHierarchy(mid);
        }

        static void BuildTreeRing(Transform fieldRoot, float fieldWorldSize)
        {
            var border = new GameObject("NatureBorder");
            border.transform.SetParent(fieldRoot, false);

            var half = fieldWorldSize * 0.5f;
            var radius = half + 1.85f;
            var placed = new List<Vector3>(64);
            var useFbx = _treePrefabs != null && _treePrefabs.Length > 0;

            for (var i = 0; i < 20; i++)
            {
                var ang = (i / 20f) * Mathf.PI * 2f + 0.08f;
                var jitter = ((i * 17) % 5) * 0.22f - 0.3f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius + jitter), 0f, Mathf.Sin(ang) * (radius + jitter));
                if (!FarEnough(placed, pos, 1.85f)) continue;
                if (useFbx) SpawnFbxProp(border.transform, Pick(_treePrefabs, i), pos, 0.9f + (i % 5) * 0.08f, i, 2.4f);
                else SpawnProcTree(border.transform, pos, 0.95f + (i % 5) * 0.1f, i);
                placed.Add(pos);
            }

            for (var i = 0; i < 14; i++)
            {
                var ang = (i / 14f) * Mathf.PI * 2f + 0.31f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius + 1.7f), 0f, Mathf.Sin(ang) * (radius + 1.7f));
                if (!FarEnough(placed, pos, 2.1f)) continue;
                if (useFbx) SpawnFbxProp(border.transform, Pick(_treePrefabs, i + 7), pos, 1.05f + (i % 4) * 0.1f, i + 40, 2.8f);
                else SpawnProcTree(border.transform, pos, 1.15f + (i % 4) * 0.12f, i + 40);
                placed.Add(pos);
            }

            // Far scatter — fills phone edges like CoC forest.
            for (var i = 0; i < 16; i++)
            {
                var ang = (i / 16f) * Mathf.PI * 2f + 0.19f;
                var dist = radius + 3.2f + (i % 3) * 0.55f;
                var pos = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                if (!FarEnough(placed, pos, 2.4f)) continue;
                if (useFbx) SpawnFbxProp(border.transform, Pick(_treePrefabs, i + 13), pos, 1.2f + (i % 3) * 0.15f, i + 80, 3.2f);
                else SpawnProcTree(border.transform, pos, 1.25f + (i % 3) * 0.12f, i + 80);
                placed.Add(pos);
            }

            for (var i = 0; i < 12; i++)
            {
                var ang = (i / 12f) * Mathf.PI * 2f + 0.5f;
                var pos = new Vector3(Mathf.Cos(ang) * (radius - 0.25f), 0f, Mathf.Sin(ang) * (radius - 0.25f));
                if (!FarEnough(placed, pos, 1.2f)) continue;
                if (i % 2 == 0)
                {
                    if (_rockPrefabs != null && _rockPrefabs.Length > 0)
                        SpawnFbxProp(border.transform, Pick(_rockPrefabs, i), pos, 0.7f + (i % 3) * 0.12f, i + 100, 1.1f);
                    else
                        SpawnProcRock(border.transform, pos, 0.55f + (i % 3) * 0.1f);
                }
                else
                {
                    if (_bushPrefabs != null && _bushPrefabs.Length > 0)
                        SpawnFbxProp(border.transform, Pick(_bushPrefabs, i), pos, 0.85f + (i % 3) * 0.1f, i + 120, 1.0f);
                    else
                        SpawnProcBush(border.transform, pos);
                }
                placed.Add(pos);
            }

            if (_plantPrefabs != null && _plantPrefabs.Length > 0)
            {
                for (var i = 0; i < 10; i++)
                {
                    var ang = (i / 10f) * Mathf.PI * 2f + 0.72f;
                    var pos = new Vector3(Mathf.Cos(ang) * (radius + 0.55f), 0f, Mathf.Sin(ang) * (radius + 0.55f));
                    if (!FarEnough(placed, pos, 1.0f)) continue;
                    SpawnFbxProp(border.transform, Pick(_plantPrefabs, i), pos, 0.9f + (i % 4) * 0.08f, i + 140, 0.7f);
                    placed.Add(pos);
                }
            }

            Debug.Log("[MiniMvp] Nature border — " + border.transform.childCount +
                      " props (fbx=" + useFbx + ") + outer world fill");
            MarkStaticHierarchy(border);
        }

        static GameObject Pick(GameObject[] arr, int seed)
        {
            return arr[Mathf.Abs(seed) % arr.Length];
        }

        static void SpawnFbxProp(Transform parent, GameObject prefab, Vector3 localPos, float scale, int seed,
            float shadowRadius)
        {
            if (prefab == null) return;

            var root = new GameObject("LP_" + prefab.name + "_" + seed);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0f, seed * 37.3f, 0f);

            SpawnShadow(root.transform, shadowRadius * scale * 0.35f);

            var go = Object.Instantiate(prefab, root.transform, false);
            go.name = "Mesh";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // Quaternius FBX often imports huge or tiny — normalize height.
            NormalizeHeight(go, targetHeight: 2.2f * scale);
            StripColliders(go);
            UrpMaterialUtil.RemapToUrp(go);
            StylizeNatureColors(go);
            // Blob shadow already drawn — only trunk casts real shadows (cheap).
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var n = r.name != null ? r.name.ToLowerInvariant() : "";
                var isTrunk = n.Contains("trunk") || n.Contains("bark") || n.Contains("wood") ||
                              n.Contains("stem") || n.Contains("log");
                r.shadowCastingMode = isTrunk
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static void NormalizeHeight(GameObject go, float targetHeight)
        {
            var local = ComputeLocalBounds(go);
            var h = local.size.y;
            if (h < 0.01f) return;
            var s = targetHeight / h;
            go.transform.localScale = Vector3.one * s;

            // Plant feet on ground — pivot often sits mid-mesh on Quaternius FBX.
            local = ComputeLocalBounds(go);
            go.transform.localPosition = new Vector3(0f, -local.min.y, 0f);
        }

        static Bounds ComputeLocalBounds(GameObject go)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>(true);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var any = false;
            for (var i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                var t = mf.transform;
                var worldCenter = t.TransformPoint(b.center);
                var localCenter = go.transform.InverseTransformPoint(worldCenter);
                var worldSize = Vector3.Scale(b.size, Abs(t.lossyScale));
                var localSize = new Vector3(
                    worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(go.transform.lossyScale.x)),
                    worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(go.transform.lossyScale.y)),
                    worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(go.transform.lossyScale.z)));
                var nb = new Bounds(localCenter, localSize);
                if (!any) { bounds = nb; any = true; }
                else bounds.Encapsulate(nb);
            }
            return any ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);
        }

        /// <summary>
        /// Shared stylized mats keyed by source instance — never use Renderer.materials
        /// (that clones every slot and leaks Material instances).
        /// </summary>
        static readonly Dictionary<int, Material> StylizedBySourceId = new Dictionary<int, Material>(64);

        /// <summary>Boost albedo toward bright M&amp;G / CoC cartoon greens.</summary>
        static void StylizeNatureColors(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;
                var nextSlots = shared;
                var replaced = false;
                for (var i = 0; i < shared.Length; i++)
                {
                    var src = shared[i];
                    if (src == null) continue;
                    var stylized = GetOrCreateStylized(src);
                    if (stylized == src) continue;
                    if (!replaced)
                    {
                        nextSlots = new Material[shared.Length];
                        for (var j = 0; j < shared.Length; j++)
                            nextSlots[j] = shared[j];
                        replaced = true;
                    }
                    nextSlots[i] = stylized;
                }

                if (replaced)
                    r.sharedMaterials = nextSlots;
            }
        }

        static Material GetOrCreateStylized(Material src)
        {
            var id = src.GetInstanceID();
            if (StylizedBySourceId.TryGetValue(id, out var cached) && cached != null)
                return cached;

            var m = new Material(src);
            m.name = src.name + "_Stylized";
            var c = Color.white;
            if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) c = m.GetColor("_Color");

            var name = (m.name ?? string.Empty).ToLowerInvariant();
            Color next;
            if (name.Contains("bark") || name.Contains("wood") || name.Contains("trunk") ||
                (c.r > c.g * 0.85f && c.g < 0.45f && c.b < 0.4f))
            {
                next = Color.Lerp(c, new Color(0.45f, 0.28f, 0.14f), 0.55f);
            }
            else if (name.Contains("rock") || name.Contains("stone") ||
                     (c.r > 0.4f && Mathf.Abs(c.r - c.g) < 0.08f && Mathf.Abs(c.g - c.b) < 0.08f && c.g < 0.65f))
            {
                next = Color.Lerp(c, new Color(0.58f, 0.56f, 0.52f), 0.4f);
            }
            else if (name.Contains("flower") || name.Contains("berry") || c.r > c.g + 0.15f)
            {
                next = Color.Lerp(c, new Color(0.92f, 0.35f, 0.45f), 0.35f);
            }
            else
            {
                var bright = new Color(0.32f, 0.72f, 0.28f);
                next = Color.Lerp(c, bright, 0.5f);
                next = new Color(
                    Mathf.Clamp01(next.r * 0.95f),
                    Mathf.Clamp01(next.g * 1.15f),
                    Mathf.Clamp01(next.b * 0.9f),
                    1f);
            }

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", next);
            if (m.HasProperty("_Color")) m.SetColor("_Color", next);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);

            StylizedBySourceId[id] = m;
            return m;
        }

        static void EnsurePrefabs()
        {
            if (_prefabsLoaded) return;
            _prefabsLoaded = true;
            _treePrefabs = LoadModels(TreeNames);
            _bushPrefabs = LoadModels(BushNames);
            _rockPrefabs = LoadModels(RockNames);
            _plantPrefabs = LoadModels(PlantNames);
            Debug.Log("[MiniMvp] Nature3D loaded trees=" + (_treePrefabs?.Length ?? 0) +
                      " bushes=" + (_bushPrefabs?.Length ?? 0) +
                      " rocks=" + (_rockPrefabs?.Length ?? 0) +
                      " plants=" + (_plantPrefabs?.Length ?? 0));
        }

        static GameObject[] LoadModels(string[] names)
        {
            var list = new List<GameObject>(names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var go = Resources.Load<GameObject>(Nature3DPath + names[i]);
                if (go != null) list.Add(go);
            }
            return list.ToArray();
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
            var shader = UrpMaterialUtil.FindLitShader();
            if (shader == null) shader = Shader.Find("Standard");

            // Bright M&G / CoC grass palette (not muddy).
            _bark = NewMat(shader, new Color(0.42f, 0.27f, 0.14f));
            _canopyA = NewMat(shader, new Color(0.28f, 0.68f, 0.24f));
            _canopyB = NewMat(shader, new Color(0.36f, 0.76f, 0.28f));
            _canopyC = NewMat(shader, new Color(0.22f, 0.62f, 0.30f));
            _rock = NewMat(shader, new Color(0.58f, 0.56f, 0.52f));
            _bush = NewMat(shader, new Color(0.30f, 0.70f, 0.26f));
            _outerGrass = NewMat(shader, new Color(0.34f, 0.62f, 0.28f));
            _midGrass = NewMat(shader, new Color(0.40f, 0.70f, 0.32f));
            _brightGrass = NewMat(shader, new Color(0.45f, 0.78f, 0.34f));
            _flower = NewMat(shader, new Color(0.92f, 0.38f, 0.48f));
            _shadowMat = NewMat(UrpMaterialUtil.FindUnlitShader() ?? shader, new Color(0.1f, 0.12f, 0.08f));
            _ = _brightGrass;
            _ = _flower;
        }

        static Material NewMat(Shader shader, Color color)
        {
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
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

            Prim(tree.transform, PrimitiveType.Cylinder, "Trunk",
                new Vector3(0f, 0.72f * scale, 0f),
                new Vector3(0.24f, 0.72f, 0.24f) * scale, _bark);

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
            go.isStatic = true;
            return go;
        }

        static void MarkStaticHierarchy(GameObject go)
        {
            if (go == null) return;
            go.isStatic = true;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = true;
        }
    }
}

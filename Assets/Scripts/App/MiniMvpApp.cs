using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using KoG.MiniMvp.Camera;
using KoG.MiniMvp.Network;
using UnityEngine;

namespace KoG.MiniMvp.App
{
    /// <summary>
    /// Self-bootstrapping Mini-MVP client (OnGUI — always clickable in Game view).
    /// </summary>
    public sealed class MiniMvpApp : MonoBehaviour
    {
        enum UiScreen
        {
            Auth,
            Game,
            Result
        }

        [Header("Backend")]
        [SerializeField] string baseUrl = "http://127.0.0.1:3000";

        [Header("Grid")]
        [SerializeField] int gridSize = 15;
        [SerializeField] float cellSize = 1.2f;

        ApiClient _api;
        UiScreen _screen = UiScreen.Auth;
        string _status = "Ready — open Game tab, then press buttons below";
        string _resultMessage = "";

        string _username = "player1";
        string _displayName = "Player One";
        string _email = "player1@test.com";
        string _password = "TestPass123!@#";

        readonly Dictionary<string, GameObject> _buildingViews = new Dictionary<string, GameObject>();
        long _gold;
        int _barbarianCount;
        float _raidStartedAt = -999f;
        string _selectedBuildingId;
        bool _busy;
        Transform _fieldRoot;
        CoCCameraController _cocCamera;
        static Material _sharedFieldMat;
        static Texture2D _sharedCheckerTex;

        GUIStyle _titleStyle;
        GUIStyle _statusStyle;
        GUIStyle _btnStyle;
        bool _stylesReady;

        /// <summary>World center of the playable checkerboard (castle sits here).</summary>
        Vector3 FieldCenter => new Vector3((gridSize - 1) * cellSize * 0.5f, 0f, (gridSize - 1) * cellSize * 0.5f);

        float FieldWorldSize => gridSize * cellSize;

        Vector3 GridToWorld(int gridX, int gridZ) => new Vector3(gridX * cellSize, 0f, gridZ * cellSize);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (FindFirstObjectByType<MiniMvpApp>() != null) return;
            var go = new GameObject("MiniMvpApp");
            go.AddComponent<MiniMvpApp>();
        }

        void Awake()
        {
            _api = new ApiClient(baseUrl);
            // Scene-ga qo'lda tashlangan AI prefab Play'da yotib qoladi / dublikat.
            // Faqat Resources orqali spawn qilingan castle_1 ishlatiladi.
            DestroyLooseSceneCastles();
            BuildGround();
            if (SessionStore.HasSession)
            {
                _screen = UiScreen.Game;
                StartCoroutine(LoadPlayerState());
            }
        }

        static void DestroyLooseSceneCastles()
        {
            // Root-only scan — avoids FindObjectsByType over every Transform in the scene.
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                // Scene-ga qo'lda tashlangan AI prefablar — runtime spawn bilan dublikat.
                // Never destroy runtime-spawned buildings (BuildingMarker).
                if (go.GetComponent<BuildingMarker>() != null) continue;
                var n = go.name;
                if (n == "UzbekCastle" || n.StartsWith("UzbekCastle") ||
                    n == "Castle_L1" || n.StartsWith("Castle_L1") ||
                    n == "Castle" || n.StartsWith("Castle (") ||
                    n == "BaseField_L1" || n == "NatureBorder_L1" ||
                    n == "BaseField" || n == "NatureBorder")
                {
                    Debug.Log("[MiniMvp] Removing loose scene object: " + n);
                    Destroy(go);
                }
            }
        }

        void BuildGround()
        {
            // Clear old ground / field (scene Plane + previous runtime field).
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                if (go.name == "Ground" || go.name == "Plane" || go.name == "BaseField")
                    Destroy(go);
            }

            _fieldRoot = BuildCheckerboardField().transform;

            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<UnityEngine.Camera>();
                cam.tag = "MainCamera";
            }

            _cocCamera = cam.GetComponent<CoCCameraController>();
            if (_cocCamera == null) _cocCamera = cam.gameObject.AddComponent<CoCCameraController>();
            _cocCamera.Configure(FieldCenter, FieldWorldSize, 3f);
            _cocCamera.FocusBase(FieldCenter, FieldWorldSize * 0.78f);
        }

        /// <summary>
        /// One mesh + one checkerboard texture (CoC / Might & Glory grass grid).
        /// Isometric cam (yaw 45°) makes the square look like a diamond.
        /// </summary>
        GameObject BuildCheckerboardField()
        {
            // Prefer AI field mesh when present; nature ring uses Nature2D (Kenney stays in ThirdParty only).
            var fieldPrefab = Resources.Load<GameObject>("Environment/BaseField_L1");
            if (fieldPrefab != null)
            {
                var root = new GameObject("BaseField");
                root.transform.position = FieldCenter;

                var field = Instantiate(fieldPrefab, root.transform);
                field.name = "CheckerGrass";
                field.transform.localPosition = Vector3.zero;
                field.transform.localRotation = Quaternion.identity;
                FitEnvPrefabToSize(field, FieldWorldSize);
                StripColliders(field);

                BuildNatureBorder(root.transform);
                return root;
            }

            var procedural = new GameObject("BaseField");
            procedural.transform.position = FieldCenter;

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "CheckerGrass";
            plane.transform.SetParent(procedural.transform, false);
            // Unity Plane = 10x10; scale to exact playable size.
            var s = FieldWorldSize / 10f;
            plane.transform.localPosition = Vector3.zero;
            plane.transform.localRotation = Quaternion.identity;
            plane.transform.localScale = new Vector3(s, 1f, s);

            // No physics cost on grass.
            var col = plane.GetComponent<Collider>();
            if (col != null) Destroy(col);

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

            BuildNatureBorder(procedural.transform);
            return procedural;
        }

        /// <summary>Scale an AI env prefab so its XZ footprint matches targetSize; plant on Y=0.</summary>
        void FitEnvPrefabToSize(GameObject go, float targetSize)
        {
            go.transform.localScale = Vector3.one;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            var bounds = EncapsulateBounds(renderers);
            var sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            var scale = Mathf.Clamp(targetSize / sizeXZ, 0.05f, 50f);
            go.transform.localScale = Vector3.one * scale;

            bounds = EncapsulateBounds(renderers);
            // Keep centered on parent; sit on ground.
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
                UnityEngine.Object.Destroy(c);
        }

        /// <summary>
        /// CoC / Might & Glory style nature ring around the playable diamond.
        /// Prefers painted 2D isometric sprites; then AI prefab; then primitives.
        /// Kenney NatureKit lives under Assets/ThirdParty only (not Resources) to keep mobile memory down.
        /// </summary>
        void BuildNatureBorder(Transform fieldRoot)
        {
            if (TryBuildNature2DBorder(fieldRoot))
                return;

            var borderPrefab = Resources.Load<GameObject>("Environment/NatureBorder_L1");
            if (borderPrefab != null)
            {
                var border = Instantiate(borderPrefab, fieldRoot);
                border.name = "NatureBorder";
                border.transform.localPosition = Vector3.zero;
                border.transform.localRotation = Quaternion.identity;
                FitEnvPrefabToSize(border, FieldWorldSize * 1.45f);
                StripColliders(border);
                Debug.Log("[MiniMvp] AI NatureBorder fallback");
                return;
            }

            BuildProceduralNatureBorder(fieldRoot);
            Debug.Log("[MiniMvp] Procedural nature border fallback");
        }

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

        bool TryBuildNature2DBorder(Transform fieldRoot)
        {
            EnsureNature2DTextures();
            if (_n2dTrees == null || _n2dTrees.Length == 0) return false;

            var border = new GameObject("NatureBorder");
            border.transform.SetParent(fieldRoot, false);
            border.transform.localPosition = Vector3.zero;

            // One rotation for all props (CoC cam) — no per-prop Camera.main lookup.
            var cam = UnityEngine.Camera.main;
            _n2dBillboardRot = cam != null
                ? Quaternion.LookRotation(cam.transform.forward, Vector3.up)
                : Quaternion.Euler(35f, 45f, 0f);

            var half = FieldWorldSize * 0.5f;
            var radius = half + 1.55f;
            const int slots = 18; // mobile-friendly draw count

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

            // Combine into fewer batches when materials are shared per texture.
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
            UnityEngine.Object.Destroy(tmp);
            return _sharedQuadMesh;
        }

        static Material GetNature2DMaterial(Texture2D tex)
        {
            var id = tex.GetInstanceID();
            if (_n2dMatByTex.TryGetValue(id, out var cached) && cached != null) return cached;

            // Prefer Legacy cutout — reliable alpha kill of sheet leftovers on URP too.
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

        static Shader _urpLit;
        static readonly Dictionary<int, Material> _urpMatCache = new Dictionary<int, Material>();

        /// <summary>Built-in Standard materials render pink under URP — remap colors to URP Lit (cached).</summary>
        static void RemapMaterialsToUrp(GameObject go)
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
                    if (!_urpMatCache.TryGetValue(key, out var mapped))
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
                        mapped.enableInstancing = true;
                        _urpMatCache[key] = mapped;
                    }
                    next[i] = mapped;
                    changed = true;
                }
                if (changed) r.sharedMaterials = next;
            }
        }

        void BuildProceduralNatureBorder(Transform fieldRoot)
        {
            var border = new GameObject("NatureBorder");
            border.transform.SetParent(fieldRoot, false);
            border.transform.localPosition = Vector3.zero;

            EnsureBorderMaterials();

            var half = FieldWorldSize * 0.5f;
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

        static Material _matBark;
        static Material _matCanopy;
        static Material _matCanopyAutumn;
        static Material _matRock;
        static Material _matBush;

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
            if (col != null) UnityEngine.Object.Destroy(col);
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

            // Might & Glory style: light lime vs darker forest green.
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
                    // Soft border so the diamond edge reads clearly.
                    if (cx == 0 || cy == 0 || cx == cells - 1 || cy == cells - 1)
                        c = Color.Lerp(c, edge, 0.45f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(false, true);
            return tex;
        }

        // Framing owned by CoCCameraController (kept helper removed).

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.95f, 0.6f) }
            };
            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _stylesReady = true;
        }

        void OnGUI()
        {
            EnsureStyles();
            var pad = 12f;
            var w = UnityEngine.Screen.width;
            var h = UnityEngine.Screen.height;

            // Always draw a dark bar so controls are visible.
            GUI.Box(new Rect(0, h - 210, w, 210), GUIContent.none);
            GUI.Label(new Rect(pad, 8, w - pad * 2, 32), "Kingdoms of Glory — Mini MVP", _titleStyle);
            GUI.Label(new Rect(pad, 40, w - pad * 2, 50), _status, _statusStyle);

            if (_screen == UiScreen.Auth) DrawAuth(pad, w, h);
            else if (_screen == UiScreen.Game) DrawGame(pad, w, h);
            else DrawResult(pad, w, h);
        }

        void DrawAuth(float pad, float w, float h)
        {
            var y = h - 190;
            GUI.Label(new Rect(pad, y, 120, 24), "Username");
            _username = GUI.TextField(new Rect(pad + 120, y, 220, 24), _username);
            y += 30;
            GUI.Label(new Rect(pad, y, 120, 24), "Display");
            _displayName = GUI.TextField(new Rect(pad + 120, y, 220, 24), _displayName);
            y += 30;
            GUI.Label(new Rect(pad, y, 120, 24), "Email");
            _email = GUI.TextField(new Rect(pad + 120, y, 220, 24), _email);
            y += 30;
            GUI.Label(new Rect(pad, y, 120, 24), "Password");
            _password = GUI.PasswordField(new Rect(pad + 120, y, 220, 24), _password, '*');
            y += 36;

            GUI.enabled = !_busy;
            if (GUI.Button(new Rect(pad, y, 160, 40), "1) Register", _btnStyle))
                StartCoroutine(Register());
            if (GUI.Button(new Rect(pad + 180, y, 160, 40), "Login", _btnStyle))
                StartCoroutine(Login());
            GUI.enabled = true;

            GUI.Label(new Rect(pad + 360, y, 400, 40), "Muhim: Game tabni bosing (Scene emas)");
        }

        void DrawGame(float pad, float w, float h)
        {
            GUI.Label(new Rect(pad, 90, 400, 24), "Gold: " + _gold + "   |   Barbarian: " + _barbarianCount);

            var y = h - 150;
            var bw = 150f;
            var gap = 8f;
            var x = pad;

            GUI.enabled = !_busy;
            if (Btn(ref x, y, bw, gap, "1 Place Mine")) StartCoroutine(PlaceBuilding("gold_mine"));
            if (Btn(ref x, y, bw, gap, "2 Barracks")) StartCoroutine(PlaceBuilding("barracks"));
            if (Btn(ref x, y, bw, gap, "3 Collect")) StartCoroutine(CollectGold());
            if (Btn(ref x, y, bw, gap, "4 Train x10")) StartCoroutine(TrainTroops(10));
            if (Btn(ref x, y, bw, gap, "5 Upgrade")) StartCoroutine(UpgradeSelected());

            y += 50;
            x = pad;
            if (Btn(ref x, y, bw, gap, "6 Start Raid")) StartCoroutine(StartRaid());
            if (Btn(ref x, y, bw + 20, gap, "7 Complete Raid")) StartCoroutine(CompleteRaid());
            if (Btn(ref x, y, bw, gap, "Logout"))
            {
                SessionStore.Clear();
                ClearBuildings();
                _screen = UiScreen.Auth;
                SetStatus("Logged out");
            }
            GUI.enabled = true;

            GUI.Label(new Rect(pad, h - 40, w - pad * 2, 30),
                "Tartib: 1→2→3→4→6, 30 soniya kut, keyin 7. Kub ustiga bosib Upgrade tanlang.");
        }

        void DrawResult(float pad, float w, float h)
        {
            GUI.Label(new Rect(pad, 90, w - pad * 2, 120), _resultMessage, _statusStyle);
            if (GUI.Button(new Rect(pad, h - 80, 200, 44), "OK — back to base", _btnStyle))
            {
                _screen = UiScreen.Game;
                StartCoroutine(LoadPlayerState());
            }
        }

        bool Btn(ref float x, float y, float bw, float gap, string label)
        {
            var clicked = GUI.Button(new Rect(x, y, bw, 40), label, _btnStyle);
            x += bw + gap;
            return clicked;
        }

        IEnumerator Register()
        {
            _busy = true;
            SetStatus("Registering...");
            var body = JsonObject(
                ("username", _username.Trim()),
                ("displayName", string.IsNullOrWhiteSpace(_displayName) ? _username.Trim() : _displayName.Trim()),
                ("email", _email.Trim()),
                ("password", _password)
            );

            yield return _api.PostJson("/api/v1/auth/register", body, null, null, (code, text) =>
            {
                _busy = false;
                if (code < 200 || code >= 300)
                {
                    SetStatus("Register failed: " + ExtractError(text));
                    return;
                }

                var res = JsonUtility.FromJson<AuthResponse>(text);
                SessionStore.Save(res.playerId, res.token, res.refreshToken);
                _screen = UiScreen.Game;
                SetStatus("Registered. Endi Place Mine bosing.");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator Login()
        {
            _busy = true;
            SetStatus("Logging in...");
            var body = JsonObject(
                ("username", _username.Trim()),
                ("password", _password)
            );

            yield return _api.PostJson("/api/v1/auth/login", body, null, null, (code, text) =>
            {
                _busy = false;
                if (code < 200 || code >= 300)
                {
                    SetStatus("Login failed: " + ExtractError(text));
                    return;
                }

                var res = JsonUtility.FromJson<AuthResponse>(text);
                SessionStore.Save(res.playerId, res.token, res.refreshToken);
                if (res.player != null) _gold = res.player.gold;
                _screen = UiScreen.Game;
                SetStatus("Logged in.");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator LoadPlayerState()
        {
            _busy = true;
            SetStatus("Loading base...");
            yield return _api.GetJson("/api/v1/player/state", SessionStore.Token, (code, text) =>
            {
                _busy = false;
                if (code < 200 || code >= 300)
                {
                    SetStatus("State load failed: " + ExtractError(text));
                    return;
                }

                var state = JsonUtility.FromJson<PlayerStateResponse>(text);
                ClearBuildings();
                if (state.player != null) _gold = state.player.gold;

                _barbarianCount = 0;
                if (state.troops != null)
                {
                    foreach (var troop in state.troops)
                    {
                        if (troop.type == "barbarian") _barbarianCount = troop.quantity;
                    }
                }

                var hasMine = false;
                var hasBarracks = false;
                var buildingCount = 0;
                if (state.buildings != null)
                {
                    foreach (var building in state.buildings)
                    {
                        buildingCount++;
                        SpawnBuilding(building.id, building.type, building.level, building.gridX, building.gridZ);
                        if (building.type == "castle") _selectedBuildingId = building.id;
                        if (building.type == "gold_mine") hasMine = true;
                        if (building.type == "barracks") hasBarracks = true;
                    }
                }

                SetStatus(BuildNextStepHint(hasMine, hasBarracks, buildingCount));
                FrameCameraOnBase();
            });
        }

        static string BuildNextStepHint(bool hasMine, bool hasBarracks, int buildingCount)
        {
            if (!hasMine) return "Base OK (" + buildingCount + " bino). Keyingi: Place Mine";
            if (!hasBarracks) return "Mine bor. Keyingi: Place Barracks";
            return "Mine+Barracks bor. Keyingi: Collect → Train → Raid (Place qayta bosilmasin)";
        }

        IEnumerator PlaceBuilding(string buildingType)
        {
            foreach (var view in _buildingViews.Values)
            {
                var marker = view != null ? view.GetComponent<BuildingMarker>() : null;
                if (marker != null && marker.buildingType == buildingType)
                {
                    SetStatus(buildingType + " allaqachon bor — qayta qo'yilmaydi (limit 1). Keyingi qadamga o'ting.");
                    yield break;
                }
            }

            var cell = FindFreeCell();
            if (cell == null)
            {
                SetStatus("No free cell");
                yield break;
            }

            _busy = true;
            SetStatus("Placing " + buildingType + "...");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("buildingType", buildingType),
                ("gridX", cell.Value.x.ToString()),
                ("gridZ", cell.Value.y.ToString())
            );

            yield return _api.PostJson(
                "/api/v1/buildings/place",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        var err = ExtractError(text);
                        if (err.IndexOf("limit", StringComparison.OrdinalIgnoreCase) >= 0)
                            SetStatus(buildingType + " limit: allaqachon 1 ta bor. Collect/Train/Raid qiling.");
                        else
                            SetStatus("Place failed: " + err);
                        StartCoroutine(LoadPlayerState());
                        return;
                    }

                    var res = JsonUtility.FromJson<PlaceBuildingResponse>(text);
                    _gold = res.goldBalance;
                    SpawnBuilding(res.building.id, res.building.type, res.building.level, res.building.gridX, res.building.gridZ);
                    _selectedBuildingId = res.building.id;
                    SetStatus("OK: " + buildingType + " qo'yildi. Gold=" + _gold);
                });
        }

        IEnumerator CollectGold()
        {
            _busy = true;
            SetStatus("Collecting...");
            var body = JsonObject(("playerId", SessionStore.PlayerId));
            yield return _api.PostJson("/api/v1/resources/collect", body, SessionStore.Token, null, (code, text) =>
            {
                _busy = false;
                if (code < 200 || code >= 300)
                {
                    SetStatus("Collect failed: " + ExtractError(text));
                    return;
                }

                SetStatus("Collected. State yangilanmoqda...");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator TrainTroops(int quantity)
        {
            _busy = true;
            SetStatus("Training...");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("troopType", "barbarian"),
                ("quantity", quantity.ToString())
            );

            yield return _api.PostJson("/api/v1/troops/train", body, SessionStore.Token, null, (code, text) =>
            {
                _busy = false;
                if (code < 200 || code >= 300)
                {
                    SetStatus("Train failed: " + ExtractError(text) + " (avval Barracks + gold kerak)");
                    return;
                }

                var res = JsonUtility.FromJson<TrainResponse>(text);
                _barbarianCount += res.trainedQuantity;
                SetStatus("Trained " + res.trainedQuantity + " barbarians. Total=" + _barbarianCount);
            });
        }

        IEnumerator UpgradeSelected()
        {
            if (string.IsNullOrEmpty(_selectedBuildingId))
            {
                SetStatus("Avval kub (bino) ustiga bosing yoki Place qiling");
                yield break;
            }

            _busy = true;
            SetStatus("Upgrading...");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("buildingId", _selectedBuildingId)
            );

            yield return _api.PostJson(
                "/api/v1/buildings/upgrade",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        SetStatus("Upgrade failed: " + ExtractError(text));
                        return;
                    }

                    SetStatus("Upgrade started");
                    StartCoroutine(LoadPlayerState());
                });
        }

        IEnumerator StartRaid()
        {
            _busy = true;
            SetStatus("Starting raid...");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("fortressId", "1")
            );

            yield return _api.PostJson("/api/v1/campaign/start", body, SessionStore.Token, null, (code, text) =>
            {
                _busy = false;
                if (code < 200 || code >= 300)
                {
                    SetStatus("Raid start failed: " + ExtractError(text) + " (askar kerak)");
                    return;
                }

                _raidStartedAt = Time.realtimeSinceStartup;
                SetStatus("Raid boshlandi. 30 soniya kutib, Complete Raid bosing.");
            });
        }

        IEnumerator CompleteRaid()
        {
            var elapsed = Time.realtimeSinceStartup - _raidStartedAt;
            if (elapsed < 30f)
            {
                SetStatus("Hali erta — yana " + Mathf.CeilToInt(30f - elapsed) + " soniya kuting");
                yield break;
            }

            _busy = true;
            SetStatus("Completing raid...");
            var body =
                "{\"playerId\":\"" + SessionStore.PlayerId +
                "\",\"fortressId\":1,\"starsEarned\":1,\"deployTicks\":[{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"}]}";

            yield return _api.PostJson(
                "/api/v1/campaign/complete",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        SetStatus("Raid complete failed: " + ExtractError(text));
                        return;
                    }

                    var res = JsonUtility.FromJson<CampaignCompleteResponse>(text);
                    _gold = res.goldBalance;
                    var stars = res.battleResult != null ? res.battleResult.stars : res.starsEarned;
                    var loot = res.loot != null ? res.loot.gold : 0;
                    _resultMessage = "G'alaba!\nStars: " + stars + "\nLoot: +" + loot + " gold\nBalance: " + _gold;
                    _screen = UiScreen.Result;
                    SetStatus("Raid complete");
                });
        }

        Vector2Int? FindFreeCell()
        {
            var occupied = new HashSet<string>();
            foreach (var view in _buildingViews.Values)
            {
                var marker = view.GetComponent<BuildingMarker>();
                if (marker != null) occupied.Add(marker.gridX + ":" + marker.gridZ);
            }

            for (var z = 0; z < gridSize; z++)
            {
                for (var x = 0; x < gridSize; x++)
                {
                    if (x == 7 && z == 7) continue;
                    var key = x + ":" + z;
                    if (!occupied.Contains(key)) return new Vector2Int(x, z);
                }
            }

            return null;
        }

        void SpawnBuilding(string id, string type, int level, int gridX, int gridZ)
        {
            if (_buildingViews.ContainsKey(id))
            {
                Destroy(_buildingViews[id]);
                _buildingViews.Remove(id);
            }

            GameObject go;
            if (type == "castle")
            {
                // Castle.prefab has Tripo -90° axis fix baked in. CastleMesh.fbx alone lies flat.
                var prefab = Resources.Load<GameObject>("Buildings/Castle");
                if (prefab == null) prefab = Resources.Load<GameObject>("Buildings/CastleMesh");
                if (prefab != null)
                {
                    go = Instantiate(prefab);
                    go.name = type + "_" + level;
                    go.SetActive(true);
                    RemapMaterialsToUrp(go);
                    ApplyCastleAlbedoIfMissing(go);
                    FitBuildingToCell(go, gridX, gridZ, 3.8f, forceUpright: true);
                    OrientCastleTowardCamera(go, gridX, gridZ);
                    EnsureClickCollider(go);

                    var rends = go.GetComponentsInChildren<Renderer>(true);
                    var hasMesh = false;
                    if (rends != null)
                    {
                        foreach (var r in rends)
                        {
                            var mf = r.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null) { hasMesh = true; break; }
                        }
                    }
                    if (!hasMesh)
                    {
                        Debug.LogWarning("[MiniMvp] Castle mesh missing after load — greybox fallback");
                        Destroy(go);
                        go = CreateGreyboxCube(type, level, gridX, gridZ);
                        go.GetComponent<Renderer>().sharedMaterial.color = new Color(0.55f, 0.55f, 0.6f);
                    }
                    else
                    {
                        SetStatus("Castle L1 3D yuklandi");
                        Debug.Log("[MiniMvp] Castle 3D at " + go.transform.position + " scale=" + go.transform.localScale);
                    }
                }
                else
                {
                    Debug.LogWarning("[MiniMvp] Buildings/Castle missing — greybox");
                    go = CreateGreyboxCube(type, level, gridX, gridZ);
                    go.GetComponent<Renderer>().sharedMaterial.color = new Color(0.55f, 0.55f, 0.6f);
                }
            }
            else
            {
                go = CreateGreyboxCube(type, level, gridX, gridZ);
            }

            var marker = go.GetComponent<BuildingMarker>();
            if (marker == null) marker = go.AddComponent<BuildingMarker>();
            marker.buildingId = id;
            marker.buildingType = type;
            marker.level = level;
            marker.gridX = gridX;
            marker.gridZ = gridZ;

            var click = go.GetComponent<BuildingClickRelay>();
            if (click == null) click = go.AddComponent<BuildingClickRelay>();
            click.onClick = () =>
            {
                _selectedBuildingId = id;
                SetStatus("Selected: " + type + " L" + level);
            };

            _buildingViews[id] = go;
        }

        /// <summary>
        /// Scales and plants a prefab so its footprint fits one grid cell and sits on the ground.
        /// forceUpright: always pick the tallest orientation (Tripo FBX often lies flat).
        /// </summary>
        void FitBuildingToCell(GameObject go, int gridX, int gridZ, float targetFootprint, bool forceUpright = false)
        {
            go.transform.localScale = Vector3.one;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                go.transform.position = GridToWorld(gridX, gridZ);
                go.transform.localScale = Vector3.one * 0.8f;
                return;
            }

            foreach (var r in renderers)
            {
                r.enabled = true;
                r.gameObject.SetActive(true);
            }

            // Prefer child-baked -90° (Castle.prefab). Rotate root only when still flat / forced search.
            var upright = EncapsulateBounds(renderers);
            var footprint0 = Mathf.Max(upright.size.x, upright.size.z, 0.01f);
            var alreadyUpright = upright.size.y >= footprint0 * 0.85f;

            // Always score orientations for castle — picks tallest (avoids double -90 on prefab).
            if (forceUpright || !alreadyUpright)
            {
                var candidates = new[]
                {
                    Quaternion.identity,
                    Quaternion.Euler(-90f, 0f, 0f),
                    Quaternion.Euler(-90f, 90f, 0f),
                    Quaternion.Euler(-90f, -90f, 0f),
                    Quaternion.Euler(-90f, 180f, 0f),
                    Quaternion.Euler(90f, 0f, 0f),
                };

                var bestRot = Quaternion.identity;
                var bestScore = float.NegativeInfinity;
                foreach (var rot in candidates)
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, rot);
                    var b = EncapsulateBounds(renderers);
                    var fp = Mathf.Max(b.size.x, b.size.z, 0.01f);
                    var score = b.size.y - fp * 0.15f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRot = rot;
                    }
                }

                go.transform.SetPositionAndRotation(Vector3.zero, bestRot);
                Debug.Log("[MiniMvp] Castle rot=" + bestRot.eulerAngles + " hScore=" + bestScore.ToString("F2") + " wasUpright=" + alreadyUpright);
            }

            var bounds = EncapsulateBounds(renderers);
            var sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            var scale = targetFootprint / sizeXZ;
            scale = Mathf.Clamp(scale, 0.5f, 25f);
            go.transform.localScale = Vector3.one * scale;

            bounds = EncapsulateBounds(renderers);
            sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            if (Mathf.Abs(sizeXZ - targetFootprint) > 0.05f)
            {
                go.transform.localScale *= targetFootprint / sizeXZ;
                bounds = EncapsulateBounds(renderers);
            }

            // Final sanity: if still flatter than tall, force -90° X once more.
            if (forceUpright && bounds.size.y < Mathf.Max(bounds.size.x, bounds.size.z) * 0.75f)
            {
                var e = go.transform.eulerAngles;
                go.transform.rotation = Quaternion.Euler(e.x - 90f, e.y, e.z);
                bounds = EncapsulateBounds(renderers);
                Debug.LogWarning("[MiniMvp] Castle still flat — forced extra -90 X");
            }

            var cell = GridToWorld(gridX, gridZ);
            var delta = cell - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            go.transform.position += delta;
            Debug.Log("[MiniMvp] Castle fit h=" + bounds.size.y.ToString("F2") + " fp=" + Mathf.Max(bounds.size.x, bounds.size.z).ToString("F2") + " scale=" + go.transform.localScale.x.ToString("F2"));
        }

        /// <summary>
        /// CoC-style: castle faces the camera (front door toward viewer), slightly diagonal.
        /// </summary>
        void OrientCastleTowardCamera(GameObject go, int gridX, int gridZ)
        {
            var cam = UnityEngine.Camera.main;
            var cell = GridToWorld(gridX, gridZ);
            var toCam = (cam != null ? cam.transform.position : cell + new Vector3(12f, 20f, -12f)) - cell;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;

            // Face camera; +25° = "sal qiya" 3/4 view (not flat front-on).
            var yaw = Quaternion.LookRotation(toCam.normalized).eulerAngles.y + 25f;
            var e = go.transform.eulerAngles;
            go.transform.rotation = Quaternion.Euler(e.x, yaw, e.z);

            // Re-plant after yaw (mesh bounds shift on XZ).
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;
            var bounds = EncapsulateBounds(renderers);
            var delta = cell - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            go.transform.position += delta;
        }

        static void ApplyCastleAlbedoIfMissing(GameObject go)
        {
            var albedo = Resources.Load<Texture2D>("Buildings/CastleMeshTextures/Color_5f773bf5-b6c0-45d4-bd53-2e31d222e9f7");
            if (albedo == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null) continue;
                var next = new Material[shared.Length];
                var changed = false;
                for (var i = 0; i < shared.Length; i++)
                {
                    var m = shared[i];
                    if (m == null) { next[i] = null; continue; }
                    if (m.mainTexture != null)
                    {
                        next[i] = m;
                        continue;
                    }
                    // Clone once into URP cache path via Remap — here just assign albedo on a copy.
                    var copy = new Material(m);
                    copy.mainTexture = albedo;
                    if (copy.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", albedo);
                    if (copy.HasProperty("_MainTex")) copy.SetTexture("_MainTex", albedo);
                    next[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = next;
            }
        }

        static Bounds EncapsulateBounds(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        void FrameCameraOnBase()
        {
            var focus = FieldCenter;

            foreach (var view in _buildingViews.Values)
            {
                if (view == null) continue;
                var marker = view.GetComponent<BuildingMarker>();
                if (marker == null || marker.buildingType != "castle") continue;
                var renderers = view.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0) break;
                var b = EncapsulateBounds(renderers);
                focus = new Vector3(b.center.x, 0f, b.center.z);
                break;
            }

            if (_fieldRoot != null)
                _fieldRoot.position = FieldCenter;

            if (_cocCamera == null)
            {
                var cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    _cocCamera = cam.GetComponent<CoCCameraController>();
                    if (_cocCamera == null) _cocCamera = cam.gameObject.AddComponent<CoCCameraController>();
                    _cocCamera.Configure(FieldCenter, FieldWorldSize, 3f);
                }
            }

            if (_cocCamera != null)
                _cocCamera.FocusBase(focus, FieldWorldSize * 0.78f);
        }

        static readonly Dictionary<string, Material> _greyboxMats = new Dictionary<string, Material>();

        static Material GetGreyboxMaterial(string type)
        {
            if (_greyboxMats.TryGetValue(type, out var existing) && existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var color = type switch
            {
                "castle" => new Color(0.75f, 0.6f, 0.2f),
                "gold_mine" => new Color(0.95f, 0.8f, 0.2f),
                "barracks" => new Color(0.4f, 0.55f, 0.9f),
                _ => Color.gray
            };

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.enableInstancing = true;
            _greyboxMats[type] = mat;
            return mat;
        }

        GameObject CreateGreyboxCube(string type, int level, int gridX, int gridZ)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = type + "_" + level;
            var footprint = type == "barracks" ? 1.4f : 1.2f;
            var height = type == "barracks" ? 1.4f : type == "castle" ? 2.2f : 1.1f;
            go.transform.localScale = new Vector3(footprint, height, footprint);
            var p = GridToWorld(gridX, gridZ);
            go.transform.position = new Vector3(p.x, height * 0.5f, p.z);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = GetGreyboxMaterial(type);
            return go;
        }

        static void EnsureClickCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var box = go.AddComponent<BoxCollider>();
            if (renderers != null && renderers.Length > 0)
            {
                var b = EncapsulateBounds(renderers);
                var localCenter = go.transform.InverseTransformPoint(b.center);
                var lossy = go.transform.lossyScale;
                box.center = localCenter;
                box.size = new Vector3(
                    Mathf.Max(b.size.x / Mathf.Max(lossy.x, 0.001f), 0.5f),
                    Mathf.Max(b.size.y / Mathf.Max(lossy.y, 0.001f), 0.5f),
                    Mathf.Max(b.size.z / Mathf.Max(lossy.z, 0.001f), 0.5f));
            }
            else
            {
                box.size = new Vector3(2.5f, 2.5f, 2.5f);
                box.center = new Vector3(0f, 1.2f, 0f);
            }
        }

        void ClearBuildings()
        {
            foreach (var view in _buildingViews.Values)
            {
                if (view != null) Destroy(view);
            }
            _buildingViews.Clear();
        }

        void SetStatus(string message)
        {
            _status = message;
            Debug.Log("[MiniMvp] " + message);
        }

        static string ExtractError(string text)
        {
            if (string.IsNullOrEmpty(text)) return "unknown error";
            try
            {
                var err = JsonUtility.FromJson<ApiError>(text);
                if (!string.IsNullOrEmpty(err.message)) return err.message;
                if (!string.IsNullOrEmpty(err.error)) return err.error;
            }
            catch
            {
                // ignore
            }

            return text.Length > 180 ? text.Substring(0, 180) : text;
        }

        static string JsonObject(params (string key, string value)[] pairs)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            for (var i = 0; i < pairs.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(pairs[i].key).Append('"').Append(':');
                if (int.TryParse(pairs[i].value, out _) || long.TryParse(pairs[i].value, out _))
                    sb.Append(pairs[i].value);
                else
                    sb.Append('"').Append(Escape(pairs[i].value)).Append('"');
            }

            sb.Append('}');
            return sb.ToString();
        }

        static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    public sealed class BuildingMarker : MonoBehaviour
    {
        public string buildingId;
        public string buildingType;
        public int level;
        public int gridX;
        public int gridZ;
    }

    public sealed class BuildingClickRelay : MonoBehaviour
    {
        public System.Action onClick;

        void OnMouseDown()
        {
            onClick?.Invoke();
        }
    }
}

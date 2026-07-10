using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Recreates the uploaded CoC / Might &amp; Glory starter village reference.
    /// Uses authored BaseField_L1 + NatureBorder_L1 as the single visual source of truth
    /// (same proportions, empty center, border composition). Falls back to procedural nature only if prefabs missing.
    /// </summary>
    public static class FieldVisualBuilder
    {
        /// <summary>Authored BaseField_L1 mesh half-extent (AABB). Full playable size = 22.</summary>
        public const float AuthoredFieldSize = 22f;

        static Material _sharedFieldMat;
        static Texture2D _sharedCheckerTex;

        public static Transform Build(int gridSize, float cellSize)
        {
            ClearStaleGround();
            var fieldWorldSize = gridSize * cellSize;
            var fieldCenter = new Vector3((gridSize - 1) * cellSize * 0.5f, 0f, (gridSize - 1) * cellSize * 0.5f);
            return BuildReferenceVillage(gridSize, cellSize, fieldWorldSize, fieldCenter).transform;
        }

        static void ClearStaleGround()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                if (go.name == "Ground" || go.name == "Plane" || go.name == "BaseField" ||
                    go.name == "Village" || go.name == "ReferenceVillage")
                    Object.Destroy(go);
            }
        }

        static GameObject BuildReferenceVillage(int gridSize, float cellSize, float fieldWorldSize, Vector3 fieldCenter)
        {
            // Hierarchy matches production env layout.
            var village = new GameObject("Village");
            village.transform.position = Vector3.zero;

            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(village.transform, false);

            var environment = new GameObject("Environment");
            environment.transform.SetParent(village.transform, false);

            var nature = new GameObject("Nature");
            nature.transform.SetParent(environment.transform, false);

            var decorations = new GameObject("Decorations");
            decorations.transform.SetParent(environment.transform, false);

            var gameplay = new GameObject("Gameplay");
            gameplay.transform.SetParent(village.transform, false);

            var fieldRoot = new GameObject("BaseField");
            fieldRoot.transform.SetParent(terrain.transform, false);
            fieldRoot.transform.position = fieldCenter;

            var fieldPrefab = Resources.Load<GameObject>("Environment/BaseField_L1");
            var borderPrefab = Resources.Load<GameObject>("Environment/NatureBorder_L1");

            if (fieldPrefab != null)
            {
                var field = Object.Instantiate(fieldPrefab, fieldRoot.transform);
                field.name = "CheckerGrass";
                field.transform.localPosition = Vector3.zero;
                field.transform.localRotation = Quaternion.identity;
                // Keep authored proportions — scale uniformly to measured playable size.
                // Authored mesh is 22×22 — scale to measured playable size, keep centered.
                ApplyUniformScale(field, AuthoredFieldSize, fieldWorldSize);
                StripColliders(field);
                MarkStatic(field);
                UrpMaterialUtil.RemapToUrp(field);
                SoftFieldLook.Apply(fieldRoot.transform, gridSize, fieldWorldSize);
            }
            else
            {
                BuildProceduralChecker(fieldRoot.transform, gridSize, fieldWorldSize);
                SoftFieldLook.Apply(fieldRoot.transform, gridSize, fieldWorldSize);
            }

            if (borderPrefab != null)
            {
                // CRITICAL: same world center + same scale factor as BaseField (reference 1:1).
                // Do NOT re-center on nature AABB — that would shift the authored composition.
                var border = Object.Instantiate(borderPrefab, nature.transform);
                border.name = "NatureBorder_L1";
                border.transform.position = fieldCenter;
                border.transform.localRotation = Quaternion.identity;
                ApplyUniformScale(border, AuthoredFieldSize, fieldWorldSize);
                StripColliders(border);
                MarkStatic(border);
                UrpMaterialUtil.RemapToUrp(border);
                OptimizeNatureRenderers(border);
                // In-place quality only — never relocate / regenerate border props.
                NatureVisualPolish.Apply(border, fieldWorldSize, fieldCenter);
            }
            else
            {
                // Fallback only — preserve builder placement; polish materials in place.
                NatureBorderBuilder.Build(fieldRoot.transform, fieldWorldSize);
                var fallback = fieldRoot.transform.Find("NatureBorder");
                if (fallback != null)
                    NatureVisualPolish.Apply(fallback.gameObject, fieldWorldSize, fieldCenter);
            }

            // Decorations reserved for future authored props — do not procedural-redesign here.
            _ = decorations;

            BuildingGrid.Build(gameplay.transform, gridSize, cellSize, fieldCenter);
            // Camera/Sun parenting happens after MiniMvpApp creates them — see FinalizeHierarchy.

            Debug.Log("[MiniMvp] Reference village built — field=" + fieldWorldSize +
                      " grid=" + gridSize + "x" + gridSize + " cell=" + cellSize +
                      " border=" + (borderPrefab != null));

            // Return BaseField transform for MiniMvpApp field-root compatibility.
            return fieldRoot;
        }

        /// <summary>Call after camera + KoG_Sun exist — clean Village hierarchy.</summary>
        public static void FinalizeHierarchy()
        {
            var village = GameObject.Find("Village");
            if (village == null) return;

            var camNode = village.transform.Find("Camera");
            if (camNode == null)
            {
                var go = new GameObject("Camera");
                go.transform.SetParent(village.transform, false);
                camNode = go.transform;
            }

            var lightNode = village.transform.Find("Lighting");
            if (lightNode == null)
            {
                var go = new GameObject("Lighting");
                go.transform.SetParent(village.transform, false);
                lightNode = go.transform;
            }

            var cam = UnityEngine.Camera.main;
            if (cam != null && (cam.transform.parent == null || cam.transform.parent.name != "Camera"))
                cam.transform.SetParent(camNode, true);

            var sun = GameObject.Find("KoG_Sun");
            if (sun != null && (sun.transform.parent == null || sun.transform.parent.name != "Lighting"))
                sun.transform.SetParent(lightNode, true);
        }

        static void BuildProceduralChecker(Transform parent, int gridSize, float fieldWorldSize)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "CheckerGrass";
            plane.transform.SetParent(parent, false);
            var s = fieldWorldSize / 10f;
            plane.transform.localPosition = Vector3.zero;
            plane.transform.localRotation = Quaternion.identity;
            plane.transform.localScale = new Vector3(s, 1f, s);

            var col = plane.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            if (_sharedFieldMat == null)
            {
                _sharedCheckerTex = CreateCheckerTexture(gridSize, 8);
                var shader = UrpMaterialUtil.FindLitShader();
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
        }

        /// <summary>
        /// Uniform scale from authored playable size → runtime field size.
        /// Preserves reference composition (no AABB re-centering).
        /// </summary>
        static void ApplyUniformScale(GameObject go, float authoredSize, float targetSize)
        {
            go.transform.localScale = Vector3.one;
            var scale = Mathf.Clamp(targetSize / Mathf.Max(authoredSize, 0.01f), 0.05f, 50f);
            go.transform.localScale = Vector3.one * scale;
        }

        static void OptimizeNatureRenderers(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                r.allowOcclusionWhenDynamic = true;
                UrpMaterialUtil.ForceMobileStylized(r.gameObject);
            }
        }

        static void MarkStatic(GameObject go)
        {
            go.isStatic = true;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = true;
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);
        }

        static Texture2D CreateCheckerTexture(int cells, int pixelsPerCell)
        {
            var size = cells * pixelsPerCell;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "FieldChecker";

            // Match reference: soft lime checker (~10% contrast), not harsh.
            var light = new Color(0.62f, 0.86f, 0.40f);
            var dark = new Color(0.56f, 0.80f, 0.36f);
            var edge = new Color(0.34f, 0.50f, 0.24f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x / pixelsPerCell;
                    var cy = y / pixelsPerCell;
                    var c = ((cx + cy) & 1) == 0 ? light : dark;
                    if (cx == 0 || cy == 0 || cx == cells - 1 || cy == cells - 1)
                        c = Color.Lerp(c, edge, 0.4f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(false, true);
            return tex;
        }
    }
}

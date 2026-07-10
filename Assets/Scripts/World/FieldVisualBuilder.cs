using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Checkerboard base field. Nature ring delegated to NatureBorderBuilder.
    /// </summary>
    public static class FieldVisualBuilder
    {
        static Material _sharedFieldMat;
        static Texture2D _sharedCheckerTex;

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

                NatureBorderBuilder.Build(root.transform, fieldWorldSize);
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

            NatureBorderBuilder.Build(procedural.transform, fieldWorldSize);
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

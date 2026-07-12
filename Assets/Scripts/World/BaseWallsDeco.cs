using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Soft-test perimeter walls (P2-walls): thin greybox segments framing the field.
    /// Presentation-only — no combat HP / placement blocking.
    /// </summary>
    public static class BaseWallsDeco
    {
        static Material _wallMat;

        public static void Build(Transform decorationsRoot, Vector3 fieldCenter, float fieldWorldSize)
        {
            if (decorationsRoot == null) return;

            var existing = decorationsRoot.Find("PerimeterWalls");
            if (existing != null) Object.Destroy(existing.gameObject);

            var root = new GameObject("PerimeterWalls");
            root.transform.SetParent(decorationsRoot, false);
            root.isStatic = true;

            // Slightly inside the nature border so walls read as village edge.
            var half = fieldWorldSize * 0.42f;
            const float h = 0.55f;
            const float thick = 0.18f;
            const float segment = 2.2f;

            // Four sides — gap at mid-south for a visual “gate”.
            PlaceSide(root.transform, fieldCenter, half, h, thick, segment, side: 0, gateGap: false); // N
            PlaceSide(root.transform, fieldCenter, half, h, thick, segment, side: 1, gateGap: false); // E
            PlaceSide(root.transform, fieldCenter, half, h, thick, segment, side: 2, gateGap: true);  // S gate
            PlaceSide(root.transform, fieldCenter, half, h, thick, segment, side: 3, gateGap: false); // W

            // Corner posts
            var corners = new[]
            {
                new Vector3(-half, 0f, -half),
                new Vector3(half, 0f, -half),
                new Vector3(-half, 0f, half),
                new Vector3(half, 0f, half),
            };
            for (var i = 0; i < corners.Length; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Corner_" + i;
                post.transform.SetParent(root.transform, false);
                post.transform.position = fieldCenter + corners[i] + Vector3.up * (h * 0.65f);
                post.transform.localScale = new Vector3(0.32f, h * 1.3f, 0.32f);
                Apply(post);
            }
        }

        static void PlaceSide(Transform parent, Vector3 center, float half, float h, float thick, float segment, int side, bool gateGap)
        {
            var length = half * 2f;
            var count = Mathf.Max(3, Mathf.FloorToInt(length / segment));
            for (var i = 0; i < count; i++)
            {
                var t = (i + 0.5f) / count;
                if (gateGap && t > 0.42f && t < 0.58f) continue;

                Vector3 pos;
                Vector3 scale;
                switch (side)
                {
                    case 0: // North (+Z)
                        pos = new Vector3(Mathf.Lerp(-half, half, t), h * 0.5f, half);
                        scale = new Vector3(segment * 0.92f, h, thick);
                        break;
                    case 1: // East (+X)
                        pos = new Vector3(half, h * 0.5f, Mathf.Lerp(-half, half, t));
                        scale = new Vector3(thick, h, segment * 0.92f);
                        break;
                    case 2: // South (−Z)
                        pos = new Vector3(Mathf.Lerp(-half, half, t), h * 0.5f, -half);
                        scale = new Vector3(segment * 0.92f, h, thick);
                        break;
                    default: // West (−X)
                        pos = new Vector3(-half, h * 0.5f, Mathf.Lerp(-half, half, t));
                        scale = new Vector3(thick, h, segment * 0.92f);
                        break;
                }

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Wall_" + side + "_" + i;
                go.transform.SetParent(parent, false);
                go.transform.position = center + pos;
                go.transform.localScale = scale;
                Apply(go);
            }
        }

        static void Apply(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = WallMat();
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = true;
            }
            go.isStatic = true;
        }

        static Material WallMat()
        {
            if (_wallMat != null) return _wallMat;
            var shader = UrpMaterialUtil.FindLitShader() ?? Shader.Find("Standard");
            _wallMat = new Material(shader);
            var c = new Color(0.62f, 0.58f, 0.52f);
            if (_wallMat.HasProperty("_BaseColor")) _wallMat.SetColor("_BaseColor", c);
            if (_wallMat.HasProperty("_Color")) _wallMat.SetColor("_Color", c);
            UrpMaterialUtil.ApplyMobileSurface(_wallMat);
            _wallMat.enableInstancing = true;
            return _wallMat;
        }
    }
}

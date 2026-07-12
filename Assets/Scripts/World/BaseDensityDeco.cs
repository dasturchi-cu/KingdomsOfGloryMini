using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Soft-test base density (P2-density): thin greybox props near the castle ring.
    /// Presentation-only — does not occupy BuildingGrid cells or change gameplay.
    /// </summary>
    public static class BaseDensityDeco
    {
        static Material _wood;
        static Material _stone;
        static Material _foliage;

        public static void Build(Transform decorationsRoot, Vector3 fieldCenter, float fieldWorldSize)
        {
            if (decorationsRoot == null) return;

            for (var i = decorationsRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(decorationsRoot.GetChild(i).gameObject);

            var ring = Mathf.Clamp(fieldWorldSize * 0.22f, 3.2f, 6.5f);
            // 12 props on an irregular ring — enough to read as “inhabited”, cheap on mobile.
            for (var i = 0; i < 12; i++)
            {
                var ang = (i / 12f) * Mathf.PI * 2f + (i % 3) * 0.11f;
                var radius = ring * (0.85f + (i % 4) * 0.06f);
                var pos = fieldCenter + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
                SpawnProp(decorationsRoot, i, pos);
            }

            // Path accents toward north edge of ring (visual anchor, not gameplay).
            for (var i = 0; i < 5; i++)
            {
                var pos = fieldCenter + new Vector3((i - 2) * 0.55f, 0.02f, ring * 0.55f);
                SpawnFlatStone(decorationsRoot, "Path_" + i, pos, 0.35f + (i % 2) * 0.08f);
            }
        }

        static void SpawnProp(Transform parent, int seed, Vector3 worldPos)
        {
            var kind = seed % 4;
            switch (kind)
            {
                case 0:
                    SpawnBox(parent, "Crate_" + seed, worldPos, new Vector3(0.42f, 0.36f, 0.42f), WoodMat());
                    break;
                case 1:
                    SpawnCylinder(parent, "Barrel_" + seed, worldPos, new Vector3(0.32f, 0.28f, 0.32f), WoodMat());
                    break;
                case 2:
                    SpawnBox(parent, "Rock_" + seed, worldPos, new Vector3(0.5f, 0.22f, 0.4f), StoneMat());
                    break;
                default:
                    SpawnBush(parent, "Bush_" + seed, worldPos);
                    break;
            }
        }

        static void SpawnBox(Transform parent, string name, Vector3 worldPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos + Vector3.up * (scale.y * 0.5f);
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(0f, worldPos.x * 17.3f + worldPos.z * 9.1f, 0f);
            ApplyMat(go, mat);
            StripCollider(go);
            go.isStatic = true;
        }

        static void SpawnCylinder(Transform parent, string name, Vector3 worldPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos + Vector3.up * (scale.y * 0.5f);
            go.transform.localScale = scale;
            ApplyMat(go, mat);
            StripCollider(go);
            go.isStatic = true;
        }

        static void SpawnFlatStone(Transform parent, string name, Vector3 worldPos, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = new Vector3(size, 0.04f, size * 0.7f);
            ApplyMat(go, StoneMat());
            StripCollider(go);
            go.isStatic = true;
        }

        static void SpawnBush(Transform parent, string name, Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos + Vector3.up * 0.22f;
            go.transform.localScale = new Vector3(0.55f, 0.4f, 0.55f);
            ApplyMat(go, FoliageMat());
            StripCollider(go);
            go.isStatic = true;
        }

        static void ApplyMat(GameObject go, Material mat)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = true;
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        static Material WoodMat()
        {
            if (_wood != null) return _wood;
            _wood = MakeMat(new Color(0.45f, 0.30f, 0.18f));
            return _wood;
        }

        static Material StoneMat()
        {
            if (_stone != null) return _stone;
            _stone = MakeMat(new Color(0.55f, 0.54f, 0.50f));
            return _stone;
        }

        static Material FoliageMat()
        {
            if (_foliage != null) return _foliage;
            _foliage = MakeMat(new Color(0.28f, 0.48f, 0.22f));
            return _foliage;
        }

        static Material MakeMat(Color c)
        {
            var shader = UrpMaterialUtil.FindLitShader() ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            UrpMaterialUtil.ApplyMobileSurface(m);
            m.enableInstancing = true;
            return m;
        }
    }
}

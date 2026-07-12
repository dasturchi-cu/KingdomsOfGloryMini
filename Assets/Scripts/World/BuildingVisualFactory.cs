using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Soft-GO compact 2.5D art pack (Built-in Standard). Palette matches Castle_L1_Reference:
    /// grey stone + cobalt roofs + cream keep + red pennants. Swap later via BuildingArtCatalog prefabs.
    /// </summary>
    public static class BuildingVisualFactory
    {
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        public static GameObject Create(string type, int level, Vector3 worldPos)
        {
            if (type == "castle") return CreateCastle(level, worldPos);
            if (type == "gold_mine") return CreateGoldMine(level, worldPos);
            if (type == "barracks") return CreateBarracks(level, worldPos);
            return CreateGreybox(type, level, worldPos);
        }

        public static GameObject CreateGreybox(string type, int level, Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = type + "_" + level;
            var footprint = BuildingArtCatalog.Footprint(type);
            var height = type == "castle" ? 2.0f : 1.1f;
            go.transform.localScale = new Vector3(footprint * 0.85f, height, footprint * 0.85f);
            go.transform.position = new Vector3(worldPos.x, height * 0.5f, worldPos.z);
            go.GetComponent<Renderer>().sharedMaterial = Mat("grey_" + type, new Color(0.55f, 0.55f, 0.58f));
            return go;
        }

        /// <summary>Compact keep: stone ring, twin blue towers, cream hall, red flags (bible silhouette).</summary>
        public static GameObject CreateCastle(int level, Vector3 worldPos)
        {
            var root = new GameObject("castle_" + level);
            root.transform.position = worldPos;

            AddPrim(root.transform, PrimitiveType.Cylinder, "Shadow",
                new Vector3(0f, 0.02f, 0f), new Vector3(2.35f, 0.02f, 2.35f), MatShadow());

            // Courtyard pad
            AddPrim(root.transform, PrimitiveType.Cube, "Yard",
                new Vector3(0f, 0.06f, 0f), new Vector3(1.9f, 0.1f, 1.9f), MatGrass());

            // Perimeter walls (low, compact)
            AddWallRing(root.transform, 2.05f, 0.55f, 0.18f);

            // Front corner towers + cobalt cones + pennants
            AddTower(root.transform, new Vector3(-0.78f, 0f, 0.78f), "TowerL");
            AddTower(root.transform, new Vector3(0.78f, 0f, 0.78f), "TowerR");

            // Gate + steps
            AddPrim(root.transform, PrimitiveType.Cube, "GateArch",
                new Vector3(0f, 0.55f, 1.02f), new Vector3(0.55f, 0.7f, 0.14f), MatStoneDark());
            AddPrim(root.transform, PrimitiveType.Cube, "Door",
                new Vector3(0f, 0.48f, 1.08f), new Vector3(0.36f, 0.55f, 0.08f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "Step1",
                new Vector3(0f, 0.1f, 1.18f), new Vector3(0.55f, 0.1f, 0.22f), MatStone());
            AddPrim(root.transform, PrimitiveType.Cube, "Step2",
                new Vector3(0f, 0.18f, 1.28f), new Vector3(0.45f, 0.08f, 0.16f), MatStone());

            // Cream keep (back-right of yard)
            AddPrim(root.transform, PrimitiveType.Cube, "Keep",
                new Vector3(0.15f, 0.85f, -0.35f), new Vector3(1.05f, 1.15f, 0.85f), MatCream());
            AddPrim(root.transform, PrimitiveType.Cube, "KeepTrim",
                new Vector3(0.15f, 0.85f, -0.35f), new Vector3(1.12f, 0.12f, 0.92f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "KeepRoof",
                new Vector3(0.15f, 1.55f, -0.35f), new Vector3(1.2f, 0.28f, 1.0f), MatRoof());
            AddPrim(root.transform, PrimitiveType.Cube, "KeepRidge",
                new Vector3(0.15f, 1.78f, -0.35f), new Vector3(0.22f, 0.22f, 0.95f), MatRoof());
            AddPrim(root.transform, PrimitiveType.Cube, "KeepWindow",
                new Vector3(0.15f, 0.95f, 0.1f), new Vector3(0.28f, 0.28f, 0.06f), MatWindow());

            // Yard prop
            AddPrim(root.transform, PrimitiveType.Cube, "Crate",
                new Vector3(-0.35f, 0.22f, 0.05f), new Vector3(0.28f, 0.26f, 0.28f), MatWood());

            AddLevelPlate(root.transform, level, new Vector3(0f, 0.02f, -1.15f));
            return root;
        }

        static void AddWallRing(Transform parent, float size, float height, float thickness)
        {
            var half = size * 0.5f;
            var mid = height * 0.5f + 0.1f;
            // Front has gate gap — split left/right
            AddPrim(parent, PrimitiveType.Cube, "WallN_L",
                new Vector3(-0.55f, mid, half), new Vector3(0.85f, height, thickness), MatStone());
            AddPrim(parent, PrimitiveType.Cube, "WallN_R",
                new Vector3(0.55f, mid, half), new Vector3(0.85f, height, thickness), MatStone());
            AddPrim(parent, PrimitiveType.Cube, "WallS",
                new Vector3(0f, mid, -half), new Vector3(size, height, thickness), MatStone());
            AddPrim(parent, PrimitiveType.Cube, "WallW",
                new Vector3(-half, mid, 0f), new Vector3(thickness, height, size), MatStone());
            AddPrim(parent, PrimitiveType.Cube, "WallE",
                new Vector3(half, mid, 0f), new Vector3(thickness, height, size), MatStone());

            // Crenellations (front corners only — light)
            AddPrim(parent, PrimitiveType.Cube, "MerlonL",
                new Vector3(-0.95f, height + 0.22f, half), new Vector3(0.22f, 0.22f, 0.2f), MatStoneDark());
            AddPrim(parent, PrimitiveType.Cube, "MerlonR",
                new Vector3(0.95f, height + 0.22f, half), new Vector3(0.22f, 0.22f, 0.2f), MatStoneDark());
            AddPrim(parent, PrimitiveType.Cube, "BoulderL",
                new Vector3(-0.95f, 0.22f, half + 0.05f), new Vector3(0.35f, 0.32f, 0.28f), MatStoneDark());
            AddPrim(parent, PrimitiveType.Cube, "BoulderR",
                new Vector3(0.95f, 0.22f, half + 0.05f), new Vector3(0.35f, 0.32f, 0.28f), MatStoneDark());
        }

        static void AddTower(Transform parent, Vector3 basePos, string prefix)
        {
            AddPrim(parent, PrimitiveType.Cube, prefix + "Body",
                basePos + new Vector3(0f, 0.85f, 0f), new Vector3(0.55f, 1.35f, 0.55f), MatStone());
            AddPrim(parent, PrimitiveType.Cube, prefix + "Wood",
                basePos + new Vector3(0f, 1.45f, 0f), new Vector3(0.62f, 0.1f, 0.62f), MatWood());
            // Pointed cobalt roof (scaled cube diamond-ish)
            AddPrim(parent, PrimitiveType.Cube, prefix + "Roof",
                basePos + new Vector3(0f, 1.85f, 0f), new Vector3(0.72f, 0.45f, 0.72f), MatRoof());
            AddPrim(parent, PrimitiveType.Cube, prefix + "Peak",
                basePos + new Vector3(0f, 2.2f, 0f), new Vector3(0.28f, 0.35f, 0.28f), MatRoof());
            AddPrim(parent, PrimitiveType.Sphere, prefix + "Finial",
                basePos + new Vector3(0f, 2.42f, 0f), new Vector3(0.12f, 0.12f, 0.12f), MatGold());
            AddPrim(parent, PrimitiveType.Cube, prefix + "Flag",
                basePos + new Vector3(0.18f, 2.55f, 0f), new Vector3(0.28f, 0.18f, 0.04f), MatBanner());
        }

        static GameObject CreateGoldMine(int level, Vector3 worldPos)
        {
            var root = new GameObject("gold_mine_" + level);
            root.transform.position = worldPos;

            AddPrim(root.transform, PrimitiveType.Cylinder, "Shadow",
                new Vector3(0f, 0.02f, 0f), new Vector3(1.4f, 0.02f, 1.4f), MatShadow());
            AddPrim(root.transform, PrimitiveType.Cube, "Pad",
                new Vector3(0f, 0.1f, 0f), new Vector3(1.2f, 0.18f, 1.2f), MatStone());
            AddPrim(root.transform, PrimitiveType.Cube, "BeamL",
                new Vector3(-0.36f, 0.65f, 0f), new Vector3(0.12f, 1.05f, 0.12f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "BeamR",
                new Vector3(0.36f, 0.65f, 0f), new Vector3(0.12f, 1.05f, 0.12f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "Cross",
                new Vector3(0f, 1.22f, 0f), new Vector3(0.95f, 0.1f, 0.1f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "Roof",
                new Vector3(0f, 1.4f, 0f), new Vector3(0.7f, 0.16f, 0.7f), MatRoof());
            AddPrim(root.transform, PrimitiveType.Cube, "OreA",
                new Vector3(0.06f, 0.36f, 0.2f), new Vector3(0.5f, 0.38f, 0.45f), MatGold());
            AddPrim(root.transform, PrimitiveType.Sphere, "OreB",
                new Vector3(-0.2f, 0.38f, -0.16f), new Vector3(0.4f, 0.32f, 0.4f), MatGold());
            AddPrim(root.transform, PrimitiveType.Sphere, "OreC",
                new Vector3(0.26f, 0.3f, -0.05f), new Vector3(0.26f, 0.22f, 0.26f), Mat("goldBright", new Color(1f, 0.88f, 0.35f)));
            AddPrim(root.transform, PrimitiveType.Cube, "Crate",
                new Vector3(0.36f, 0.24f, -0.36f), new Vector3(0.28f, 0.26f, 0.28f), MatWood());
            AddLevelPlate(root.transform, level, new Vector3(0f, 0.02f, -0.72f));
            return root;
        }

        static GameObject CreateBarracks(int level, Vector3 worldPos)
        {
            var root = new GameObject("barracks_" + level);
            root.transform.position = worldPos;

            AddPrim(root.transform, PrimitiveType.Cylinder, "Shadow",
                new Vector3(0f, 0.02f, 0f), new Vector3(2.05f, 0.02f, 1.75f), MatShadow());
            AddPrim(root.transform, PrimitiveType.Cube, "Base",
                new Vector3(0f, 0.12f, 0f), new Vector3(1.85f, 0.24f, 1.55f), MatStone());
            AddPrim(root.transform, PrimitiveType.Cube, "Walls",
                new Vector3(0f, 0.8f, 0f), new Vector3(1.65f, 1.05f, 1.3f), MatCream());
            AddPrim(root.transform, PrimitiveType.Cube, "Roof",
                new Vector3(0f, 1.5f, 0f), new Vector3(1.9f, 0.32f, 1.5f), MatRoof());
            AddPrim(root.transform, PrimitiveType.Cube, "Ridge",
                new Vector3(0f, 1.78f, 0f), new Vector3(0.28f, 0.26f, 1.4f), MatRoof());
            AddPrim(root.transform, PrimitiveType.Cube, "Door",
                new Vector3(0f, 0.6f, 0.68f), new Vector3(0.42f, 0.72f, 0.08f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "WindowL",
                new Vector3(-0.5f, 0.85f, 0.66f), new Vector3(0.24f, 0.24f, 0.06f), MatWindow());
            AddPrim(root.transform, PrimitiveType.Cube, "WindowR",
                new Vector3(0.5f, 0.85f, 0.66f), new Vector3(0.24f, 0.24f, 0.06f), MatWindow());
            AddPrim(root.transform, PrimitiveType.Cylinder, "Pole",
                new Vector3(0.78f, 1.2f, 0.55f), new Vector3(0.08f, 0.95f, 0.08f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "Banner",
                new Vector3(0.78f, 1.65f, 0.7f), new Vector3(0.38f, 0.32f, 0.05f), MatBanner());
            AddLevelPlate(root.transform, level, new Vector3(0f, 0.02f, -0.95f));
            return root;
        }

        static void AddLevelPlate(Transform parent, int level, Vector3 localPos)
        {
            AddPrim(parent, PrimitiveType.Cube, "LvlPlate",
                localPos + Vector3.up * 0.08f, new Vector3(0.5f, 0.1f, 0.2f),
                Mat("lvlPlate", new Color(0.15f, 0.16f, 0.2f)));
            var badge = new GameObject("LvlText");
            badge.transform.SetParent(parent, false);
            badge.transform.localPosition = localPos + new Vector3(0f, 0.2f, 0f);
            var tm = badge.AddComponent<TextMesh>();
            tm.text = "L" + Mathf.Max(level, 1);
            tm.fontSize = 64;
            tm.characterSize = 0.032f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.9f, 0.4f);
            tm.fontStyle = FontStyle.Bold;
            badge.transform.rotation = Quaternion.Euler(55f, 45f, 0f);
        }

        static Material MatStone() => Mat("stone", new Color(0.62f, 0.60f, 0.56f));
        static Material MatStoneDark() => Mat("stoneDark", new Color(0.42f, 0.40f, 0.38f));
        static Material MatWood() => Mat("wood", new Color(0.42f, 0.28f, 0.14f));
        static Material MatGold() => Mat("gold", new Color(0.95f, 0.78f, 0.18f));
        static Material MatRoof() => Mat("roof", new Color(0.12f, 0.42f, 0.78f));
        static Material MatCream() => Mat("cream", new Color(0.92f, 0.88f, 0.78f));
        static Material MatWindow() => Mat("window", new Color(0.35f, 0.55f, 0.75f));
        static Material MatBanner() => Mat("banner", new Color(0.82f, 0.12f, 0.12f));
        static Material MatGrass() => Mat("yard", new Color(0.35f, 0.55f, 0.28f));
        static Material MatShadow() => Mat("shadow", new Color(0.12f, 0.14f, 0.10f, 0.55f));

        static Material Mat(string key, Color color)
        {
            if (Mats.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var mat = UrpMaterialUtil.CreateColorMaterial(color, "KoG_" + key);
            if (mat == null) return null;
            Mats[key] = mat;
            return mat;
        }

        static void AddPrim(Transform parent, PrimitiveType kind, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(kind);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = true;
            }
        }
    }
}

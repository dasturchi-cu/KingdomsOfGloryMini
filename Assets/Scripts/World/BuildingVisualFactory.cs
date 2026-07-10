using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Procedural CoC-style building stand-ins (mine/barracks/greybox).
    /// Shared URP materials — no per-instance material churn.
    /// </summary>
    public static class BuildingVisualFactory
    {
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        public static GameObject Create(string type, int level, Vector3 worldPos)
        {
            if (type == "gold_mine") return CreateGoldMine(level, worldPos);
            if (type == "barracks") return CreateBarracks(level, worldPos);
            return CreateGreybox(type, level, worldPos);
        }

        public static GameObject CreateGreybox(string type, int level, Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = type + "_" + level;
            var footprint = type == "barracks" ? 1.4f : 1.2f;
            var height = type == "barracks" ? 1.4f : type == "castle" ? 2.2f : 1.1f;
            go.transform.localScale = new Vector3(footprint, height, footprint);
            go.transform.position = new Vector3(worldPos.x, height * 0.5f, worldPos.z);

            var color = type == "castle"
                ? new Color(0.55f, 0.55f, 0.6f)
                : type == "gold_mine"
                    ? new Color(0.95f, 0.8f, 0.2f)
                    : type == "barracks"
                        ? new Color(0.4f, 0.55f, 0.9f)
                        : Color.gray;
            go.GetComponent<Renderer>().sharedMaterial = Mat("grey_" + type, color);
            return go;
        }

        static GameObject CreateGoldMine(int level, Vector3 worldPos)
        {
            var root = new GameObject("gold_mine_" + level);
            root.transform.position = worldPos;

            // Ground shadow disc.
            AddPrim(root.transform, PrimitiveType.Cylinder, "Shadow",
                new Vector3(0f, 0.02f, 0f), new Vector3(1.35f, 0.02f, 1.35f), Mat("shadow", new Color(0.12f, 0.14f, 0.10f)));
            AddPrim(root.transform, PrimitiveType.Cube, "Pad",
                new Vector3(0f, 0.08f, 0f), new Vector3(1.2f, 0.16f, 1.2f), MatStone());
            AddPrim(root.transform, PrimitiveType.Cube, "BeamL",
                new Vector3(-0.4f, 0.58f, 0f), new Vector3(0.13f, 1.05f, 0.13f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "BeamR",
                new Vector3(0.4f, 0.58f, 0f), new Vector3(0.13f, 1.05f, 0.13f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "Cross",
                new Vector3(0f, 1.12f, 0f), new Vector3(1.0f, 0.11f, 0.11f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "Roof",
                new Vector3(0f, 1.28f, 0f), new Vector3(0.55f, 0.12f, 0.55f), Mat("mineRoof", new Color(0.5f, 0.32f, 0.14f)));
            AddPrim(root.transform, PrimitiveType.Cube, "OreA",
                new Vector3(0.08f, 0.3f, 0.25f), new Vector3(0.58f, 0.38f, 0.48f), MatGold());
            AddPrim(root.transform, PrimitiveType.Sphere, "OreB",
                new Vector3(-0.22f, 0.34f, -0.18f), new Vector3(0.45f, 0.34f, 0.45f), MatGold());
            AddPrim(root.transform, PrimitiveType.Sphere, "OreC",
                new Vector3(0.28f, 0.26f, -0.05f), new Vector3(0.28f, 0.22f, 0.28f), Mat("goldBright", new Color(1f, 0.88f, 0.35f)));
            AddPrim(root.transform, PrimitiveType.Cube, "Crate",
                new Vector3(0.38f, 0.22f, -0.38f), new Vector3(0.3f, 0.28f, 0.3f), MatDark());
            AddPrim(root.transform, PrimitiveType.Cylinder, "Peg",
                new Vector3(0f, 1.42f, 0f), new Vector3(0.14f, 0.08f, 0.14f), MatGold());
            AddLevelPlate(root.transform, level, new Vector3(0f, 0.02f, -0.72f));
            return root;
        }

        static GameObject CreateBarracks(int level, Vector3 worldPos)
        {
            var root = new GameObject("barracks_" + level);
            root.transform.position = worldPos;

            AddPrim(root.transform, PrimitiveType.Cylinder, "Shadow",
                new Vector3(0f, 0.02f, 0f), new Vector3(1.5f, 0.02f, 1.3f), Mat("shadow", new Color(0.12f, 0.14f, 0.10f)));
            AddPrim(root.transform, PrimitiveType.Cube, "Base",
                new Vector3(0f, 0.1f, 0f), new Vector3(1.4f, 0.2f, 1.2f), MatStone());
            AddPrim(root.transform, PrimitiveType.Cube, "Walls",
                new Vector3(0f, 0.58f, 0f), new Vector3(1.25f, 0.75f, 1.0f), MatCanvas());
            AddPrim(root.transform, PrimitiveType.Cube, "Roof",
                new Vector3(0f, 1.1f, 0f), new Vector3(1.4f, 0.24f, 1.15f), MatRoof());
            AddPrim(root.transform, PrimitiveType.Cube, "Ridge",
                new Vector3(0f, 1.28f, 0f), new Vector3(0.22f, 0.2f, 1.1f), MatRoof());
            AddPrim(root.transform, PrimitiveType.Cube, "Door",
                new Vector3(0f, 0.45f, 0.52f), new Vector3(0.34f, 0.55f, 0.07f), MatDark());
            AddPrim(root.transform, PrimitiveType.Cube, "WindowL",
                new Vector3(-0.38f, 0.62f, 0.51f), new Vector3(0.18f, 0.18f, 0.05f), Mat("window", new Color(0.35f, 0.55f, 0.75f)));
            AddPrim(root.transform, PrimitiveType.Cube, "WindowR",
                new Vector3(0.38f, 0.62f, 0.51f), new Vector3(0.18f, 0.18f, 0.05f), Mat("window", new Color(0.35f, 0.55f, 0.75f)));
            AddPrim(root.transform, PrimitiveType.Cylinder, "Pole",
                new Vector3(0.58f, 0.9f, 0.42f), new Vector3(0.07f, 0.75f, 0.07f), MatWood());
            AddPrim(root.transform, PrimitiveType.Cube, "Banner",
                new Vector3(0.58f, 1.22f, 0.55f), new Vector3(0.3f, 0.26f, 0.04f),
                Mat("banner", new Color(0.78f, 0.12f, 0.12f)));
            AddLevelPlate(root.transform, level, new Vector3(0f, 0.02f, -0.78f));
            return root;
        }

        static void AddLevelPlate(Transform parent, int level, Vector3 localPos)
        {
            AddPrim(parent, PrimitiveType.Cube, "LvlPlate",
                localPos + Vector3.up * 0.08f, new Vector3(0.55f, 0.12f, 0.22f),
                Mat("lvlPlate", new Color(0.15f, 0.16f, 0.2f)));
            var badge = new GameObject("LvlText");
            badge.transform.SetParent(parent, false);
            badge.transform.localPosition = localPos + new Vector3(0f, 0.22f, 0f);
            var tm = badge.AddComponent<TextMesh>();
            tm.text = "L" + Mathf.Max(level, 1);
            tm.fontSize = 64;
            tm.characterSize = 0.035f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.9f, 0.4f);
            tm.fontStyle = FontStyle.Bold;
            // Match CoC cam so L# reads in Game view.
            badge.transform.rotation = Quaternion.Euler(55f, 45f, 0f);
        }

        static Material MatStone() => Mat("stone", new Color(0.62f, 0.60f, 0.56f));
        static Material MatWood() => Mat("wood", new Color(0.45f, 0.30f, 0.16f));
        static Material MatGold() => Mat("gold", new Color(0.95f, 0.78f, 0.18f));
        static Material MatRoof() => Mat("roof", new Color(0.18f, 0.38f, 0.72f));
        static Material MatCanvas() => Mat("canvas", new Color(0.78f, 0.72f, 0.58f));
        static Material MatDark() => Mat("dark", new Color(0.28f, 0.26f, 0.24f));

        static Material Mat(string key, Color color)
        {
            if (Mats.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.enableInstancing = true;
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

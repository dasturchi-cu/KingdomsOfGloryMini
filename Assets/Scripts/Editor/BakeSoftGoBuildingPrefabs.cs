#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using KoG.MiniMvp.World;

namespace KoG.MiniMvp.EditorTools
{
    /// <summary>
    /// Bakes Soft-GO Factory meshes into Resources/Buildings with persisted .mat assets
    /// (runtime-only Materials strip to null on SaveAsPrefabAsset → pink/magenta).
    /// </summary>
    public static class BakeSoftGoBuildingPrefabs
    {
        const string Dir = "Assets/Resources/Buildings";
        const string MatsRoot = Dir + "/Mats";

        [MenuItem("KoG/Art/Bake Soft-GO Building Prefabs")]
        public static void Bake()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(Dir);
            EnsureFolder(MatsRoot);

            BakeOne("GoldMine", () => BuildingVisualFactory.Create("gold_mine", 1, Vector3.zero));
            BakeOne("Barracks", () => BuildingVisualFactory.Create("barracks", 1, Vector3.zero));
            BakeOne("CastleSoftGo", () => BuildingVisualFactory.Create("castle", 1, Vector3.zero));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KoG] Soft-GO prefabs baked under " + Dir);
        }

        static void BakeOne(string fileName, System.Func<GameObject> factory)
        {
            var go = factory();
            PersistMaterials(go, fileName);
            var path = Dir + "/" + fileName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[KoG] Baked " + path);
        }

        /// <summary>Write every sharedMaterial to disk so prefab references survive.</summary>
        static void PersistMaterials(GameObject go, string prefabName)
        {
            var matFolder = MatsRoot + "/" + prefabName;
            EnsureFolder(matFolder);

            var map = new Dictionary<int, Material>(32);
            var seq = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;
                var next = new Material[shared.Length];
                for (var i = 0; i < shared.Length; i++)
                {
                    var src = shared[i];
                    if (src == null)
                    {
                        var solid = new Material(UrpMaterialUtil.FindLitShader() ?? Shader.Find("Standard"));
                        var c = UrpMaterialUtil.GuessColor(r.name);
                        if (solid.HasProperty("_Color")) solid.SetColor("_Color", c);
                        if (solid.HasProperty("_BaseColor")) solid.SetColor("_BaseColor", c);
                        UrpMaterialUtil.ApplyMobileSurface(solid);
                        src = solid;
                    }

                    if (AssetDatabase.Contains(src))
                    {
                        next[i] = src;
                        continue;
                    }

                    var id = src.GetInstanceID();
                    if (!map.TryGetValue(id, out var assetMat))
                    {
                        assetMat = new Material(src);
                        assetMat.name = Sanitize(src.name) + "_" + seq;
                        seq++;
                        var assetPath = matFolder + "/" + assetMat.name + ".mat";
                        AssetDatabase.CreateAsset(assetMat, assetPath);
                        map[id] = assetMat;
                    }
                    next[i] = assetMat;
                }
                r.sharedMaterials = next;
            }
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "mat";
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '_')) chars[i] = '_';
            }
            return new string(chars);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif

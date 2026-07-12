#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using KoG.MiniMvp.World;

namespace KoG.MiniMvp.EditorTools
{
    /// <summary>
    /// Creates / refreshes Resources BuildingArtConfig with GUID prefab refs (no string paths).
    /// Menu: KoG → Art → Ensure Building Art Config
    /// </summary>
    public static class EnsureBuildingArtConfig
    {
        const string AssetPath = "Assets/Resources/Buildings/BuildingArtConfig.asset";

        [MenuItem("KoG/Art/Ensure Building Art Config")]
        public static void Ensure()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Buildings");

            var config = AssetDatabase.LoadAssetAtPath<BuildingArtConfig>(AssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BuildingArtConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
            }

            var castle = LoadPrefab("Assets/Resources/Buildings/CastleSoftGo.prefab")
                         ?? LoadPrefab("Assets/Resources/Buildings/Castle.prefab");
            var castleFallback = LoadPrefab("Assets/Resources/Buildings/CastleMesh.prefab");
            var mine = LoadPrefab("Assets/Resources/Buildings/GoldMine.prefab");
            var barracks = LoadPrefab("Assets/Resources/Buildings/Barracks.prefab");

            config.EditorSetEntries(new[]
            {
                new BuildingArtConfig.Entry
                {
                    typeId = "castle",
                    prefab = castle,
                    fallbackPrefab = castleFallback,
                    footprint = 2.2f,
                    preferProcedural = false
                },
                new BuildingArtConfig.Entry
                {
                    typeId = "gold_mine",
                    prefab = mine,
                    fallbackPrefab = null,
                    footprint = 1.35f,
                    preferProcedural = false
                },
                new BuildingArtConfig.Entry
                {
                    typeId = "barracks",
                    prefab = barracks,
                    fallbackPrefab = null,
                    footprint = 1.9f,
                    preferProcedural = false
                }
            });

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildingArtCatalog.SetConfig(config);
            Debug.Log("[KoG] BuildingArtConfig ready at " + AssetPath
                      + " castle=" + (castle != null)
                      + " mine=" + (mine != null)
                      + " barracks=" + (barracks != null));
        }

        static GameObject LoadPrefab(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
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

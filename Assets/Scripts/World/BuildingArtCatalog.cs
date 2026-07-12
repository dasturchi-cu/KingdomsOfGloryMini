using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Presentation config only — stable building IDs map to Resources prefabs + footprints.
    /// Domain (API types) never changes; swap art by replacing prefabs or flipping PreferProcedural.
    /// </summary>
    public static class BuildingArtCatalog
    {
        public readonly struct Entry
        {
            public readonly string TypeId;
            public readonly string ResourcesPath;
            public readonly float Footprint;
            /// <summary>True = Soft-GO procedural pack (Factory). False = load Resources prefab first.</summary>
            public readonly bool PreferProcedural;

            public Entry(string typeId, string resourcesPath, float footprint, bool preferProcedural)
            {
                TypeId = typeId;
                ResourcesPath = resourcesPath;
                Footprint = footprint;
                PreferProcedural = preferProcedural;
            }
        }

        static readonly Entry[] Entries =
        {
            // Resources Soft-GO prefabs ship in Mini — prefer them for one consistent look.
            new Entry("castle", "Buildings/CastleSoftGo", 2.2f, preferProcedural: false),
            new Entry("gold_mine", "Buildings/GoldMine", 1.35f, preferProcedural: false),
            new Entry("barracks", "Buildings/Barracks", 1.9f, preferProcedural: false),
        };

        static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>(8);

        public static bool TryGet(string type, out Entry entry)
        {
            for (var i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].TypeId == type)
                {
                    entry = Entries[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }

        public static float Footprint(string type)
        {
            return TryGet(type, out var e) ? e.Footprint : 1.2f;
        }

        /// <summary>
        /// Instantiates Resources prefab when PreferProcedural is false (or forced).
        /// Returns null if missing / empty mesh — caller falls back to BuildingVisualFactory.
        /// </summary>
        public static GameObject TryInstantiatePrefab(string type, int level, bool forcePrefab = false)
        {
            if (!TryGet(type, out var entry)) return null;
            if (entry.PreferProcedural && !forcePrefab) return null;
            if (string.IsNullOrEmpty(entry.ResourcesPath)) return null;

            var prefab = LoadPrefabCached(entry.ResourcesPath);
            if (prefab == null && type == "castle")
                prefab = LoadPrefabCached("Buildings/CastleMesh");
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab);
            go.name = type + "_" + level;
            go.SetActive(true);
            UrpMaterialUtil.RemapToUrp(go);
            if (type == "castle") BuildingFitUtil.ApplyCastleAlbedoIfMissing(go);

            if (!HasRenderableMesh(go))
            {
                Object.Destroy(go);
                return null;
            }
            return go;
        }

        public static bool HasRenderableMesh(GameObject go)
        {
            if (go == null) return false;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return false;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) return true;
            }
            return false;
        }

        static GameObject LoadPrefabCached(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath)) return null;
            if (PrefabCache.TryGetValue(resourcesPath, out var cached))
                return cached;
            var loaded = Resources.Load<GameObject>(resourcesPath);
            PrefabCache[resourcesPath] = loaded;
            return loaded;
        }
    }
}

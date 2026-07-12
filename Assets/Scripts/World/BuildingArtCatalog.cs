using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Runtime facade over <see cref="BuildingArtConfig"/> (prefab GUID refs).
    /// Domain IDs stay stable; art links live on the ScriptableObject — not string paths in C#.
    /// </summary>
    public static class BuildingArtCatalog
    {
        public const string ConfigResourcePath = "Buildings/BuildingArtConfig";

        public readonly struct Entry
        {
            public readonly string TypeId;
            public readonly float Footprint;
            public readonly bool PreferProcedural;
            public readonly GameObject Prefab;
            public readonly GameObject FallbackPrefab;

            public Entry(
                string typeId,
                float footprint,
                bool preferProcedural,
                GameObject prefab,
                GameObject fallbackPrefab)
            {
                TypeId = typeId;
                Footprint = footprint;
                PreferProcedural = preferProcedural;
                Prefab = prefab;
                FallbackPrefab = fallbackPrefab;
            }
        }

        static BuildingArtConfig _config;
        static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>(8);

        /// <summary>Optional inject (tests / bootstrap). Null clears to Resources load.</summary>
        public static void SetConfig(BuildingArtConfig config)
        {
            _config = config;
            PrefabCache.Clear();
        }

        static BuildingArtConfig Config
        {
            get
            {
                if (_config == null)
                    _config = Resources.Load<BuildingArtConfig>(ConfigResourcePath);
                return _config;
            }
        }

        public static bool TryGet(string type, out Entry entry)
        {
            var cfg = Config;
            if (cfg != null && cfg.TryGet(type, out var raw) && raw != null)
            {
                entry = new Entry(
                    raw.typeId,
                    raw.footprint,
                    raw.preferProcedural,
                    raw.prefab,
                    raw.fallbackPrefab);
                return true;
            }

            entry = default;
            return false;
        }

        public static float Footprint(string type)
        {
            return TryGet(type, out var e) ? e.Footprint : 1.2f;
        }

        /// <summary>
        /// Instantiates catalog prefab when PreferProcedural is false (or forced).
        /// Returns null if missing / empty mesh — caller falls back to BuildingVisualFactory.
        /// </summary>
        public static GameObject TryInstantiatePrefab(string type, int level, bool forcePrefab = false)
        {
            if (!TryGet(type, out var entry)) return null;
            if (entry.PreferProcedural && !forcePrefab) return null;

            var prefab = ResolvePrefab(entry);
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
                if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null) return true;
            }
            return false;
        }

        static GameObject ResolvePrefab(Entry entry)
        {
            var key = entry.TypeId ?? "";
            if (PrefabCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            GameObject prefab = entry.Prefab;
            if (prefab == null)
                prefab = entry.FallbackPrefab;

            PrefabCache[key] = prefab;
            return prefab;
        }
    }
}

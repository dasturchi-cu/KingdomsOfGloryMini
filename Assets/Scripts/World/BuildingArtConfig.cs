using System;
using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Presentation catalog: stable building IDs → Inspector prefab refs (GUID-safe).
    /// Prefer editing this asset over hardcoding Resources paths in C#.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingArtConfig", menuName = "KoG/Building Art Config", order = 10)]
    public sealed class BuildingArtConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Server / domain ID — do not rename casually (castle, gold_mine, barracks).")]
            public string typeId = "";

            [Tooltip("Primary visual prefab. Move freely in Project — Unity GUID keeps the link.")]
            public GameObject prefab;

            [Tooltip("Optional second prefab if primary is missing (e.g. legacy castle mesh).")]
            public GameObject fallbackPrefab;

            [Tooltip("World scale hint for placement / factory footprint.")]
            public float footprint = 1.2f;

            [Tooltip("If true, skip prefab and use BuildingVisualFactory procedural mesh.")]
            public bool preferProcedural;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;

        public bool TryGet(string typeId, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(typeId) || entries == null) return false;
            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || e.typeId != typeId) continue;
                entry = e;
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(Entry[] next)
        {
            entries = next ?? Array.Empty<Entry>();
        }
#endif
    }
}

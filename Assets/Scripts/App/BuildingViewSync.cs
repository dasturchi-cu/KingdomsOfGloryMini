using System;
using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.App
{
    /// <summary>
    /// Reuses building GameObjects across player-state reloads when id+pose match.
    /// </summary>
    public static class BuildingViewSync
    {
        public static void Sync(
            Dictionary<string, GameObject> views,
            BuildingDtoLite[] buildings,
            Action<string, string, int, int, int, int> spawn,
            Action<GameObject> destroy)
        {
            if (views == null || spawn == null) return;

            var keep = new HashSet<string>(StringComparer.Ordinal);
            if (buildings != null)
            {
                for (var i = 0; i < buildings.Length; i++)
                {
                    var b = buildings[i];
                    if (b == null || string.IsNullOrEmpty(b.Id)) continue;
                    keep.Add(b.Id);

                    if (views.TryGetValue(b.Id, out var existing) && existing != null &&
                        CanReuse(existing, b))
                    {
                        continue;
                    }

                    spawn(b.Id, b.Type, b.Level, b.GridX, b.GridZ, b.RotationSteps);
                }
            }

            var remove = new List<string>();
            foreach (var kv in views)
            {
                if (!keep.Contains(kv.Key)) remove.Add(kv.Key);
            }

            for (var i = 0; i < remove.Count; i++)
            {
                var id = remove[i];
                if (views.TryGetValue(id, out var go) && go != null)
                    destroy?.Invoke(go);
                views.Remove(id);
            }
        }

        static bool CanReuse(GameObject go, BuildingDtoLite b)
        {
            var m = go.GetComponent<BuildingMarker>();
            if (m == null) return false;
            return m.buildingType == b.Type
                   && m.level == b.Level
                   && m.gridX == b.GridX
                   && m.gridZ == b.GridZ
                   && m.rotationSteps == b.RotationSteps;
        }

        public sealed class BuildingDtoLite
        {
            public string Id;
            public string Type;
            public int Level;
            public int GridX;
            public int GridZ;
            public int RotationSteps;
        }
    }
}

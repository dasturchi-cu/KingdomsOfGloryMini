using System.Collections.Generic;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>Pure occupancy map — no UnityEngine dependency beyond optional none.</summary>
    public sealed class GridOccupancyMap
    {
        readonly int _size;
        readonly HashSet<long> _occupied = new HashSet<long>();
        readonly Dictionary<string, List<long>> _byBuildingId = new Dictionary<string, List<long>>();
        readonly List<GridCoord> _scratch = new List<GridCoord>(16);
        readonly Stack<List<long>> _keyListPool = new Stack<List<long>>(16);

        public GridOccupancyMap(int size)
        {
            _size = size > 0 ? size : 1;
        }

        public int Size => _size;

        public void Clear()
        {
            foreach (var pair in _byBuildingId)
                ReturnKeyList(pair.Value);
            _occupied.Clear();
            _byBuildingId.Clear();
        }

        public void Occupy(string buildingId, GridCoord anchor, BuildingFootprint footprint)
        {
            if (string.IsNullOrEmpty(buildingId)) return;
            Free(buildingId);
            footprint.CollectCells(anchor, _scratch);
            var keys = RentKeyList();
            for (var i = 0; i < _scratch.Count; i++)
            {
                var key = Pack(_scratch[i]);
                _occupied.Add(key);
                keys.Add(key);
            }

            _byBuildingId[buildingId] = keys;
        }

        public void Free(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return;
            if (!_byBuildingId.TryGetValue(buildingId, out var keys)) return;
            for (var i = 0; i < keys.Count; i++)
                _occupied.Remove(keys[i]);
            _byBuildingId.Remove(buildingId);
            ReturnKeyList(keys);
        }

        List<long> RentKeyList()
        {
            if (_keyListPool.Count > 0)
            {
                var list = _keyListPool.Pop();
                list.Clear();
                return list;
            }

            return new List<long>(8);
        }

        void ReturnKeyList(List<long> list)
        {
            if (list == null) return;
            list.Clear();
            _keyListPool.Push(list);
        }

        public bool IsInside(GridCoord coord)
        {
            return coord.X >= 0 && coord.Z >= 0 && coord.X < _size && coord.Z < _size;
        }

        public bool CanPlace(GridCoord anchor, BuildingFootprint footprint, string ignoreBuildingId = null)
        {
            footprint.CollectCells(anchor, _scratch);
            for (var i = 0; i < _scratch.Count; i++)
            {
                var cell = _scratch[i];
                if (!IsInside(cell)) return false;
                var key = Pack(cell);
                if (!_occupied.Contains(key)) continue;
                if (!string.IsNullOrEmpty(ignoreBuildingId) &&
                    _byBuildingId.TryGetValue(ignoreBuildingId, out var owned) &&
                    owned.Contains(key))
                    continue;
                return false;
            }

            return true;
        }

        public bool IsCellOccupied(GridCoord coord) => _occupied.Contains(Pack(coord));

        static long Pack(GridCoord c) => ((long)c.X << 32) ^ (uint)c.Z;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.AI
{
    /// <summary>Walkability grid for mobile A*. Origin = cell (0,0) world center.</summary>
    public sealed class PathGrid
    {
        readonly bool[] _blocked;
        public int Size { get; }
        public float CellSize { get; }

        public PathGrid(int size, float cellSize)
        {
            Size = Mathf.Max(1, size);
            CellSize = Mathf.Max(0.01f, cellSize);
            _blocked = new bool[Size * Size];
        }

        public void ClearObstacles()
        {
            System.Array.Clear(_blocked, 0, _blocked.Length);
        }

        public void SetBlocked(int x, int z, bool blocked)
        {
            if (!InBounds(x, z)) return;
            _blocked[z * Size + x] = blocked;
        }

        public bool IsBlocked(int x, int z)
        {
            if (!InBounds(x, z)) return true;
            return _blocked[z * Size + x];
        }

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Size && z < Size;

        public Vector3 IndexToWorld(int x, int z) => new Vector3(x * CellSize, 0f, z * CellSize);

        public bool WorldToIndex(Vector3 world, out int x, out int z)
        {
            x = Mathf.RoundToInt(world.x / CellSize);
            z = Mathf.RoundToInt(world.z / CellSize);
            return InBounds(x, z);
        }

        public void ClampIndex(ref int x, ref int z)
        {
            x = Mathf.Clamp(x, 0, Size - 1);
            z = Mathf.Clamp(z, 0, Size - 1);
        }
    }

    /// <summary>Bounded A* — iteration-capped for mobile CPU.</summary>
    public sealed class GridPathfinder
    {
        struct Node
        {
            public int X;
            public int Z;
            public int G;
            public int F;
            public int Parent;
            public bool Open;
            public bool Closed;
            public ushort SessionId;
        }

        readonly PathGrid _grid;
        readonly Node[] _nodes;
        readonly int[] _openHeap;
        int _openCount;
        ushort _sessionId;
        readonly List<Vector3> _scratchPath = new List<Vector3>(64);

        public GridPathfinder(PathGrid grid)
        {
            _grid = grid;
            var n = grid.Size * grid.Size;
            _nodes = new Node[n];
            _openHeap = new int[n];
        }

        ref Node GetNode(int id)
        {
            ref var n = ref _nodes[id];
            if (n.SessionId != _sessionId)
            {
                n.SessionId = _sessionId;
                n.Open = false;
                n.Closed = false;
                n.Parent = -1;
                n.G = int.MaxValue;
            }
            return ref n;
        }

        public bool TryFindPath(Vector3 from, Vector3 to, int maxIterations, int maxNodes, List<Vector3> pathOut)
        {
            pathOut.Clear();
            if (!_grid.WorldToIndex(from, out var sx, out var sz))
            {
                _grid.ClampIndex(ref sx, ref sz);
            }

            if (!_grid.WorldToIndex(to, out var gx, out var gz))
            {
                _grid.ClampIndex(ref gx, ref gz);
            }

            if (_grid.IsBlocked(gx, gz))
                FindNearestWalkable(ref gx, ref gz);

            if (_grid.IsBlocked(sx, sz))
                FindNearestWalkable(ref sx, ref sz);

            if (sx == gx && sz == gz)
            {
                pathOut.Add(_grid.IndexToWorld(gx, gz));
                return true;
            }

            var size = _grid.Size;
            var total = size * size;

            _sessionId++;
            if (_sessionId == 0)
            {
                // Handle session ID wrap-around safely
                for (var i = 0; i < total; i++)
                    _nodes[i].SessionId = 0;
                _sessionId = 1;
            }

            _openCount = 0;
            var start = Idx(sx, sz);
            ref var startNode = ref GetNode(start);
            startNode.X = sx;
            startNode.Z = sz;
            startNode.G = 0;
            startNode.F = Heuristic(sx, sz, gx, gz);
            startNode.Open = true;
            PushOpen(start);

            var iterations = 0;
            var found = -1;
            while (_openCount > 0 && iterations < maxIterations)
            {
                iterations++;
                var current = PopOpen();
                ref var cn = ref GetNode(current);
                cn.Open = false;
                cn.Closed = true;

                if (cn.X == gx && cn.Z == gz)
                {
                    found = current;
                    break;
                }

                TryNeighbor(current, cn.X + 1, cn.Z, gx, gz, 10);
                TryNeighbor(current, cn.X - 1, cn.Z, gx, gz, 10);
                TryNeighbor(current, cn.X, cn.Z + 1, gx, gz, 10);
                TryNeighbor(current, cn.X, cn.Z - 1, gx, gz, 10);
                TryNeighbor(current, cn.X + 1, cn.Z + 1, gx, gz, 14);
                TryNeighbor(current, cn.X + 1, cn.Z - 1, gx, gz, 14);
                TryNeighbor(current, cn.X - 1, cn.Z + 1, gx, gz, 14);
                TryNeighbor(current, cn.X - 1, cn.Z - 1, gx, gz, 14);
            }

            if (found < 0) return false;

            _scratchPath.Clear();
            var walk = found;
            while (walk >= 0 && _scratchPath.Count < maxNodes)
            {
                ref var n = ref _nodes[walk];
                _scratchPath.Add(_grid.IndexToWorld(n.X, n.Z));
                walk = n.Parent;
            }

            for (var i = _scratchPath.Count - 1; i >= 0; i--)
                pathOut.Add(_scratchPath[i]);
            return pathOut.Count > 0;
        }

        void TryNeighbor(int parent, int x, int z, int gx, int gz, int cost)
        {
            if (_grid.IsBlocked(x, z)) return;
            // Diagonal corner cut prevention
            if (cost == 14)
            {
                var px = _nodes[parent].X;
                var pz = _nodes[parent].Z;
                if (_grid.IsBlocked(x, pz) || _grid.IsBlocked(px, z)) return;
            }

            var id = Idx(x, z);
            ref var n = ref GetNode(id);
            if (n.Closed) return;

            var g = GetNode(parent).G + cost;
            if (n.Open && g >= n.G) return;

            n.X = x;
            n.Z = z;
            n.G = g;
            n.F = g + Heuristic(x, z, gx, gz);
            n.Parent = parent;
            if (!n.Open)
            {
                n.Open = true;
                PushOpen(id);
            }
        }

        void FindNearestWalkable(ref int x, ref int z)
        {
            if (!_grid.IsBlocked(x, z)) return;
            for (var r = 1; r < _grid.Size; r++)
            {
                for (var dz = -r; dz <= r; dz++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        var nx = x + dx;
                        var nz = z + dz;
                        if (!_grid.IsBlocked(nx, nz))
                        {
                            x = nx;
                            z = nz;
                            return;
                        }
                    }
                }
            }
        }

        static int Heuristic(int x, int z, int gx, int gz)
        {
            var dx = Mathf.Abs(x - gx);
            var dz = Mathf.Abs(z - gz);
            return 10 * (dx + dz) + 4 * Mathf.Min(dx, dz);
        }

        int Idx(int x, int z) => z * _grid.Size + x;

        void PushOpen(int id)
        {
            var idx = _openCount++;
            _openHeap[idx] = id;
            
            // Percolate up
            while (idx > 0)
            {
                var parent = (idx - 1) / 2;
                if (_nodes[_openHeap[idx]].F >= _nodes[_openHeap[parent]].F)
                    break;
                
                // Swap
                var tmp = _openHeap[idx];
                _openHeap[idx] = _openHeap[parent];
                _openHeap[parent] = tmp;
                
                idx = parent;
            }
        }

        int PopOpen()
        {
            var id = _openHeap[0];
            _openCount--;
            if (_openCount > 0)
            {
                _openHeap[0] = _openHeap[_openCount];
                
                // Percolate down
                var idx = 0;
                while (true)
                {
                    var left = 2 * idx + 1;
                    var right = 2 * idx + 2;
                    var smallest = idx;
                    
                    if (left < _openCount && _nodes[_openHeap[left]].F < _nodes[_openHeap[smallest]].F)
                        smallest = left;
                    if (right < _openCount && _nodes[_openHeap[right]].F < _nodes[_openHeap[smallest]].F)
                        smallest = right;
                        
                    if (smallest == idx)
                        break;
                        
                    // Swap
                    var tmp = _openHeap[idx];
                    _openHeap[idx] = _openHeap[smallest];
                    _openHeap[smallest] = tmp;
                    
                    idx = smallest;
                }
            }
            return id;
        }
    }

    /// <summary>Consumes A* waypoints and issues motor destinations.</summary>
    public sealed class PathFollower
    {
        readonly List<Vector3> _waypoints = new List<Vector3>(64);
        int _index;
        float _arriveDist = 0.35f;

        public bool HasPath => _index < _waypoints.Count;
        public IReadOnlyList<Vector3> Waypoints => _waypoints;

        public void Clear()
        {
            _waypoints.Clear();
            _index = 0;
        }

        public void SetPath(List<Vector3> path, float arriveDistance)
        {
            _waypoints.Clear();
            if (path != null)
            {
                for (var i = 0; i < path.Count; i++)
                    _waypoints.Add(path[i]);
            }

            _index = 0;
            _arriveDist = Mathf.Max(0.05f, arriveDistance);
            // Skip first node if it's essentially current cell
            if (_waypoints.Count > 1)
                _index = 1;
        }

        public bool TryGetCurrent(out Vector3 waypoint)
        {
            if (_index >= _waypoints.Count)
            {
                waypoint = default;
                return false;
            }

            waypoint = _waypoints[_index];
            return true;
        }

        public void AdvanceIfArrived(Vector3 position)
        {
            if (_index >= _waypoints.Count) return;
            var w = _waypoints[_index];
            var dx = position.x - w.x;
            var dz = position.z - w.z;
            if (dx * dx + dz * dz <= _arriveDist * _arriveDist)
                _index++;
        }

        public bool IsComplete => _index >= _waypoints.Count;
    }
}

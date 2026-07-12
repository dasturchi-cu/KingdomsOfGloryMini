using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Invisible RTS building grid that fills the playable BaseField.
    /// Hidden in Play mode; editor gizmos for placement/snapping.
    /// </summary>
    public sealed class BuildingGrid : MonoBehaviour
    {
        [SerializeField] int gridSize = 20;
        [SerializeField] float cellSize = 1.1f;
        [SerializeField] bool drawGizmos = true;
        [SerializeField] Color gizmoColor = new Color(1f, 1f, 1f, 0.22f);

        /// <summary>World position of cell (0,0) center.</summary>
        public Vector3 Origin { get; private set; }

        public int GridSize => gridSize;
        public float CellSize => cellSize;
        public float WorldSize => gridSize * cellSize;

        public static BuildingGrid Build(Transform parent, int size, float cell, Vector3 fieldCenter)
        {
            var go = new GameObject("BuildingGrid");
            go.transform.SetParent(parent, false);
            go.transform.position = fieldCenter;
            go.layer = 0;

            var grid = go.AddComponent<BuildingGrid>();
            grid.gridSize = size;
            grid.cellSize = cell;
            grid.drawGizmos = !Application.isPlaying; // Play: invisible; Edit: helpful
            // Cell (0,0) world = (0,0,0) in MiniMvpApp GridToWorld; field center is mid-cell.
            grid.Origin = Vector3.zero;
            grid.EnsureSnapSurface();
            return grid;
        }

        void EnsureSnapSurface()
        {
            // Thin trigger plane for future raycast snapping — not visible.
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "SnapSurface";
            plane.transform.SetParent(transform, false);
            plane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plane.transform.localPosition = Vector3.up * 0.01f;
            var world = WorldSize;
            plane.transform.localScale = new Vector3(world, world, 1f);

            var rend = plane.GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;

            // Quad ships with a non-convex MeshCollider — Unity forbids triggers on those.
            var meshCol = plane.GetComponent<MeshCollider>();
            if (meshCol != null) Object.Destroy(meshCol);
            var box = plane.GetComponent<BoxCollider>();
            if (box == null) box = plane.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1f, 1f, 0.02f);
        }

        public Vector3 GridToWorld(int x, int z)
        {
            return new Vector3(x * cellSize, 0f, z * cellSize);
        }

        public bool WorldToGrid(Vector3 world, out int x, out int z)
        {
            x = Mathf.RoundToInt(world.x / cellSize);
            z = Mathf.RoundToInt(world.z / cellSize);
            return x >= 0 && z >= 0 && x < gridSize && z < gridSize;
        }

        public Vector3 SnapWorld(Vector3 world)
        {
            if (!WorldToGrid(world, out var x, out var z))
            {
                x = Mathf.Clamp(Mathf.RoundToInt(world.x / cellSize), 0, gridSize - 1);
                z = Mathf.Clamp(Mathf.RoundToInt(world.z / cellSize), 0, gridSize - 1);
            }
            return GridToWorld(x, z);
        }

        void OnDrawGizmos()
        {
            // Never draw during Play — kataklar only via SoftFieldLook grass texture.
            if (Application.isPlaying || !drawGizmos) return;
            if (gridSize <= 0) return;

            Gizmos.color = gizmoColor;
            var half = cellSize * 0.5f;
            var minVal = -half;
            var maxVal = (gridSize - 1) * cellSize + half;

            for (var i = 0; i <= gridSize; i++)
            {
                var coord = minVal + i * cellSize;
                // Lines parallel to Z axis (fixed X)
                Gizmos.DrawLine(new Vector3(coord, 0.02f, minVal), new Vector3(coord, 0.02f, maxVal));
                // Lines parallel to X axis (fixed Z)
                Gizmos.DrawLine(new Vector3(minVal, 0.02f, coord), new Vector3(maxVal, 0.02f, coord));
            }
        }
    }
}

using UnityEngine;

namespace KoG.IsoBase.Grid
{
    /// <summary>
    /// Sprite-based isometric grid (Tilemap YO'Q).
    /// Grid (x,y) ↔ world position konversiya + cell occupancy.
    /// </summary>
    public sealed class IsometricGridSystem : MonoBehaviour
    {
        [Header("Grid Size")]
        [SerializeField] int width = 30;
        [SerializeField] int height = 30;

        [Header("Tile Size (world units)")]
        [Tooltip("Isometric diamond kengligi. 2:1 iso uchun odatda tileHeight * 2.")]
        [SerializeField] float tileWidth = 1f;
        [Tooltip("Isometric diamond balandligi.")]
        [SerializeField] float tileHeight = 0.5f;

        [Header("Origin")]
        [Tooltip("Grid (0,0) world pozitsiyasi (odatda Ground markazi).")]
        [SerializeField] Vector3 origin = Vector3.zero;

        [Header("Debug Gizmos")]
        [SerializeField] bool drawGizmos = true;
        [SerializeField] Color gizmoLineColor = new Color(0.2f, 1f, 0.4f, 0.35f);
        [SerializeField] Color gizmoOccupiedColor = new Color(1f, 0.25f, 0.2f, 0.45f);
        [SerializeField] bool drawCellCenters = true;

        GridCell[,] _cells;

        public int Width => width;
        public int Height => height;
        public float TileWidth => tileWidth;
        public float TileHeight => tileHeight;
        public Vector3 Origin => origin;

        void Awake()
        {
            Initialize();
        }

        /// <summary>Gridni qayta yaratadi (Inspector o'zgarishidan keyin ham chaqirish mumkin).</summary>
        public void Initialize()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            tileWidth = Mathf.Max(0.01f, tileWidth);
            tileHeight = Mathf.Max(0.01f, tileHeight);

            _cells = new GridCell[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _cells[x, y] = new GridCell(x, y);
                }
            }
        }

        public void SetOrigin(Vector3 worldOrigin)
        {
            origin = worldOrigin;
        }

        public void SetTileSize(float worldTileWidth, float worldTileHeight)
        {
            tileWidth = Mathf.Max(0.01f, worldTileWidth);
            tileHeight = Mathf.Max(0.01f, worldTileHeight);
        }

        /// <summary>Grid o'lchamini o'zgartirib qayta Initialize qiladi.</summary>
        public void Configure(int gridWidth, int gridHeight, float worldTileWidth, float worldTileHeight)
        {
            width = Mathf.Max(1, gridWidth);
            height = Mathf.Max(1, gridHeight);
            SetTileSize(worldTileWidth, worldTileHeight);
            Initialize();
        }

        /// <summary>
        /// Grid → World.
        /// worldX = (x - y) * (tileWidth / 2)
        /// worldY = (x + y) * (tileHeight / 2) * -1
        /// </summary>
        public Vector3 GridToWorld(int x, int y)
        {
            float worldX = (x - y) * (tileWidth * 0.5f);
            float worldY = (x + y) * (tileHeight * 0.5f) * -1f;
            return origin + new Vector3(worldX, worldY, 0f);
        }

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return GridToWorld(gridPos.x, gridPos.y);
        }

        /// <summary>World → eng yaqin grid katak (chegara ichida clamp).</summary>
        public Vector2Int WorldToGrid(Vector3 world)
        {
            Vector3 local = world - origin;
            // Inverse of:
            // wx = (x - y) * (tw/2)
            // wy = -(x + y) * (th/2)
            float a = local.x / (tileWidth * 0.5f);   // x - y
            float b = -local.y / (tileHeight * 0.5f); // x + y
            float fx = (a + b) * 0.5f;
            float fy = (b - a) * 0.5f;

            int x = Mathf.RoundToInt(fx);
            int y = Mathf.RoundToInt(fy);
            return ClampToGrid(x, y);
        }

        public Vector2Int ClampToGrid(int x, int y)
        {
            return new Vector2Int(
                Mathf.Clamp(x, 0, width - 1),
                Mathf.Clamp(y, 0, height - 1));
        }

        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        public bool IsInBounds(Vector2Int pos)
        {
            return IsInBounds(pos.x, pos.y);
        }

        public GridCell GetCell(int x, int y)
        {
            if (!IsInBounds(x, y))
            {
                return null;
            }

            EnsureInitialized();
            return _cells[x, y];
        }

        public GridCell GetCell(Vector2Int pos)
        {
            return GetCell(pos.x, pos.y);
        }

        /// <summary>
        /// footprintW × footprintH kataklar bo'sh va walkable ekanini tekshiradi.
        /// originCell — bino pastki-chap (min x, min y) katakasi.
        /// </summary>
        public bool CanPlace(int originX, int originY, int footprintW, int footprintH)
        {
            EnsureInitialized();
            footprintW = Mathf.Max(1, footprintW);
            footprintH = Mathf.Max(1, footprintH);

            for (int x = originX; x < originX + footprintW; x++)
            {
                for (int y = originY; y < originY + footprintH; y++)
                {
                    if (!IsInBounds(x, y))
                    {
                        return false;
                    }

                    GridCell cell = _cells[x, y];
                    if (cell.Occupied || !cell.Walkable)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool Occupy(int originX, int originY, int footprintW, int footprintH, string buildingId)
        {
            if (!CanPlace(originX, originY, footprintW, footprintH))
            {
                return false;
            }

            for (int x = originX; x < originX + footprintW; x++)
            {
                for (int y = originY; y < originY + footprintH; y++)
                {
                    _cells[x, y].Occupy(buildingId);
                }
            }

            return true;
        }

        public void Free(int originX, int originY, int footprintW, int footprintH)
        {
            EnsureInitialized();
            for (int x = originX; x < originX + footprintW; x++)
            {
                for (int y = originY; y < originY + footprintH; y++)
                {
                    if (!IsInBounds(x, y))
                    {
                        continue;
                    }

                    _cells[x, y].ClearOccupation();
                    _cells[x, y].Walkable = true;
                }
            }
        }

        /// <summary>
        /// Isometric depth sorting: pastroq world Y = kamera oldida = yuqori sortingOrder.
        /// </summary>
        public static int SortingOrderFromWorldY(float worldY, int scale = 100)
        {
            return Mathf.RoundToInt(-worldY * scale);
        }

        void EnsureInitialized()
        {
            if (_cells == null || _cells.GetLength(0) != width || _cells.GetLength(1) != height)
            {
                Initialize();
            }
        }

        void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            tileWidth = Mathf.Max(0.01f, tileWidth);
            tileHeight = Mathf.Max(0.01f, tileHeight);
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            // Ensure cells are initialized so occupancy colors draw correctly in Edit mode
            EnsureInitialized();

            Gizmos.color = gizmoLineColor;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    DrawCellDiamond(x, y);

                    if (drawCellCenters)
                    {
                        Vector3 c = GridToWorld(x, y);
                        Gizmos.DrawSphere(c, Mathf.Min(tileWidth, tileHeight) * 0.04f);
                    }

                    if (_cells[x, y] != null && _cells[x, y].Occupied)
                    {
                        Gizmos.color = gizmoOccupiedColor;
                        DrawCellDiamond(x, y, filled: true);
                        Gizmos.color = gizmoLineColor;
                    }
                }
            }
        }

        void DrawCellDiamond(int x, int y, bool filled = false)
        {
            // Cell markazi atrofida isometric diamond (1 cell)
            Vector3 c = GridToWorld(x, y);
            float hw = tileWidth * 0.5f;
            float hh = tileHeight * 0.5f;

            Vector3 top = c + new Vector3(0f, hh, 0f);
            Vector3 right = c + new Vector3(hw, 0f, 0f);
            Vector3 bottom = c + new Vector3(0f, -hh, 0f);
            Vector3 left = c + new Vector3(-hw, 0f, 0f);

            if (filled)
            {
                // Oddiy wire + markaz nuqta (mesh fill gizmo og'ir)
                Gizmos.DrawLine(top, right);
                Gizmos.DrawLine(right, bottom);
                Gizmos.DrawLine(bottom, left);
                Gizmos.DrawLine(left, top);
                Gizmos.DrawSphere(c, Mathf.Min(hw, hh) * 0.2f);
            }
            else
            {
                Gizmos.DrawLine(top, right);
                Gizmos.DrawLine(right, bottom);
                Gizmos.DrawLine(bottom, left);
                Gizmos.DrawLine(left, top);
            }
        }
    }
}

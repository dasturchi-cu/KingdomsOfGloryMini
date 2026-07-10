using UnityEngine;

namespace KoG.IsoBase.Ground
{
    /// <summary>
    /// Diamond isometric ground PNG ni sprite sifatida joylashtiradi.
    /// Tilemap ishlatilmaydi. Katta xarita uchun Tiled rejim mavjud.
    /// Sorting Layer: "Ground" (eng orqada) — Project Settings da yarating.
    /// </summary>
    public sealed class GroundRenderer : MonoBehaviour
    {
        public enum GroundLayoutMode
        {
            /// <summary>Bitta katta base sprite (Clash of Clans uslubi).</summary>
            Single,
            /// <summary>Bir nechta tile ni grid bo'ylab takrorlash.</summary>
            Tiled
        }

        [Header("Sprite")]
        [SerializeField] Sprite groundSprite;
        [Tooltip("Sprite bo'sh bo'lsa runtime da placeholder (yashil diamond) yaratiladi.")]
        [SerializeField] bool createPlaceholderIfMissing = true;

        [Header("Layout")]
        [SerializeField] GroundLayoutMode layoutMode = GroundLayoutMode.Single;
        [SerializeField] Vector3 groundOrigin = Vector3.zero;

        [Header("Single Mode")]
        [Tooltip("Bitta sprite world o'lchami. 0 = sprite pixels/PPU dan avtomatik.")]
        [SerializeField] Vector2 singleWorldSize = Vector2.zero;

        [Header("Tiled Mode")]
        [SerializeField] int tilesX = 1;
        [SerializeField] int tilesY = 1;
        [SerializeField] float tileWorldWidth = 1f;
        [SerializeField] float tileWorldHeight = 0.5f;

        [Header("Sorting")]
        [SerializeField] string sortingLayerName = "Ground";
        [SerializeField] int sortingOrder = 0;

        [Header("Parent")]
        [SerializeField] Transform tilesRoot;

        SpriteRenderer _singleRenderer;

        public Sprite GroundSprite => groundSprite;
        public GroundLayoutMode LayoutMode => layoutMode;

        void Awake()
        {
            Rebuild();
        }

        /// <summary>Inspector yoki boshqa skriptdan qayta chizish.</summary>
        [ContextMenu("Rebuild Ground")]
        public void Rebuild()
        {
            ClearTiles();

            if (groundSprite == null && createPlaceholderIfMissing)
            {
                groundSprite = CreatePlaceholderDiamondSprite();
            }

            if (groundSprite == null)
            {
                Debug.LogWarning("[GroundRenderer] groundSprite null — Assign PNG (Sprite) in Inspector.");
                return;
            }

            if (tilesRoot == null)
            {
                var rootGo = new GameObject("GroundTiles");
                rootGo.transform.SetParent(transform, false);
                tilesRoot = rootGo.transform;
            }

            if (layoutMode == GroundLayoutMode.Single)
            {
                BuildSingle();
            }
            else
            {
                BuildTiled();
            }
        }

        public void SetSprite(Sprite sprite)
        {
            groundSprite = sprite;
            Rebuild();
        }

        public void SetSingleWorldSize(Vector2 worldSize)
        {
            layoutMode = GroundLayoutMode.Single;
            singleWorldSize = worldSize;
            Rebuild();
        }

        public void ConfigureTiling(int countX, int countY, float worldTileW, float worldTileH)
        {
            layoutMode = GroundLayoutMode.Tiled;
            tilesX = Mathf.Max(1, countX);
            tilesY = Mathf.Max(1, countY);
            tileWorldWidth = Mathf.Max(0.01f, worldTileW);
            tileWorldHeight = Mathf.Max(0.01f, worldTileH);
            Rebuild();
        }

        /// <summary>
        /// Grid o'lchami bilan tile world size ni moslashtirish (Bosqich 1 bilan sync).
        /// </summary>
        public void SyncTileSizeFromGrid(float gridTileWidth, float gridTileHeight, int gridW, int gridH)
        {
            tileWorldWidth = gridTileWidth;
            tileWorldHeight = gridTileHeight;
            // Bitta ground sprite butun bazani qoplashi uchun Single default;
            // Tiled kerak bo'lsa grid o'lchamiga teng qilamiz.
            if (layoutMode == GroundLayoutMode.Tiled)
            {
                tilesX = Mathf.Max(1, gridW);
                tilesY = Mathf.Max(1, gridH);
            }

            Rebuild();
        }

        void BuildSingle()
        {
            var go = new GameObject("Ground_Single");
            go.transform.SetParent(tilesRoot, false);
            go.transform.localPosition = groundOrigin;

            _singleRenderer = go.AddComponent<SpriteRenderer>();
            _singleRenderer.sprite = groundSprite;
            ApplySorting(_singleRenderer);

            if (singleWorldSize.x > 0.01f && singleWorldSize.y > 0.01f)
            {
                // Sprite local bounds → kerakli scale
                Bounds b = groundSprite.bounds;
                float sx = singleWorldSize.x / Mathf.Max(0.0001f, b.size.x);
                float sy = singleWorldSize.y / Mathf.Max(0.0001f, b.size.y);
                go.transform.localScale = new Vector3(sx, sy, 1f);
            }
        }

        void BuildTiled()
        {
            // Isometric tiling: har tile GridToWorld formula bilan
            // worldX = (x - y) * (tw/2), worldY = (x + y) * (th/2) * -1
            for (int x = 0; x < tilesX; x++)
            {
                for (int y = 0; y < tilesY; y++)
                {
                    float worldX = (x - y) * (tileWorldWidth * 0.5f);
                    float worldY = (x + y) * (tileWorldHeight * 0.5f) * -1f;

                    var go = new GameObject($"Ground_{x}_{y}");
                    go.transform.SetParent(tilesRoot, false);
                    go.transform.localPosition = groundOrigin + new Vector3(worldX, worldY, 0f);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = groundSprite;
                    ApplySorting(sr);
                    // Tiled da har tile orqada qolishi uchun order biroz farq qilishi mumkin
                    sr.sortingOrder = sortingOrder - (x + y);

                    // Tile world size ga mos scale
                    Bounds b = groundSprite.bounds;
                    float sx = tileWorldWidth / Mathf.Max(0.0001f, b.size.x);
                    float sy = tileWorldHeight / Mathf.Max(0.0001f, b.size.y);
                    go.transform.localScale = new Vector3(sx, sy, 1f);
                }
            }
        }

        void ApplySorting(SpriteRenderer sr)
        {
            // Layer yo'q bo'lsa Default ga tushadi (warning bermaydi Unityda)
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
        }

        void ClearTiles()
        {
            _singleRenderer = null;
            if (tilesRoot == null)
            {
                return;
            }

            for (int i = tilesRoot.childCount - 1; i >= 0; i--)
            {
                var child = tilesRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        /// <summary>
        /// Asset hali assign qilinmaganda test uchun oddiy yashil diamond sprite.
        /// </summary>
        static Sprite CreatePlaceholderDiamondSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color grass = new Color(0.35f, 0.72f, 0.28f, 1f);
            Color border = new Color(0.55f, 0.55f, 0.58f, 1f);

            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            // 2:1 isometric diamond
            float halfW = size * 0.48f;
            float halfH = size * 0.24f;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float dx = Mathf.Abs(px - cx) / halfW;
                    float dy = Mathf.Abs(py - cy) / halfH;
                    float d = dx + dy; // diamond metric

                    if (d > 1.02f)
                    {
                        tex.SetPixel(px, py, clear);
                    }
                    else if (d > 0.88f)
                    {
                        tex.SetPixel(px, py, border);
                    }
                    else
                    {
                        tex.SetPixel(px, py, grass);
                    }
                }
            }

            tex.Apply(false, true);
            // PPU = 64 → world size ~ 2 x 2 (diamond visual ~2 x 1)
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                64f);
        }

        void OnValidate()
        {
            tilesX = Mathf.Max(1, tilesX);
            tilesY = Mathf.Max(1, tilesY);
            tileWorldWidth = Mathf.Max(0.01f, tileWorldWidth);
            tileWorldHeight = Mathf.Max(0.01f, tileWorldHeight);
        }
    }
}

using KoG.IsoBase.Grid;
using KoG.IsoBase.Ground;
using UnityEngine;

namespace KoG.IsoBase
{
    /// <summary>
    /// Bosqich 1–2 ni bir Scene da tez test qilish.
    /// Hierarchy: bo'sh GO → IsoBaseBootstrap → Play.
    /// </summary>
    public sealed class IsoBaseBootstrap : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] int gridWidth = 30;
        [SerializeField] int gridHeight = 30;
        [SerializeField] float tileWidth = 1f;
        [SerializeField] float tileHeight = 0.5f;

        [Header("Ground")]
        [SerializeField] Sprite groundSprite;
        [SerializeField] GroundRenderer.GroundLayoutMode groundMode =
            GroundRenderer.GroundLayoutMode.Single;
        [Tooltip("Single: butun baza fon o'lchami. Diamond 2:1 — width ≈ height*2.")]
        [SerializeField] Vector2 singleGroundWorldSize = new Vector2(30f, 15f);

        [Header("Camera")]
        [SerializeField] float cameraSize = 10f;
        [SerializeField] Color backgroundColor = new Color(0.25f, 0.55f, 0.28f, 1f);

        IsometricGridSystem _grid;
        GroundRenderer _ground;

        public IsometricGridSystem Grid => _grid;
        public GroundRenderer Ground => _ground;

        void Awake()
        {
            SetupCamera();
            SetupGrid();
            SetupGround();
        }

        void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
            }

            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        void SetupGrid()
        {
            var go = new GameObject("IsometricGrid");
            go.transform.SetParent(transform, false);
            _grid = go.AddComponent<IsometricGridSystem>();
            _grid.Configure(gridWidth, gridHeight, tileWidth, tileHeight);
            _grid.SetOrigin(Vector3.zero);
        }

        void SetupGround()
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(transform, false);
            _ground = go.AddComponent<GroundRenderer>();

            if (groundSprite != null)
            {
                _ground.SetSprite(groundSprite);
            }

            if (groundMode == GroundRenderer.GroundLayoutMode.Tiled)
            {
                _ground.ConfigureTiling(gridWidth, gridHeight, tileWidth, tileHeight);
            }
            else
            {
                _ground.SetSingleWorldSize(singleGroundWorldSize);
            }
        }
    }
}

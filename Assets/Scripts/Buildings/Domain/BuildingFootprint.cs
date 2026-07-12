using UnityEngine;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>
    /// Axis-aligned footprint in cells. Rotation is 0/90/180/270 degrees about Y.
    /// Odd rotations swap width/depth for collision.
    /// </summary>
    public readonly struct BuildingFootprint
    {
        public readonly int Width;
        public readonly int Depth;
        public readonly int RotationSteps;

        public BuildingFootprint(int width, int depth, int rotationSteps = 0)
        {
            Width = Mathf.Max(1, width);
            Depth = Mathf.Max(1, depth);
            RotationSteps = ((rotationSteps % 4) + 4) % 4;
        }

        public int OccupiedWidth => (RotationSteps % 2 == 0) ? Width : Depth;

        public int OccupiedDepth => (RotationSteps % 2 == 0) ? Depth : Width;

        public float YawDegrees => RotationSteps * 90f;

        public BuildingFootprint Rotated(int deltaSteps)
        {
            return new BuildingFootprint(Width, Depth, RotationSteps + deltaSteps);
        }

        /// <summary>Cells covered with anchor at min corner (inclusive).</summary>
        public void CollectCells(GridCoord anchor, System.Collections.Generic.List<GridCoord> into)
        {
            into.Clear();
            var w = OccupiedWidth;
            var d = OccupiedDepth;
            for (var z = 0; z < d; z++)
            {
                for (var x = 0; x < w; x++)
                    into.Add(new GridCoord(anchor.X + x, anchor.Z + z));
            }
        }
    }
}

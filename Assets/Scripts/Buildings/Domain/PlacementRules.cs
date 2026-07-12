using System.Collections.Generic;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>Placement / keep-out rules for castle-centered bases.</summary>
    public static class PlacementRules
    {
        /// <summary>Reused across drag-frame CanPlace calls — avoids GC while placing.</summary>
        static readonly List<GridCoord> KeepOutScratch = new List<GridCoord>(16);

        /// <summary>
        /// Returns false when anchor footprint collides, is OOB, or violates castle keep-out
        /// (non-castle buildings cannot sit inside keep-out Chebyshev ring).
        /// </summary>
        public static bool CanPlace(
            GridOccupancyMap map,
            string buildingType,
            GridCoord anchor,
            BuildingFootprint footprint,
            GridCoord? castleAnchor,
            int castleKeepOut)
        {
            if (map == null) return false;
            if (!map.CanPlace(anchor, footprint)) return false;

            if (buildingType == "castle" || castleKeepOut <= 0 || castleAnchor == null)
                return true;

            var cells = KeepOutScratch;
            footprint.CollectCells(anchor, cells);
            var cx = castleAnchor.Value.X;
            var cz = castleAnchor.Value.Z;
            for (var i = 0; i < cells.Count; i++)
            {
                var dx = cells[i].X - cx;
                if (dx < 0) dx = -dx;
                var dz = cells[i].Z - cz;
                if (dz < 0) dz = -dz;
                var cheb = dx > dz ? dx : dz;
                if (cheb < castleKeepOut) return false;
            }

            return true;
        }

        /// <summary>Spiral search from grid center for first valid cell (never defaults to 0,0).</summary>
        public static bool TryFindFreeNearCenter(
            GridOccupancyMap map,
            string buildingType,
            BuildingFootprint footprint,
            GridCoord? castleAnchor,
            int castleKeepOut,
            out GridCoord found)
        {
            found = default;
            if (map == null) return false;

            var size = map.Size;
            var cx = size / 2;
            var cz = size / 2;
            var startRadius = buildingType == "castle" ? 0 : (castleKeepOut > 0 ? castleKeepOut : 0);

            for (var radius = startRadius; radius < size; radius++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (radius > 0 && System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius)
                            continue;

                        var anchor = new GridCoord(cx + dx, cz + dz);
                        if (!CanPlace(map, buildingType, anchor, footprint, castleAnchor, castleKeepOut))
                            continue;

                        found = anchor;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

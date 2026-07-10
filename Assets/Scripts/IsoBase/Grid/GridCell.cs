using System;

namespace KoG.IsoBase.Grid
{
    /// <summary>
    /// Bitta isometric grid katakchasining holati.
    /// Tilemap ishlatilmaydi — barcha joylashuv qo'lda hisoblanadi.
    /// </summary>
    [Serializable]
    public sealed class GridCell
    {
        public int X;
        public int Y;
        public bool Occupied;
        public bool Walkable = true;

        /// <summary>Band qilgan bino instance id (bo'sh bo'lsa null).</summary>
        public string BuildingId;

        public GridCell(int x, int y)
        {
            X = x;
            Y = y;
            Occupied = false;
            Walkable = true;
            BuildingId = null;
        }

        public void ClearOccupation()
        {
            Occupied = false;
            BuildingId = null;
        }

        public void Occupy(string buildingId)
        {
            Occupied = true;
            BuildingId = buildingId;
            Walkable = false;
        }
    }
}

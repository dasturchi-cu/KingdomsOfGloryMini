using System;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>Immutable integer grid coordinate (X/Z ground plane).</summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public readonly int X;
        public readonly int Z;

        public GridCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(GridCoord other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Z;

        public override string ToString() => X + ":" + Z;

        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);

        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);
    }
}

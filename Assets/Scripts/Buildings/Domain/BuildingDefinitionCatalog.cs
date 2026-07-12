using System.Collections.Generic;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>Static content contract for Mini buildings (LiveOps IDs stay canonical).</summary>
    public sealed class BuildingDef
    {
        public readonly string Id;
        public readonly int Width;
        public readonly int Depth;
        public readonly bool CanDestroy;
        public readonly bool CanRotate;
        public readonly int KeepOutChebyshev;

        public BuildingDef(string id, int width, int depth, bool canDestroy, bool canRotate, int keepOutChebyshev = 0)
        {
            Id = id;
            Width = width;
            Depth = depth;
            CanDestroy = canDestroy;
            CanRotate = canRotate;
            KeepOutChebyshev = keepOutChebyshev;
        }

        public BuildingFootprint DefaultFootprint => new BuildingFootprint(Width, Depth, 0);
    }

    /// <summary>Catalog keyed by server building_type ids.</summary>
    public static class BuildingDefinitionCatalog
    {
        static readonly Dictionary<string, BuildingDef> Map = new Dictionary<string, BuildingDef>
        {
            { "castle", new BuildingDef("castle", 1, 1, canDestroy: false, canRotate: true, keepOutChebyshev: 3) },
            { "gold_mine", new BuildingDef("gold_mine", 1, 1, canDestroy: true, canRotate: true) },
            { "barracks", new BuildingDef("barracks", 1, 1, canDestroy: true, canRotate: true) },
        };

        public static bool TryGet(string buildingType, out BuildingDef def)
        {
            if (string.IsNullOrEmpty(buildingType))
            {
                def = null;
                return false;
            }

            return Map.TryGetValue(buildingType, out def);
        }

        public static BuildingDef GetOrDefault(string buildingType)
        {
            if (TryGet(buildingType, out var def)) return def;
            return new BuildingDef(buildingType ?? "unknown", 1, 1, canDestroy: true, canRotate: true);
        }
    }
}

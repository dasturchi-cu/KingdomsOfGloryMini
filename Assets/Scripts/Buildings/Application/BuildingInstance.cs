namespace KoG.MiniMvp.Buildings
{
    /// <summary>Runtime building snapshot mirrored from server DTOs.</summary>
    public sealed class BuildingInstance
    {
        public string Id;
        public string Type;
        public int Level;
        public GridCoord Anchor;
        public int RotationSteps;
        public bool IsUnderConstruction;
        public bool IsDamaged;
        public int ConstructionSecondsLeft;

        public BuildingFootprint Footprint
        {
            get
            {
                var def = BuildingDefinitionCatalog.GetOrDefault(Type);
                return new BuildingFootprint(def.Width, def.Depth, RotationSteps);
            }
        }
    }
}

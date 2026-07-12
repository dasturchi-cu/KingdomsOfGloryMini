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

        /// <summary>Temporary grid cell while dragging (visual + commit use this Anchor).</summary>
        public void SetAnchorPreview(GridCoord anchor) => Anchor = anchor;

        /// <summary>Persist committed cell after server accepts move.</summary>
        public void CommitAnchor(GridCoord anchor) => Anchor = anchor;
    }
}

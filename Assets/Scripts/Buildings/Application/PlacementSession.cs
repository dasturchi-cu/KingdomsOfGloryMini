using System;

namespace KoG.MiniMvp.Buildings
{
    public enum PlacementPhase
    {
        Idle,
        Previewing
    }

    /// <summary>Client-side placement session: preview, rotate, collision, cancel.</summary>
    public sealed class PlacementSession
    {
        public PlacementPhase Phase { get; private set; } = PlacementPhase.Idle;
        public string BuildingType { get; private set; }
        public GridCoord Anchor { get; private set; }
        public BuildingFootprint Footprint { get; private set; }
        public bool IsValid { get; private set; }

        public bool IsActive => Phase == PlacementPhase.Previewing;

        public void Begin(string buildingType, GridCoord start, BuildingFootprint footprint, bool valid)
        {
            BuildingType = buildingType;
            Anchor = start;
            Footprint = footprint;
            IsValid = valid;
            Phase = PlacementPhase.Previewing;
        }

        public void Cancel()
        {
            Phase = PlacementPhase.Idle;
            BuildingType = null;
            IsValid = false;
        }

        public void SetAnchor(GridCoord anchor, bool valid)
        {
            if (!IsActive) return;
            if (Anchor.Equals(anchor) && IsValid == valid) return;
            Anchor = anchor;
            IsValid = valid;
        }

        /// <summary>True when SetAnchor would change state (caller can skip preview refresh).</summary>
        public bool WouldChangeAnchor(GridCoord anchor, bool valid) =>
            IsActive && (!Anchor.Equals(anchor) || IsValid != valid);

        public void Rotate(int steps = 1)
        {
            if (!IsActive) return;
            var def = BuildingDefinitionCatalog.GetOrDefault(BuildingType);
            if (!def.CanRotate) return;
            Footprint = Footprint.Rotated(steps);
        }

        public void Revalidate(Func<GridCoord, BuildingFootprint, bool> validator)
        {
            if (!IsActive || validator == null) return;
            IsValid = validator(Anchor, Footprint);
        }
    }
}

using UnityEngine;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>
    /// Attach this to any GameObject (trees, rocks, water, decorations, roads) 
    /// that should strictly block building placement even if it's not tracked by the GridOccupancyMap.
    /// Used by the BuildingSystem's physics validation layer to reject overlapping placements.
    /// </summary>
    public sealed class NonBuildable : MonoBehaviour
    {
    }
}

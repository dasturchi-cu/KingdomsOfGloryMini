using UnityEngine;

namespace KoG.MiniMvp.AI
{
    public enum AiStateId : byte
    {
        Idle = 0,
        Move = 1,
        Attack = 2,
        Return = 3,
        Death = 4
    }

    /// <summary>Mobile AI profile — leash, scan rates, path limits.</summary>
    [CreateAssetMenu(fileName = "AiProfile", menuName = "KoG/AI/AI Profile", order = 30)]
    public sealed class AiProfile : ScriptableObject
    {
        [Min(0.5f)] public float AggroRange = 7f;
        [Min(1f)] public float LeashRange = 14f;
        [Min(0.05f)] public float ThinkIntervalSeconds = 0.2f;
        [Min(0.1f)] public float IdleScanIntervalSeconds = 0.45f;
        [Min(0.05f)] public float WaypointArriveDistance = 0.35f;
        [Min(4)] public int MaxPathNodes = 48;
        [Min(16)] public int MaxPathIterations = 256;
        public bool ReturnHomeAfterCombat = true;
        public bool AutoAggro = true;
    }
}

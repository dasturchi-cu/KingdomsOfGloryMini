using UnityEngine;

namespace KoG.MiniMvp.Combat
{
    public enum StatusEffectKind : byte
    {
        Stun = 0,
        Slow = 1,
        Bleed = 2,
        Burn = 3,
        ArmorShred = 4
    }

    /// <summary>Scriptable status effect — applied via DamageInfo or AttackController.</summary>
    [CreateAssetMenu(fileName = "StatusEffect", menuName = "KoG/Combat/Status Effect", order = 20)]
    public sealed class StatusEffectDefinition : ScriptableObject
    {
        public string Id = "slow";
        public StatusEffectKind Kind = StatusEffectKind.Slow;
        [Min(0.05f)] public float DurationSeconds = 2f;
        [Min(0f)] public float Magnitude = 0.3f;
        [Min(0f)] public float TickIntervalSeconds = 0.5f;
        [Range(1, 8)] public int MaxStacks = 1;
        public bool RefreshDurationOnReapply = true;
    }
}

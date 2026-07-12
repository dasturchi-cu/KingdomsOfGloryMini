using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>
    /// Authoring asset for one troop type. Stable <see cref="Id"/> must match server troop_type
    /// (e.g. barbarian). Prefab optional — runtime greybox used when null (mobile-safe).
    /// </summary>
    [CreateAssetMenu(fileName = "TroopDefinition", menuName = "KoG/Troops/Troop Definition", order = 10)]
    public sealed class TroopDefinition : ScriptableObject
    {
        [Tooltip("Server id — never rename lightly.")]
        public string Id = "barbarian";

        public string DisplayName = "Barbarian";

        public TroopStats Stats = TroopStats.BarbarianDefault;

        [Tooltip("Optional skinned/mesh prefab with Animator. Null → procedural capsule.")]
        public GameObject Prefab;

        [Header("Combat extras (optional SOs)")]
        public KoG.MiniMvp.Combat.ProjectileDefinition Projectile;
        public KoG.MiniMvp.Combat.StatusEffectDefinition OnHitStatus;

        [Tooltip("Animator state names (optional).")]
        public string IdleState = "Idle";
        public string MoveState = "Move";
        public string AttackState = "Attack";
        public string DeathState = "Death";

        [Header("Mobile")]
        [Range(1, 64)] public int PoolPrewarm = 8;
        [Range(1, 128)] public int PoolMax = 40;
        public bool CastShadows;
        public Color Tint = new Color(0.82f, 0.55f, 0.28f, 1f);

        public void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id)) Id = name;
            if (Stats.MaxHealth < 1f) Stats.MaxHealth = 1f;
            if (Stats.MoveSpeed < 0.1f) Stats.MoveSpeed = 0.1f;
            if (Stats.AttackSpeed < 0.05f) Stats.AttackSpeed = 1f;
            if (Stats.AcquireRange < Stats.AttackRange) Stats.AcquireRange = Stats.AttackRange * 2f;
            if (PoolMax < PoolPrewarm) PoolMax = PoolPrewarm;
        }
    }
}

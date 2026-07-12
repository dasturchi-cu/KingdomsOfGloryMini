using UnityEngine;

namespace KoG.MiniMvp.Combat
{
    /// <summary>Projectile authoring. Null prefab → pooled sphere greybox.</summary>
    [CreateAssetMenu(fileName = "ProjectileDefinition", menuName = "KoG/Combat/Projectile Definition", order = 21)]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        public string Id = "arrow";
        public GameObject Prefab;
        [Min(1f)] public float Speed = 12f;
        [Min(0.1f)] public float LifetimeSeconds = 3f;
        public bool Homing = true;
        [Min(0.05f)] public float HitRadius = 0.35f;
        public Color Tint = new Color(1f, 0.85f, 0.2f, 1f);
        [Range(1, 64)] public int PoolPrewarm = 6;
        [Range(1, 128)] public int PoolMax = 32;
    }
}

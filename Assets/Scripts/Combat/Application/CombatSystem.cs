using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.Combat
{
    /// <summary>
    /// Central combat services: projectile pools + shared damageable registry for targeting.
    /// TroopSystem registers actors; AttackController fires through here.
    /// </summary>
    public sealed class CombatSystem : MonoBehaviour
    {
        readonly List<IDamageable> _damageables = new List<IDamageable>(64);
        readonly List<Projectile> _activeProjectiles = new List<Projectile>(32);
        readonly Dictionary<string, ProjectilePool> _projectilePools =
            new Dictionary<string, ProjectilePool>(8);
        Transform _poolRoot;
        ProjectileDefinition _defaultProjectile;
        bool _ready;

        public IReadOnlyList<IDamageable> Damageables => _damageables;

        public void Configure()
        {
            if (_poolRoot == null)
            {
                var go = new GameObject("ProjectilePool");
                go.transform.SetParent(transform, false);
                _poolRoot = go.transform;
            }

            if (_defaultProjectile == null)
            {
                _defaultProjectile = ScriptableObject.CreateInstance<ProjectileDefinition>();
                _defaultProjectile.Id = "bolt";
                _defaultProjectile.Speed = 14f;
                _defaultProjectile.Homing = true;
                _defaultProjectile.PoolPrewarm = 8;
                _defaultProjectile.PoolMax = 48;
            }

            GetOrCreatePool(_defaultProjectile).Prewarm();
            _ready = true;
        }

        public void Register(IDamageable unit)
        {
            if (unit == null) return;
            if (!_damageables.Contains(unit))
                _damageables.Add(unit);
        }

        public void Unregister(IDamageable unit)
        {
            _damageables.Remove(unit);
        }

        public void ClearRegistry()
        {
            _damageables.Clear();
            for (var i = _activeProjectiles.Count - 1; i >= 0; i--)
                RetireProjectile(_activeProjectiles[i]);
            _activeProjectiles.Clear();
        }

        public bool SpawnProjectile(
            ProjectileDefinition def,
            IDamageable source,
            IDamageable target,
            in DamageInfo payload)
        {
            if (!_ready || source == null || target == null) return false;
            var use = def != null ? def : _defaultProjectile;
            var pool = GetOrCreatePool(use);
            if (!pool.TryRent(out var projectile)) return false;

            var origin = source.Position + Vector3.up * 0.8f + source.Transform.forward * 0.4f;
            projectile.transform.SetParent(null, true);
            projectile.Launch(use, origin, target, in payload, RetireProjectile);
            _activeProjectiles.Add(projectile);
            return true;
        }

        public ProjectileDefinition DefaultProjectile => _defaultProjectile;

        void Update()
        {
            if (!_ready || _activeProjectiles.Count == 0) return;
            var dt = Time.deltaTime;
            for (var i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                var p = _activeProjectiles[i];
                if (p == null || !p.IsAlive)
                {
                    if (p != null) _activeProjectiles.RemoveAt(i);
                    continue;
                }

                p.Tick(dt);
            }
        }

        void RetireProjectile(Projectile projectile)
        {
            if (projectile == null) return;
            _activeProjectiles.Remove(projectile);
            var id = string.IsNullOrEmpty(projectile.PoolId) ? "default" : projectile.PoolId;
            if (_projectilePools.TryGetValue(id, out var pool))
                pool.Return(projectile);
            else
                projectile.ResetForPool();
        }

        ProjectilePool GetOrCreatePool(ProjectileDefinition def)
        {
            var id = def != null ? def.Id : "default";
            if (_projectilePools.TryGetValue(id, out var pool)) return pool;
            pool = new ProjectilePool(def, _poolRoot);
            _projectilePools[id] = pool;
            return pool;
        }
    }
}

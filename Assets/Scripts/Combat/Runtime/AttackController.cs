using System;
using KoG.MiniMvp.Troops;
using UnityEngine;

namespace KoG.MiniMvp.Combat
{
    /// <summary>
    /// Attack speed, range gate, melee vs projectile fire.
    /// TroopActor owns movement/anim; this owns "when/how we deal damage".
    /// </summary>
    public sealed class AttackController
    {
        public enum FireMode : byte
        {
            Melee = 0,
            Projectile = 1
        }

        TroopStats _stats;
        float _cooldown;
        IDamageable _self;
        IDamageable _target;
        FireMode _mode;
        ProjectileDefinition _projectile;
        StatusEffectDefinition _onHitStatus;
        Func<IDamageable, IDamageable, DamageInfo, bool> _spawnProjectile;
        Action<IDamageable, DamageResult> _onMeleeHit;

        public IDamageable Target => _target;
        public float Range => _stats.AttackRange;
        public bool HasTarget => _target != null && _target.IsAlive;

        public void Bind(
            IDamageable self,
            in TroopStats stats,
            FireMode mode,
            ProjectileDefinition projectile,
            StatusEffectDefinition onHitStatus,
            Func<IDamageable, IDamageable, DamageInfo, bool> spawnProjectile,
            Action<IDamageable, DamageResult> onMeleeHit)
        {
            _self = self;
            _stats = stats;
            _mode = mode;
            _projectile = projectile;
            _onHitStatus = onHitStatus;
            _spawnProjectile = spawnProjectile;
            _onMeleeHit = onMeleeHit;
            _cooldown = 0f;
            _target = null;
        }

        public void ClearTarget() => _target = null;

        public void SetTarget(IDamageable target)
        {
            if (target == null || !target.IsAlive) return;
            if (_self != null && target.Faction == _self.Faction) return;
            _target = target;
        }

        public void TickCooldown(float dt) => _cooldown = Mathf.Max(0f, _cooldown - dt);

        public bool TryAcquireNearest(System.Collections.Generic.IReadOnlyList<IDamageable> candidates)
        {
            var found = Targeting.FindNearestEnemy(candidates, _self, _stats.AcquireRange > 0f
                ? _stats.AcquireRange
                : _stats.AttackRange * 3f);
            if (found == null) return false;
            _target = found;
            return true;
        }

        /// <summary>Call on animation hit or when no animator (immediate).</summary>
        public bool TryFire()
        {
            if (_self == null || !_self.IsAlive) return false;
            if (_target == null || !_target.IsAlive)
            {
                _target = null;
                return false;
            }

            if (_cooldown > 0f) return false;
            if (!Targeting.IsInRange(_self, _target, _stats.AttackRange)) return false;

            _cooldown = CombatMath.AttackInterval(_stats.AttackIntervalSeconds, _stats.AttackSpeed);

            var info = DamageInfo.FromAttacker(_stats.AttackDamage, in _stats, _self);
            info.StatusToApply = _onHitStatus;

            if (_mode == FireMode.Projectile && _projectile != null && _spawnProjectile != null)
            {
                return _spawnProjectile(_self, _target, info);
            }

            var result = _target.ApplyHit(in info);
            _onMeleeHit?.Invoke(_target, result);
            return true;
        }
    }
}

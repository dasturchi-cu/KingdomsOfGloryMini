using System;
using System.Collections.Generic;
using KoG.MiniMvp.Troops;
using UnityEngine;

namespace KoG.MiniMvp.Combat
{
    /// <summary>
    /// Health / armor / status owner. TroopActor delegates all damage here — no duplicate HP fields.
    /// </summary>
    public sealed class CombatUnit : IDamageable
    {
        public event Action<CombatUnit> Died;
        public event Action<CombatUnit, DamageResult> HitReceived;

        Transform _transform;
        TroopFaction _faction;
        TroopStats _stats;
        float _health;
        readonly StatusEffectController _status = new StatusEffectController();
        bool _dead;

        public bool IsAlive => !_dead && _health > 0f;
        public TroopFaction Faction => _faction;
        public Vector3 Position => _transform != null ? _transform.position : Vector3.zero;
        public Transform Transform => _transform;
        public float CurrentHealth => _health;
        public float MaxHealth => _stats.MaxHealth;
        public float Armor => Mathf.Max(0f, _stats.Armor + _status.ArmorModifier);
        public StatusEffectController Status => _status;
        public bool IsStunned => _status.IsStunned;
        public float MoveSpeedMultiplier => _status.MoveSpeedMultiplier;

        public void Bind(Transform transform, TroopFaction faction, in TroopStats stats)
        {
            _transform = transform;
            _faction = faction;
            _stats = stats;
            _health = stats.MaxHealth;
            _dead = false;
            _status.Clear();
        }

        public void ResetForPool()
        {
            _dead = false;
            _health = 0f;
            _status.Clear();
            Died = null;
            HitReceived = null;
        }

        public void TickStatuses(float dt)
        {
            if (!IsAlive) return;
            _status.Tick(dt, dot =>
            {
                var info = new DamageInfo
                {
                    Amount = dot,
                    CanCrit = false,
                    CritChance = 0f,
                    CritMultiplier = 1f,
                    ArmorPenetration = 0f,
                    Source = null
                };
                ApplyHit(in info);
            });
        }

        public DamageResult ApplyHit(in DamageInfo info)
        {
            var result = new DamageResult();
            if (!IsAlive)
            {
                result.Blocked = true;
                return result;
            }

            var working = info;
            var applied = CombatMath.ResolveOutgoing(ref working, Armor);
            if (applied <= 0f)
            {
                result.Blocked = true;
                HitReceived?.Invoke(this, result);
                return result;
            }

            _health -= applied;
            result.Applied = applied;
            result.WasCritical = working.IsCritical;

            if (info.StatusToApply != null)
                _status.Apply(info.StatusToApply);

            if (_health <= 0f)
            {
                _health = 0f;
                _dead = true;
                result.Killed = true;
                Died?.Invoke(this);
            }

            HitReceived?.Invoke(this, result);
            return result;
        }

        public void SetFaction(TroopFaction faction) => _faction = faction;
    }

    /// <summary>Target queries over IDamageable lists — shared by AI and input.</summary>
    public static class Targeting
    {
        public static IDamageable FindNearestEnemy(
            IReadOnlyList<IDamageable> candidates,
            IDamageable self,
            float maxRange)
        {
            if (self == null || candidates == null) return null;
            IDamageable best = null;
            var bestDist = maxRange;
            var origin = self.Position;
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c == null || !c.IsAlive || ReferenceEquals(c, self)) continue;
                if (c.Faction == self.Faction) continue;
                var d = HorizontalDistance(origin, c.Position);
                if (d > bestDist) continue;
                bestDist = d;
                best = c;
            }

            return best;
        }

        public static bool IsInRange(IDamageable a, IDamageable b, float range)
        {
            if (a == null || b == null) return false;
            return HorizontalDistance(a.Position, b.Position) <= range;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}

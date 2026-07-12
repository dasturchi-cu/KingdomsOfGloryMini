using System;
using KoG.MiniMvp.Troops;
using UnityEngine;

namespace KoG.MiniMvp.Combat
{
    /// <summary>Single hit payload — all damage flows through this (no ad-hoc floats).</summary>
    public struct DamageInfo
    {
        public float Amount;
        public bool CanCrit;
        public float CritChance;
        public float CritMultiplier;
        public float ArmorPenetration;
        public IDamageable Source;
        public StatusEffectDefinition StatusToApply;
        public bool IsCritical; // filled by CombatMath

        public static DamageInfo FromAttacker(float baseDamage, in TroopStats stats, IDamageable source)
        {
            return new DamageInfo
            {
                Amount = baseDamage,
                CanCrit = stats.CritChance > 0f,
                CritChance = stats.CritChance,
                CritMultiplier = stats.CritMultiplier > 0f ? stats.CritMultiplier : 1.5f,
                ArmorPenetration = stats.ArmorPenetration,
                Source = source,
                StatusToApply = null,
                IsCritical = false
            };
        }
    }

    public struct DamageResult
    {
        public float Applied;
        public bool WasCritical;
        public bool Killed;
        public bool Blocked;
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        TroopFaction Faction { get; }
        Vector3 Position { get; }
        Transform Transform { get; }
        float CurrentHealth { get; }
        float Armor { get; }
        DamageResult ApplyHit(in DamageInfo info);
    }
}

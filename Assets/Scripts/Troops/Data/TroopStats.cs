using System;
using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>Pure combat/move numbers. LiveOps may override later; SO is the default authoring surface.</summary>
    [Serializable]
    public struct TroopStats
    {
        [Header("Vitality")]
        [Min(1f)] public float MaxHealth;
        [Min(0f)] public float Armor;

        [Header("Offense")]
        [Min(0f)] public float AttackDamage;
        [Min(0.05f)] public float AttackIntervalSeconds;
        [Min(0.05f)] public float AttackSpeed;
        [Min(0.1f)] public float AttackRange;
        [Min(0.1f)] public float AcquireRange;
        [Range(0f, 1f)] public float CritChance;
        [Min(1f)] public float CritMultiplier;
        [Min(0f)] public float ArmorPenetration;

        [Header("Movement")]
        [Min(0.1f)] public float MoveSpeed;
        [Min(0.05f)] public float TurnSpeedDegrees;
        [Min(0.1f)] public float SelectionRadius;
        [Min(1)] public int HousingSpace;

        [Header("Ranged")]
        public bool UseProjectile;

        public static TroopStats BarbarianDefault => new TroopStats
        {
            MaxHealth = 100f,
            Armor = 5f,
            AttackDamage = 12f,
            AttackIntervalSeconds = 0.85f,
            AttackSpeed = 1f,
            AttackRange = 1.35f,
            AcquireRange = 6f,
            CritChance = 0.08f,
            CritMultiplier = 1.75f,
            ArmorPenetration = 0f,
            MoveSpeed = 3.2f,
            TurnSpeedDegrees = 540f,
            SelectionRadius = 0.55f,
            HousingSpace = 1,
            UseProjectile = false
        };

        public static TroopStats ArcherDefault => new TroopStats
        {
            MaxHealth = 70f,
            Armor = 0f,
            AttackDamage = 9f,
            AttackIntervalSeconds = 1.0f,
            AttackSpeed = 1f,
            AttackRange = 5.5f,
            AcquireRange = 9f,
            CritChance = 0.12f,
            CritMultiplier = 2f,
            ArmorPenetration = 2f,
            MoveSpeed = 2.8f,
            TurnSpeedDegrees = 480f,
            SelectionRadius = 0.5f,
            HousingSpace = 1,
            UseProjectile = true
        };

        /// <summary>Fills zeros from older SO assets so combat stays sane.</summary>
        public TroopStats Normalized()
        {
            var s = this;
            if (s.MaxHealth < 1f) s.MaxHealth = 1f;
            if (s.AttackIntervalSeconds < 0.05f) s.AttackIntervalSeconds = 0.85f;
            if (s.AttackSpeed < 0.05f) s.AttackSpeed = 1f;
            if (s.AttackRange < 0.1f) s.AttackRange = 1.35f;
            if (s.AcquireRange < s.AttackRange) s.AcquireRange = s.AttackRange * 3f;
            if (s.CritMultiplier < 1f) s.CritMultiplier = 1.5f;
            if (s.MoveSpeed < 0.1f) s.MoveSpeed = 3f;
            if (s.TurnSpeedDegrees < 1f) s.TurnSpeedDegrees = 540f;
            if (s.SelectionRadius < 0.1f) s.SelectionRadius = 0.5f;
            if (s.HousingSpace < 1) s.HousingSpace = 1;
            return s;
        }
    }
}

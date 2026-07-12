using UnityEngine;

namespace KoG.MiniMvp.Combat
{
    /// <summary>Pure combat formulas — one place for armor + crit. No MonoBehaviour.</summary>
    public static class CombatMath
    {
        const float ArmorConstant = 100f;

        /// <summary>
        /// Final damage after crit roll and armor mitigation.
        /// Armor model: damage *= ArmorConstant / (ArmorConstant + effectiveArmor).
        /// </summary>
        public static float ResolveOutgoing(ref DamageInfo info, float targetArmor, System.Random rng = null)
        {
            var amount = Mathf.Max(0f, info.Amount);
            if (info.CanCrit && info.CritChance > 0f)
            {
                var roll = rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
                if (roll < info.CritChance)
                {
                    info.IsCritical = true;
                    amount *= Mathf.Max(1f, info.CritMultiplier);
                }
            }

            var effectiveArmor = Mathf.Max(0f, targetArmor - info.ArmorPenetration);
            var mitigation = ArmorConstant / (ArmorConstant + effectiveArmor);
            return Mathf.Max(0f, amount * mitigation);
        }

        /// <summary>Attack interval from base interval and attack-speed multiplier.</summary>
        public static float AttackInterval(float baseIntervalSeconds, float attackSpeed)
        {
            var speed = Mathf.Max(0.05f, attackSpeed);
            return Mathf.Max(0.05f, baseIntervalSeconds / speed);
        }
    }
}

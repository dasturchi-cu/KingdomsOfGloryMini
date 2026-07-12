using System;
using UnityEngine;

namespace KoG.MiniMvp.Combat
{
    struct StatusRuntime
    {
        public StatusEffectDefinition Def;
        public float Remaining;
        public float TickTimer;
        public int Stacks;
    }

    /// <summary>Runs status effects on a CombatUnit — single implementation for all units.</summary>
    public sealed class StatusEffectController
    {
        readonly StatusRuntime[] _slots = new StatusRuntime[8];
        int _count;

        public bool IsStunned { get; private set; }
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float ArmorModifier { get; private set; }

        public void Clear()
        {
            _count = 0;
            IsStunned = false;
            MoveSpeedMultiplier = 1f;
            ArmorModifier = 0f;
        }

        public void Apply(StatusEffectDefinition def)
        {
            if (def == null) return;
            for (var i = 0; i < _count; i++)
            {
                if (_slots[i].Def != null && _slots[i].Def.Id == def.Id)
                {
                    if (def.RefreshDurationOnReapply)
                        _slots[i].Remaining = def.DurationSeconds;
                    _slots[i].Stacks = Mathf.Min(def.MaxStacks, _slots[i].Stacks + 1);
                    RebuildFlags();
                    return;
                }
            }

            if (_count >= _slots.Length) return;
            _slots[_count++] = new StatusRuntime
            {
                Def = def,
                Remaining = def.DurationSeconds,
                TickTimer = def.TickIntervalSeconds,
                Stacks = 1
            };
            RebuildFlags();
        }

        public void Tick(float dt, Action<float> onDotDamage)
        {
            if (_count == 0) return;
            for (var i = _count - 1; i >= 0; i--)
            {
                ref var s = ref _slots[i];
                s.Remaining -= dt;
                if (s.Def != null &&
                    (s.Def.Kind == StatusEffectKind.Bleed || s.Def.Kind == StatusEffectKind.Burn))
                {
                    s.TickTimer -= dt;
                    if (s.TickTimer <= 0f)
                    {
                        s.TickTimer = Mathf.Max(0.05f, s.Def.TickIntervalSeconds);
                        onDotDamage?.Invoke(s.Def.Magnitude * s.Stacks);
                    }
                }

                if (s.Remaining <= 0f)
                {
                    _slots[i] = _slots[_count - 1];
                    _count--;
                }
            }

            RebuildFlags();
        }

        void RebuildFlags()
        {
            IsStunned = false;
            MoveSpeedMultiplier = 1f;
            ArmorModifier = 0f;
            for (var i = 0; i < _count; i++)
            {
                var d = _slots[i].Def;
                if (d == null) continue;
                var stacks = _slots[i].Stacks;
                switch (d.Kind)
                {
                    case StatusEffectKind.Stun:
                        IsStunned = true;
                        break;
                    case StatusEffectKind.Slow:
                        MoveSpeedMultiplier *= Mathf.Clamp01(1f - d.Magnitude * stacks);
                        break;
                    case StatusEffectKind.ArmorShred:
                        ArmorModifier -= d.Magnitude * stacks;
                        break;
                }
            }
        }
    }
}

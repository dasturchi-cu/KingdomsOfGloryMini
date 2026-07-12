# Combat System

Client-side combat layer for presentation / local skirmish. Server raid results stay authoritative.

## Review → design (no duplication)

| Existing | Decision |
|----------|----------|
| `TroopActor` HP / flat damage | Extracted into `CombatUnit` + `CombatMath` |
| `TroopStats` damage/range/interval | Extended (armor, crit, AS, acquire, projectile flag) — **one SO** |
| `TroopPool` | Projectile pool mirrors same pattern |
| `TroopFaction` | Reused by `IDamageable` (no second team enum) |

## Layout

```
Combat/
  Domain/     IDamageable, DamageInfo, DamageResult, CombatMath
  Data/       StatusEffectDefinition (SO), ProjectileDefinition (SO)
  Runtime/    CombatUnit, StatusEffectController, AttackController,
              Targeting, Projectile + ProjectilePool
  Application/CombatSystem (registry + projectile tick)
```

## Features

| Feature | Where |
|---------|--------|
| Damage | `CombatMath.ResolveOutgoing` → `CombatUnit.ApplyHit` |
| Armor | `% = 100/(100+armor-pen)` |
| Critical | chance + multiplier on `DamageInfo` |
| Attack Speed | `interval / AttackSpeed` |
| Range | `AttackController` + `Targeting.IsInRange` |
| Target Detection | `Targeting.FindNearestEnemy` / `AcquireRange` |
| Status Effects | Stun / Slow / Bleed / Burn / ArmorShred SO |
| Projectiles | pooled + homing; `UseProjectile` on stats |

## Flow

`TroopActor` → `AttackController.TryFire` → melee `ApplyHit` **or** `CombatSystem.SpawnProjectile` → `CombatMath` on impact.

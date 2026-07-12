# Troop System (Mini client)

Scalable, mobile-first troop presentation + local sim. Server train/raid counts remain authoritative.

## Architecture

```
Data/          TroopStats, TroopDefinition (SO), TroopCatalogAsset (SO)
Domain/        TroopFaction, TroopAnimState
Pooling/       TroopPool (prewarm + hard max)
Runtime/       TroopActor, TroopMotor, TroopAnimEventSink, TroopVisualFactory
Application/   TroopSystem (spawn/select/sync/tick)
Presentation/  TroopInputDriver
```

## Features

| Feature | Implementation |
|---------|----------------|
| Troop Data | `TroopDefinition` ScriptableObject |
| Stats | `TroopStats` on SO |
| Movement | `TroopMotor` steering (no NavMesh) |
| Selection | tap + selection ring |
| Spawn | pool rent + `Spawn` |
| Death | HP → Death anim/timer → pool return |
| Attack | range chase + interval / Anim_AttackHit |
| Animation Events | `TroopAnimEventSink.Anim_AttackHit` / `Anim_DeathEnd` |
| Pooling | per-definition `TroopPool` |

## Mobile rules

- Centralized `TroopSystem.Update` ticks actors (predictable cost)
- Max visible troops capped (`maxVisibleTroops`, default 24)
- Shadows off by default; shared tint materials; GPU instancing flag
- Greybox capsule when prefab null — no heavy meshes required for Mini
- Input disabled during building placement

## Authoring

1. Create `KoG/Troops/Troop Definition` assets (id must match server: `barbarian`)
2. Optional catalog at `Resources/Troops/TroopCatalog`
3. Without assets, runtime creates a default barbarian definition

## Combat integration

Damage / armor / crit / attack speed / range / targeting / status / projectiles live in
`Scripts/Combat/` — `TroopActor` implements `IDamageable` and delegates to `CombatUnit` +
`AttackController`. Do not re-add flat `ApplyDamage` math on the actor.


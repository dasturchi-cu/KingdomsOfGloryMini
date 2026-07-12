# AI System

Scalable mobile AI for troops. **No per-agent `Update()`** — brains are plain C# FSMs ticked by one `AiSystem`.

## Architecture

```
AiProfile (SO)
AiBrain (FSM: Idle → Move → Attack → Return → Death)
PathGrid + GridPathfinder (bounded A*)
PathFollower (waypoints → TroopActor.CommandMove)
AiSystem (single Update, staggered buckets)
```

## Reuse (no duplication)

| Need | Source |
|------|--------|
| Target selection | `Combat.Targeting.FindNearestEnemy` |
| Attack / damage | `TroopActor.CommandAttack` → Combat layer |
| Movement | `TroopActor.CommandMove` → `TroopMotor` |
| Death | CombatUnit / TroopActor death pipeline |

## Mobile

- Think interval on profile (default 0.2s)
- Stagger brains across N buckets (default 3)
- A* iteration + node caps
- Grid matches base (`gridSize` × `cellSize`)
- Building cells marked blocked via `RefreshPathObstacles`

## Usage

- `TroopFaction.Enemy` spawns attach AI automatically
- `troopSystem.EnableAi(actor, home)` for manual attach
- Author: `Create → KoG/AI/AI Profile`

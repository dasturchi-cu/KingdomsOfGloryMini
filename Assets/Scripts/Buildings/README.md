# Building System (Mini client)

Production building slice for Kingdoms of Glory Mini / Kingdom of War client path.

## Layers

| Layer | Responsibility |
|-------|----------------|
| `Domain/` | `GridCoord`, `BuildingFootprint`, `GridOccupancyMap`, `PlacementRules`, catalog |
| `Application/` | `PlacementSession`, `BuildingSystem`, `ConstructionTimer`, `BuildingInstance` |
| `Presentation/` | `PlacementInputDriver` (touch/mouse ghost move) |
| `World/PlacePreviewFx` | Green/red footprint preview |

## Flows

1. **Place** — Mine/Barracks → preview session → drag → Rotate / Confirm / Cancel → `POST /buildings/place`
2. **Upgrade** — `POST /buildings/upgrade` (construction timer from `constructionSecondsLeft`)
3. **Cancel upgrade** — `POST /buildings/cancel-upgrade`
4. **Destroy** — `POST /buildings/destroy` (castle blocked; 50% placement refund)
5. **Repair** — `POST /buildings/repair` (requires `isDamaged`)

Server remains authoritative. Client occupancy is a UX gate only.

## Notes

- Rotate is visual / footprint-swap ready; server does not yet persist `rotationSteps`.
- Castle keep-out Chebyshev = 3 (matches Mini visual keep-out).

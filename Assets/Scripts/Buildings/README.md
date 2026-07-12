# Building System (Mini client)

Production building slice for Kingdoms of Glory Mini / Kingdom of War client path.

## Layers

| Layer | Responsibility |
|-------|----------------|
| `Domain/` | `GridCoord`, `BuildingFootprint`, `GridOccupancyMap`, `PlacementRules`, `BuildingDefinitionCatalog` |
| `Application/` | `PlacementSession`, `BuildingSystem`, `ConstructionTimer`, `BuildingInstance` |
| `Presentation/` | `PlacementInputDriver` (touch place + relocate) |
| `World/PlacePreviewFx` | Smooth green/red footprint + translucent ghost |

## Place flow (mobile)

1. **Kon / Kazarma** → `BeginPlacement` (free cell near grid center via catalog footprint)
2. Finger drag → `MovePreviewToWorld` → snap to grid → occupancy + castle keep-out validate
3. **Preview** — green = valid, red = invalid; ghost breathes; Tasdiq button tints with validity
4. **Aylantir** — if `BuildingDef.CanRotate` (catalog); otherwise error cue
5. **Bekor** — `CancelPlacement` + hide preview
6. **Tasdiq** — `ConfirmPlacement` → `POST /buildings/place` → `PlayPlace` + `PlaceDrop`/`PlaceBurst`
7. Invalid confirm → `PlayError` + reject shake on preview

## Relocate flow

Press mine/barracks → drag past threshold → camera pan blocked → release → `POST /buildings/move`.
Castle cannot move. Invalid / API fail snaps back.

## Future building types

Add an entry to `BuildingDefinitionCatalog` (`Id`, `Width`, `Depth`, `CanRotate`, `CanDestroy`, optional `KeepOutChebyshev`).
Placement / preview / rotate automatically use `GetOrDefault` — no driver changes required.

## Perf notes

- Preview tiles + ghost are pooled; `SmoothDamp` on LateUpdate (no GC).
- Placement drag uses plane ray only.
- Relocate pick uses `RaycastNonAlloc` + static hit buffer.
- `PlacementSession.SetAnchor` no-ops when cell/validity unchanged.

## Other mutations

| Action | API |
|--------|-----|
| Upgrade | `POST /buildings/upgrade` |
| Cancel upgrade | `POST /buildings/cancel-upgrade` |
| Destroy | `POST /buildings/destroy` |
| Repair | `POST /buildings/repair` |

Server remains authoritative. Client occupancy is a UX gate only.

## Notes

- Rotate is client footprint yaw; server accepts `rotationSteps` on place (persist may still be soft).
- Castle keep-out Chebyshev = 3 (matches Mini visual keep-out).

# Building System (Mini client)

Production building slice for Kingdoms of Glory Mini / Kingdom of War client path.

## Layers

| Layer | Responsibility |
|-------|----------------|
| `Domain/` | `GridCoord`, `BuildingFootprint`, `GridOccupancyMap`, `PlacementRules`, `BuildingDefinitionCatalog` |
| `Application/` | `PlacementSession`, `BuildingSystem`, `ConstructionTimer`, `BuildingInstance` |
| `Presentation/` | `PlacementInputDriver` (touch place + relocate + empty-tap deselect) |
| `World/PlacePreviewFx` | Smooth green/red footprint + translucent ghost |

## Place flow (mobile)

1. **Kon / Kazarma** → `BeginPlacement` (free cell near grid center via catalog footprint)
2. Finger drag → aim **+72px** above fingertip → `MovePreviewToWorld` → snap → occupancy + keep-out
3. **Preview** — green = valid, red = invalid; ghost breathes; cell hop → gated snap SFX; Tasdiq tints
4. **Aylantir** — if `BuildingDef.CanRotate` (catalog); otherwise error cue
5. **Bekor** — `CancelPlacement` + hide preview
6. **Tasdiq** — `ConfirmPlacement` → `POST /buildings/place` → `PlayPlace` + `PlaceDrop`/`PlaceBurst`
7. Invalid confirm → `PlayError` + reject shake on preview

## Selection

- Tap building → select ring pop-in + `PlaySelect` + smooth camera focus
- Short empty-ground tap → deselect (ignored after a pan)

## Relocate flow

Press **any building** (castle / mine / barracks / future catalog types) → drag past threshold →
camera pan blocked → release → `POST /buildings/move`.
Invalid cell → snap back + error SFX + reject pulse.
Nature border trees / field deco have no `BuildingMarker` — not draggable.

## Future building types

Add an entry to `BuildingDefinitionCatalog` (`Id`, `Width`, `Depth`, `CanRotate`, `CanDestroy`, optional `KeepOutChebyshev`).
Placement / preview / rotate automatically use `GetOrDefault` — no driver changes required.

## Perf notes

- Preview tiles + ghost are pooled; `SmoothDamp` on LateUpdate (no GC).
- Placement drag uses plane ray only; finger offset is screen-space only.
- Relocate pick uses `RaycastNonAlloc` + static hit buffer.
- `PlacementSession.SetAnchor` / relocate move no-op when cell/validity unchanged.
- Camera pinch nudges ortho **target** (smooth zoom without lag undershoot).

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
- Interaction audit: `docs/studio/INTERACTION_AUDIT_2026-07-12.md`

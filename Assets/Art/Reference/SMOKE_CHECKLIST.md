# Starter Village — Smoke Checklist (1:1 + mobile)

Reference: `StarterVillage_Reference.png`

## Visual (side-by-side with reference)

- [ ] Empty diamond center (no castle in environment)
- [ ] Soft checker kataklar visible but subtle
- [ ] Dark green playable edge rim
- [ ] NatureBorder trees / autumn / rocks / stumps / flowers match composition
- [ ] Soft shadows fall bottom-right (light from top-left)
- [ ] Warm bright greens (not muddy / not pink shaders)
- [ ] Ortho framing shows full diamond + border

## Interaction

- [ ] Pan / pinch / scroll zoom smooth
- [ ] Soft edge bounce at map bounds
- [ ] Place Mine shows green preview tile
- [ ] Select building shows gold ring (no Update cost when cleared)

## Performance (Device Simulator / mid Android)

- [ ] Console: `Draw budget — … OK` (renderers ≤ 60)
- [ ] Zoom out hides flower detail (LOD)
- [ ] MSAA / soft shadows / no HDR bloom wash
- [ ] Target feel: 60 FPS, no hitch on Guest login

## Hierarchy

- [ ] `Village / Terrain / Environment / Gameplay / Camera / Lighting`
- [ ] `KoG_PostVolume` under Lighting
- [ ] `KoG_Sun` under Lighting

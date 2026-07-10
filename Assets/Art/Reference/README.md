# Starter Village Reference (Source of Truth)

Image: `StarterVillage_Reference.png`

## Measured playable field
- Authored `BaseField_L1` mesh AABB extent = 11 → **22 × 22** world units
- Runtime grid: **20 × 20** cells × **1.1** cell size = **22** world units
- Nature border: `NatureBorder_L1` scaled with the same factor (fieldSize / 22)

## Composition (do not redesign)
- Empty diamond center (no buildings in environment prefab)
- Dense nature ring: green canopies, autumn trees, rocks, wood/stumps/logs, bushes, flowers, grass
- Camera: ortho yaw 45°, pitch ~52°

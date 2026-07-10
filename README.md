# Kingdoms of Glory Mini — Unity Client

**Bu repo FAQAT Unity frontend.**  
Backend / admin-panel / monorepo kodlari bu yerga **kirmaydi**.

| Nima | Qayerda |
|---|---|
| **Unity client** | shu repo → https://github.com/dasturchi-cu/KingdomsOfGloryMini |
| **Backend + Admin** | alohida monorepo (`mightglory` / `kingdom-of-war`) |

---

## Holat (2026-07-10)

### ✅ Ishlatiladi — Bosqich 1–2 (Isometric Grid + Ground)

Sprite-based 2D isometric asos (Tilemap **yo‘q**).

| Fayl | Nima qiladi |
|---|---|
| `Assets/Scripts/IsoBase/Grid/GridCell.cs` | Bitta katak: occupied, buildingId, walkable |
| `Assets/Scripts/IsoBase/Grid/IsometricGridSystem.cs` | Grid↔world formula, occupancy, Scene gizmo |
| `Assets/Scripts/IsoBase/Ground/GroundRenderer.cs` | Diamond ground PNG (Single / Tiled) |
| `Assets/Scripts/IsoBase/IsoBaseBootstrap.cs` | Tez test: kamera + grid + ground |
| `Assets/Art/Ground/base_ground_isometric.png` | Asosiy baza ground (1024×1024) |

**Formula:**
```
worldX = (x - y) * (tileWidth / 2)
worldY = (x + y) * (tileHeight / 2) * -1
```

Batafsil: [`ISO_BASE_BOSQICH_1_2.md`](./ISO_BASE_BOSQICH_1_2.md)

### ⏳ Keyingi (hali yo‘q)

- Bosqich 3: BuildingPlacer + ghost
- Bosqich 4–7: lifecycle, resurs, API, kamera

---

## Qanday ochish

1. Unity Hub → **Add** → shu papka (`KingdomsOfGloryMini`)
2. Unity `6000.3.x` yoki 2022 LTS
3. PNG: `Assets/Art/Ground/base_ground_isometric.png` → Texture Type = **Sprite (2D and UI)** → Apply
4. Sorting Layer: **Ground** qo‘shing (Project Settings → Tags and Layers)
5. Empty GameObject → `IsoBaseBootstrap` → Ground Sprite ni torting → **Play**
6. Scene view da yashil **gizmo grid** ko‘rinishi kerak

---

## Eski 3D village map

`Assets/Scripts/World/`, nature polish va boshqalar — avvalgi prototip.  
Yangi isometric base-building yo‘li: **`Assets/Scripts/IsoBase/`**.

## Render Pipeline

**Built-in (Standard)** � URP o�chirilgan (Polygon assetlar pushti bo�lmasin).

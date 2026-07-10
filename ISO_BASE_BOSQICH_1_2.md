# Bosqich 1–2 — Nima qilindi (ISHLATILADI)

**Sana:** 2026-07-10  
**Maqsad:** Clash of Clans uslubidagi 2D isometric baza — Tilemap siz.

---

## ✅ Tayyor va ishlatiladi

### Bosqich 1 — Isometric Grid Foundation

- `IsometricGridSystem.cs` — sprite-based grid (Tilemap emas)
- Grid (x,y) ↔ world position
- Sozlanadigan o‘lcham (default 30×30)
- `GridCell`: occupied, buildingId, walkable
- Scene view gizmo (diamond kataklar)

### Bosqich 2 — Ground Rendering

- `GroundRenderer.cs` — diamond ground PNG
- **Single** mode: bitta katta baza fon (CoC uslubi)
- **Tiled** mode: kerak bo‘lsa bir nechta tile
- Sorting Layer: `Ground`
- Asset: `Assets/Art/Ground/base_ground_isometric.png`

### Tez test

- `IsoBaseBootstrap.cs` — Play bosilganda kamera + grid + ground

---

## Qanday test qilish

1. Unityda loyihani oching
2. `base_ground_isometric.png` → Sprite (2D and UI)
3. Sorting Layer `Ground` yarating
4. Empty GO → `IsoBaseBootstrap` → sprite assign → Play
5. **Kutilgan:** Game view da ground, Scene da gizmo grid, Console 0 error

---

## ❌ Hali qilinmagan (keyingi commit)

| Bosqich | Holat |
|---|---|
| 3 Building Placement | kutilyapti |
| 4 Building Lifecycle | kutilyapti |
| 5 Resurs | kutilyapti |
| 6 Backend API | kutilyapti (backend alohida repoda) |
| 7 Kamera / touch | kutilyapti |

---

## Muhim

Bu **Unity-only** repo.  
Backend kodlari bu yerga push qilinmaydi.

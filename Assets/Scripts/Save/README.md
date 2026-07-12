#name Save / Persistence

Local + cloud-ready player cache for Kingdoms of Glory Mini.

## Authority

| Data | Source of truth |
|------|-----------------|
| Gold / mana / diamond / buildings / troops | **Server** (`GET /api/v1/player/state`) |
| Auth tokens | `SessionStore` (AES PlayerPrefs) |
| Offline UI hint / faster boot | **Local save** (`SaveSystem`) |
| Device mute / locale | Local `device_settings` |

Client cache **never** grants currency. After load, always revalidate with API.

## Features

- **Auto save** — dirty flag + interval; also on pause/quit
- **Manual save** — `SaveSystem.ManualSave()` / `ForceSave()`
- **Cloud ready** — `ICloudSaveBackend` (local mirror + `HttpCloudSaveBackend` via `SaveSystem.BindHttpCloud`)
- HTTP routes: `POST /api/v1/player/cloud-save/push`, `GET /api/v1/player/cloud-save/pull` (ADR)
- **Versioning** — `SaveEnvelope.schemaVersion` + `SaveMigrationPipeline`
- **Encryption ready** — `ISaveCrypto` (`DeviceAesSaveCrypto` / `PassThroughSaveCrypto`)
- **Multiplayer ready** — slots keyed by `playerId`; cloud push uses revision conflict detection

## Layout

```
Save/
  SaveSystem.cs              # orchestrator
  SaveEnvelope.cs            # versioned wrapper + payloads
  SaveSchema.cs
  SaveMigrationPipeline.cs
  ISaveCrypto.cs / DeviceAes… / PassThrough…
  ISaveRepository.cs / LocalFileSaveRepository.cs
  CloudSaveBackends.cs       # NoOp + LocalMirror
  SavePayloadFactory.cs
  SaveChecksum.cs
```

## Usage

```csharp
_save.CapturePlayerState(serverState); // marks dirty → auto flush
_save.ManualSave();
_save.TryLoadPlayerCache(playerId, out var cache); // offline hint only
_save.SyncFromCloud(playerId);
```

Files live under `Application.persistentDataPath/kog_saves/`.

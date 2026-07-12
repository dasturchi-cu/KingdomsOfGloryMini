using System;
using KoG.MiniMvp.Network;
using UnityEngine;

namespace KoG.MiniMvp.Save
{
    /// <summary>
    /// Auto + manual save orchestrator. Local cache + optional cloud mirror.
    /// Never treats client cache as economy authority — server wins on conflict.
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour
    {
        [SerializeField] bool encryptPayloads = true;
        [SerializeField] bool enableCloudMirror = true;
        [SerializeField] float autoSaveIntervalSeconds = 45f;
        [SerializeField] bool autoSaveOnPause = true;
        [SerializeField] bool autoSaveOnQuit = true;

        ISaveRepository _local;
        ISaveRepository _cloudMirrorRepo;
        ICloudSaveBackend _cloud;
        HttpCloudSaveBackend _httpCloud;
        ISaveCrypto _crypto;

        PlayerCachePayload _pendingCache;
        DeviceSettingsPayload _deviceSettings;
        long _revision;
        bool _dirty;
        float _autoTimer;
        bool _booted;

        public event Action<string> StatusChanged;
        public event Action<SaveResult> SaveCompleted;

        public bool IsDirty => _dirty;
        public long Revision => _revision;
        public PlayerCachePayload LastCache => _pendingCache;
        public DeviceSettingsPayload DeviceSettings => _deviceSettings;

        public void Configure(
            bool? encrypt = null,
            bool? cloudMirror = null,
            float? autoInterval = null)
        {
            if (encrypt.HasValue) encryptPayloads = encrypt.Value;
            if (cloudMirror.HasValue) enableCloudMirror = cloudMirror.Value;
            if (autoInterval.HasValue) autoSaveIntervalSeconds = Mathf.Max(5f, autoInterval.Value);
            EnsureDeps();
        }

        void Awake()
        {
            EnsureDeps();
            LoadDeviceSettings();
            _booted = true;
        }

        void Update()
        {
            if (!_booted || autoSaveIntervalSeconds <= 0f) return;
            if (!_dirty) return;
            _autoTimer += Time.unscaledDeltaTime;
            if (_autoTimer < autoSaveIntervalSeconds) return;
            _autoTimer = 0f;
            Flush(SaveTrigger.Auto);
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && autoSaveOnPause && _dirty)
                Flush(SaveTrigger.Pause);
        }

        void OnApplicationQuit()
        {
            if (autoSaveOnQuit && _dirty)
                Flush(SaveTrigger.Quit);
        }

        /// <summary>Wire HTTP cloud after ApiClient exists (best-effort async push).</summary>
        public void BindHttpCloud(ApiClient api, Func<string> tokenProvider)
        {
            if (api == null) return;
            _httpCloud = new HttpCloudSaveBackend(api, tokenProvider);
        }

        /// <summary>Mark player state from server for next auto/manual flush.</summary>
        public void CapturePlayerState(PlayerStateResponse state)
        {
            EnsureDeps();
            if (state == null || state.player == null) return;
            _pendingCache = SavePayloadFactory.FromPlayerState(state);
            _dirty = true;
            _autoTimer = 0f;
        }

        /// <summary>Update device settings blob (mute, volume, locale).</summary>
        public void SetDeviceSettings(DeviceSettingsPayload settings)
        {
            if (settings == null) return;
            _deviceSettings = settings;
            _dirty = true;
        }

        /// <summary>Manual save — flushes cache + settings + optional cloud push.</summary>
        public SaveResult ManualSave()
        {
            return Flush(SaveTrigger.Manual);
        }

        /// <summary>Force flush even if not dirty (manual / debug).</summary>
        public SaveResult ForceSave()
        {
            _dirty = true;
            return Flush(SaveTrigger.Manual);
        }

        /// <summary>Load last local player cache (offline hint). Server must revalidate.</summary>
        public SaveResult TryLoadPlayerCache(string playerId, out PlayerCachePayload cache)
        {
            cache = null;
            EnsureDeps();
            var slot = PlayerSlot(playerId);
            var read = _local.Read(slot);
            if (!read.Success) return read;

            var migrated = SaveMigrationPipeline.Migrate(read.Envelope);
            if (!migrated.Success) return migrated;

            var opened = OpenEnvelope(migrated.Envelope);
            if (!opened.Success) return opened;

            cache = SavePayloadFactory.DeserializePayload<PlayerCachePayload>(opened.Envelope.payload);
            _revision = Math.Max(_revision, migrated.Envelope.revision);
            return SaveResult.Ok(migrated.Envelope);
        }

        /// <summary>
        /// Pull cloud mirror then merge by revision (higher wins). Multiplayer-ready conflict hook.
        /// </summary>
        public SaveResult SyncFromCloud(string playerId)
        {
            EnsureDeps();
            if (_cloud == null || !_cloud.IsAvailable)
                return SaveResult.Fail(SaveResultKind.Missing, "cloud unavailable");

            var pull = _cloud.Pull(playerId, SaveSchema.PlayerCachePayloadType);
            if (!pull.Success) return pull;

            var migrated = SaveMigrationPipeline.Migrate(pull.Envelope);
            if (!migrated.Success) return migrated;

            var local = _local.Read(PlayerSlot(playerId));
            if (local.Success && local.Envelope != null &&
                local.Envelope.revision > migrated.Envelope.revision)
            {
                StatusChanged?.Invoke("Cloud older — keeping local r" + local.Envelope.revision);
                return SaveResult.Fail(SaveResultKind.Conflict, "local newer");
            }

            var write = _local.Write(PlayerSlot(playerId), migrated.Envelope);
            if (write.Success)
            {
                _revision = migrated.Envelope.revision;
                var opened = OpenEnvelope(migrated.Envelope);
                if (opened.Success)
                    _pendingCache = SavePayloadFactory.DeserializePayload<PlayerCachePayload>(
                        opened.Envelope.payload);
                StatusChanged?.Invoke("Cloud sync OK r" + _revision);
            }

            if (_httpCloud != null && _httpCloud.IsAvailable)
                StartCoroutine(PullHttpCloud(playerId));

            return write;
        }

        System.Collections.IEnumerator PushHttpCloud(SaveEnvelope envelope)
        {
            if (_httpCloud == null || envelope == null) yield break;
            SaveResult result = SaveResult.Fail(SaveResultKind.IoError, "pending");
            yield return _httpCloud.PushCoroutine(envelope.playerId, envelope, r => result = r);
            if (result.Kind == SaveResultKind.Conflict)
                StatusChanged?.Invoke("HTTP cloud conflict: " + result.Message);
            else if (result.Success)
                StatusChanged?.Invoke("HTTP cloud push OK r" + envelope.revision);
        }

        System.Collections.IEnumerator PullHttpCloud(string playerId)
        {
            if (_httpCloud == null) yield break;
            SaveResult result = SaveResult.Fail(SaveResultKind.Missing, "pending");
            yield return _httpCloud.PullCoroutine(
                playerId,
                SaveSchema.PlayerCachePayloadType,
                r => result = r);
            if (!result.Success || result.Envelope == null) yield break;
            if (result.Envelope.revision <= _revision) yield break;
            var write = _local.Write(PlayerSlot(playerId), result.Envelope);
            if (write.Success)
            {
                _revision = result.Envelope.revision;
                StatusChanged?.Invoke("HTTP cloud pull OK r" + _revision);
            }
        }

        public void ClearLocalPlayerCache(string playerId)
        {
            EnsureDeps();
            _local.Delete(PlayerSlot(playerId));
            _pendingCache = null;
            _dirty = false;
        }

        SaveResult Flush(SaveTrigger trigger)
        {
            EnsureDeps();
            if (!_dirty && trigger != SaveTrigger.Manual)
                return SaveResult.Ok(null);

            SaveResult last = SaveResult.Ok(null);

            if (_pendingCache != null && !string.IsNullOrEmpty(_pendingCache.playerId))
            {
                last = WritePayload(
                    PlayerSlot(_pendingCache.playerId),
                    SaveSchema.PlayerCachePayloadType,
                    _pendingCache.playerId,
                    SavePayloadFactory.SerializePayload(_pendingCache));
                if (!last.Success)
                {
                    StatusChanged?.Invoke("Save failed: " + last.Message);
                    SaveCompleted?.Invoke(last);
                    return last;
                }

                if (_cloud != null && _cloud.IsAvailable)
                {
                    var push = _cloud.Push(_pendingCache.playerId, last.Envelope);
                    if (!push.Success && push.Kind == SaveResultKind.Conflict)
                        StatusChanged?.Invoke("Cloud conflict: " + push.Message);
                }

                if (_httpCloud != null && _httpCloud.IsAvailable && last.Envelope != null)
                    StartCoroutine(PushHttpCloud(last.Envelope));
            }

            if (_deviceSettings != null)
            {
                var settingsResult = WritePayload(
                    "device_settings",
                    SaveSchema.DeviceSettingsPayloadType,
                    string.Empty,
                    SavePayloadFactory.SerializePayload(_deviceSettings));
                if (!settingsResult.Success)
                    last = settingsResult;
            }

            _dirty = false;
            _autoTimer = 0f;
            var msg = trigger + " save OK r" + _revision;
            StatusChanged?.Invoke(msg);
            SaveCompleted?.Invoke(last);
            return last;
        }

        SaveResult WritePayload(string slotKey, string payloadType, string playerId, string plainPayload)
        {
            _revision = Math.Max(1, _revision + 1);
            var storePayload = plainPayload ?? string.Empty;
            var encrypted = false;
            if (_crypto != null && _crypto.IsEnabled)
            {
                storePayload = _crypto.Encrypt(storePayload);
                encrypted = true;
            }

            var envelope = new SaveEnvelope
            {
                schemaVersion = SaveSchema.CurrentVersion,
                payloadType = payloadType,
                playerId = playerId ?? string.Empty,
                revision = _revision,
                writtenAtUtc = DateTime.UtcNow.ToString("o"),
                encrypted = encrypted,
                payload = storePayload,
                checksumSha256 = SaveChecksum.Sha256Hex(plainPayload ?? string.Empty)
            };

            return _local.Write(slotKey, envelope);
        }

        SaveResult OpenEnvelope(SaveEnvelope envelope)
        {
            if (envelope == null)
                return SaveResult.Fail(SaveResultKind.Corrupt, "null");

            var plain = envelope.payload ?? string.Empty;
            if (envelope.encrypted)
            {
                if (_crypto == null || !_crypto.IsEnabled)
                    _crypto = new DeviceAesSaveCrypto();
                plain = _crypto.Decrypt(plain);
                if (string.IsNullOrEmpty(plain) && !string.IsNullOrEmpty(envelope.payload))
                    return SaveResult.Fail(SaveResultKind.Corrupt, "decrypt failed");
            }

            if (!SaveChecksum.Matches(plain, envelope.checksumSha256))
                return SaveResult.Fail(SaveResultKind.Corrupt, "checksum mismatch");

            // Return a shallow copy with decrypted payload for callers.
            var opened = new SaveEnvelope
            {
                schemaVersion = envelope.schemaVersion,
                payloadType = envelope.payloadType,
                playerId = envelope.playerId,
                revision = envelope.revision,
                writtenAtUtc = envelope.writtenAtUtc,
                checksumSha256 = envelope.checksumSha256,
                encrypted = false,
                payload = plain
            };
            return SaveResult.Ok(opened);
        }

        void LoadDeviceSettings()
        {
            var read = _local.Read("device_settings");
            if (!read.Success)
            {
                _deviceSettings = new DeviceSettingsPayload();
                return;
            }

            var migrated = SaveMigrationPipeline.Migrate(read.Envelope);
            if (!migrated.Success)
            {
                _deviceSettings = new DeviceSettingsPayload();
                return;
            }

            var opened = OpenEnvelope(migrated.Envelope);
            _deviceSettings = opened.Success
                ? SavePayloadFactory.DeserializePayload<DeviceSettingsPayload>(opened.Envelope.payload)
                : new DeviceSettingsPayload();
        }

        void EnsureDeps()
        {
            if (_local == null)
                _local = new LocalFileSaveRepository();
            if (_crypto == null)
                _crypto = encryptPayloads
                    ? (ISaveCrypto)new DeviceAesSaveCrypto()
                    : new PassThroughSaveCrypto();
            if (_cloud == null)
            {
                if (enableCloudMirror)
                {
                    _cloudMirrorRepo = new LocalFileSaveRepository(SaveSchema.RootFolderName + "_cloud");
                    _cloud = new LocalMirrorCloudBackend(_cloudMirrorRepo);
                }
                else
                {
                    _cloud = new NoOpCloudSaveBackend();
                }
            }
        }

        static string PlayerSlot(string playerId) =>
            "player_" + (string.IsNullOrEmpty(playerId) ? "anon" : playerId);

        enum SaveTrigger
        {
            Auto,
            Manual,
            Pause,
            Quit
        }
    }
}

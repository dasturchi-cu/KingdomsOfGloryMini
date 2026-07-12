namespace KoG.MiniMvp.Save
{
    /// <summary>Cloud disabled — local-only until HTTP cloud save ships.</summary>
    public sealed class NoOpCloudSaveBackend : ICloudSaveBackend
    {
        public string BackendId => "noop";
        public bool IsAvailable => false;

        public SaveResult Push(string playerId, SaveEnvelope envelope) =>
            SaveResult.Fail(SaveResultKind.Missing, "cloud disabled");

        public SaveResult Pull(string playerId, string payloadType) =>
            SaveResult.Fail(SaveResultKind.Missing, "cloud disabled");
    }

    /// <summary>
    /// Cloud-ready local mirror: writes a second copy under cloud_mirror/.
    /// Swap for HttpCloudSaveBackend when multiplayer account sync is live.
    /// </summary>
    public sealed class LocalMirrorCloudBackend : ICloudSaveBackend
    {
        readonly ISaveRepository _mirror;

        public LocalMirrorCloudBackend(ISaveRepository mirror)
        {
            _mirror = mirror;
        }

        public string BackendId => "local-mirror";
        public bool IsAvailable => true;

        public SaveResult Push(string playerId, SaveEnvelope envelope)
        {
            if (envelope == null) return SaveResult.Fail(SaveResultKind.IoError, "null");
            var key = Slot(playerId, envelope.payloadType);
            var existing = _mirror.Read(key);
            if (existing.Success && existing.Envelope != null &&
                existing.Envelope.revision > envelope.revision)
            {
                return SaveResult.Fail(SaveResultKind.Conflict,
                    "remote revision newer (" + existing.Envelope.revision + ")");
            }

            return _mirror.Write(key, envelope);
        }

        public SaveResult Pull(string playerId, string payloadType)
        {
            return _mirror.Read(Slot(playerId, payloadType));
        }

        static string Slot(string playerId, string payloadType) =>
            "cloud_" + (playerId ?? "anon") + "_" + (payloadType ?? "unknown");
    }
}

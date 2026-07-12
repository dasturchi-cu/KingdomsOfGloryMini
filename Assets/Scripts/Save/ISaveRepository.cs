using System;

namespace KoG.MiniMvp.Save
{
    public enum SaveResultKind
    {
        Ok = 0,
        Missing = 1,
        Corrupt = 2,
        VersionUnsupported = 3,
        IoError = 4,
        Conflict = 5
    }

    public readonly struct SaveResult
    {
        public readonly SaveResultKind Kind;
        public readonly string Message;
        public readonly SaveEnvelope Envelope;

        public bool Success => Kind == SaveResultKind.Ok;

        public SaveResult(SaveResultKind kind, string message = "", SaveEnvelope envelope = null)
        {
            Kind = kind;
            Message = message ?? string.Empty;
            Envelope = envelope;
        }

        public static SaveResult Ok(SaveEnvelope envelope) =>
            new SaveResult(SaveResultKind.Ok, string.Empty, envelope);

        public static SaveResult Fail(SaveResultKind kind, string message) =>
            new SaveResult(kind, message);
    }

    /// <summary>Local durable store (files under persistentDataPath).</summary>
    public interface ISaveRepository
    {
        SaveResult Write(string slotKey, SaveEnvelope envelope);
        SaveResult Read(string slotKey);
        bool Exists(string slotKey);
        void Delete(string slotKey);
    }

    /// <summary>
    /// Cloud-ready port. Push/Pull keyed by playerId for multiplayer accounts.
    /// Mini-MVP ships with a local mirror; HTTP adapter plugs in later.
    /// </summary>
    public interface ICloudSaveBackend
    {
        string BackendId { get; }
        bool IsAvailable { get; }
        SaveResult Push(string playerId, SaveEnvelope envelope);
        SaveResult Pull(string playerId, string payloadType);
    }
}

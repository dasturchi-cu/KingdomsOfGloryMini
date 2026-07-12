using System;

namespace KoG.MiniMvp.Save
{
    /// <summary>
    /// Versioned wrapper around any save payload. Cloud and local share the same shape.
    /// </summary>
    [Serializable]
    public class SaveEnvelope
    {
        public int schemaVersion = SaveSchema.CurrentVersion;
        public string payloadType = "";
        public string playerId = "";
        public long revision;
        public string writtenAtUtc = "";
        public string checksumSha256 = "";
        public bool encrypted;
        public string payload = "";
    }

    [Serializable]
    public class PlayerCachePayload
    {
        public string playerId = "";
        public string nickname = "";
        public int castleLevel;
        public long gold;
        public long mana;
        public long diamond;
        public string buildingsJson = "[]";
        public string troopsJson = "[]";
        public string campaignsJson = "[]";
        public string source = "server";
        public long capturedAtUnix;
    }

    [Serializable]
    public class DeviceSettingsPayload
    {
        public bool muted;
        public float masterVolume = 1f;
        public string locale = "uz";
        public int lastUiTheme;
    }
}

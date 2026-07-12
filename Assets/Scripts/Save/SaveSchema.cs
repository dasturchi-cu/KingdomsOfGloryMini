namespace KoG.MiniMvp.Save
{
    /// <summary>
    /// Schema versions for local/cloud envelopes. Bump when payload shape changes.
    /// </summary>
    public static class SaveSchema
    {
        public const int CurrentVersion = 1;
        public const string PlayerCachePayloadType = "player_cache";
        public const string DeviceSettingsPayloadType = "device_settings";
        public const string RootFolderName = "kog_saves";
    }
}

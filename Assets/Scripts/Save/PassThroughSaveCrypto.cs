namespace KoG.MiniMvp.Save
{
    /// <summary>No encryption — useful for Editor debugging only.</summary>
    public sealed class PassThroughSaveCrypto : ISaveCrypto
    {
        public string Name => "passthrough";
        public bool IsEnabled => false;

        public string Encrypt(string plainUtf8) => plainUtf8 ?? string.Empty;

        public string Decrypt(string cipherOrPlain) => cipherOrPlain ?? string.Empty;
    }
}

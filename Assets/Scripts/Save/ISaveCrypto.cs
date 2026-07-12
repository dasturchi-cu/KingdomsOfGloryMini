namespace KoG.MiniMvp.Save
{
    /// <summary>
    /// Pluggable payload encryption. Production can swap device AES for Keychain/platform crypto.
    /// </summary>
    public interface ISaveCrypto
    {
        string Name { get; }
        bool IsEnabled { get; }
        string Encrypt(string plainUtf8);
        string Decrypt(string cipherOrPlain);
    }
}

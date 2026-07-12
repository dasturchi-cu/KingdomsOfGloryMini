using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KoG.MiniMvp.Save
{
    /// <summary>
    /// AES-CBC with device-bound key. Encryption-ready baseline — not a substitute for server authority.
    /// </summary>
    public sealed class DeviceAesSaveCrypto : ISaveCrypto
    {
        const string AppSalt = "KoG.MiniMvp.Save.v1";

        public string Name => "aes-device";
        public bool IsEnabled => true;

        public string Encrypt(string plainUtf8)
        {
            if (string.IsNullOrEmpty(plainUtf8)) return string.Empty;
            try
            {
                var key = DeriveKey();
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.GenerateIV();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (var encryptor = aes.CreateEncryptor())
                    {
                        var plainBytes = Encoding.UTF8.GetBytes(plainUtf8);
                        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                        var packed = new byte[aes.IV.Length + cipher.Length];
                        Buffer.BlockCopy(aes.IV, 0, packed, 0, aes.IV.Length);
                        Buffer.BlockCopy(cipher, 0, packed, aes.IV.Length, cipher.Length);
                        return Convert.ToBase64String(packed);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Encrypt failed: " + e.Message);
                return string.Empty;
            }
        }

        public string Decrypt(string packedB64)
        {
            if (string.IsNullOrEmpty(packedB64)) return string.Empty;
            try
            {
                var packed = Convert.FromBase64String(packedB64);
                if (packed.Length < 17) return string.Empty;
                var key = DeriveKey();
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    var iv = new byte[16];
                    Buffer.BlockCopy(packed, 0, iv, 0, 16);
                    aes.IV = iv;
                    var cipher = new byte[packed.Length - 16];
                    Buffer.BlockCopy(packed, 16, cipher, 0, cipher.Length);
                    using (var decryptor = aes.CreateDecryptor())
                    {
                        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Decrypt failed: " + e.Message);
                return string.Empty;
            }
        }

        static byte[] DeriveKey()
        {
            var material = AppSalt + "|" + SystemInfo.deviceUniqueIdentifier;
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(material));
        }
    }
}

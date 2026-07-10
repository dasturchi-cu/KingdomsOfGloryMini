using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KoG.MiniMvp.Network
{
    /// <summary>
    /// AES-encrypted PlayerPrefs for auth secrets.
    /// Key is device-bound (not a substitute for platform Keychain in production).
    /// </summary>
    public static class SessionStore
    {
        const string TokenKey = "kog_mini_token_v2";
        const string RefreshKey = "kog_mini_refresh_v2";
        const string PlayerIdKey = "kog_mini_player_id_v2";

        // Legacy plaintext keys — migrated once then deleted.
        const string LegacyTokenKey = "kog_mini_token";
        const string LegacyRefreshKey = "kog_mini_refresh";
        const string LegacyPlayerIdKey = "kog_mini_player_id";

        const string AppSalt = "KoG.MiniMvp.Session.v1";

        static bool _migrated;

        public static string Token
        {
            get
            {
                EnsureMigrated();
                return Decrypt(PlayerPrefs.GetString(TokenKey, string.Empty));
            }
            set => PlayerPrefs.SetString(TokenKey, Encrypt(value ?? string.Empty));
        }

        public static string RefreshToken
        {
            get
            {
                EnsureMigrated();
                return Decrypt(PlayerPrefs.GetString(RefreshKey, string.Empty));
            }
            set => PlayerPrefs.SetString(RefreshKey, Encrypt(value ?? string.Empty));
        }

        public static string PlayerId
        {
            get
            {
                EnsureMigrated();
                return Decrypt(PlayerPrefs.GetString(PlayerIdKey, string.Empty));
            }
            set => PlayerPrefs.SetString(PlayerIdKey, Encrypt(value ?? string.Empty));
        }

        public static bool HasSession =>
            !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(PlayerId);

        public static void Save(string playerId, string token, string refreshToken)
        {
            EnsureMigrated();
            PlayerId = playerId;
            Token = token;
            RefreshToken = refreshToken;
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(RefreshKey);
            PlayerPrefs.DeleteKey(PlayerIdKey);
            PlayerPrefs.DeleteKey(LegacyTokenKey);
            PlayerPrefs.DeleteKey(LegacyRefreshKey);
            PlayerPrefs.DeleteKey(LegacyPlayerIdKey);
            PlayerPrefs.Save();
            _migrated = true;
        }

        static void EnsureMigrated()
        {
            if (_migrated) return;
            _migrated = true;

            var legacyToken = PlayerPrefs.GetString(LegacyTokenKey, string.Empty);
            var legacyRefresh = PlayerPrefs.GetString(LegacyRefreshKey, string.Empty);
            var legacyPlayerId = PlayerPrefs.GetString(LegacyPlayerIdKey, string.Empty);
            if (string.IsNullOrEmpty(legacyToken) && string.IsNullOrEmpty(legacyPlayerId))
                return;

            // Only migrate if v2 slots are empty.
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(TokenKey, string.Empty)) &&
                !string.IsNullOrEmpty(legacyToken))
            {
                PlayerPrefs.SetString(TokenKey, Encrypt(legacyToken));
                PlayerPrefs.SetString(RefreshKey, Encrypt(legacyRefresh));
                PlayerPrefs.SetString(PlayerIdKey, Encrypt(legacyPlayerId));
                PlayerPrefs.DeleteKey(LegacyTokenKey);
                PlayerPrefs.DeleteKey(LegacyRefreshKey);
                PlayerPrefs.DeleteKey(LegacyPlayerIdKey);
                PlayerPrefs.Save();
                Debug.Log("[MiniMvp] Session migrated to encrypted store");
            }
        }

        static byte[] DeriveKey()
        {
            var material = AppSalt + "|" + SystemInfo.deviceUniqueIdentifier;
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(material));
        }

        static string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
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
                        var plainBytes = Encoding.UTF8.GetBytes(plain);
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
                Debug.LogWarning("[MiniMvp] Session encrypt failed: " + e.Message);
                return string.Empty;
            }
        }

        static string Decrypt(string packedB64)
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
                Debug.LogWarning("[MiniMvp] Session decrypt failed: " + e.Message);
                return string.Empty;
            }
        }
    }
}

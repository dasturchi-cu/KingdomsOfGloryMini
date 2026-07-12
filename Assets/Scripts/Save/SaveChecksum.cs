using System;
using System.Security.Cryptography;
using System.Text;

namespace KoG.MiniMvp.Save
{
    public static class SaveChecksum
    {
        public static string Sha256Hex(string text)
        {
            if (text == null) text = string.Empty;
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool Matches(string text, string expectedHex)
        {
            if (string.IsNullOrEmpty(expectedHex)) return true;
            return string.Equals(Sha256Hex(text), expectedHex, StringComparison.OrdinalIgnoreCase);
        }
    }
}

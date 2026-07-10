using UnityEngine;

namespace KoG.MiniMvp.Network
{
    /// <summary>
    /// Local session storage for Mini-MVP auth tokens.
    /// </summary>
    public static class SessionStore
    {
        const string TokenKey = "kog_mini_token";
        const string RefreshKey = "kog_mini_refresh";
        const string PlayerIdKey = "kog_mini_player_id";

        public static string Token
        {
            get => PlayerPrefs.GetString(TokenKey, string.Empty);
            set => PlayerPrefs.SetString(TokenKey, value ?? string.Empty);
        }

        public static string RefreshToken
        {
            get => PlayerPrefs.GetString(RefreshKey, string.Empty);
            set => PlayerPrefs.SetString(RefreshKey, value ?? string.Empty);
        }

        public static string PlayerId
        {
            get => PlayerPrefs.GetString(PlayerIdKey, string.Empty);
            set => PlayerPrefs.SetString(PlayerIdKey, value ?? string.Empty);
        }

        public static bool HasSession =>
            !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(PlayerId);

        public static void Save(string playerId, string token, string refreshToken)
        {
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
            PlayerPrefs.Save();
        }
    }
}

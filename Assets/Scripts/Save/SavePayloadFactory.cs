using System;
using KoG.MiniMvp.Network;
using UnityEngine;

namespace KoG.MiniMvp.Save
{
    /// <summary>
    /// Builds / reads typed payloads inside envelopes. Player cache is a hint only —
    /// economy and buildings remain server-authoritative.
    /// </summary>
    public static class SavePayloadFactory
    {
        public static PlayerCachePayload FromPlayerState(PlayerStateResponse state)
        {
            var p = state != null ? state.player : null;
            return new PlayerCachePayload
            {
                playerId = p != null ? p.id ?? string.Empty : string.Empty,
                nickname = p != null ? p.nickname ?? string.Empty : string.Empty,
                castleLevel = p != null ? p.castleLevel : 0,
                gold = p != null ? p.gold : 0,
                mana = p != null ? p.mana : 0,
                diamond = p != null ? p.diamond : 0,
                buildingsJson = ToJsonArray(state != null ? state.buildings : null),
                troopsJson = ToJsonArray(state != null ? state.troops : null),
                campaignsJson = ToJsonArray(state != null ? state.campaigns : null),
                source = "server",
                capturedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        public static string SerializePayload<T>(T payload) where T : class =>
            payload == null ? "{}" : JsonUtility.ToJson(payload, false);

        public static T DeserializePayload<T>(string json) where T : class, new()
        {
            if (string.IsNullOrEmpty(json)) return new T();
            try
            {
                var obj = JsonUtility.FromJson<T>(json);
                return obj ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        static string ToJsonArray<T>(T[] items)
        {
            if (items == null || items.Length == 0) return "[]";
            // JsonUtility cannot serialize top-level arrays — wrap.
            return JsonUtility.ToJson(new ArrayBox<T> { items = items }, false);
        }

        [Serializable]
        class ArrayBox<T>
        {
            public T[] items;
        }
    }
}

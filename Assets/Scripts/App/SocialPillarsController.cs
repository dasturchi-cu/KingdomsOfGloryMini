using System;
using System.Collections;
using System.Text;
using KoG.MiniMvp.Network;
using UnityEngine;

namespace KoG.MiniMvp.App
{
    /// <summary>
    /// Core social pillars: Clan, Chat, PvP find, Tournament join.
    /// Driven from MiniMvpHud; talks to existing /api/v1 routes.
    /// </summary>
    public sealed class SocialPillarsController : MonoBehaviour
    {
        ApiClient _api;
        Action<string> _setStatus;
        Action<bool> _setBusy;
        Func<string, string> _extractError;
        string _clanId = "";
        string _tournamentId = "";

        public string ClanId => _clanId;
        public string TournamentId => _tournamentId;

        public void Bind(
            ApiClient api,
            Action<string> setStatus,
            Action<bool> setBusy,
            Func<string, string> extractError)
        {
            _api = api;
            _setStatus = setStatus;
            _setBusy = setBusy;
            _extractError = extractError ?? (text => text ?? "");
        }

        public IEnumerator CreateClan()
        {
            if (_api == null || !SessionStore.HasSession)
            {
                _setStatus?.Invoke("Avval Guest/Login qiling");
                yield break;
            }

            _setBusy?.Invoke(true);
            _setStatus?.Invoke("Klan yaratilmoqda...");
            var name = "Klan_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var body = Json(
                ("playerId", SessionStore.PlayerId),
                ("name", name),
                ("type", "open")
            );
            yield return _api.PostJson(
                "/api/v1/clan/create",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _setBusy?.Invoke(false);
                    if (code < 200 || code >= 300)
                    {
                        _setStatus?.Invoke("Klan xato: " + _extractError(text));
                        return;
                    }

                    var id = ReadString(text, "clanId");
                    var clanName = ReadString(text, "clanName");
                    if (!string.IsNullOrEmpty(id)) _clanId = id;
                    _setStatus?.Invoke("✅ Klan: " + (string.IsNullOrEmpty(clanName) ? name : clanName));
                });
        }

        public IEnumerator SendGlobalChat()
        {
            if (_api == null || !SessionStore.HasSession)
            {
                _setStatus?.Invoke("Avval Guest/Login qiling");
                yield break;
            }

            _setBusy?.Invoke(true);
            _setStatus?.Invoke("Chat yuborilmoqda...");
            var msg = "Salom KoG! " + DateTime.UtcNow.ToString("HH:mm:ss");
            var body = Json(
                ("channelType", "global"),
                ("body", msg),
                ("senderId", SessionStore.PlayerId)
            );
            yield return _api.PostJson("/api/v1/chat/send", body, SessionStore.Token, null, (code, text) =>
            {
                _setBusy?.Invoke(false);
                if (code < 200 || code >= 300)
                {
                    _setStatus?.Invoke("Chat xato: " + _extractError(text));
                    return;
                }

                _setStatus?.Invoke("✅ Chat: " + msg);
            });
        }

        public IEnumerator FindPvp()
        {
            if (_api == null || !SessionStore.HasSession)
            {
                _setStatus?.Invoke("Avval Guest/Login qiling");
                yield break;
            }

            _setBusy?.Invoke(true);
            _setStatus?.Invoke("Live PvP jang...");
            var practiceBody = "{\"playerId\":\"" + SessionStore.PlayerId + "\",\"skipCost\":true}";
            yield return _api.PostJson(
                "/api/v1/battle/live/practice",
                practiceBody,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _setBusy?.Invoke(false);
                    if (code < 200 || code >= 300)
                    {
                        _setStatus?.Invoke("Live PvP xato: " + _extractError(text));
                        return;
                    }

                    var nick = ReadNestedString(text, "opponent", "nickname");
                    if (string.IsNullOrEmpty(nick)) nick = "raqib";
                    var ticket = ReadString(text, "ticketId");
                    var tip = string.IsNullOrEmpty(ticket) ? "" : " · ticket " + ShortId(ticket);
                    _setStatus?.Invoke("✅ Live PvP vs " + nick + tip);
                });
        }

        public IEnumerator JoinTournament()
        {
            if (_api == null || !SessionStore.HasSession)
            {
                _setStatus?.Invoke("Avval Guest/Login qiling");
                yield break;
            }

            _setBusy?.Invoke(true);
            _setStatus?.Invoke("Turnirga yozilmoqda...");

            string ensureText = null;
            long ensureCode = 0;
            yield return _api.PostJson(
                "/api/v1/tournament/ensure-default",
                "{}",
                SessionStore.Token,
                null,
                (code, text) =>
                {
                    ensureCode = code;
                    ensureText = text;
                });

            if (ensureCode < 200 || ensureCode >= 300)
            {
                _setBusy?.Invoke(false);
                _setStatus?.Invoke("Turnir ensure failed: " + _extractError(ensureText));
                yield break;
            }

            _tournamentId = ReadString(ensureText, "tournamentId");
            var joinBody = Json(
                ("playerId", SessionStore.PlayerId),
                ("tournamentId", _tournamentId)
            );
            var joinOk = false;
            yield return _api.PostJson(
                "/api/v1/tournament/join",
                joinBody,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    if (code < 200 || code >= 300)
                    {
                        _setBusy?.Invoke(false);
                        _setStatus?.Invoke("Turnir join xato: " + _extractError(text));
                        return;
                    }

                    joinOk = true;
                });

            if (!joinOk) yield break;

            // Generate only if missing; server reuses existing bracket (no wipe).
            yield return _api.PostJson(
                "/api/v1/tournament/" + _tournamentId + "/bracket/generate",
                "{}",
                SessionStore.Token,
                null,
                (code, text) =>
                {
                    _setBusy?.Invoke(false);
                    var shortId = _tournamentId.Length > 8 ? _tournamentId.Substring(0, 8) : _tournamentId;
                    if (code < 200 || code >= 300)
                    {
                        _setStatus?.Invoke("Turnir OK (" + shortId + "). Bracket: " + _extractError(text));
                        return;
                    }

                    var size = ReadNestedString(text, "bracket", "bracketSize");
                    if (string.IsNullOrEmpty(size)) size = ReadString(text, "bracketSize");
                    var reused = text != null && text.IndexOf("\"reused\":true", StringComparison.Ordinal) >= 0;
                    _setStatus?.Invoke(
                        "✅ Turnir + Bracket (" + shortId + ", size " + size +
                        (reused ? ", mavjud" : "") + ")");
                });
        }

        /// <summary>
        /// Client "watched self ad" stub → server grants 500 gold + 500 mana + 5 diamond (all regions).
        /// Trust: short watch delay + watchedStub=true (server rejects bare claims).
        /// </summary>
        public IEnumerator ClaimRewardedAd(Action<long, long, long> onBalance = null)
        {
            if (_api == null || !SessionStore.HasSession)
            {
                _setStatus?.Invoke("Avval Guest/Login qiling");
                yield break;
            }

            _setBusy?.Invoke(true);
            _setStatus?.Invoke("📺 Reklama stub (~1s)…");
            // Soft-GO watch stub (~1.2s). Real SDK replaces this later.
            yield return new WaitForSeconds(1.2f);

            _setStatus?.Invoke("Mukofot olinmoqda…");
            var body = "{\"playerId\":\"" + SessionStore.PlayerId +
                       "\",\"region\":\"*\",\"placement\":\"rewarded_self\",\"watchedStub\":true}";
            yield return _api.PostJson(
                "/api/v1/ads/rewarded/claim",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _setBusy?.Invoke(false);
                    if (code < 200 || code >= 300)
                    {
                        _setStatus?.Invoke("Reklama xato: " + _extractError(text));
                        return;
                    }

                    var g = ReadNestedString(text, "balance", "gold");
                    var m = ReadNestedString(text, "balance", "mana");
                    var d = ReadNestedString(text, "balance", "diamond");
                    long gold = 0, mana = 0, diamond = 0;
                    long.TryParse(g, out gold);
                    long.TryParse(m, out mana);
                    long.TryParse(d, out diamond);
                    onBalance?.Invoke(gold, mana, diamond);
                    _setStatus?.Invoke("✅ +500● +500◆ +5◇ (reklama). Balans yangilandi.");
                });
        }

        static string Json(params (string key, string value)[] pairs)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            for (var i = 0; i < pairs.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(pairs[i].key).Append("\":\"");
                sb.Append(Escape(pairs[i].value)).Append('"');
            }

            sb.Append('}');
            return sb.ToString();
        }

        static string Escape(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string ReadString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            var token = "\"" + key + "\":\"";
            var i = json.IndexOf(token, StringComparison.Ordinal);
            if (i < 0)
            {
                // unquoted number/bool skip
                token = "\"" + key + "\":";
                i = json.IndexOf(token, StringComparison.Ordinal);
                if (i < 0) return "";
                i += token.Length;
                while (i < json.Length && (json[i] == ' ' || json[i] == '"')) i++;
                var end = i;
                while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != '"') end++;
                return json.Substring(i, end - i).Trim('"');
            }

            i += token.Length;
            var e = json.IndexOf('"', i);
            return e > i ? json.Substring(i, e - i) : "";
        }

        static string ReadNestedString(string json, string obj, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var marker = "\"" + obj + "\":";
            var i = json.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return "";
            return ReadString(json.Substring(i), key);
        }

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            return id.Length > 8 ? id.Substring(0, 8) : id;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using KoG.MiniMvp.Network;
using UnityEngine;

namespace KoG.MiniMvp.App
{
    /// <summary>
    /// Core social pillars: Clan, Chat, PvP, Tournament, Rewarded ad.
    /// P0-09: minimum viable depth (roster / history / result / bracket summary).
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
                    if (code < 200 || code >= 300)
                    {
                        _setBusy?.Invoke(false);
                        _setStatus?.Invoke("Klan xato: " + _extractError(text));
                        return;
                    }

                    var id = ReadString(text, "clanId");
                    var clanName = ReadString(text, "clanName");
                    if (!string.IsNullOrEmpty(id)) _clanId = id;
                    _setStatus?.Invoke("Klan: " + (string.IsNullOrEmpty(clanName) ? name : clanName));
                });

            if (string.IsNullOrEmpty(_clanId))
            {
                _setBusy?.Invoke(false);
                yield break;
            }

            // Minimum depth: show roster.
            string membersText = null;
            long membersCode = 0;
            yield return _api.GetJson(
                "/api/v1/clan/" + _clanId + "/members",
                SessionStore.Token,
                (code, text) =>
                {
                    membersCode = code;
                    membersText = text;
                });

            _setBusy?.Invoke(false);
            if (membersCode >= 200 && membersCode < 300)
            {
                var nicknames = ExtractNicknames(membersText, 5);
                _setStatus?.Invoke(
                    "Klan OK · a’zolar " + nicknames.Count +
                    (nicknames.Count > 0 ? ": " + string.Join(", ", nicknames) : ""));
            }
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
            var sendOk = false;
            yield return _api.PostJson("/api/v1/chat/send", body, SessionStore.Token, null, (code, text) =>
            {
                if (code < 200 || code >= 300)
                {
                    _setBusy?.Invoke(false);
                    _setStatus?.Invoke("Chat xato: " + _extractError(text));
                    return;
                }

                sendOk = true;
            });

            if (!sendOk) yield break;

            string histText = null;
            long histCode = 0;
            yield return _api.GetJson(
                "/api/v1/chat/history?channelType=global&limit=8",
                SessionStore.Token,
                (code, text) =>
                {
                    histCode = code;
                    histText = text;
                });

            _setBusy?.Invoke(false);
            if (histCode >= 200 && histCode < 300)
            {
                var lines = ExtractChatBodies(histText, 3);
                if (lines.Count > 0)
                    _setStatus?.Invoke("Chat · oxirgi: " + string.Join(" | ", lines));
                else
                    _setStatus?.Invoke("Chat yuborildi: " + msg);
            }
            else
            {
                _setStatus?.Invoke("Chat yuborildi: " + msg);
            }
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
                    if (string.IsNullOrEmpty(nick)) nick = ReadNestedString(text, "defender", "nickname");
                    if (string.IsNullOrEmpty(nick)) nick = "raqib";
                    var stars = ReadString(text, "stars");
                    if (string.IsNullOrEmpty(stars)) stars = ReadNestedString(text, "result", "stars");
                    var lootG = ReadString(text, "lootGold");
                    if (string.IsNullOrEmpty(lootG)) lootG = ReadNestedString(text, "loot", "gold");
                    var lootM = ReadString(text, "lootMana");
                    if (string.IsNullOrEmpty(lootM)) lootM = ReadNestedString(text, "loot", "mana");
                    var won = text != null && (
                        text.IndexOf("\"victory\":true", StringComparison.Ordinal) >= 0
                        || text.IndexOf("\"won\":true", StringComparison.Ordinal) >= 0
                        || (!string.IsNullOrEmpty(stars) && stars != "0"));
                    var outcome = won ? "G‘alaba" : "Natija";
                    _setStatus?.Invoke(
                        "PvP " + outcome + " vs " + nick +
                        (string.IsNullOrEmpty(stars) ? "" : " · ★" + stars) +
                        (string.IsNullOrEmpty(lootG) ? "" : " · +" + lootG + "●") +
                        (string.IsNullOrEmpty(lootM) ? "" : " +" + lootM + "◆"));
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

            yield return _api.PostJson(
                "/api/v1/tournament/" + _tournamentId + "/bracket/generate",
                "{}",
                SessionStore.Token,
                null,
                (code, text) =>
                {
                    // continue to GET bracket either way
                });

            string bracketText = null;
            long bracketCode = 0;
            yield return _api.GetJson(
                "/api/v1/tournament/" + _tournamentId + "/bracket",
                SessionStore.Token,
                (code, text) =>
                {
                    bracketCode = code;
                    bracketText = text;
                });

            _setBusy?.Invoke(false);
            var shortId = _tournamentId.Length > 8 ? _tournamentId.Substring(0, 8) : _tournamentId;
            if (bracketCode >= 200 && bracketCode < 300)
            {
                var size = ReadNestedString(bracketText, "bracket", "bracketSize");
                if (string.IsNullOrEmpty(size)) size = ReadString(bracketText, "bracketSize");
                var matchCount = CountOccurrences(bracketText, "\"matchId\"");
                _setStatus?.Invoke(
                    "Turnir " + shortId + " · bracket " +
                    (string.IsNullOrEmpty(size) ? "?" : size) +
                    " · match " + matchCount);
            }
            else
            {
                _setStatus?.Invoke("Turnir OK (" + shortId + "). Bracket hali yo‘q (2+ o‘yinchi kerak).");
            }
        }

        public IEnumerator ClaimRewardedAd(Action<long, long, long> onBalance = null)
        {
            if (_api == null || !SessionStore.HasSession)
            {
                _setStatus?.Invoke("Avval Guest/Login qiling");
                yield break;
            }

            _setBusy?.Invoke(true);
            _setStatus?.Invoke("Reklama stub (~1s)…");
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
                    _setStatus?.Invoke("+500● +500◆ +5◇ (reklama). Balans yangilandi.");
                });
        }

        static List<string> ExtractNicknames(string json, int max)
        {
            var list = new List<string>(max);
            if (string.IsNullOrEmpty(json)) return list;
            var idx = 0;
            while (list.Count < max)
            {
                var token = "\"nickname\":\"";
                var i = json.IndexOf(token, idx, StringComparison.Ordinal);
                if (i < 0) break;
                i += token.Length;
                var e = json.IndexOf('"', i);
                if (e <= i) break;
                list.Add(json.Substring(i, e - i));
                idx = e + 1;
            }
            return list;
        }

        static List<string> ExtractChatBodies(string json, int max)
        {
            var list = new List<string>(max);
            if (string.IsNullOrEmpty(json)) return list;
            // Prefer "body" then "message"
            CollectQuotedField(json, "body", list, max);
            if (list.Count == 0) CollectQuotedField(json, "message", list, max);
            return list;
        }

        static void CollectQuotedField(string json, string key, List<string> list, int max)
        {
            var idx = 0;
            var token = "\"" + key + "\":\"";
            while (list.Count < max)
            {
                var i = json.IndexOf(token, idx, StringComparison.Ordinal);
                if (i < 0) break;
                i += token.Length;
                var e = json.IndexOf('"', i);
                if (e <= i) break;
                var s = json.Substring(i, Math.Min(40, e - i));
                if (!string.IsNullOrEmpty(s)) list.Add(s);
                idx = e + 1;
            }
        }

        static int CountOccurrences(string hay, string needle)
        {
            if (string.IsNullOrEmpty(hay) || string.IsNullOrEmpty(needle)) return 0;
            var count = 0;
            var idx = 0;
            while (true)
            {
                var i = hay.IndexOf(needle, idx, StringComparison.Ordinal);
                if (i < 0) break;
                count++;
                idx = i + needle.Length;
            }
            return count;
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
    }
}

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KoG.MiniMvp.Network
{
    /// <summary>
    /// Minimal HTTP client for Kingdoms of Glory Mini-MVP backend.
    /// Retries once on 401 after refresh-token rotation.
    /// </summary>
    public sealed class ApiClient
    {
        readonly string _baseUrl;
        bool _refreshInFlight;

        public string BaseUrl => _baseUrl;

        public ApiClient(string baseUrl)
        {
            _baseUrl = (baseUrl ?? "http://127.0.0.1:3000").TrimEnd('/');
        }

        public IEnumerator PostJson(
            string path,
            string jsonBody,
            string bearerToken,
            string idempotencyKey,
            Action<long, string> onDone)
        {
            yield return SendWithRefresh(
                () => BuildPost(path, jsonBody, bearerToken, idempotencyKey),
                path,
                jsonBody,
                idempotencyKey,
                onDone,
                isPost: true);
        }

        public IEnumerator GetJson(string path, string bearerToken, Action<long, string> onDone)
        {
            yield return SendWithRefresh(
                () => BuildGet(path, bearerToken),
                path,
                null,
                null,
                onDone,
                isPost: false);
        }

        IEnumerator SendWithRefresh(
            Func<UnityWebRequest> firstBuild,
            string path,
            string jsonBody,
            string idempotencyKey,
            Action<long, string> onDone,
            bool isPost)
        {
            using (var request = firstBuild())
            {
                yield return request.SendWebRequest();
                if (request.responseCode != 401 || string.IsNullOrEmpty(SessionStore.RefreshToken))
                {
                    onDone?.Invoke(request.responseCode, FormatBody(request));
                    yield break;
                }
            }

            var refreshed = false;
            yield return TryRefresh(ok => refreshed = ok);
            if (!refreshed)
            {
                onDone?.Invoke(401, "{\"error\":\"unauthorized\",\"message\":\"Session expired\"}");
                yield break;
            }

            using (var retry = isPost
                       ? BuildPost(path, jsonBody, SessionStore.Token, idempotencyKey)
                       : BuildGet(path, SessionStore.Token))
            {
                yield return retry.SendWebRequest();
                onDone?.Invoke(retry.responseCode, FormatBody(retry));
            }
        }

        IEnumerator TryRefresh(Action<bool> done)
        {
            if (_refreshInFlight)
            {
                // Avoid stampede — wait briefly for in-flight refresh.
                var wait = 0f;
                while (_refreshInFlight && wait < 2f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                done?.Invoke(!string.IsNullOrEmpty(SessionStore.Token));
                yield break;
            }

            _refreshInFlight = true;
            var body = "{\"refreshToken\":\"" + Escape(SessionStore.RefreshToken) + "\"}";
            using (var request = BuildPost("/api/v1/auth/refresh", body, null, null))
            {
                yield return request.SendWebRequest();
                _refreshInFlight = false;
                if (request.responseCode < 200 || request.responseCode >= 300)
                {
                    done?.Invoke(false);
                    yield break;
                }

                var text = FormatBody(request);
                var token = ReadJsonString(text, "token");
                if (string.IsNullOrEmpty(token)) token = ReadJsonString(text, "accessToken");
                var refresh = ReadJsonString(text, "refreshToken");
                if (string.IsNullOrEmpty(token))
                {
                    done?.Invoke(false);
                    yield break;
                }

                SessionStore.Save(
                    SessionStore.PlayerId,
                    token,
                    string.IsNullOrEmpty(refresh) ? SessionStore.RefreshToken : refresh);
                done?.Invoke(true);
            }
        }

        UnityWebRequest BuildPost(string path, string jsonBody, string bearerToken, string idempotencyKey)
        {
            var url = _baseUrl + path;
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var body = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(bearerToken))
                request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
            if (!string.IsNullOrEmpty(idempotencyKey))
                request.SetRequestHeader("Idempotency-Key", idempotencyKey);
            return request;
        }

        UnityWebRequest BuildGet(string path, string bearerToken)
        {
            var url = _baseUrl + path;
            var request = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(bearerToken))
                request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
            return request;
        }

        static string FormatBody(UnityWebRequest request)
        {
            var text = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (!string.IsNullOrEmpty(text)) return text;

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError ||
                request.responseCode == 0)
            {
                var detail = string.IsNullOrEmpty(request.error) ? "connection failed" : request.error;
                return "{\"error\":\"backend_unreachable\",\"message\":\"Backend ishlamayapti (" + detail +
                       "). Terminalda: npm run dev\"}";
            }

            if (request.result == UnityWebRequest.Result.ProtocolError)
                return "{\"error\":\"http_error\",\"message\":\"HTTP " + request.responseCode + "\"}";

            return string.Empty;
        }

        static string Escape(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string ReadJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            var needle = "\"" + key + "\"";
            var i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return "";
            var colon = json.IndexOf(':', i + needle.Length);
            if (colon < 0) return "";
            var q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            var q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        public static string NewIdempotencyKey()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 24);
        }
    }
}

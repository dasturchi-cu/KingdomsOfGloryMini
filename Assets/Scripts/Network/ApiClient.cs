using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KoG.MiniMvp.Network
{
    /// <summary>
    /// Minimal HTTP client for Kingdoms of Glory Mini-MVP backend.
    /// </summary>
    public sealed class ApiClient
    {
        readonly string _baseUrl;

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
            var url = _baseUrl + path;
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var body = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(bearerToken))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
                }
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    request.SetRequestHeader("Idempotency-Key", idempotencyKey);
                }

                yield return request.SendWebRequest();
                onDone?.Invoke(request.responseCode, FormatBody(request));
            }
        }

        public IEnumerator GetJson(string path, string bearerToken, Action<long, string> onDone)
        {
            var url = _baseUrl + path;
            using (var request = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(bearerToken))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
                }

                yield return request.SendWebRequest();
                onDone?.Invoke(request.responseCode, FormatBody(request));
            }
        }

        /// <summary>Empty download + transport failure → actionable text (not blank "unknown error").</summary>
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
            {
                return "{\"error\":\"http_error\",\"message\":\"HTTP " + request.responseCode + "\"}";
            }

            return string.Empty;
        }

        public static string NewIdempotencyKey()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 24);
        }
    }
}

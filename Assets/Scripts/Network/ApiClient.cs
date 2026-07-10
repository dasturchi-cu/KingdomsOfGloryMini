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
                onDone?.Invoke(request.responseCode, request.downloadHandler?.text ?? string.Empty);
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
                onDone?.Invoke(request.responseCode, request.downloadHandler?.text ?? string.Empty);
            }
        }

        public static string NewIdempotencyKey()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 24);
        }
    }
}

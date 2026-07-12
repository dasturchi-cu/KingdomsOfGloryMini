using System;
using System.Collections;
using KoG.MiniMvp.Network;
using UnityEngine;

namespace KoG.MiniMvp.Save
{
    /// <summary>
    /// HTTP cloud save against ADR routes.
    /// Sync Push/Pull are unavailable — use coroutines from a MonoBehaviour host.
    /// </summary>
    public sealed class HttpCloudSaveBackend : ICloudSaveBackend
    {
        readonly ApiClient _api;
        readonly Func<string> _tokenProvider;
        readonly string _pushPath;
        readonly string _pullPath;

        public HttpCloudSaveBackend(
            ApiClient api,
            Func<string> tokenProvider,
            string pushPath = "/api/v1/player/cloud-save/push",
            string pullPath = "/api/v1/player/cloud-save/pull")
        {
            _api = api;
            _tokenProvider = tokenProvider;
            _pushPath = pushPath;
            _pullPath = pullPath;
        }

        public string BackendId => "http";
        public bool IsAvailable => _api != null && !string.IsNullOrEmpty(_tokenProvider?.Invoke());

        public IEnumerator PushCoroutine(string playerId, SaveEnvelope envelope, Action<SaveResult> done)
        {
            if (_api == null || envelope == null)
            {
                done?.Invoke(SaveResult.Fail(SaveResultKind.IoError, "not configured"));
                yield break;
            }

            var body = JsonUtility.ToJson(envelope, false);
            var finished = false;
            SaveResult result = SaveResult.Fail(SaveResultKind.IoError, "timeout");
            yield return _api.PostJson(
                _pushPath,
                body,
                _tokenProvider?.Invoke(),
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    finished = true;
                    if (code == 409)
                        result = SaveResult.Fail(SaveResultKind.Conflict, text);
                    else if (code < 200 || code >= 300)
                        result = SaveResult.Fail(SaveResultKind.IoError, "HTTP " + code);
                    else
                        result = SaveResult.Ok(envelope);
                });

            if (!finished)
                result = SaveResult.Fail(SaveResultKind.IoError, "no response");
            done?.Invoke(result);
        }

        public IEnumerator PullCoroutine(string playerId, string payloadType, Action<SaveResult> done)
        {
            if (_api == null)
            {
                done?.Invoke(SaveResult.Fail(SaveResultKind.IoError, "not configured"));
                yield break;
            }

            var path = _pullPath + "?type=" + Uri.EscapeDataString(payloadType ?? "player_cache");
            var finished = false;
            SaveResult result = SaveResult.Fail(SaveResultKind.Missing, "timeout");
            yield return _api.GetJson(path, _tokenProvider?.Invoke(), (code, text) =>
            {
                finished = true;
                if (code == 404)
                    result = SaveResult.Fail(SaveResultKind.Missing, "not found");
                else if (code < 200 || code >= 300)
                    result = SaveResult.Fail(SaveResultKind.IoError, "HTTP " + code);
                else
                {
                    var envelope = JsonUtility.FromJson<SaveEnvelope>(text);
                    result = envelope != null
                        ? SaveResult.Ok(envelope)
                        : SaveResult.Fail(SaveResultKind.Corrupt, "bad json");
                }
            });

            if (!finished)
                result = SaveResult.Fail(SaveResultKind.IoError, "no response");
            done?.Invoke(result);
        }

        public SaveResult Push(string playerId, SaveEnvelope envelope) =>
            SaveResult.Fail(SaveResultKind.Missing,
                "use PushCoroutine — sync HTTP not supported from SaveSystem flush");

        public SaveResult Pull(string playerId, string payloadType) =>
            SaveResult.Fail(SaveResultKind.Missing,
                "use PullCoroutine — sync HTTP not supported from SaveSystem flush");
    }
}

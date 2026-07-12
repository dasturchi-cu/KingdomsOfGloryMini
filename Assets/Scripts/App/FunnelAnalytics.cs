using System;
using System.Collections;
using System.Text;
using KoG.MiniMvp.Network;
using UnityEngine;

namespace KoG.MiniMvp.App
{
    /// <summary>
    /// Soft-GO funnel telemetry — fire-and-forget to /api/v1/analytics/track.
    /// Never treats analytics failure as gameplay failure.
    /// </summary>
    public sealed class FunnelAnalytics : MonoBehaviour
    {
        ApiClient _api;
        static FunnelAnalytics _instance;
        float _lastErrorAt = -999f;

        public static FunnelAnalytics Instance => _instance;

        public void Bind(ApiClient api)
        {
            _api = api;
            _instance = this;
        }

        public void Track(string eventName, params (string key, string value)[] props)
        {
            if (_api == null || string.IsNullOrEmpty(eventName)) return;
            if (!SessionStore.HasSession && eventName != "session_start") return;
            StartCoroutine(PostEvent(eventName, props));
        }

        public void TrackError(string message, string stack = null)
        {
            if (Time.unscaledTime - _lastErrorAt < 5f) return;
            _lastErrorAt = Time.unscaledTime;
            var trunc = string.IsNullOrEmpty(message) ? "unknown" : message;
            if (trunc.Length > 180) trunc = trunc.Substring(0, 180);
            Track("client_error", ("message", trunc), ("stack", Trunc(stack, 240)));
        }

        static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        IEnumerator PostEvent(string eventName, (string key, string value)[] props)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"eventName\":\"").Append(Escape(eventName)).Append("\"");
            if (SessionStore.HasSession)
                sb.Append(",\"playerId\":\"").Append(Escape(SessionStore.PlayerId)).Append("\"");
            sb.Append(",\"props\":{");
            if (props != null)
            {
                for (var i = 0; i < props.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('"').Append(Escape(props[i].key)).Append("\":\"")
                        .Append(Escape(props[i].value ?? "")).Append('"');
                }
            }
            sb.Append("}}");

            yield return _api.PostJson(
                "/api/v1/analytics/track",
                sb.ToString(),
                SessionStore.HasSession ? SessionStore.Token : null,
                null,
                (_, __) => { /* ignore */ });
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        void OnEnable()
        {
            Application.logMessageReceived += OnLog;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            TrackError(condition, stackTrace);
        }
    }
}

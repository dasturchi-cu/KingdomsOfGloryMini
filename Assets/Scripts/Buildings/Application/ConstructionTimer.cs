using System;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>Local countdown helper for construction UI (server remains authoritative).</summary>
    public sealed class ConstructionTimer
    {
        readonly Func<float> _unscaledTime;
        float _localDeadline = -1f;
        string _buildingId;

        public ConstructionTimer(Func<float> unscaledTime = null)
        {
            _unscaledTime = unscaledTime ?? (() => UnityEngine.Time.unscaledTime);
        }

        public void SyncFromServer(string buildingId, int constructionSecondsLeft)
        {
            _buildingId = buildingId;
            if (constructionSecondsLeft <= 0)
            {
                _localDeadline = -1f;
                return;
            }

            _localDeadline = _unscaledTime() + constructionSecondsLeft;
        }

        public void Clear()
        {
            _buildingId = null;
            _localDeadline = -1f;
        }

        public bool TryGetRemaining(string buildingId, out int secondsLeft)
        {
            secondsLeft = 0;
            if (string.IsNullOrEmpty(buildingId) || buildingId != _buildingId || _localDeadline < 0f)
                return false;

            var left = _localDeadline - _unscaledTime();
            if (left <= 0f)
            {
                secondsLeft = 0;
                return true;
            }

            secondsLeft = (int)Math.Ceiling(left);
            return true;
        }
    }
}

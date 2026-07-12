using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>Lightweight steering motor — no NavMesh (mobile Mini default).</summary>
    public sealed class TroopMotor
    {
        readonly Transform _transform;
        TroopStats _stats;
        Vector3? _destination;
        bool _hasDest;

        public bool HasDestination => _hasDest;
        public Vector3 Destination => _destination ?? _transform.position;

        public TroopMotor(Transform transform)
        {
            _transform = transform;
        }

        public void ApplyStats(in TroopStats stats) => _stats = stats;

        public void SetDestination(Vector3 world)
        {
            _destination = world;
            _hasDest = true;
        }

        public void ClearDestination()
        {
            _hasDest = false;
            _destination = null;
        }

        /// <returns>True while still moving.</returns>
        public bool Tick(float dt, float speedMultiplier = 1f)
        {
            if (!_hasDest || _transform == null) return false;

            var pos = _transform.position;
            var target = _destination.Value;
            target.y = pos.y;
            var to = target - pos;
            var dist = to.magnitude;
            if (dist <= 0.08f)
            {
                _transform.position = target;
                ClearDestination();
                return false;
            }

            var step = _stats.MoveSpeed * Mathf.Max(0f, speedMultiplier) * dt;
            if (step >= dist)
            {
                _transform.position = target;
                ClearDestination();
                return false;
            }

            var dir = to / dist;
            _transform.position = pos + dir * step;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(dir, Vector3.up);
                _transform.rotation = Quaternion.RotateTowards(
                    _transform.rotation, look, _stats.TurnSpeedDegrees * dt);
            }

            return true;
        }

        public void Face(Vector3 worldPoint, float dt)
        {
            var pos = _transform.position;
            var to = worldPoint - pos;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            var look = Quaternion.LookRotation(to.normalized, Vector3.up);
            _transform.rotation = Quaternion.RotateTowards(
                _transform.rotation, look, _stats.TurnSpeedDegrees * dt);
        }
    }
}

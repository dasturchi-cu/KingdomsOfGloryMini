using KoG.MiniMvp.InputUtil;
using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>
    /// Touch/mouse: tap troop to select, tap ground to move, tap enemy to attack.
    /// Input System only — UI hits use finger-aware pointer id.
    /// </summary>
    public sealed class TroopInputDriver : MonoBehaviour
    {
        TroopSystem _troops;
        UnityEngine.Camera _cam;
        bool _enabled = true;

        public void Bind(TroopSystem troops, UnityEngine.Camera cam)
        {
            _troops = troops;
            _cam = cam;
        }

        public void SetEnabled(bool enabled) => _enabled = enabled;

        void Update()
        {
            if (!_enabled || _troops == null) return;
            if (_cam == null) _cam = UnityEngine.Camera.main;
            if (_cam == null) return;

            if (!PointerInputUtil.WasPressedThisFrame()) return;
            if (PointerInputUtil.IsPointerOverUi()) return;
            if (!PointerInputUtil.TryGetScreenPosition(out var screenPos)) return;

            var ray = _cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 500f))
            {
                var actor = hit.collider.GetComponentInParent<TroopActor>();
                if (actor != null && actor.IsAlive)
                {
                    if (actor.Faction == TroopFaction.Player)
                    {
                        _troops.TrySelectAt(actor.transform.position);
                        return;
                    }

                    if (_troops.Selected.Count > 0)
                    {
                        _troops.CommandSelectedAttack(actor);
                        return;
                    }
                }

                _troops.CommandSelectedMove(hit.point);
                return;
            }

            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var enter))
                _troops.CommandSelectedMove(ray.GetPoint(enter));
        }
    }
}

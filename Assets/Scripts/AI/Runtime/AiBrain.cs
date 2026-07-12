using System.Collections.Generic;
using KoG.MiniMvp.Combat;
using KoG.MiniMvp.Troops;
using UnityEngine;

namespace KoG.MiniMvp.AI
{
    /// <summary>
    /// Plain C# FSM brain — no MonoBehaviour, no Update.
    /// Ticked by <see cref="AiSystem"/> on a budgeted cadence.
    /// </summary>
    public sealed class AiBrain
    {
        readonly List<Vector3> _pathScratch = new List<Vector3>(64);
        TroopActor _actor;
        AiProfile _profile;
        GridPathfinder _pathfinder;
        CombatSystem _combat;
        readonly PathFollower _follower = new PathFollower();
        Vector3 _home;
        AiStateId _state = AiStateId.Idle;
        float _thinkCd;
        float _scanCd;
        bool _enabled;

        public AiStateId State => _state;
        public bool Enabled => _enabled;
        public TroopActor Actor => _actor;

        public void Bind(
            TroopActor actor,
            AiProfile profile,
            GridPathfinder pathfinder,
            CombatSystem combat,
            Vector3 home)
        {
            _actor = actor;
            _profile = profile;
            _pathfinder = pathfinder;
            _combat = combat;
            _home = home;
            _state = AiStateId.Idle;
            _thinkCd = 0f;
            _scanCd = 0f;
            _follower.Clear();
            _enabled = actor != null && profile != null && pathfinder != null;
        }

        public void Disable()
        {
            _enabled = false;
            _follower.Clear();
            _state = AiStateId.Idle;
        }

        public void ResetForPool()
        {
            Disable();
            _actor = null;
            _combat = null;
            _pathfinder = null;
            _profile = null;
        }

        /// <summary>Called from AiSystem only — may early-out on think interval.</summary>
        public void Tick(float dt)
        {
            if (!_enabled || _actor == null) return;

            if (!_actor.IsAlive || _actor.AnimState == TroopAnimState.Death)
            {
                Enter(AiStateId.Death);
                return;
            }

            _thinkCd -= dt;
            _scanCd -= dt;
            if (_thinkCd > 0f)
            {
                DriveFollower();
                return;
            }

            _thinkCd = Mathf.Max(0.05f, _profile.ThinkIntervalSeconds);
            Think();
            DriveFollower();
        }

        void Think()
        {
            switch (_state)
            {
                case AiStateId.Idle:
                    ThinkIdle();
                    break;
                case AiStateId.Move:
                    ThinkMove();
                    break;
                case AiStateId.Attack:
                    ThinkAttack();
                    break;
                case AiStateId.Return:
                    ThinkReturn();
                    break;
                case AiStateId.Death:
                    break;
            }
        }

        void ThinkIdle()
        {
            if (!_profile.AutoAggro) return;
            if (_scanCd > 0f) return;
            _scanCd = _profile.IdleScanIntervalSeconds;

            var target = SelectTarget();
            if (target == null) return;

            _actor.CommandAttack(target);
            Enter(AiStateId.Attack);
        }

        void ThinkMove()
        {
            // Optional mid-path aggro
            if (_profile.AutoAggro)
            {
                var target = SelectTarget();
                if (target != null)
                {
                    _follower.Clear();
                    _actor.CommandAttack(target);
                    Enter(AiStateId.Attack);
                    return;
                }
            }

            if (_follower.IsComplete)
                Enter(AiStateId.Idle);
        }

        void ThinkAttack()
        {
            var leash = HorizontalDist(_actor.Position, _home);
            if (leash > _profile.LeashRange)
            {
                BeginReturn();
                return;
            }

            var target = SelectTarget();
            if (target == null)
            {
                if (_profile.ReturnHomeAfterCombat)
                    BeginReturn();
                else
                    Enter(AiStateId.Idle);
                return;
            }

            _actor.CommandAttack(target);
        }

        void ThinkReturn()
        {
            if (_follower.IsComplete || HorizontalDist(_actor.Position, _home) <= _profile.WaypointArriveDistance * 2f)
            {
                _follower.Clear();
                Enter(AiStateId.Idle);
                return;
            }

            // Re-aggro only if still inside leash soft zone
            if (_profile.AutoAggro && HorizontalDist(_actor.Position, _home) < _profile.LeashRange * 0.85f)
            {
                var target = SelectTarget();
                if (target != null)
                {
                    _follower.Clear();
                    _actor.CommandAttack(target);
                    Enter(AiStateId.Attack);
                }
            }
        }

        void BeginReturn()
        {
            ClearCombat();
            if (RequestPath(_home))
                Enter(AiStateId.Return);
            else
            {
                _actor.CommandMove(_home);
                Enter(AiStateId.Return);
            }
        }

        /// <summary>External order: move to world point via pathfinding.</summary>
        public void OrderMove(Vector3 world)
        {
            if (!_enabled || _actor == null || !_actor.IsAlive) return;
            ClearCombat();
            if (RequestPath(world))
                Enter(AiStateId.Move);
            else
            {
                _actor.CommandMove(world);
                Enter(AiStateId.Move);
            }
        }

        public void OrderAttack(IDamageable target)
        {
            if (!_enabled || _actor == null || !_actor.IsAlive || target == null) return;
            _follower.Clear();
            _actor.CommandAttack(target);
            Enter(AiStateId.Attack);
        }

        IDamageable SelectTarget()
        {
            if (_combat == null || _actor == null) return null;
            // Prefer combat Targeting — single selection implementation.
            var acquire = Mathf.Max(_profile.AggroRange, _actor.Definition != null
                ? _actor.Definition.Stats.AcquireRange
                : _profile.AggroRange);
            return Targeting.FindNearestEnemy(_combat.Damageables, _actor, acquire);
        }

        bool RequestPath(Vector3 destination)
        {
            _follower.Clear();
            if (_pathfinder == null) return false;
            _pathScratch.Clear();
            var ok = _pathfinder.TryFindPath(
                _actor.Position,
                destination,
                _profile.MaxPathIterations,
                _profile.MaxPathNodes,
                _pathScratch);
            if (!ok || _pathScratch.Count == 0) return false;
            _follower.SetPath(_pathScratch, _profile.WaypointArriveDistance);
            return true;
        }

        void DriveFollower()
        {
            if (_state != AiStateId.Move && _state != AiStateId.Return) return;
            if (!_follower.TryGetCurrent(out var wp)) return;
            _follower.AdvanceIfArrived(_actor.Position);
            if (_follower.TryGetCurrent(out wp))
                _actor.CommandMove(wp);
        }

        void ClearCombat()
        {
            _actor.ClearAttackTarget();
        }

        void Enter(AiStateId next)
        {
            if (_state == next) return;
            _state = next;
            if (next == AiStateId.Idle)
                _follower.Clear();
            if (next == AiStateId.Death)
            {
                _follower.Clear();
                _enabled = false;
            }
        }

        static float HorizontalDist(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}

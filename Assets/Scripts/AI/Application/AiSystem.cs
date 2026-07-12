using System.Collections.Generic;
using KoG.MiniMvp.Combat;
using KoG.MiniMvp.Troops;
using UnityEngine;

namespace KoG.MiniMvp.AI
{
    /// <summary>
    /// Single Update owner for all AI brains. Staggers think work for mobile.
    /// Pathfinding + FSM live here; no per-agent MonoBehaviour Update.
    /// </summary>
    public sealed class AiSystem : MonoBehaviour
    {
        [SerializeField] int staggerBuckets = 3;
        [SerializeField] int gridSize = 20;
        [SerializeField] float cellSize = 1.1f;

        readonly List<AiBrain> _brains = new List<AiBrain>(32);
        readonly Stack<AiBrain> _pool = new Stack<AiBrain>(16);
        PathGrid _grid;
        GridPathfinder _pathfinder;
        CombatSystem _combat;
        AiProfile _defaultProfile;
        int _frame;
        bool _ready;

        public PathGrid Grid => _grid;
        public int BrainCount => _brains.Count;

        public void Configure(CombatSystem combat, int size, float cell, AiProfile profile = null)
        {
            _combat = combat;
            gridSize = size > 0 ? size : gridSize;
            cellSize = cell > 0f ? cell : cellSize;
            _grid = new PathGrid(gridSize, cellSize);
            _pathfinder = new GridPathfinder(_grid);
            _defaultProfile = profile != null ? profile : CreateRuntimeProfile();
            staggerBuckets = Mathf.Max(1, staggerBuckets);
            _ready = true;
        }

        public void SetBlocked(int x, int z, bool blocked) => _grid?.SetBlocked(x, z, blocked);

        public void ClearObstacles() => _grid?.ClearObstacles();

        public void MarkObstacleWorld(Vector3 world, bool blocked)
        {
            if (_grid == null) return;
            if (_grid.WorldToIndex(world, out var x, out var z))
                _grid.SetBlocked(x, z, blocked);
        }

        public AiBrain Attach(TroopActor actor, Vector3 home, AiProfile profile = null)
        {
            if (!_ready || actor == null) return null;
            DetachActor(actor);
            var brain = _pool.Count > 0 ? _pool.Pop() : new AiBrain();
            brain.Bind(actor, profile != null ? profile : _defaultProfile, _pathfinder, _combat, home);
            actor.SetAiControlled(true);
            _brains.Add(brain);
            return brain;
        }

        public void Detach(AiBrain brain)
        {
            if (brain == null) return;
            if (brain.Actor != null)
                brain.Actor.SetAiControlled(false);
            brain.ResetForPool();
            _brains.Remove(brain);
            _pool.Push(brain);
        }

        public void DetachActor(TroopActor actor)
        {
            for (var i = _brains.Count - 1; i >= 0; i--)
            {
                if (_brains[i].Actor != actor) continue;
                Detach(_brains[i]);
                return;
            }
        }

        public void ClearAll()
        {
            for (var i = _brains.Count - 1; i >= 0; i--)
                Detach(_brains[i]);
            _brains.Clear();
        }

        void Update()
        {
            if (!_ready || _brains.Count == 0) return;
            _frame++;
            var dt = Time.deltaTime;
            var buckets = staggerBuckets;
            var bucket = _frame % buckets;
            // Scale dt so staggered brains keep wall-clock timers accurate.
            var scaledDt = dt * buckets;

            for (var i = _brains.Count - 1; i >= 0; i--)
            {
                if ((i % buckets) != bucket) continue;
                var brain = _brains[i];
                if (brain == null || !brain.Enabled)
                {
                    if (brain != null && brain.State == AiStateId.Death)
                        Detach(brain);
                    continue;
                }

                brain.Tick(scaledDt);
            }
        }

        static AiProfile CreateRuntimeProfile()
        {
            var p = ScriptableObject.CreateInstance<AiProfile>();
            p.AggroRange = 7f;
            p.LeashRange = 14f;
            p.ThinkIntervalSeconds = 0.2f;
            p.IdleScanIntervalSeconds = 0.4f;
            p.MaxPathNodes = 48;
            p.MaxPathIterations = 220;
            p.ReturnHomeAfterCombat = true;
            p.AutoAggro = true;
            return p;
        }
    }
}

using System;
using System.Collections.Generic;
using KoG.MiniMvp.AI;
using KoG.MiniMvp.Combat;
using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>
    /// Troop orchestrator: catalog, pools, spawn/select/commands, centralized tick.
    /// Server train counts remain authoritative — this system is presentation + local sim.
    /// </summary>
    public sealed class TroopSystem : MonoBehaviour
    {
        public event Action<string> StatusChanged;
        public event Action<TroopActor> SelectionChanged;

        [SerializeField] int maxVisibleTroops = 24;
        [SerializeField] float gatherRadius = 2.4f;
        [SerializeField] int pathGridSize = 20;
        [SerializeField] float pathCellSize = 1.1f;

        TroopCatalogAsset _catalog;
        CombatSystem _combat;
        AiSystem _ai;
        readonly Dictionary<string, TroopPool> _pools = new Dictionary<string, TroopPool>();
        readonly List<TroopActor> _active = new List<TroopActor>(64);
        readonly List<TroopActor> _selected = new List<TroopActor>(16);
        Transform _poolRoot;
        Transform _worldRoot;
        TroopDefinition _runtimeBarbarian;
        bool _ready;

        public int ActiveCount => _active.Count;
        public IReadOnlyList<TroopActor> Selected => _selected;
        public CombatSystem Combat => _combat;
        public AiSystem Ai => _ai;

        public void Configure(Transform worldRoot, TroopCatalogAsset catalog = null, int gridSize = 20, float cellSize = 1.1f)
        {
            _worldRoot = worldRoot != null ? worldRoot : transform;
            _catalog = catalog != null ? catalog : Resources.Load<TroopCatalogAsset>("Troops/TroopCatalog");
            pathGridSize = gridSize > 0 ? gridSize : pathGridSize;
            pathCellSize = cellSize > 0f ? cellSize : pathCellSize;
            EnsurePoolRoot();
            EnsureCombat();
            EnsureAi();
            EnsureRuntimeBarbarian();
            WarmPools();
            _ready = true;
        }

        void EnsureCombat()
        {
            if (_combat != null) return;
            _combat = GetComponent<CombatSystem>();
            if (_combat == null) _combat = gameObject.AddComponent<CombatSystem>();
            _combat.Configure();
        }

        void EnsureAi()
        {
            if (_ai != null) return;
            _ai = GetComponent<AiSystem>();
            if (_ai == null) _ai = gameObject.AddComponent<AiSystem>();
            _ai.Configure(_combat, pathGridSize, pathCellSize);
        }

        public TroopDefinition GetDefinition(string id)
        {
            if (_catalog != null && _catalog.TryGet(id, out var def) && def != null)
                return def;
            if (id == "barbarian") return _runtimeBarbarian;
            return _runtimeBarbarian;
        }

        /// <summary>Match visible army to server count (capped for mobile).</summary>
        public void SyncArmyCount(string troopId, int serverCount, Vector3 gatherCenter)
        {
            if (!_ready) return;
            var def = GetDefinition(troopId);
            if (def == null) return;

            var target = Mathf.Clamp(serverCount, 0, Mathf.Min(maxVisibleTroops, def.PoolMax));
            var current = CountActiveOf(troopId);

            while (current > target)
            {
                if (!TryFindActive(troopId, out var actor)) break;
                Despawn(actor);
                current--;
            }

            while (current < target)
            {
                var offset = RandomOnRing(gatherRadius * (0.35f + 0.05f * current));
                var pos = gatherCenter + offset;
                if (Spawn(troopId, TroopFaction.Player, pos, Quaternion.identity) == null)
                    break;
                current++;
            }
        }

        public TroopActor Spawn(string troopId, TroopFaction faction, Vector3 position, Quaternion rotation)
        {
            if (!_ready) return null;
            var pool = GetOrCreatePool(troopId);
            if (pool == null) return null;
            if (!pool.TryRent(out var actor))
            {
                Emit("Troop pool full (" + troopId + ")");
                return null;
            }

            actor.transform.SetParent(_worldRoot, true);
            actor.BindCombatSystem(_combat);
            actor.Spawn(faction, position, rotation);
            if (faction == TroopFaction.Enemy)
                _ai?.Attach(actor, position);
            actor.Died += OnTroopDied;
            actor.Despawned += OnTroopDespawned;
            _active.Add(actor);
            return actor;
        }

        /// <summary>Enable FSM AI on an existing actor (e.g. after player command ends).</summary>
        public AiBrain EnableAi(TroopActor actor, Vector3? home = null)
        {
            EnsureAi();
            if (actor == null || _ai == null) return null;
            return _ai.Attach(actor, home ?? actor.Position);
        }

        public void RefreshPathObstacles(IEnumerable<Vector3> blockedWorldCells)
        {
            EnsureAi();
            if (_ai == null) return;
            _ai.ClearObstacles();
            if (blockedWorldCells == null) return;
            foreach (var p in blockedWorldCells)
                _ai.MarkObstacleWorld(p, true);
        }

        public void Despawn(TroopActor actor)
        {
            if (actor == null) return;
            _ai?.DetachActor(actor);
            Unselect(actor);
            _active.Remove(actor);
            actor.Died -= OnTroopDied;
            actor.Despawned -= OnTroopDespawned;
            var id = actor.DefinitionId;
            if (!string.IsNullOrEmpty(id) && _pools.TryGetValue(id, out var pool))
                pool.Return(actor);
            else
                actor.gameObject.SetActive(false);
        }

        public void ClearAll()
        {
            ClearSelection();
            for (var i = _active.Count - 1; i >= 0; i--)
                Despawn(_active[i]);
            _active.Clear();
            _combat?.ClearRegistry();
            _ai?.ClearAll();
        }

        public bool TrySelectAt(Vector3 worldPoint, float maxDist = 1.25f)
        {
            TroopActor best = null;
            var bestDist = maxDist;
            var px = worldPoint.x;
            var pz = worldPoint.z;
            for (var i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a == null || !a.IsAlive || a.Faction != TroopFaction.Player) continue;
                var dx = a.transform.position.x - px;
                var dz = a.transform.position.z - pz;
                var d = Mathf.Sqrt(dx * dx + dz * dz);
                var rad = a.Definition != null ? a.Definition.Stats.SelectionRadius : 0.5f;
                var limit = Mathf.Max(maxDist, rad);
                if (d > limit || d >= bestDist) continue;
                bestDist = d;
                best = a;
            }

            ClearSelection();
            if (best == null)
            {
                SelectionChanged?.Invoke(null);
                return false;
            }

            best.SetSelected(true);
            _selected.Add(best);
            SelectionChanged?.Invoke(best);
            Emit("Troop selected: " + best.DefinitionId);
            return true;
        }

        public void CommandSelectedMove(Vector3 world)
        {
            for (var i = 0; i < _selected.Count; i++)
            {
                var a = _selected[i];
                if (a != null && a.IsAlive)
                    a.CommandMove(world);
            }
        }

        public void CommandSelectedAttack(TroopActor target)
        {
            for (var i = 0; i < _selected.Count; i++)
            {
                var a = _selected[i];
                if (a != null && a.IsAlive)
                    a.CommandAttack(target);
            }
        }

        public TroopActor FindNearestEnemy(Vector3 from, TroopFaction againstFaction)
        {
            TroopActor best = null;
            var bestDist = float.MaxValue;
            for (var i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a == null || !a.IsAlive || a.Faction != againstFaction) continue;
                var d = (a.transform.position - from).sqrMagnitude;
                if (d >= bestDist) continue;
                bestDist = d;
                best = a;
            }

            return best;
        }

        void Update()
        {
            if (!_ready || _active.Count == 0) return;
            var dt = Time.deltaTime;
            
            ApplyLocalSeparation(dt);
            
            // Copy count — death may despawn during tick.
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                if (a != null) a.Tick(dt);
            }
        }

        void ApplyLocalSeparation(float dt)
        {
            var count = _active.Count;
            if (count < 2) return;

            const float sepRadius = 0.48f;
            const float sepRadiusSqr = sepRadius * sepRadius;
            const float forceStrength = 1.6f;

            for (var i = 0; i < count; i++)
            {
                var a = _active[i];
                if (a == null || !a.IsAlive) continue;

                var posA = a.transform.position;
                var pushDir = Vector3.zero;
                var overlaps = 0;

                for (var j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    var b = _active[j];
                    if (b == null || !b.IsAlive) continue;

                    var posB = b.transform.position;
                    var dx = posA.x - posB.x;
                    var dz = posA.z - posB.z;
                    var distSqr = dx * dx + dz * dz;

                    if (distSqr < sepRadiusSqr)
                    {
                        var dist = Mathf.Sqrt(distSqr);
                        if (dist > 0.001f)
                        {
                            pushDir.x += (dx / dist) * (sepRadius - dist);
                            pushDir.z += (dz / dist) * (sepRadius - dist);
                        }
                        else
                        {
                            pushDir.x += UnityEngine.Random.Range(-0.1f, 0.1f);
                            pushDir.z += UnityEngine.Random.Range(-0.1f, 0.1f);
                        }
                        overlaps++;
                    }
                }

                if (overlaps > 0)
                {
                    var pushStep = pushDir * (forceStrength * dt);
                    pushStep.y = 0f;
                    
                    var maxPush = 2f * dt;
                    if (pushStep.sqrMagnitude > maxPush * maxPush)
                    {
                        pushStep = pushStep.normalized * maxPush;
                    }
                    
                    a.transform.position += pushStep;
                }
            }
        }

        void OnTroopDied(TroopActor actor)
        {
            Unselect(actor);
            Emit("Troop fallen");
        }

        void OnTroopDespawned(TroopActor actor)
        {
            Despawn(actor);
        }

        void ClearSelection()
        {
            for (var i = 0; i < _selected.Count; i++)
            {
                if (_selected[i] != null)
                    _selected[i].SetSelected(false);
            }

            _selected.Clear();
        }

        void Unselect(TroopActor actor)
        {
            if (actor == null) return;
            actor.SetSelected(false);
            _selected.Remove(actor);
        }

        int CountActiveOf(string troopId)
        {
            var n = 0;
            for (var i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a != null && a.DefinitionId == troopId && a.IsAlive) n++;
            }

            return n;
        }

        bool TryFindActive(string troopId, out TroopActor actor)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                if (a != null && a.DefinitionId == troopId)
                {
                    actor = a;
                    return true;
                }
            }

            actor = null;
            return false;
        }

        TroopPool GetOrCreatePool(string troopId)
        {
            if (_pools.TryGetValue(troopId, out var pool)) return pool;
            var def = GetDefinition(troopId);
            if (def == null) return null;
            pool = new TroopPool(def, _poolRoot);
            pool.Prewarm();
            _pools[troopId] = pool;
            return pool;
        }

        void WarmPools()
        {
            if (_catalog != null && _catalog.Definitions != null)
            {
                for (var i = 0; i < _catalog.Definitions.Length; i++)
                {
                    var d = _catalog.Definitions[i];
                    if (d == null || string.IsNullOrEmpty(d.Id)) continue;
                    GetOrCreatePool(d.Id);
                }
            }

            GetOrCreatePool("barbarian");
        }

        void EnsurePoolRoot()
        {
            if (_poolRoot != null) return;
            var go = new GameObject("TroopPool");
            go.transform.SetParent(transform, false);
            _poolRoot = go.transform;
        }

        void EnsureRuntimeBarbarian()
        {
            if (_runtimeBarbarian != null) return;
            _runtimeBarbarian = ScriptableObject.CreateInstance<TroopDefinition>();
            _runtimeBarbarian.Id = "barbarian";
            _runtimeBarbarian.DisplayName = "Barbarian";
            _runtimeBarbarian.Stats = TroopStats.BarbarianDefault;
            _runtimeBarbarian.PoolPrewarm = 10;
            _runtimeBarbarian.PoolMax = 40;
            _runtimeBarbarian.CastShadows = false;
            _runtimeBarbarian.Tint = new Color(0.82f, 0.55f, 0.28f, 1f);
        }

        static Vector3 RandomOnRing(float radius)
        {
            var a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var r = UnityEngine.Random.Range(0.2f, Mathf.Max(0.25f, radius));
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        void Emit(string msg) => StatusChanged?.Invoke(msg);
    }
}

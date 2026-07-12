using System;
using KoG.MiniMvp.Combat;
using KoG.MiniMvp.World;
using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>
    /// Pooled troop unit. Movement/anim here; damage/armor/crit/status/projectiles via Combat layer.
    /// Tick is driven by <see cref="TroopSystem"/>.
    /// </summary>
    public sealed class TroopActor : MonoBehaviour, IDamageable
    {
        public event Action<TroopActor> Died;
        public event Action<TroopActor> Despawned;
        public event Action<TroopActor, DamageResult> AttackHit;

        TroopDefinition _def;
        TroopMotor _motor;
        TroopAnimEventSink _animSink;
        Animator _animator;
        CombatUnit _combat;
        AttackController _attack;
        CombatSystem _combatSystem;
        int _animHashIdle;
        int _animHashMove;
        int _animHashAttack;
        int _animHashDeath;

        float _deathTimer;
        bool _awaitingDeathAnim;
        bool _selected;
        Transform _selectionRing;
        bool _autoAcquire;
        bool _aiControlled;
        ProjectileDefinition _boundProjectile;
        Func<IDamageable, IDamageable, DamageInfo, bool> _spawnProjectileDel;
        Action<IDamageable, DamageResult> _meleeHitDel;

        static Material _sharedSelectionRingMat;

        public string DefinitionId => _def != null ? _def.Id : null;
        public TroopDefinition Definition => _def;
        public TroopFaction Faction { get; private set; }
        public TroopAnimState AnimState { get; private set; }
        public CombatUnit Combat => _combat;
        public bool IsAlive => _combat != null && _combat.IsAlive && AnimState != TroopAnimState.Death;
        public bool IsSelected => _selected;
        public float Health => _combat != null ? _combat.CurrentHealth : 0f;
        public float MaxHealth => _combat != null ? _combat.MaxHealth : 1f;

        // IDamageable
        public Vector3 Position => transform.position;
        public Transform Transform => transform;
        public float CurrentHealth => Health;
        public float Armor => _combat != null ? _combat.Armor : 0f;

        public DamageResult ApplyHit(in DamageInfo info) =>
            _combat != null ? _combat.ApplyHit(in info) : default;

        public void BindDefinition(TroopDefinition def)
        {
            _def = def;
            _motor = new TroopMotor(transform);
            _motor.ApplyStats(def.Stats);
            _combat = new CombatUnit();
            _attack = new AttackController();
            _animSink = GetComponent<TroopAnimEventSink>();
            if (_animSink == null) _animSink = gameObject.AddComponent<TroopAnimEventSink>();
            _animSink.Bind(this);
            _animator = GetComponentInChildren<Animator>();
            CacheAnimHashes();
            EnsureSelectionRing();
        }

        public void BindCombatSystem(CombatSystem combatSystem)
        {
            _combatSystem = combatSystem;
        }

        public void Spawn(TroopFaction faction, Vector3 position, Quaternion rotation)
        {
            Faction = faction;
            transform.SetPositionAndRotation(position, rotation);
            var stats = _def.Stats.Normalized();
            _combat.Bind(transform, faction, stats);
            _combat.Died += OnCombatDied;
            _motor.ApplyStats(stats);
            WireAttackController(stats);
            _deathTimer = 0f;
            _awaitingDeathAnim = false;
            _autoAcquire = false;
            _motor.ClearDestination();
            SetSelected(false);
            SetAnim(TroopAnimState.Idle);
            _combatSystem?.Register(this);
        }

        public void ResetForPool()
        {
            _combatSystem?.Unregister(this);
            if (_combat != null)
            {
                _combat.Died -= OnCombatDied;
                _combat.ResetForPool();
            }

            _attack?.ClearTarget();
            _motor?.ClearDestination();
            SetSelected(false);
            AnimState = TroopAnimState.Idle;
            _awaitingDeathAnim = false;
            _autoAcquire = false;
            _aiControlled = false;
            Died = null;
            AttackHit = null;
            Despawned = null;
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_selectionRing != null)
                _selectionRing.gameObject.SetActive(selected);
        }

        public void SetAiControlled(bool enabled)
        {
            _aiControlled = enabled;
            if (enabled) _autoAcquire = false;
        }

        public void SetAutoAcquire(bool enabled)
        {
            if (_aiControlled) return;
            _autoAcquire = enabled;
        }

        public void ClearAttackTarget() => _attack?.ClearTarget();

        public void CommandMove(Vector3 world)
        {
            if (!IsAlive || (_combat != null && _combat.IsStunned)) return;
            _attack.ClearTarget();
            _motor.SetDestination(world);
            SetAnim(TroopAnimState.Move);
        }

        public void CommandAttack(IDamageable target)
        {
            if (!IsAlive || target == null || !target.IsAlive) return;
            if (target.Faction == Faction) return;
            _attack.SetTarget(target);
        }

        public void CommandAttack(TroopActor target) => CommandAttack((IDamageable)target);

        /// <summary>Legacy entry — routes through CombatUnit (armor/crit).</summary>
        public void ApplyDamage(float amount)
        {
            var info = new DamageInfo
            {
                Amount = amount,
                CanCrit = false,
                CritChance = 0f,
                CritMultiplier = 1f
            };
            ApplyHit(in info);
        }

        public void NotifyAnimAttackHit()
        {
            if (!IsAlive || _combat != null && _combat.IsStunned) return;
            if (_attack.TryFire())
            {
                // Melee path already applied; notify with synthetic result for FX.
            }
        }

        public void NotifyAnimDeathEnd()
        {
            _awaitingDeathAnim = false;
            FinishDeath();
        }

        public void Tick(float dt)
        {
            if (AnimState == TroopAnimState.Death)
            {
                if (_awaitingDeathAnim)
                {
                    _deathTimer -= dt;
                    if (_deathTimer <= 0f)
                        FinishDeath();
                }
                return;
            }

            if (!IsAlive) return;

            _combat.TickStatuses(dt);
            _attack.TickCooldown(dt);

            if (_combat.IsStunned)
            {
                _motor.ClearDestination();
                SetAnim(TroopAnimState.Idle);
                return;
            }

            if (_autoAcquire && !_aiControlled && !_attack.HasTarget && _combatSystem != null)
                _attack.TryAcquireNearest(_combatSystem.Damageables);

            if (_attack.HasTarget)
            {
                var target = _attack.Target;
                if (!Targeting.IsInRange(this, target, _def.Stats.AttackRange))
                {
                    _motor.SetDestination(target.Position);
                    if (_motor.Tick(dt, _combat.MoveSpeedMultiplier))
                        SetAnim(TroopAnimState.Move);
                    return;
                }

                _motor.ClearDestination();
                _motor.Face(target.Position, dt);
                SetAnim(TroopAnimState.Attack);

                // Animator-driven OR immediate fire when no animator.
                if (_animator == null || !_animator.enabled)
                    _attack.TryFire();
                return;
            }

            if (_motor.HasDestination)
            {
                if (_motor.Tick(dt, _combat.MoveSpeedMultiplier))
                    SetAnim(TroopAnimState.Move);
                else
                    SetAnim(TroopAnimState.Idle);
            }
            else if (AnimState == TroopAnimState.Move)
            {
                SetAnim(TroopAnimState.Idle);
            }
        }

        void WireAttackController(TroopStats stats)
        {
            var mode = stats.UseProjectile
                ? AttackController.FireMode.Projectile
                : AttackController.FireMode.Melee;
            _boundProjectile = _def.Projectile != null
                ? _def.Projectile
                : (_combatSystem != null ? _combatSystem.DefaultProjectile : null);

            if (_spawnProjectileDel == null)
                _spawnProjectileDel = TrySpawnBoundProjectile;
            if (_meleeHitDel == null)
                _meleeHitDel = ForwardMeleeHit;

            _attack.Bind(
                this,
                stats,
                mode,
                _boundProjectile,
                _def.OnHitStatus,
                _spawnProjectileDel,
                _meleeHitDel);
        }

        bool TrySpawnBoundProjectile(IDamageable src, IDamageable tgt, DamageInfo info)
        {
            if (_combatSystem == null) return false;
            return _combatSystem.SpawnProjectile(_boundProjectile, src, tgt, in info);
        }

        void ForwardMeleeHit(IDamageable tgt, DamageResult result)
        {
            AttackHit?.Invoke(this, result);
        }

        void OnCombatDied(CombatUnit unit)
        {
            BeginDeath();
        }

        void BeginDeath()
        {
            _attack.ClearTarget();
            _motor.ClearDestination();
            SetSelected(false);
            SetAnim(TroopAnimState.Death);
            _awaitingDeathAnim = true;
            _deathTimer = _animator != null ? 1.25f : 0.35f;
            _combatSystem?.Unregister(this);
            Died?.Invoke(this);
        }

        void FinishDeath()
        {
            if (!_awaitingDeathAnim && AnimState != TroopAnimState.Death) return;
            _awaitingDeathAnim = false;
            Despawned?.Invoke(this);
        }

        void SetAnim(TroopAnimState state)
        {
            if (AnimState == state) return;
            AnimState = state;
            if (_animator == null || !_animator.isActiveAndEnabled) return;

            switch (state)
            {
                case TroopAnimState.Idle:
                    if (_animHashIdle != 0) _animator.CrossFade(_animHashIdle, 0.08f, 0, 0f);
                    break;
                case TroopAnimState.Move:
                    if (_animHashMove != 0) _animator.CrossFade(_animHashMove, 0.08f, 0, 0f);
                    break;
                case TroopAnimState.Attack:
                    if (_animHashAttack != 0) _animator.CrossFade(_animHashAttack, 0.05f, 0, 0f);
                    break;
                case TroopAnimState.Death:
                    if (_animHashDeath != 0) _animator.CrossFade(_animHashDeath, 0.05f, 0, 0f);
                    break;
            }
        }

        void CacheAnimHashes()
        {
            _animHashIdle = HashOrZero(_def.IdleState);
            _animHashMove = HashOrZero(_def.MoveState);
            _animHashAttack = HashOrZero(_def.AttackState);
            _animHashDeath = HashOrZero(_def.DeathState);
        }

        static int HashOrZero(string stateName) =>
            string.IsNullOrEmpty(stateName) ? 0 : Animator.StringToHash(stateName);

        void EnsureSelectionRing()
        {
            if (_selectionRing != null) return;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SelectRing";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            ring.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
            UnityEngine.Object.Destroy(ring.GetComponent<Collider>());
            var rend = ring.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = SharedSelectionRingMat();
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            ring.SetActive(false);
            _selectionRing = ring.transform;
        }

        static Material SharedSelectionRingMat()
        {
            if (_sharedSelectionRingMat != null) return _sharedSelectionRingMat;
            var shader = UrpMaterialUtil.FindUnlitShader() ?? UrpMaterialUtil.FindLitShader();
            if (shader == null) return null;
            _sharedSelectionRingMat = new Material(shader);
            var c = new Color(0.2f, 0.95f, 0.35f, 0.55f);
            if (_sharedSelectionRingMat.HasProperty("_BaseColor"))
                _sharedSelectionRingMat.SetColor("_BaseColor", c);
            if (_sharedSelectionRingMat.HasProperty("_Color"))
                _sharedSelectionRingMat.SetColor("_Color", c);
            return _sharedSelectionRingMat;
        }
    }
}

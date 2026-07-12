using System;
using KoG.MiniMvp.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace KoG.MiniMvp.Combat
{
    public sealed class Projectile : MonoBehaviour
    {
        ProjectileDefinition _def;
        IDamageable _target;
        DamageInfo _payload;
        float _life;
        bool _alive;
        Action<Projectile> _onDone;
        string _poolId = "default";

        public bool IsAlive => _alive;
        public string PoolId => _poolId;

        public void Launch(
            ProjectileDefinition def,
            Vector3 origin,
            IDamageable target,
            in DamageInfo payload,
            Action<Projectile> onDone)
        {
            _def = def;
            _poolId = def != null && !string.IsNullOrEmpty(def.Id) ? def.Id : "default";
            _target = target;
            _payload = payload;
            _onDone = onDone;
            _life = def != null ? def.LifetimeSeconds : 3f;
            _alive = true;
            transform.position = origin;
            gameObject.SetActive(true);
        }

        public void Tick(float dt)
        {
            if (!_alive) return;
            _life -= dt;
            if (_life <= 0f || _target == null || !_target.IsAlive)
            {
                Finish();
                return;
            }

            var targetPos = _target.Position + Vector3.up * 0.6f;
            var pos = transform.position;
            var to = targetPos - pos;
            var dist = to.magnitude;
            var speed = _def != null ? _def.Speed : 12f;
            var hitR = _def != null ? _def.HitRadius : 0.35f;

            if (dist <= hitR)
            {
                _target.ApplyHit(in _payload);
                Finish();
                return;
            }

            if (_def != null && _def.Homing && dist > 0.001f)
            {
                transform.position = pos + to.normalized * Mathf.Min(speed * dt, dist);
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
            else
            {
                var forward = transform.forward;
                transform.position = pos + forward * speed * dt;
            }
        }

        public void ResetForPool()
        {
            _alive = false;
            _target = null;
            _onDone = null;
            gameObject.SetActive(false);
        }

        void Finish()
        {
            if (!_alive) return;
            _alive = false;
            var cb = _onDone;
            _onDone = null;
            cb?.Invoke(this);
        }
    }

    public sealed class ProjectilePool
    {
        readonly ProjectileDefinition _def;
        readonly Transform _root;
        readonly System.Collections.Generic.Stack<Projectile> _free =
            new System.Collections.Generic.Stack<Projectile>(16);
        int _created;
        static Material _sharedGreyboxMat;

        public ProjectilePool(ProjectileDefinition def, Transform root)
        {
            _def = def;
            _root = root;
        }

        public void Prewarm()
        {
            var n = _def != null ? Mathf.Clamp(_def.PoolPrewarm, 0, _def.PoolMax) : 4;
            for (var i = 0; i < n; i++)
                _free.Push(Create());
        }

        public bool TryRent(out Projectile projectile)
        {
            projectile = null;
            var max = _def != null ? _def.PoolMax : 32;
            if (_created - _free.Count >= max && _free.Count == 0) return false;
            projectile = _free.Count > 0 ? _free.Pop() : Create();
            return projectile != null;
        }

        public void Return(Projectile projectile)
        {
            if (projectile == null) return;
            projectile.ResetForPool();
            projectile.transform.SetParent(_root, false);
            _free.Push(projectile);
        }

        Projectile Create()
        {
            GameObject go;
            if (_def != null && _def.Prefab != null)
            {
                go = UnityEngine.Object.Instantiate(_def.Prefab, _root);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(_root, false);
                go.transform.localScale = Vector3.one * 0.22f;
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = SharedGreyboxMat(_def != null ? _def.Tint : Color.yellow);
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            go.name = "Projectile_" + (_def != null ? _def.Id : "default");
            go.SetActive(false);
            var p = go.GetComponent<Projectile>();
            if (p == null) p = go.AddComponent<Projectile>();
            _created++;
            return p;
        }

        static Material SharedGreyboxMat(Color tint)
        {
            // One shared greybox mat for default spheres — tint baked once from first create.
            if (_sharedGreyboxMat != null) return _sharedGreyboxMat;
            var shader = UrpMaterialUtil.FindUnlitShader();
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _sharedGreyboxMat = new Material(shader != null ? shader : Shader.Find("Standard"));
            if (_sharedGreyboxMat.HasProperty("_BaseColor"))
                _sharedGreyboxMat.SetColor("_BaseColor", tint);
            if (_sharedGreyboxMat.HasProperty("_Color"))
                _sharedGreyboxMat.SetColor("_Color", tint);
            return _sharedGreyboxMat;
        }
    }
}

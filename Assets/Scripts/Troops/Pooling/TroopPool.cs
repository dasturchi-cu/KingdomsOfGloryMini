using System.Collections.Generic;
using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>
    /// Per-definition object pool. Mobile: prewarm + hard max; never Instantiate under fire.
    /// </summary>
    public sealed class TroopPool
    {
        readonly TroopDefinition _def;
        readonly Transform _root;
        readonly Stack<TroopActor> _free = new Stack<TroopActor>(32);
        readonly HashSet<TroopActor> _freeSet = new HashSet<TroopActor>();
        readonly List<TroopActor> _all = new List<TroopActor>(32);
        int _created;

        public TroopDefinition Definition => _def;
        public int ActiveCount { get; private set; }
        public int CreatedCount => _created;

        public TroopPool(TroopDefinition def, Transform root)
        {
            _def = def;
            _root = root;
        }

        public void Prewarm()
        {
            var n = Mathf.Clamp(_def.PoolPrewarm, 0, _def.PoolMax);
            for (var i = 0; i < n; i++)
            {
                var actor = CreateNew();
                actor.gameObject.SetActive(false);
                if (_freeSet.Add(actor))
                    _free.Push(actor);
            }
        }

        public bool TryRent(out TroopActor actor)
        {
            actor = null;
            if (ActiveCount >= _def.PoolMax) return false;

            if (_free.Count > 0)
            {
                actor = _free.Pop();
                _freeSet.Remove(actor);
            }
            else
                actor = CreateNew();

            if (actor == null) return false;
            actor.gameObject.SetActive(true);
            ActiveCount++;
            return true;
        }

        public void Return(TroopActor actor)
        {
            if (actor == null) return;
            actor.ResetForPool();
            actor.gameObject.SetActive(false);
            actor.transform.SetParent(_root, false);
            if (_freeSet.Add(actor))
                _free.Push(actor);
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }

        public void ReturnAllActive()
        {
            for (var i = 0; i < _all.Count; i++)
            {
                var a = _all[i];
                if (a != null && a.gameObject.activeSelf)
                    Return(a);
            }
        }

        TroopActor CreateNew()
        {
            if (_created >= _def.PoolMax) return null;
            var go = TroopVisualFactory.Create(_def, _root);
            var actor = go.GetComponent<TroopActor>();
            if (actor == null) actor = go.AddComponent<TroopActor>();
            actor.BindDefinition(_def);
            _all.Add(actor);
            _created++;
            return actor;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace KoG.MiniMvp.UI
{
    /// <summary>
    /// Soft press squash for mobile HUD buttons — instant down, spring back.
    /// </summary>
    public sealed class UiButtonPressFx : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float PressScale = 0.94f;
        const float Spring = 22f;

        RectTransform _rt;
        Vector3 _rest = Vector3.one;
        Vector3 _target = Vector3.one;
        bool _bound;

        void Awake() => Bind();

        void Bind()
        {
            if (_bound) return;
            _rt = transform as RectTransform;
            if (_rt == null) return;
            _rest = _rt.localScale;
            if (_rest.sqrMagnitude < 0.0001f) _rest = Vector3.one;
            _target = _rest;
            _bound = true;
        }

        void Update()
        {
            if (_rt == null) return;
            var cur = _rt.localScale;
            if ((cur - _target).sqrMagnitude < 0.00001f)
            {
                _rt.localScale = _target;
                return;
            }

            _rt.localScale = Vector3.Lerp(cur, _target, 1f - Mathf.Exp(-Spring * Time.unscaledDeltaTime));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Bind();
            _target = _rest * PressScale;
            _rt.localScale = _target;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Bind();
            if (_rt != null) _rt.localScale = _rest * 1.04f;
            _target = _rest;
        }

        public void OnPointerExit(PointerEventData eventData) => _target = _rest;
    }
}

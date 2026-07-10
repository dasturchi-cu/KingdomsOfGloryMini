using UnityEngine;

namespace KoG.MiniMvp.UI
{
    /// <summary>
    /// Anchors a full-screen RectTransform to Screen.safeArea (notches / home indicator).
    /// </summary>
    public sealed class SafeAreaPad : MonoBehaviour
    {
        RectTransform _rt;
        Rect _last;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _last) Apply();
        }

        void Apply()
        {
            if (_rt == null) return;
            _last = Screen.safeArea;
            var sa = _last;
            var min = sa.position;
            var max = min + sa.size;
            var w = Mathf.Max(Screen.width, 1);
            var h = Mathf.Max(Screen.height, 1);
            _rt.anchorMin = new Vector2(min.x / w, min.y / h);
            _rt.anchorMax = new Vector2(max.x / w, max.y / h);
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}

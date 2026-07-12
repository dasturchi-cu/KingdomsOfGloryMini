using UnityEngine;
using UnityEngine.UI;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// World-space construction progress bar above a building.
    /// </summary>
    public sealed class ConstructionWorldBar : MonoBehaviour
    {
        Transform _target;
        float _totalSeconds = 1f;
        float _leftSeconds;
        Image _fill;
        Text _label;
        Canvas _canvas;

        public static ConstructionWorldBar Attach(Transform building, float totalSeconds, float leftSeconds)
        {
            if (building == null) return null;
            var existing = building.GetComponentInChildren<ConstructionWorldBar>(true);
            if (existing == null)
            {
                var go = new GameObject("ConstructionWorldBar");
                go.transform.SetParent(building, false);
                existing = go.AddComponent<ConstructionWorldBar>();
                existing.BuildUi();
            }

            existing._target = building;
            existing._totalSeconds = Mathf.Max(0.01f, totalSeconds);
            existing._leftSeconds = Mathf.Max(0f, leftSeconds);
            existing.gameObject.SetActive(leftSeconds > 0.05f);
            existing.Refresh();
            return existing;
        }

        public static void Clear(Transform building)
        {
            if (building == null) return;
            var bar = building.GetComponentInChildren<ConstructionWorldBar>(true);
            if (bar != null) Object.Destroy(bar.gameObject);
        }

        public void Tick(float leftSeconds)
        {
            _leftSeconds = Mathf.Max(0f, leftSeconds);
            if (_leftSeconds <= 0.05f)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            Refresh();
        }

        void LateUpdate()
        {
            if (_target == null || _canvas == null) return;
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;
            transform.position = _target.position + Vector3.up * 2.4f;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 40;
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 28f);
            transform.localScale = Vector3.one * 0.012f;

            var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.9f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bg.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            _fill = fillGo.GetComponent<Image>();
            _fill.color = new Color(0.35f, 0.78f, 0.42f, 1f);
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = (int)Image.OriginHorizontal.Left;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _label = labelGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 18;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.raycastTarget = false;
        }

        void Refresh()
        {
            var pct = 1f - (_leftSeconds / _totalSeconds);
            if (_fill != null) _fill.fillAmount = Mathf.Clamp01(pct);
            if (_label != null) _label.text = Mathf.CeilToInt(_leftSeconds) + "s";
        }
    }
}

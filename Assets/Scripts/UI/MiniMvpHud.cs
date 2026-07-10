using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KoG.MiniMvp.UI
{
    /// <summary>
    /// CoC-style runtime HUD tuned for Unity Simulator / Game view.
    /// </summary>
    public sealed class MiniMvpHud : MonoBehaviour
    {
        public const float BottomBarHeight = 178f;

        Text _title;
        Text _status;
        Text _goldChip;
        Text _troopChip;
        Text _selectedChip;
        GameObject _authRoot;
        GameObject _gameRoot;
        GameObject _resultRoot;
        Text _resultBody;
        InputField _userField;
        InputField _displayField;
        InputField _emailField;
        InputField _passwordField;
        bool _busy;

        public Action OnPlaceMine;
        public Action OnPlaceBarracks;
        public Action OnCollect;
        public Action OnTrain;
        public Action OnUpgrade;
        public Action OnStartRaid;
        public Action OnCompleteRaid;
        public Action OnLogout;
        public Action OnResultOk;
        public Action OnRegister;
        public Action OnLogin;
        public Action OnGuest;

        public string Username => _userField != null ? _userField.text : "";
        public string DisplayName => _displayField != null ? _displayField.text : "";
        public string Email => _emailField != null ? _emailField.text : "";
        public string Password => _passwordField != null ? _passwordField.text : "";

        public static MiniMvpHud Ensure(Transform host)
        {
            var existing = host.GetComponent<MiniMvpHud>();
            if (existing != null) return existing;
            return host.gameObject.AddComponent<MiniMvpHud>();
        }

        void Awake()
        {
            EnsureEventSystem();
            BuildCanvas();
            ShowAuth(false);
            ShowGame(false);
            ShowResult(false);
        }

        public void SetBusy(bool busy) => _busy = busy;

        public void SetStatus(string status)
        {
            if (_status != null) _status.text = status ?? "";
        }

        public void SetResources(long gold, int barbarians)
        {
            if (_goldChip != null) _goldChip.text = "●  " + FormatNum(gold);
            if (_troopChip != null) _troopChip.text = "⚔  " + barbarians;
        }

        public void SetSelected(string label)
        {
            if (_selectedChip == null) return;
            _selectedChip.text = string.IsNullOrEmpty(label) ? "Tanlangan: —" : "Tanlangan: " + label;
        }

        public void SetAuthFields(string username, string displayName, string email, string password)
        {
            if (_userField != null) _userField.text = username ?? "";
            if (_displayField != null) _displayField.text = displayName ?? "";
            if (_emailField != null) _emailField.text = email ?? "";
            if (_passwordField != null) _passwordField.text = password ?? "";
        }

        public void ShowAuth(bool visible)
        {
            if (_authRoot != null) _authRoot.SetActive(visible);
        }

        public void ShowGame(bool visible)
        {
            if (_gameRoot != null) _gameRoot.SetActive(visible);
        }

        public void ShowResult(bool visible)
        {
            if (_resultRoot != null) _resultRoot.SetActive(visible);
        }

        public void SetResultMessage(string message)
        {
            if (_resultBody != null) _resultBody.text = message ?? "";
        }

        static string FormatNum(long n)
        {
            if (n >= 1_000_000) return (n / 1_000_000f).ToString("0.#") + "M";
            if (n >= 10_000) return (n / 1000f).ToString("0.#") + "K";
            return n.ToString();
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("MiniMvpCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.55f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var safeGo = new GameObject("SafeArea", typeof(RectTransform));
            safeGo.transform.SetParent(canvasGo.transform, false);
            var safeRt = safeGo.GetComponent<RectTransform>();
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;
            safeGo.AddComponent<SafeAreaPad>();
            var root = safeGo.transform;

            // Top resource strip (CoC-like).
            var top = Panel(root, "TopBar",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(1040f, 132f),
                new Color(0.04f, 0.05f, 0.08f, 0.72f));

            _title = Label(top.transform, "Title", "Kingdoms of Glory", 26, TextAnchor.UpperLeft,
                new Vector2(18f, -10f), new Vector2(520f, 34f), new Color(1f, 0.92f, 0.55f));

            var goldPanel = Panel(top.transform, "GoldChip",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-210f, -18f), new Vector2(190f, 44f),
                new Color(0.35f, 0.22f, 0.05f, 0.92f));
            goldPanel.anchorMin = new Vector2(1f, 1f);
            goldPanel.anchorMax = new Vector2(1f, 1f);
            goldPanel.pivot = new Vector2(1f, 1f);
            _goldChip = Label(goldPanel, "GoldTxt", "●  0", 22, TextAnchor.MiddleCenter,
                new Vector2(8f, -6f), new Vector2(174f, 32f), new Color(1f, 0.86f, 0.25f));
            CenterLabel(_goldChip);

            var troopPanel = Panel(top.transform, "TroopChip",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -18f), new Vector2(170f, 44f),
                new Color(0.12f, 0.18f, 0.32f, 0.92f));
            troopPanel.anchorMin = new Vector2(1f, 1f);
            troopPanel.anchorMax = new Vector2(1f, 1f);
            troopPanel.pivot = new Vector2(1f, 1f);
            _troopChip = Label(troopPanel, "TroopTxt", "⚔  0", 22, TextAnchor.MiddleCenter,
                new Vector2(8f, -6f), new Vector2(154f, 32f), new Color(0.75f, 0.88f, 1f));
            CenterLabel(_troopChip);

            _status = Label(top.transform, "Status", "Simulator — Guest bilan boshlang", 18, TextAnchor.UpperLeft,
                new Vector2(18f, -48f), new Vector2(1000f, 36f), new Color(0.95f, 0.93f, 0.78f));
            _selectedChip = Label(top.transform, "Selected", "Tanlangan: —", 17, TextAnchor.UpperLeft,
                new Vector2(18f, -86f), new Vector2(700f, 28f), new Color(0.7f, 0.85f, 1f));

            BuildAuthPanel(root);

            _gameRoot = Panel(root, "GameBar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomBarHeight * 0.5f), new Vector2(1060f, BottomBarHeight),
                new Color(0.03f, 0.04f, 0.07f, 0.92f)).gameObject;

            var row1Y = 18f;
            var row2Y = -40f;
            var x = -510f;
            const float bw = 148f;
            const float gap = 10f;
            MakeBtn(_gameRoot.transform, "Mine", "Mine", ref x, row1Y, bw, gap,
                new Color(0.55f, 0.42f, 0.12f), () => Safe(OnPlaceMine));
            MakeBtn(_gameRoot.transform, "Barracks", "Barracks", ref x, row1Y, bw, gap,
                new Color(0.22f, 0.38f, 0.62f), () => Safe(OnPlaceBarracks));
            MakeBtn(_gameRoot.transform, "Collect", "Collect", ref x, row1Y, bw, gap,
                new Color(0.62f, 0.48f, 0.10f), () => Safe(OnCollect));
            MakeBtn(_gameRoot.transform, "Train", "Train ×10", ref x, row1Y, bw, gap,
                new Color(0.28f, 0.48f, 0.28f), () => Safe(OnTrain));
            MakeBtn(_gameRoot.transform, "Upgrade", "Upgrade", ref x, row1Y, bw, gap,
                new Color(0.42f, 0.28f, 0.55f), () => Safe(OnUpgrade));

            x = -510f;
            MakeBtn(_gameRoot.transform, "Raid", "Start Raid", ref x, row2Y, bw + 10f, gap,
                new Color(0.62f, 0.22f, 0.18f), () => Safe(OnStartRaid));
            MakeBtn(_gameRoot.transform, "Complete", "Complete", ref x, row2Y, bw + 10f, gap,
                new Color(0.55f, 0.28f, 0.18f), () => Safe(OnCompleteRaid));
            MakeBtn(_gameRoot.transform, "Logout", "Logout", ref x, row2Y, bw, gap,
                new Color(0.25f, 0.26f, 0.30f), () => Safe(OnLogout));

            Label(_gameRoot.transform, "Hint",
                "Simulator: drag / scroll / pinch · bino bos → Upgrade · 1→2→3→4→Raid",
                15, TextAnchor.LowerLeft, new Vector2(18f, -BottomBarHeight * 0.5f + 16f), new Vector2(1020f, 24f),
                new Color(0.72f, 0.76f, 0.82f));

            _resultRoot = Panel(root, "ResultPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 360f),
                new Color(0.06f, 0.07f, 0.11f, 0.96f)).gameObject;
            _resultBody = Label(_resultRoot.transform, "Body", "", 22, TextAnchor.UpperLeft,
                new Vector2(24f, -24f), new Vector2(670f, 240f), new Color(1f, 0.95f, 0.7f));
            var okX = -100f;
            MakeBtn(_resultRoot.transform, "Ok", "OK — bazaga", ref okX, -140f, 280f, 0f,
                new Color(0.28f, 0.48f, 0.28f), () => Safe(OnResultOk));
        }

        void BuildAuthPanel(Transform canvas)
        {
            _authRoot = Panel(canvas, "AuthPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(740f, 400f),
                new Color(0.05f, 0.06f, 0.09f, 0.94f)).gameObject;

            Label(_authRoot.transform, "AuthTitle", "Simulator kirish", 26, TextAnchor.UpperLeft,
                new Vector2(28f, -18f), new Vector2(680f, 34f), new Color(1f, 0.9f, 0.55f));

            _userField = Field(_authRoot.transform, "Username", "player1", new Vector2(28f, -70f), false);
            _displayField = Field(_authRoot.transform, "Display", "Player One", new Vector2(28f, -120f), false);
            _emailField = Field(_authRoot.transform, "Email", "player1@test.com", new Vector2(28f, -170f), false);
            _passwordField = Field(_authRoot.transform, "Password", "TestPass123!@#", new Vector2(28f, -220f), true);

            var x = -330f;
            MakeBtn(_authRoot.transform, "Guest", "▶  Guest Play", ref x, -155f, 200f, 14f,
                new Color(0.18f, 0.52f, 0.32f), () => Safe(OnGuest));
            MakeBtn(_authRoot.transform, "Login", "Login", ref x, -155f, 140f, 14f,
                new Color(0.22f, 0.35f, 0.55f), () => Safe(OnLogin));
            MakeBtn(_authRoot.transform, "Register", "Register", ref x, -155f, 150f, 14f,
                new Color(0.35f, 0.28f, 0.48f), () => Safe(OnRegister));

            Label(_authRoot.transform, "AuthHint",
                "Guest = tez test. Drag maydon, scroll zoom. Release emas — faqat simulator.",
                15, TextAnchor.LowerLeft, new Vector2(28f, -380f), new Vector2(680f, 28f),
                new Color(0.75f, 0.78f, 0.85f));
        }

        static void CenterLabel(Text t)
        {
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 4f);
            rt.offsetMax = new Vector2(-6f, -4f);
            t.alignment = TextAnchor.MiddleCenter;
        }

        static InputField Field(Transform parent, string label, string value, Vector2 pos, bool password)
        {
            Label(parent, label + "Lbl", label, 16, TextAnchor.UpperLeft, pos, new Vector2(120f, 28f),
                new Color(0.8f, 0.82f, 0.88f));
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x + 130f, pos.y);
            rt.sizeDelta = new Vector2(500f, 38f);
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 4f);
            trt.offsetMax = new Vector2(-10f, -4f);
            var text = textGo.GetComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = 18;
            text.color = Color.white;
            text.supportRichText = false;

            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phGo.transform.SetParent(go.transform, false);
            var prt = phGo.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(10f, 4f);
            prt.offsetMax = new Vector2(-10f, -4f);
            var ph = phGo.GetComponent<Text>();
            ph.font = BuiltinFont();
            ph.fontSize = 18;
            ph.fontStyle = FontStyle.Italic;
            ph.color = new Color(1f, 1f, 1f, 0.35f);
            ph.text = label;

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = ph;
            field.text = value;
            if (password) field.contentType = InputField.ContentType.Password;
            return field;
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        void Safe(Action action)
        {
            if (_busy || action == null) return;
            action();
        }

        static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        static Text Label(Transform parent, string name, string text, int size, TextAnchor align,
            Vector2 anchoredPos, Vector2 sizeDelta, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = BuiltinFont();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void MakeBtn(Transform parent, string name, string label, ref float x, float y, float width, float gap,
            Color color, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + width * 0.5f, y);
            rt.sizeDelta = new Vector2(width, 48f);
            go.GetComponent<Image>().color = color;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.22f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            colors.disabledColor = new Color(0.2f, 0.2f, 0.22f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var t = labelGo.GetComponent<Text>();
            t.font = BuiltinFont();
            t.text = label;
            t.fontSize = 17;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            x += width + gap;
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KoG.MiniMvp.UI
{
    /// <summary>
    /// Runtime Screen-Space Overlay HUD for the Mini-MVP game loop.
    /// Built in code so SampleScene needs no prefab wiring.
    /// </summary>
    public sealed class MiniMvpHud : MonoBehaviour
    {
        public const float BottomBarHeight = 168f;

        Text _title;
        Text _status;
        Text _resources;
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
            if (_resources != null)
                _resources.text = "Gold: " + gold + "   |   Barbarian: " + barbarians;
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
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Top status strip.
            var top = Panel(canvasGo.transform, "TopBar",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(1040f, 120f),
                new Color(0f, 0f, 0f, 0.55f));
            _title = Label(top.transform, "Title", "Kingdoms of Glory — Mini MVP", 28, TextAnchor.UpperLeft,
                new Vector2(16f, -10f), new Vector2(1000f, 36f));
            _status = Label(top.transform, "Status", "Ready", 20, TextAnchor.UpperLeft,
                new Vector2(16f, -48f), new Vector2(1000f, 56f), new Color(1f, 0.95f, 0.6f));

            BuildAuthPanel(canvasGo.transform);

            // Bottom game actions.
            _gameRoot = Panel(canvasGo.transform, "GameBar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomBarHeight * 0.5f), new Vector2(1060f, BottomBarHeight),
                new Color(0.05f, 0.05f, 0.08f, 0.88f)).gameObject;

            _resources = Label(_gameRoot.transform, "Resources", "Gold: 0   |   Barbarian: 0", 22, TextAnchor.MiddleLeft,
                new Vector2(16f, 58f), new Vector2(700f, 32f), Color.white);

            var row1Y = -8f;
            var row2Y = -62f;
            var x = -500f;
            const float bw = 140f;
            const float gap = 10f;
            MakeBtn(_gameRoot.transform, "Mine", "1 Mine", ref x, row1Y, bw, gap, () => Safe(OnPlaceMine));
            MakeBtn(_gameRoot.transform, "Barracks", "2 Barracks", ref x, row1Y, bw, gap, () => Safe(OnPlaceBarracks));
            MakeBtn(_gameRoot.transform, "Collect", "3 Collect", ref x, row1Y, bw, gap, () => Safe(OnCollect));
            MakeBtn(_gameRoot.transform, "Train", "4 Train x10", ref x, row1Y, bw, gap, () => Safe(OnTrain));
            MakeBtn(_gameRoot.transform, "Upgrade", "5 Upgrade", ref x, row1Y, bw, gap, () => Safe(OnUpgrade));

            x = -500f;
            MakeBtn(_gameRoot.transform, "Raid", "6 Start Raid", ref x, row2Y, bw, gap, () => Safe(OnStartRaid));
            MakeBtn(_gameRoot.transform, "Complete", "7 Complete", ref x, row2Y, bw + 20f, gap, () => Safe(OnCompleteRaid));
            MakeBtn(_gameRoot.transform, "Logout", "Logout", ref x, row2Y, bw, gap, () => Safe(OnLogout));

            Label(_gameRoot.transform, "Hint", "Tartib: 1→2→3→4→6, 30s kut, keyin 7. Binoni bosib Upgrade.",
                16, TextAnchor.LowerLeft, new Vector2(16f, -BottomBarHeight * 0.5f + 14f), new Vector2(1000f, 24f),
                new Color(0.85f, 0.85f, 0.9f));

            // Result overlay.
            _resultRoot = Panel(canvasGo.transform, "ResultPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 360f),
                new Color(0.08f, 0.08f, 0.12f, 0.94f)).gameObject;
            _resultBody = Label(_resultRoot.transform, "Body", "", 22, TextAnchor.UpperLeft,
                new Vector2(24f, -24f), new Vector2(670f, 240f), new Color(1f, 0.95f, 0.7f));
            var okX = -100f;
            MakeBtn(_resultRoot.transform, "Ok", "OK — back to base", ref okX, -140f, 280f, 0f, () => Safe(OnResultOk));
        }

        void BuildAuthPanel(Transform canvas)
        {
            _authRoot = Panel(canvas, "AuthPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 210f), new Vector2(720f, 380f),
                new Color(0.06f, 0.07f, 0.10f, 0.92f)).gameObject;

            Label(_authRoot.transform, "AuthTitle", "Kirish / Ro'yxat", 24, TextAnchor.UpperLeft,
                new Vector2(24f, -16f), new Vector2(660f, 32f));

            _userField = Field(_authRoot.transform, "Username", "player1", new Vector2(24f, -60f), false);
            _displayField = Field(_authRoot.transform, "Display", "Player One", new Vector2(24f, -110f), false);
            _emailField = Field(_authRoot.transform, "Email", "player1@test.com", new Vector2(24f, -160f), false);
            _passwordField = Field(_authRoot.transform, "Password", "TestPass123!@#", new Vector2(24f, -210f), true);

            var x = -320f;
            MakeBtn(_authRoot.transform, "Register", "Register", ref x, -150f, 160f, 12f, () => Safe(OnRegister));
            MakeBtn(_authRoot.transform, "Login", "Login", ref x, -150f, 140f, 12f, () => Safe(OnLogin));
            MakeBtn(_authRoot.transform, "Guest", "Guest", ref x, -150f, 140f, 12f, () => Safe(OnGuest));

            Label(_authRoot.transform, "AuthHint", "Guest = yangi test akkaunt. Google/Apple keyinroq.",
                16, TextAnchor.LowerLeft, new Vector2(24f, -360f), new Vector2(660f, 28f),
                new Color(0.8f, 0.82f, 0.88f));
        }

        static InputField Field(Transform parent, string label, string value, Vector2 pos, bool password)
        {
            Label(parent, label + "Lbl", label, 16, TextAnchor.UpperLeft, pos, new Vector2(120f, 28f));
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x + 130f, pos.y);
            rt.sizeDelta = new Vector2(480f, 36f);
            go.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.20f, 1f);

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

        static void MakeBtn(Transform parent, string name, string label, ref float x, float y, float width, float gap, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + width * 0.5f, y);
            rt.sizeDelta = new Vector2(width, 44f);
            go.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.40f, 1f);
            var btn = go.GetComponent<Button>();
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
            t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            x += width + gap;
        }
    }
}

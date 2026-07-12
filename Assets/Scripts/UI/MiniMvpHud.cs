using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KoG.MiniMvp.UI
{
    /// <summary>
    /// CoC-style runtime HUD — thumb-zone loop bar + social sheet + busy/error overlays.
    /// </summary>
    public sealed class MiniMvpHud : MonoBehaviour
    {
        /// <summary>Thumb-zone action bar — loop + build tools; social is a sheet.</summary>
        public const float BottomBarHeight = 210f;

        Text _title;
        Text _status;
        Text _goalCard;
        Text _raidHud;
        Text _goldChip;
        Text _manaChip;
        Text _diamondChip;
        Text _troopChip;
        Text _selectedChip;
        Text _busyLabel;
        Text _errorLabel;
        Text _manaTip;
        GameObject _authRoot;
        GameObject _gameRoot;
        GameObject _resultRoot;
        GameObject _busyOverlay;
        GameObject _errorBanner;
        GameObject _socialSheet;
        GameObject _achievementsSheet;
        GameObject _unlockSheet;
        GameObject _clanSheet;
        GameObject _chatSheet;
        GameObject _pvpSheet;
        GameObject _tournamentSheet;
        Text[] _achievementProgressLabels;
        Button[] _achievementClaimButtons;
        Text _unlockBodyLabel;
        Text _clanRosterLabel;
        Text _chatHistoryLabel;
        Text _pvpResultLabel;
        Text _tournamentBracketLabel;
        InputField _clanJoinField;
        Text _resultBody;
        Text _resultStars;
        Text _resultLoot;
        Text _muteLabel;
        Text _trainLabel;
        Text _troopToggleLabel;
        Button _troopToggleBtn;
        InputField _userField;
        InputField _displayField;
        InputField _emailField;
        InputField _passwordField;
        bool _busy;
        bool _raidCompleteReady;
        readonly List<Button> _actionButtons = new List<Button>(24);

        public Action OnPlaceMine;
        public Action OnPlaceBarracks;
        public Action OnCollect;
        public Action OnTrain;
        public Action OnUpgrade;
        public Action OnConfirmPlace;
        public Action OnCancelPlace;
        public Action OnRotatePlace;
        public Action OnDestroyBuilding;
        public Action OnRepairBuilding;
        public Action OnCancelUpgrade;
        public Action OnStartRaid;
        public Action OnCompleteRaid;
        public Action OnLogout;
        public Action OnResultOk;
        public Action OnRegister;
        public Action OnLogin;
        public Action OnGuest;
        public Action OnClan;
        public Action OnChat;
        public Action OnPvp;
        public Action OnTournament;
        public Action OnRewardedAd;
        public Action OnManualSave;
        public Action OnRetryLoad;
        public Action OnOpenAchievements;
        public Action OnOpenUnlocks;
        public Action OnToggleTroopType;
        public Action<string> OnClaimAchievement;
        public Action OnOpenClan;
        public Action OnClanCreate;
        public Action OnClanJoin;
        public Action OnClanLeave;
        public Action OnClanRefresh;
        public Action OnOpenChat;
        public Action OnChatSend;
        public Action OnChatRefresh;
        public Action OnOpenPvp;
        public Action OnPvpFight;
        public Action OnPvpRematch;
        public Action OnOpenTournament;
        public Action OnTournamentJoin;
        public Action OnTournamentRefresh;

        public string ClanJoinId => _clanJoinField != null ? (_clanJoinField.text ?? "").Trim() : "";

        GameObject _placementBar;
        GameObject _buildActionBar;
        GameObject _loopActionBar;
        Button _completeRaidBtn;
        Text _completeRaidLabel;

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
            SetBusy(false);
            ClearError();
            ShowSocialSheet(false);
            ShowAchievementsSheet(false);
            ShowUnlockSheet(false);
            ShowClanSheet(false);
            ShowChatSheet(false);
            ShowPvpSheet(false);
            ShowTournamentSheet(false);
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            if (_busyOverlay != null) _busyOverlay.SetActive(busy);
            if (_busyLabel != null)
                _busyLabel.text = busy ? "Kutilmoqda…" : "";
            for (var i = 0; i < _actionButtons.Count; i++)
            {
                var btn = _actionButtons[i];
                if (btn == null) continue;
                if (btn == _completeRaidBtn)
                {
                    btn.interactable = !busy && _raidCompleteReady;
                    continue;
                }
                btn.interactable = !busy;
            }
        }

        public void SetStatus(string status)
        {
            if (_status != null) _status.text = status ?? "";
        }

        public void SetGoal(string title, string cta)
        {
            if (_goalCard == null) return;
            if (string.IsNullOrEmpty(title))
            {
                _goalCard.text = "";
                return;
            }

            _goalCard.text = string.IsNullOrEmpty(cta)
                ? title
                : title + "  ·  " + cta;
        }

        public void SetRaidHud(int fortressId, int deployCount, int hpPercent)
        {
            if (_raidHud == null) return;
            if (fortressId <= 0)
            {
                _raidHud.text = "";
                return;
            }

            var hp = Mathf.Clamp(hpPercent, 0, 100);
            _raidHud.text = "Lager " + fortressId + "  ·  Deploy " + deployCount + "  ·  HP " + hp + "%";
        }

        public void ShowError(string message, bool showRetry)
        {
            if (_errorBanner != null) _errorBanner.SetActive(true);
            if (_errorLabel != null) _errorLabel.text = message ?? "Xato";
            var retryBtn = _errorBanner != null
                ? _errorBanner.transform.Find("Retry")?.GetComponent<Button>()
                : null;
            if (retryBtn != null) retryBtn.gameObject.SetActive(showRetry);
        }

        public void ClearError()
        {
            if (_errorBanner != null) _errorBanner.SetActive(false);
            if (_errorLabel != null) _errorLabel.text = "";
        }

        public void SetResources(long gold, int barbarians)
        {
            SetResources(gold, 0, 0, barbarians);
        }

        public void SetResources(long gold, long mana, long diamond, int barbarians)
        {
            SetResources(gold, mana, diamond, barbarians, barbarians, 0);
        }

        public void SetResources(long gold, long mana, long diamond, int barbarians, int housingUsed, int housingMax)
        {
            if (_goldChip != null) _goldChip.text = "● " + FormatNum(gold);
            if (_manaChip != null) _manaChip.text = "◆ " + FormatNum(mana);
            if (_diamondChip != null) _diamondChip.text = "◇ " + FormatNum(diamond);
            if (_troopChip != null)
            {
                if (housingMax > 0)
                    _troopChip.text = "⚔ " + housingUsed + "/" + housingMax;
                else
                    _troopChip.text = "⚔ " + barbarians;
            }
        }

        public void SetSelected(string label)
        {
            if (_selectedChip == null) return;
            _selectedChip.text = string.IsNullOrEmpty(label) ? "Tanlangan: —" : "Tanlangan: " + label;
        }

        public void SetPlacementMode(bool placing)
        {
            if (_placementBar != null) _placementBar.SetActive(placing);
            if (_buildActionBar != null) _buildActionBar.SetActive(!placing);
            if (_loopActionBar != null) _loopActionBar.SetActive(!placing);
        }

        /// <summary>Disable Complete until raid wait elapsed; show remaining seconds on label.</summary>
        public void SetRaidCompleteReady(bool ready, int secondsLeft)
        {
            _raidCompleteReady = ready;
            if (_completeRaidBtn != null)
                _completeRaidBtn.interactable = !_busy && ready;
            if (_completeRaidLabel != null)
            {
                _completeRaidLabel.text = ready
                    ? "Yakunla"
                    : (secondsLeft > 0 ? "Kut " + secondsLeft + "s" : "Yakunla");
            }
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
            if (visible)
            {
                CloseAllOverlaySheets();
            }
        }

        public void ShowGame(bool visible)
        {
            if (_gameRoot != null) _gameRoot.SetActive(visible);
            if (!visible)
            {
                CloseAllOverlaySheets();
            }
        }

        public void ShowResult(bool visible)
        {
            if (_resultRoot != null) _resultRoot.SetActive(visible);
            if (visible)
            {
                CloseAllOverlaySheets();
            }
        }

        public void SetResultMessage(string message)
        {
            if (_resultBody != null) _resultBody.text = message ?? "";
        }

        public void SetResultChips(int stars, long lootGold, bool won)
        {
            if (_resultStars != null)
                _resultStars.text = won ? "★  " + Mathf.Max(0, stars) : "★  0";
            if (_resultLoot != null)
                _resultLoot.text = lootGold > 0 ? "+ " + lootGold + " ●" : "Loot 0";
        }

        void CloseAllOverlaySheets()
        {
            ShowSocialSheet(false);
            ShowAchievementsSheet(false);
            ShowClanSheet(false);
            ShowChatSheet(false);
            ShowPvpSheet(false);
            ShowTournamentSheet(false);
        }

        void ShowSocialSheet(bool visible)
        {
            if (visible)
            {
                ShowAchievementsSheet(false);
                ShowClanSheet(false);
                ShowChatSheet(false);
                ShowPvpSheet(false);
                ShowTournamentSheet(false);
            }
            if (_socialSheet != null) _socialSheet.SetActive(visible);
        }

        public void ShowAchievementsSheet(bool visible)
        {
            if (visible)
            {
                if (_socialSheet != null) _socialSheet.SetActive(false);
                ShowUnlockSheet(false);
                ShowClanSheet(false);
                ShowChatSheet(false);
                ShowPvpSheet(false);
                ShowTournamentSheet(false);
            }
            if (_achievementsSheet != null) _achievementsSheet.SetActive(visible);
        }

        public void ShowUnlockSheet(bool visible)
        {
            if (visible)
            {
                if (_socialSheet != null) _socialSheet.SetActive(false);
                ShowAchievementsSheet(false);
                ShowClanSheet(false);
                ShowChatSheet(false);
                ShowPvpSheet(false);
                ShowTournamentSheet(false);
            }
            if (_unlockSheet != null) _unlockSheet.SetActive(visible);
        }

        public void SetUnlockBody(string text)
        {
            if (_unlockBodyLabel != null) _unlockBodyLabel.text = text ?? "";
        }

        /// <summary>Train button + optional L4 troop-type toggle (Askar / Archer).</summary>
        public void SetTrainTroopUi(string troopType, bool archerUnlocked)
        {
            var isArcher = string.Equals(troopType, "archer", StringComparison.Ordinal);
            var label = isArcher ? "Archer ×10" : "Askar ×10";
            if (_trainLabel != null) _trainLabel.text = label;
            if (_troopToggleBtn != null) _troopToggleBtn.gameObject.SetActive(archerUnlocked);
            if (_troopToggleLabel != null)
                _troopToggleLabel.text = isArcher ? "Archer" : "Askar";
        }

        public void ShowClanSheet(bool visible)
        {
            if (visible)
            {
                if (_socialSheet != null) _socialSheet.SetActive(false);
                ShowAchievementsSheet(false);
                ShowUnlockSheet(false);
                ShowChatSheet(false);
                ShowPvpSheet(false);
                ShowTournamentSheet(false);
            }
            if (_clanSheet != null) _clanSheet.SetActive(visible);
        }

        public void ShowChatSheet(bool visible)
        {
            if (visible)
            {
                if (_socialSheet != null) _socialSheet.SetActive(false);
                ShowAchievementsSheet(false);
                ShowUnlockSheet(false);
                ShowClanSheet(false);
                ShowPvpSheet(false);
                ShowTournamentSheet(false);
            }
            if (_chatSheet != null) _chatSheet.SetActive(visible);
        }

        public void ShowPvpSheet(bool visible)
        {
            if (visible)
            {
                if (_socialSheet != null) _socialSheet.SetActive(false);
                ShowAchievementsSheet(false);
                ShowUnlockSheet(false);
                ShowClanSheet(false);
                ShowChatSheet(false);
                ShowTournamentSheet(false);
            }
            if (_pvpSheet != null) _pvpSheet.SetActive(visible);
        }

        public void ShowTournamentSheet(bool visible)
        {
            if (visible)
            {
                if (_socialSheet != null) _socialSheet.SetActive(false);
                ShowAchievementsSheet(false);
                ShowUnlockSheet(false);
                ShowClanSheet(false);
                ShowChatSheet(false);
                ShowPvpSheet(false);
            }
            if (_tournamentSheet != null) _tournamentSheet.SetActive(visible);
        }

        public void SetClanRoster(string text)
        {
            if (_clanRosterLabel != null) _clanRosterLabel.text = text ?? "";
        }

        public void SetChatHistory(string text)
        {
            if (_chatHistoryLabel != null) _chatHistoryLabel.text = text ?? "";
        }

        public void SetPvpResult(string text)
        {
            if (_pvpResultLabel != null) _pvpResultLabel.text = text ?? "";
        }

        public void SetTournamentBracket(string text)
        {
            if (_tournamentBracketLabel != null) _tournamentBracketLabel.text = text ?? "";
        }

        /// <summary>Bind soft-test achievement rows (order: train_troops, place_mine, first_raid).</summary>
        public void SetAchievements(KoG.MiniMvp.Network.AchievementDto[] rows)
        {
            if (_achievementProgressLabels == null || _achievementClaimButtons == null) return;
            for (var i = 0; i < _achievementProgressLabels.Length; i++)
            {
                var row = rows != null && i < rows.Length ? rows[i] : null;
                var progressLabel = _achievementProgressLabels[i];
                var claimBtn = _achievementClaimButtons[i];
                if (progressLabel == null || claimBtn == null) continue;

                if (row == null)
                {
                    progressLabel.text = "—";
                    claimBtn.interactable = false;
                    var emptyTxt = claimBtn.GetComponentInChildren<Text>();
                    if (emptyTxt != null) emptyTxt.text = "—";
                    continue;
                }

                progressLabel.text = row.progress + "/" + row.target
                    + (row.claimed ? " · olindi" : (row.claimable ? " · tayyor" : ""));
                var label = claimBtn.GetComponentInChildren<Text>();
                if (row.claimed)
                {
                    if (label != null) label.text = "Olindi";
                    claimBtn.interactable = false;
                }
                else if (row.claimable)
                {
                    if (label != null) label.text = "+" + row.rewardDiamonds + "◇";
                    claimBtn.interactable = !_busy;
                }
                else
                {
                    if (label != null) label.text = "Kut";
                    claimBtn.interactable = false;
                }
            }
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

            var top = Panel(root, "TopBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -10f), new Vector2(-24f, 168f),
                new Color(0.05f, 0.07f, 0.10f, 0.88f));
            top.pivot = new Vector2(0.5f, 1f);
            top.anchoredPosition = new Vector2(0f, -10f);
            top.sizeDelta = new Vector2(-24f, 168f);

            _title = Label(top.transform, "Title", "Kingdoms of Glory", 22, TextAnchor.UpperLeft,
                new Vector2(16f, -10f), new Vector2(360f, 28f), new Color(1f, 0.90f, 0.48f));

            var muteX = 390f;
            MakeBtn(top.transform, "Mute", MuteLabel(), ref muteX, -32f, 52f, 0f, 48f,
                new Color(0.18f, 0.20f, 0.26f), () =>
                {
                    KoG.MiniMvp.Audio.MiniAudio.ToggleMute();
                    RefreshMuteLabel();
                });
            var muteGo = top.transform.Find("Mute");
            if (muteGo != null) _muteLabel = muteGo.GetComponentInChildren<Text>();

            // Resource chips (right → left): troops, diamond, mana, gold
            _troopChip = MakeResourceChip(top.transform, "TroopChip", "⚔ 0/100",
                new Color(0.10f, 0.20f, 0.34f, 0.95f), new Color(0.78f, 0.90f, 1f), -12f, 130f);
            _diamondChip = MakeResourceChip(top.transform, "DiamondChip", "◇ 0",
                new Color(0.22f, 0.16f, 0.34f, 0.95f), new Color(0.85f, 0.78f, 1f), -150f, 100f);
            _manaChip = MakeResourceChip(top.transform, "ManaChip", "◆ 0",
                new Color(0.10f, 0.28f, 0.36f, 0.95f), new Color(0.55f, 0.88f, 1f), -258f, 100f);
            _goldChip = MakeResourceChip(top.transform, "GoldChip", "● 0",
                new Color(0.38f, 0.26f, 0.06f, 0.95f), new Color(1f, 0.88f, 0.28f), -366f, 110f);

            _status = Label(top.transform, "Status", "Guest bilan boshlang", 16, TextAnchor.UpperLeft,
                new Vector2(16f, -48f), new Vector2(980f, 28f), new Color(0.96f, 0.94f, 0.82f));
            _goalCard = Label(top.transform, "Goal", "Maqsad: Kon qo‘ying  ·  Kon → Tasdiq", 15, TextAnchor.UpperLeft,
                new Vector2(16f, -78f), new Vector2(980f, 26f), new Color(0.55f, 0.95f, 0.72f));
            _selectedChip = Label(top.transform, "Selected", "Tanlangan: —", 14, TextAnchor.UpperLeft,
                new Vector2(16f, -106f), new Vector2(520f, 24f), new Color(0.68f, 0.84f, 1f));
            _raidHud = Label(top.transform, "RaidHud", "", 14, TextAnchor.UpperLeft,
                new Vector2(540f, -106f), new Vector2(460f, 24f), new Color(1f, 0.72f, 0.55f));
            _manaTip = Label(top.transform, "ManaTip", "◆ Mana = askar · ⚔ = lager joyi · ● Gold = qurilish", 13, TextAnchor.UpperLeft,
                new Vector2(16f, -132f), new Vector2(780f, 22f), new Color(0.70f, 0.78f, 0.86f));

            BuildErrorBanner(root);
            BuildAuthPanel(root);
            BuildGameBar(root);
            BuildSocialSheet(root);
            BuildAchievementsSheet(root);
            BuildUnlockSheet(root);
            BuildClanSheet(root);
            BuildChatSheet(root);
            BuildPvpSheet(root);
            BuildTournamentSheet(root);
            BuildResultPanel(root);
            BuildBusyOverlay(root);
        }

        void BuildErrorBanner(Transform root)
        {
            var banner = Panel(root, "ErrorBanner",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -188f), new Vector2(1000f, 72f),
                new Color(0.42f, 0.12f, 0.10f, 0.96f));
            banner.pivot = new Vector2(0.5f, 1f);
            _errorBanner = banner.gameObject;
            _errorLabel = Label(banner, "ErrorTxt", "", 16, TextAnchor.MiddleLeft,
                new Vector2(20f, -12f), new Vector2(720f, 48f), new Color(1f, 0.92f, 0.88f));
            var rx = 320f;
            MakeBtn(banner, "Retry", "Qayta urin", ref rx, -36f, 160f, 0f, 48f,
                new Color(0.55f, 0.22f, 0.16f), () =>
                {
                    ClearError();
                    Safe(OnRetryLoad);
                });
            _errorBanner.SetActive(false);
        }

        void BuildBusyOverlay(Transform root)
        {
            var overlay = Panel(root, "BusyOverlay",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.45f));
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            _busyOverlay = overlay.gameObject;
            // Block taps while busy.
            var blocker = _busyOverlay.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.onClick.AddListener(() => { });

            var chip = Panel(overlay, "BusyChip",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(320f, 72f),
                new Color(0.08f, 0.10f, 0.14f, 0.96f));
            _busyLabel = Label(chip, "BusyTxt", "Kutilmoqda…", 20, TextAnchor.MiddleCenter,
                new Vector2(20f, -18f), new Vector2(280f, 40f), new Color(1f, 0.92f, 0.7f));
            CenterLabel(_busyLabel);
            _busyOverlay.SetActive(false);
        }

        void BuildGameBar(Transform root)
        {
            _gameRoot = Panel(root, "GameBar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomBarHeight * 0.5f), new Vector2(1060f, BottomBarHeight),
                new Color(0.04f, 0.06f, 0.09f, 0.94f)).gameObject;

            var accent = Panel(_gameRoot.transform, "BarAccent",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -2f), new Vector2(1060f, 4f),
                new Color(0.85f, 0.65f, 0.18f, 0.85f));
            accent.pivot = new Vector2(0.5f, 1f);

            Label(_gameRoot.transform, "Hint",
                "Kon/Kazarma → Tasdiq/Aylantir/Bekor · Tanlangan: Yangila/Buz/Tuzat",
                15, TextAnchor.UpperLeft, new Vector2(20f, -6f), new Vector2(1020f, 22f),
                new Color(0.78f, 0.82f, 0.88f));

            const float bw = 148f;
            const float gap = 10f;
            const float btnH = 48f;
            var toolsY = 78f;
            var row1Y = 22f;
            var row2Y = -36f;

            _placementBar = new GameObject("PlacementBar", typeof(RectTransform));
            _placementBar.transform.SetParent(_gameRoot.transform, false);
            var placeRt = _placementBar.GetComponent<RectTransform>();
            placeRt.anchorMin = new Vector2(0.5f, 0.5f);
            placeRt.anchorMax = new Vector2(0.5f, 0.5f);
            placeRt.sizeDelta = new Vector2(1060f, 56f);
            placeRt.anchoredPosition = new Vector2(0f, toolsY);

            var px = -520f;
            MakeBtn(_placementBar.transform, "ConfirmPlace", "Tasdiq", ref px, 0f, bw, gap, btnH,
                new Color(0.18f, 0.52f, 0.28f), () => Safe(OnConfirmPlace));
            MakeBtn(_placementBar.transform, "RotatePlace", "Aylantir", ref px, 0f, bw, gap, btnH,
                new Color(0.28f, 0.40f, 0.55f), () => Safe(OnRotatePlace));
            MakeBtn(_placementBar.transform, "CancelPlace", "Bekor", ref px, 0f, bw, gap, btnH,
                new Color(0.45f, 0.22f, 0.20f), () => Safe(OnCancelPlace));
            _placementBar.SetActive(false);

            _buildActionBar = new GameObject("BuildActionBar", typeof(RectTransform));
            _buildActionBar.transform.SetParent(_gameRoot.transform, false);
            var buildRt = _buildActionBar.GetComponent<RectTransform>();
            buildRt.anchorMin = new Vector2(0.5f, 0.5f);
            buildRt.anchorMax = new Vector2(0.5f, 0.5f);
            buildRt.sizeDelta = new Vector2(1060f, 56f);
            buildRt.anchoredPosition = new Vector2(0f, toolsY);

            var bx = -520f;
            MakeBtn(_buildActionBar.transform, "Destroy", "Buz", ref bx, 0f, bw, gap, btnH,
                new Color(0.50f, 0.18f, 0.18f), () => Safe(OnDestroyBuilding));
            MakeBtn(_buildActionBar.transform, "Repair", "Tuzat", ref bx, 0f, bw, gap, btnH,
                new Color(0.35f, 0.42f, 0.22f), () => Safe(OnRepairBuilding));
            MakeBtn(_buildActionBar.transform, "CancelUpg", "Bekor yangi", ref bx, 0f, bw, gap, btnH,
                new Color(0.42f, 0.32f, 0.18f), () => Safe(OnCancelUpgrade));

            _loopActionBar = new GameObject("LoopActionBar", typeof(RectTransform));
            _loopActionBar.transform.SetParent(_gameRoot.transform, false);
            var loopRt = _loopActionBar.GetComponent<RectTransform>();
            loopRt.anchorMin = new Vector2(0.5f, 0.5f);
            loopRt.anchorMax = new Vector2(0.5f, 0.5f);
            loopRt.sizeDelta = new Vector2(1060f, 140f);
            loopRt.anchoredPosition = new Vector2(0f, -8f);

            var x = -520f;
            MakeBtn(_loopActionBar.transform, "Mine", "Kon", ref x, row1Y + 8f, bw, gap, btnH,
                new Color(0.52f, 0.40f, 0.12f), () => Safe(OnPlaceMine));
            MakeBtn(_loopActionBar.transform, "Barracks", "Kazarma", ref x, row1Y + 8f, bw, gap, btnH,
                new Color(0.20f, 0.36f, 0.58f), () => Safe(OnPlaceBarracks));
            MakeBtn(_loopActionBar.transform, "Collect", "Yig‘ish", ref x, row1Y + 8f, bw, gap, btnH,
                new Color(0.58f, 0.46f, 0.10f), () => Safe(OnCollect));
            var troopToggle = MakeBtn(_loopActionBar.transform, "TroopType", "Askar", ref x, row1Y + 8f, 110f, gap, btnH,
                new Color(0.22f, 0.38f, 0.34f), () => Safe(OnToggleTroopType));
            _troopToggleBtn = troopToggle;
            _troopToggleLabel = troopToggle != null ? troopToggle.GetComponentInChildren<Text>() : null;
            if (_troopToggleBtn != null) _troopToggleBtn.gameObject.SetActive(false);
            var trainBtn = MakeBtn(_loopActionBar.transform, "Train", "Askar ×10", ref x, row1Y + 8f, bw, gap, btnH,
                new Color(0.26f, 0.46f, 0.28f), () => Safe(OnTrain));
            _trainLabel = trainBtn != null ? trainBtn.GetComponentInChildren<Text>() : null;
            MakeBtn(_loopActionBar.transform, "Upgrade", "Yangila", ref x, row1Y + 8f, bw, gap, btnH,
                new Color(0.40f, 0.28f, 0.52f), () => Safe(OnUpgrade));

            x = -520f;
            MakeBtn(_loopActionBar.transform, "Raid", "Reyd", ref x, row2Y + 8f, bw, gap, btnH,
                new Color(0.58f, 0.20f, 0.16f), () => Safe(OnStartRaid));
            var completeBtn = MakeBtn(_loopActionBar.transform, "Complete", "Yakunla", ref x, row2Y + 8f, bw, gap, btnH,
                new Color(0.50f, 0.26f, 0.16f), () => Safe(OnCompleteRaid));
            _completeRaidBtn = completeBtn;
            if (completeBtn != null)
                _completeRaidLabel = completeBtn.GetComponentInChildren<Text>();
            completeBtn.interactable = false;
            MakeBtn(_loopActionBar.transform, "Social", "Ijtimoiy", ref x, row2Y + 8f, bw, gap, btnH,
                new Color(0.28f, 0.34f, 0.52f), () =>
                {
                    if (_busy) return;
                    ShowSocialSheet(true);
                });
            MakeBtn(_loopActionBar.transform, "Logout", "Chiqish", ref x, row2Y + 8f, bw, gap, btnH,
                new Color(0.22f, 0.24f, 0.28f), () => Safe(OnLogout));
        }

        Text MakeResourceChip(Transform parent, string name, string initial, Color bg, Color fg, float rightX, float width)
        {
            var panel = Panel(parent, name,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(rightX, -10f), new Vector2(width, 36f),
                bg);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(rightX, -10f);
            panel.sizeDelta = new Vector2(width, 36f);
            var chip = Label(panel, name + "Txt", initial, 16, TextAnchor.MiddleCenter,
                new Vector2(4f, -4f), new Vector2(width - 12f, 28f), fg);
            CenterLabel(chip);
            return chip;
        }

        void BuildSocialSheet(Transform root)
        {
            var dim = Panel(root, "SocialDim",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.55f));
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            _socialSheet = dim.gameObject;
            var dimBtn = _socialSheet.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => ShowSocialSheet(false));

            var sheet = Panel(dim, "SocialSheet",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 230f), new Vector2(720f, 400f),
                new Color(0.06f, 0.08f, 0.12f, 0.98f));
            // Panel Image already blocks raycasts so dim-close only fires outside the sheet.

            Label(sheet, "SocialTitle", "Ijtimoiy", 24, TextAnchor.UpperLeft,
                new Vector2(28f, -20f), new Vector2(400f, 32f), new Color(1f, 0.9f, 0.55f));
            Label(sheet, "SocialHint", "Klan · Chat · PvP · Turnir · Reklama (+500g/500m/5💎) · Saqlash",
                15, TextAnchor.UpperLeft, new Vector2(28f, -56f), new Vector2(660f, 28f),
                new Color(0.78f, 0.82f, 0.88f));

            const float bw = 200f;
            const float gap = 14f;
            const float btnH = 52f;
            var x = -320f;
            MakeBtn(sheet, "Clan", "Klan", ref x, 80f, bw, gap, btnH,
                new Color(0.34f, 0.28f, 0.52f), () => { ShowSocialSheet(false); Safe(OnOpenClan); });
            MakeBtn(sheet, "Chat", "Chat", ref x, 80f, bw, gap, btnH,
                new Color(0.16f, 0.40f, 0.46f), () => { ShowSocialSheet(false); Safe(OnOpenChat); });
            MakeBtn(sheet, "Pvp", "PvP", ref x, 80f, bw, gap, btnH,
                new Color(0.52f, 0.16f, 0.26f), () => { ShowSocialSheet(false); Safe(OnOpenPvp); });

            x = -320f;
            MakeBtn(sheet, "Cup", "Turnir", ref x, 10f, bw, gap, btnH,
                new Color(0.52f, 0.40f, 0.12f), () => { ShowSocialSheet(false); Safe(OnOpenTournament); });
            MakeBtn(sheet, "Ad", "Reklama 📺", ref x, 10f, bw, gap, btnH,
                new Color(0.46f, 0.32f, 0.12f), () => { ShowSocialSheet(false); Safe(OnRewardedAd); });
            MakeBtn(sheet, "Save", "Saqlash", ref x, 10f, bw, gap, btnH,
                new Color(0.18f, 0.42f, 0.36f), () => { ShowSocialSheet(false); Safe(OnManualSave); });

            x = -320f;
            MakeBtn(sheet, "Achievements", "Yutuqlar", ref x, -60f, 150f, gap, btnH,
                new Color(0.42f, 0.28f, 0.14f), () =>
                {
                    ShowSocialSheet(false);
                    Safe(OnOpenAchievements);
                });
            MakeBtn(sheet, "Unlocks", "Qal’a", ref x, -60f, 150f, gap, btnH,
                new Color(0.36f, 0.28f, 0.48f), () =>
                {
                    ShowSocialSheet(false);
                    Safe(OnOpenUnlocks);
                });
            MakeBtn(sheet, "CloseSocial", "Yopish", ref x, -60f, 150f, gap, btnH,
                new Color(0.22f, 0.24f, 0.28f), () => ShowSocialSheet(false));

            _socialSheet.SetActive(false);
        }

        void BuildUnlockSheet(Transform root)
        {
            var dim = Panel(root, "UnlockDim",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.55f));
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            _unlockSheet = dim.gameObject;
            var dimBtn = _unlockSheet.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => ShowUnlockSheet(false));

            var sheet = Panel(dim, "UnlockSheet",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(720f, 480f),
                new Color(0.06f, 0.08f, 0.12f, 0.98f));

            Label(sheet, "UnlockTitle", "Qal’a ochilishlari", 24, TextAnchor.UpperLeft,
                new Vector2(28f, -20f), new Vector2(500f, 32f), new Color(1f, 0.9f, 0.55f));
            _unlockBodyLabel = Label(sheet, "UnlockBody",
                "L1 Kon · L2 Kazarma/Askar · L3 Lager2 · L4 Archer/Lager3",
                16, TextAnchor.UpperLeft,
                new Vector2(28f, -70f), new Vector2(660f, 300f), new Color(0.88f, 0.90f, 0.94f));

            var closeX = -100f;
            MakeBtn(sheet, "CloseUnlock", "Yopish", ref closeX, -160f, 280f, 0f, 52f,
                new Color(0.22f, 0.24f, 0.28f), () => ShowUnlockSheet(false));
            _unlockSheet.SetActive(false);
        }

        void BuildAchievementsSheet(Transform root)
        {
            var dim = Panel(root, "AchievementsDim",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.55f));
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            _achievementsSheet = dim.gameObject;
            var dimBtn = _achievementsSheet.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => ShowAchievementsSheet(false));

            var sheet = Panel(dim, "AchievementsSheet",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 230f), new Vector2(720f, 420f),
                new Color(0.06f, 0.08f, 0.12f, 0.98f));

            Label(sheet, "AchTitle", "Yutuqlar", 24, TextAnchor.UpperLeft,
                new Vector2(28f, -20f), new Vector2(400f, 32f), new Color(1f, 0.9f, 0.55f));
            Label(sheet, "AchHint", "Soft-test: Askar 5 · Kon · Birinchi reyd",
                15, TextAnchor.UpperLeft, new Vector2(28f, -56f), new Vector2(660f, 28f),
                new Color(0.78f, 0.82f, 0.88f));

            var ids = new[] { "train_troops", "place_mine", "first_raid" };
            var titles = new[] { "Askar 5", "Kon qo‘yish", "Birinchi reyd" };
            _achievementProgressLabels = new Text[3];
            _achievementClaimButtons = new Button[3];
            var claimYs = new[] { 70f, 0f, -70f };

            for (var i = 0; i < 3; i++)
            {
                var yTop = -100f - i * 72f;
                Label(sheet, "AchName" + i, titles[i], 18, TextAnchor.UpperLeft,
                    new Vector2(28f, yTop), new Vector2(280f, 28f), new Color(0.95f, 0.93f, 0.85f));
                _achievementProgressLabels[i] = Label(sheet, "AchProg" + i, "0/1", 16, TextAnchor.UpperLeft,
                    new Vector2(300f, yTop), new Vector2(220f, 28f), new Color(0.75f, 0.85f, 0.95f));

                var claimX = 200f;
                var claimBtn = MakeBtn(sheet, "AchClaim" + i, "Kut", ref claimX, claimYs[i], 140f, 0f, 52f,
                    new Color(0.42f, 0.32f, 0.12f), () => { });
                var achievementId = ids[i];
                claimBtn.onClick.RemoveAllListeners();
                claimBtn.onClick.AddListener(() =>
                {
                    if (_busy) return;
                    OnClaimAchievement?.Invoke(achievementId);
                });
                _achievementClaimButtons[i] = claimBtn;
            }

            var closeX = -100f;
            MakeBtn(sheet, "CloseAch", "Yopish", ref closeX, -160f, 280f, 0f, 52f,
                new Color(0.22f, 0.24f, 0.28f), () => ShowAchievementsSheet(false));

            _achievementsSheet.SetActive(false);
        }

        void BuildClanSheet(Transform root)
        {
            var dim = Panel(root, "ClanDim",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.55f));
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            _clanSheet = dim.gameObject;
            var dimBtn = _clanSheet.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => ShowClanSheet(false));

            var sheet = Panel(dim, "ClanSheet",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(720f, 460f),
                new Color(0.06f, 0.08f, 0.12f, 0.98f));

            Label(sheet, "ClanTitle", "Klan", 24, TextAnchor.UpperLeft,
                new Vector2(28f, -20f), new Vector2(400f, 32f), new Color(1f, 0.9f, 0.55f));
            _clanJoinField = Field(sheet, "Klan ID", "", new Vector2(28f, -70f), false);
            var joinRt = _clanJoinField.GetComponent<RectTransform>();
            joinRt.sizeDelta = new Vector2(660f, 48f);

            _clanRosterLabel = Label(sheet, "ClanRoster", "Klan ma’lumoti…", 16, TextAnchor.UpperLeft,
                new Vector2(28f, -130f), new Vector2(660f, 160f), new Color(0.85f, 0.90f, 0.95f));

            const float bw = 150f;
            const float gap = 10f;
            const float btnH = 52f;
            var x = -320f;
            MakeBtn(sheet, "ClanCreate", "Yaratish", ref x, -40f, bw, gap, btnH,
                new Color(0.34f, 0.28f, 0.52f), () => Safe(OnClanCreate));
            MakeBtn(sheet, "ClanJoin", "Qo‘shilish", ref x, -40f, bw, gap, btnH,
                new Color(0.16f, 0.40f, 0.46f), () => Safe(OnClanJoin));
            MakeBtn(sheet, "ClanLeave", "Chiqish", ref x, -40f, bw, gap, btnH,
                new Color(0.52f, 0.16f, 0.26f), () => Safe(OnClanLeave));
            MakeBtn(sheet, "ClanRefresh", "Yangila", ref x, -40f, bw, gap, btnH,
                new Color(0.18f, 0.42f, 0.36f), () => Safe(OnClanRefresh));

            x = -100f;
            MakeBtn(sheet, "ClanClose", "Yopish", ref x, -110f, 280f, 0f, btnH,
                new Color(0.22f, 0.24f, 0.28f), () => ShowClanSheet(false));
            _clanSheet.SetActive(false);
        }

        void BuildChatSheet(Transform root)
        {
            var dim = Panel(root, "ChatDim",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.55f));
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            _chatSheet = dim.gameObject;
            var dimBtn = _chatSheet.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => ShowChatSheet(false));

            var sheet = Panel(dim, "ChatSheet",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(720f, 440f),
                new Color(0.06f, 0.08f, 0.12f, 0.98f));

            Label(sheet, "ChatTitle", "Chat", 24, TextAnchor.UpperLeft,
                new Vector2(28f, -20f), new Vector2(400f, 32f), new Color(1f, 0.9f, 0.55f));
            _chatHistoryLabel = Label(sheet, "ChatHist", "Yangila bosing…", 16, TextAnchor.UpperLeft,
                new Vector2(28f, -70f), new Vector2(660f, 240f), new Color(0.85f, 0.90f, 0.95f));

            const float bw = 180f;
            const float gap = 12f;
            const float btnH = 52f;
            var x = -300f;
            MakeBtn(sheet, "ChatSend", "Yuborish", ref x, -50f, bw, gap, btnH,
                new Color(0.16f, 0.40f, 0.46f), () => Safe(OnChatSend));
            MakeBtn(sheet, "ChatRefresh", "Yangila", ref x, -50f, bw, gap, btnH,
                new Color(0.18f, 0.42f, 0.36f), () => Safe(OnChatRefresh));
            MakeBtn(sheet, "ChatClose", "Yopish", ref x, -50f, bw, gap, btnH,
                new Color(0.22f, 0.24f, 0.28f), () => ShowChatSheet(false));
            _chatSheet.SetActive(false);
        }

        void BuildPvpSheet(Transform root)
        {
            var dim = Panel(root, "PvpDim",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.55f));
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            _pvpSheet = dim.gameObject;
            var dimBtn = _pvpSheet.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => ShowPvpSheet(false));

            var sheet = Panel(dim, "PvpSheet",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 230f), new Vector2(720f, 380f),
                new Color(0.06f, 0.08f, 0.12f, 0.98f));

            Label(sheet, "PvpTitle", "Live PvP", 24, TextAnchor.UpperLeft,
                new Vector2(28f, -20f), new Vector2(400f, 32f), new Color(1f, 0.9f, 0.55f));
            _pvpResultLabel = Label(sheet, "PvpResult", "Jang bosing — practice natija shu yerda.", 18, TextAnchor.UpperLeft,
                new Vector2(28f, -80f), new Vector2(660f, 160f), new Color(0.95f, 0.88f, 0.75f));

            const float bw = 180f;
            const float gap = 12f;
            const float btnH = 52f;
            var x = -300f;
            MakeBtn(sheet, "PvpFight", "Jang", ref x, -50f, bw, gap, btnH,
                new Color(0.52f, 0.16f, 0.26f), () => Safe(OnPvpFight));
            MakeBtn(sheet, "PvpRematch", "Qayta", ref x, -50f, bw, gap, btnH,
                new Color(0.46f, 0.28f, 0.14f), () => Safe(OnPvpRematch));
            MakeBtn(sheet, "PvpClose", "Yopish", ref x, -50f, bw, gap, btnH,
                new Color(0.22f, 0.24f, 0.28f), () => ShowPvpSheet(false));
            _pvpSheet.SetActive(false);
        }

        void BuildTournamentSheet(Transform root)
        {
            var dim = Panel(root, "CupDim",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.55f));
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            _tournamentSheet = dim.gameObject;
            var dimBtn = _tournamentSheet.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => ShowTournamentSheet(false));

            var sheet = Panel(dim, "CupSheet",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(720f, 420f),
                new Color(0.06f, 0.08f, 0.12f, 0.98f));

            Label(sheet, "CupTitle", "Turnir", 24, TextAnchor.UpperLeft,
                new Vector2(28f, -20f), new Vector2(400f, 32f), new Color(1f, 0.9f, 0.55f));
            _tournamentBracketLabel = Label(sheet, "CupBody", "Yozilish / Yangila — bracket counts.", 16, TextAnchor.UpperLeft,
                new Vector2(28f, -70f), new Vector2(660f, 220f), new Color(0.90f, 0.88f, 0.78f));

            const float bw = 180f;
            const float gap = 12f;
            const float btnH = 52f;
            var x = -300f;
            MakeBtn(sheet, "CupJoin", "Yozilish", ref x, -50f, bw, gap, btnH,
                new Color(0.52f, 0.40f, 0.12f), () => Safe(OnTournamentJoin));
            MakeBtn(sheet, "CupRefresh", "Yangila", ref x, -50f, bw, gap, btnH,
                new Color(0.18f, 0.42f, 0.36f), () => Safe(OnTournamentRefresh));
            MakeBtn(sheet, "CupClose", "Yopish", ref x, -50f, bw, gap, btnH,
                new Color(0.22f, 0.24f, 0.28f), () => ShowTournamentSheet(false));
            _tournamentSheet.SetActive(false);
        }

        void BuildResultPanel(Transform root)
        {
            var dim = Panel(root, "ResultDim",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.55f));
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            _resultRoot = dim.gameObject;
            var dimBtn = _resultRoot.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => Safe(OnResultOk));

            var sheet = Panel(dim, "ResultPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 380f),
                new Color(0.06f, 0.07f, 0.11f, 0.98f));

            Label(sheet, "ResultTitle", "Reyd natijasi", 24, TextAnchor.UpperLeft,
                new Vector2(28f, -20f), new Vector2(400f, 32f), new Color(1f, 0.9f, 0.55f));

            var starChip = Panel(sheet, "StarChip",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -70f), new Vector2(200f, 48f),
                new Color(0.42f, 0.32f, 0.08f, 0.95f));
            starChip.pivot = new Vector2(0f, 1f);
            _resultStars = Label(starChip, "StarTxt", "★  0", 22, TextAnchor.MiddleCenter,
                new Vector2(8f, -8f), new Vector2(184f, 32f), new Color(1f, 0.92f, 0.4f));
            CenterLabel(_resultStars);

            var lootChip = Panel(sheet, "LootChip",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(250f, -70f), new Vector2(220f, 48f),
                new Color(0.38f, 0.26f, 0.06f, 0.95f));
            lootChip.pivot = new Vector2(0f, 1f);
            _resultLoot = Label(lootChip, "LootTxt", "Loot 0", 20, TextAnchor.MiddleCenter,
                new Vector2(8f, -8f), new Vector2(204f, 32f), new Color(1f, 0.88f, 0.28f));
            CenterLabel(_resultLoot);

            _resultBody = Label(sheet, "Body", "", 20, TextAnchor.UpperLeft,
                new Vector2(28f, -140f), new Vector2(660f, 140f), new Color(1f, 0.95f, 0.7f));
            var okX = -100f;
            MakeBtn(sheet, "Ok", "OK — bazaga", ref okX, -150f, 280f, 0f, 52f,
                new Color(0.28f, 0.48f, 0.28f), () => Safe(OnResultOk));
        }

        void BuildAuthPanel(Transform canvas)
        {
            _authRoot = Panel(canvas, "AuthPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(740f, 400f),
                new Color(0.05f, 0.06f, 0.09f, 0.94f)).gameObject;

            Label(_authRoot.transform, "AuthTitle", "Kingdoms of Glory", 26, TextAnchor.UpperLeft,
                new Vector2(28f, -18f), new Vector2(680f, 34f), new Color(1f, 0.9f, 0.55f));

            _userField = Field(_authRoot.transform, "Username", "player1", new Vector2(28f, -70f), false);
            _displayField = Field(_authRoot.transform, "Display", "Player One", new Vector2(28f, -120f), false);
            _emailField = Field(_authRoot.transform, "Email", "player1@test.com", new Vector2(28f, -170f), false);
            _passwordField = Field(_authRoot.transform, "Password", "TestPass123!@#", new Vector2(28f, -220f), true);

            var x = -330f;
            MakeBtn(_authRoot.transform, "Guest", "▶  Mehmon", ref x, -155f, 200f, 14f, 52f,
                new Color(0.18f, 0.52f, 0.32f), () => Safe(OnGuest));
            MakeBtn(_authRoot.transform, "Login", "Kirish", ref x, -155f, 140f, 14f, 52f,
                new Color(0.22f, 0.35f, 0.55f), () => Safe(OnLogin));
            MakeBtn(_authRoot.transform, "Register", "Ro‘yxat", ref x, -155f, 150f, 14f, 52f,
                new Color(0.35f, 0.28f, 0.48f), () => Safe(OnRegister));

            Label(_authRoot.transform, "AuthHint",
                "Mehmon = tez start. Maydonni sudrab, pinch bilan zoom.",
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
            KoG.MiniMvp.Audio.MiniAudio.PlayTap();
            action();
        }

        static string MuteLabel() =>
            KoG.MiniMvp.Audio.MiniAudio.Muted ? "🔇" : "🔊";

        void RefreshMuteLabel()
        {
            if (_muteLabel != null) _muteLabel.text = MuteLabel();
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

        Button MakeBtn(Transform parent, string name, string label, ref float x, float y, float width, float gap,
            float height, Color color, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + width * 0.5f, y);
            rt.sizeDelta = new Vector2(width, height);
            go.GetComponent<Image>().color = color;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.22f);
            colors.selectedColor = Color.Lerp(color, Color.white, 0.10f);
            colors.disabledColor = new Color(0.2f, 0.2f, 0.22f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
            _actionButtons.Add(btn);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(4f, 2f);
            lrt.offsetMax = new Vector2(-4f, -2f);
            var t = labelGo.GetComponent<Text>();
            t.font = BuiltinFont();
            t.text = label;
            t.fontSize = label.Length > 10 ? 15 : 17;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;

            x += width + gap;
            return btn;
        }
    }
}

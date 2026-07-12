using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using KoG.MiniMvp.Audio;
using KoG.MiniMvp.Buildings;
using KoG.MiniMvp.Camera;
using KoG.MiniMvp.Lighting;
using KoG.MiniMvp.Network;
using KoG.MiniMvp.Save;
using KoG.MiniMvp.Troops;
using KoG.MiniMvp.UI;
using KoG.MiniMvp.World;
using UnityEngine;

namespace KoG.MiniMvp.App
{
    /// <summary>
    /// Self-bootstrapping Mini-MVP client. Auth/Game/Result use runtime uGUI (MiniMvpHud).
    /// </summary>
    public sealed class MiniMvpApp : MonoBehaviour
    {
        enum UiScreen
        {
            Auth,
            Game,
            Result
        }

        [Header("Backend")]
        [Tooltip("Editor / DEVELOPMENT_BUILD only. Cleartext allowed via insecureHttpOption=DevelopmentOnly.")]
        [SerializeField] string baseUrl = "http://127.0.0.1:3000";
        [Tooltip("Non-dev player builds. Must be HTTPS.")]
        [SerializeField] string releaseBaseUrl = "https://api.kingdomsofglory.com";

        [Header("Grid")]
        // Measured from BaseField_L1 mesh (22×22) → 20 cells × 1.1 = 22 world units.
        [SerializeField] int gridSize = 20;
        [SerializeField] float cellSize = 1.1f;

        ApiClient _api;
        UiScreen _screen = UiScreen.Auth;
        string _status = "Ready — open Game tab, then press buttons below";
        string _resultMessage = "";

        string _username = "player1";
        string _displayName = "Player One";
        string _email = "player1@test.com";
        string _password = "TestPass123!@#";

        readonly Dictionary<string, GameObject> _buildingViews = new Dictionary<string, GameObject>();
        long _gold;
        long _mana;
        long _diamond;
        int _barbarianCount;
        int _housingUsed;
        int _housingMax = 100;
        AchievementDto[] _achievements;
        int _castleLevel = 1;
        int _campaignMax = 1;
        int _raidFortressId = 1;
        int _raidDeployCount;
        CampaignDto[] _campaigns;
        string[] _placeableUnlocks = { "gold_mine" };
        string[] _trainableTroops = System.Array.Empty<string>();
        string[] _unlockLabels = System.Array.Empty<string>();
        string _trainTroopType = "barbarian";
        int _archerCount;
        float _raidStartedAt = -999f;
        bool _raidActive;
        Coroutine _raidCountdownCo;
        Coroutine _trainWaitCo;
        string _pendingDestroyId;
        int _stateLoadGen;
        string _selectedBuildingId;
        bool _busy;
        bool _loginStreakClaimedThisSession;
        Transform _fieldRoot;
        Transform _villageGameplay;
        CoCCameraController _cocCamera;
        MiniMvpHud _hud;
        SocialPillarsController _social;
        BuildingSystem _buildings;
        BuildingGrid _buildingGrid;
        PlacementInputDriver _placementInput;
        TroopSystem _troops;
        TroopInputDriver _troopInput;
        SaveSystem _save;

        /// <summary>World center of the playable checkerboard (castle sits here).</summary>
        Vector3 FieldCenter => new Vector3((gridSize - 1) * cellSize * 0.5f, 0f, (gridSize - 1) * cellSize * 0.5f);

        float FieldWorldSize => gridSize * cellSize;

        Vector3 GridToWorld(int gridX, int gridZ) => new Vector3(gridX * cellSize, 0f, gridZ * cellSize);

        /// <summary>
        /// Castle visual is wider than 1 cell — push mine/barracks off the keep-out ring so they do not clip.
        /// </summary>
        const int CastleKeepOutChebyshev = 3;

        Vector3 ResolveBuildingWorldPos(string type, int gridX, int gridZ)
        {
            var world = GridToWorld(gridX, gridZ);
            if (type == "castle") return world;

            var cx = gridSize / 2;
            var cz = gridSize / 2;
            var dx = (float)(gridX - cx);
            var dz = (float)(gridZ - cz);
            var cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
            if (cheb >= CastleKeepOutChebyshev) return world;

            // AI-assisted: nudge overlapping legacy placements outward along castle→building direction.
            var len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len < 0.01f)
            {
                dx = 1f;
                dz = 0f;
                len = 1f;
            }

            var scale = CastleKeepOutChebyshev / len;
            return new Vector3(
                (cx + dx * scale) * cellSize,
                0f,
                (cz + dz * scale) * cellSize);
        }

        string ResolveBaseUrl()
        {
            // Soft-test phone installs: set PlayerPrefs "kog_api_base" to staging/LAN HTTPS (or http in DEV).
            var prefsUrl = PlayerPrefs.GetString("kog_api_base", string.Empty).Trim();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _ = releaseBaseUrl;
            if (!string.IsNullOrEmpty(prefsUrl))
                return prefsUrl.TrimEnd('/');
            var resolved = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:3000" : baseUrl.Trim().TrimEnd('/');
#if UNITY_ANDROID && !UNITY_EDITOR
            // Emulator loopback → host machine. Physical device: set kog_api_base to LAN IP.
            if (resolved.Contains("127.0.0.1") || resolved.Contains("localhost"))
                resolved = resolved.Replace("127.0.0.1", "10.0.2.2").Replace("localhost", "10.0.2.2");
#endif
            return resolved;
#else
            if (!string.IsNullOrEmpty(prefsUrl))
            {
                if (prefsUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("[MiniMvp] kog_api_base cleartext refused in release — use HTTPS staging");
                }
                else
                {
                    return prefsUrl.TrimEnd('/');
                }
            }
            var url = string.IsNullOrWhiteSpace(releaseBaseUrl) ? baseUrl : releaseBaseUrl;
            url = (url ?? string.Empty).Trim();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[MiniMvp] Release build refuses cleartext HTTP — set releaseBaseUrl or kog_api_base to HTTPS");
                url = "https://api.kingdomsofglory.com";
            }
            return url.TrimEnd('/');
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (FindFirstObjectByType<MiniMvpApp>() != null) return;
            var go = new GameObject("MiniMvpApp");
            go.AddComponent<MiniMvpApp>();
        }

        void Awake()
        {
            // Soft-GO phone: always landscape (user: yonbosh). Portrait lock caused sideways crop on rotate.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // Keep Game view clear — Dev Console overlay hid thumb CTAs on Android.
            Debug.developerConsoleVisible = false;
            Debug.developerConsoleEnabled = false;
#endif

            _api = new ApiClient(ResolveBaseUrl());
            var funnel = GetComponent<FunnelAnalytics>();
            if (funnel == null) funnel = gameObject.AddComponent<FunnelAnalytics>();
            funnel.Bind(_api);
            // Scene-ga qo'lda tashlangan AI prefab Play'da yotib qoladi / dublikat.
            // Faqat Resources orqali spawn qilingan castle_1 ishlatiladi.
            DestroyLooseSceneCastles();
            BuildGround();
            EnsureBuildingSystem();
            EnsureTroopSystem();
            EnsureSaveSystem();
            EnsureSocial();
            EnsureHud();
            if (SessionStore.HasSession)
            {
                SetScreen(UiScreen.Game);
                _save?.SyncFromCloud(SessionStore.PlayerId);
                StartCoroutine(LoadPlayerState());
            }
            else
            {
                SetScreen(UiScreen.Auth);
                StartCoroutine(ProbeBackend());
            }
        }

        /// <summary>Auth ekranda backend o‘likligini darhol ko‘rsatadi (unknown error o‘rniga).</summary>
        IEnumerator ProbeBackend()
        {
            yield return _api.GetJson("/health", null, (code, text) =>
            {
                if (_screen != UiScreen.Auth) return;
                if (code >= 200 && code < 300)
                {
                    SetStatus("Backend OK (" + _api.BaseUrl + "). Guest Play bosing.");
                    return;
                }

                SetStatus("Backend yo‘q: " + ExtractError(text) +
                          " — terminalda `npm run dev` (http://127.0.0.1:3000)");
            });
        }

        void EnsureHud()
        {
            if (_hud != null) return;
            _hud = MiniMvpHud.Ensure(transform);
            _hud.OnPlaceMine = () => BeginPlace("gold_mine");
            _hud.OnPlaceBarracks = () => BeginPlace("barracks");
            _hud.OnConfirmPlace = () =>
            {
                if (_buildings == null) return;
                StartCoroutine(ConfirmPlaceAndReload());
            };
            _hud.OnCancelPlace = () =>
            {
                _buildings?.CancelPlacement();
                _hud.SetPlacementMode(false);
                if (_troopInput != null) _troopInput.SetEnabled(true);
            };
            _hud.OnRotatePlace = () => _buildings?.RotatePlacement();
            _hud.OnCollect = () => StartCoroutine(CollectGold());
            _hud.OnTrain = () => StartCoroutine(TrainTroops(10));
            _hud.OnToggleTroopType = ToggleTrainTroopType;
            _hud.OnUpgrade = () => StartCoroutine(UpgradeSelected());
            _hud.OnSpeedup = () => StartCoroutine(SpeedupSelected());
            _hud.OnDestroyBuilding = () =>
            {
                if (string.IsNullOrEmpty(_selectedBuildingId) ||
                    _selectedBuildingId == "local_castle_fallback")
                {
                    SetStatus("Bino tanlang");
                    return;
                }

                if (_pendingDestroyId != _selectedBuildingId)
                {
                    _pendingDestroyId = _selectedBuildingId;
                    SetStatus("Destroy? Yana bir marta bosing (tasdiq)");
                    return;
                }

                _pendingDestroyId = null;
                StartCoroutine(DestroySelected());
            };
            _hud.OnRepairBuilding = () => StartCoroutine(RepairSelected());
            _hud.OnCancelUpgrade = () => StartCoroutine(CancelUpgradeSelected());
            _hud.OnStartRaid = () => StartCoroutine(StartRaid());
            _hud.OnCompleteRaid = () => StartCoroutine(CompleteRaid());
            _hud.OnLogout = () =>
            {
                EnsureSaveSystem();
                SyncDeviceSettingsToSave();
                _save?.ForceSave();
                SessionStore.Clear();
                _buildings?.CancelPlacement();
                _troops?.ClearAll();
                ClearBuildings();
                BuildingSelectFx.Clear();
                PlacePreviewFx.Hide();
                if (_hud != null) _hud.SetPlacementMode(false);
                if (_troopInput != null) _troopInput.SetEnabled(false);
                SetScreen(UiScreen.Auth);
                SetStatus("Logged out");
            };
            _hud.OnResultOk = () =>
            {
                SetScreen(UiScreen.Game);
                StartCoroutine(LoadPlayerState());
            };
            _hud.OnRegister = () =>
            {
                PullAuthFields();
                StartCoroutine(Register());
            };
            _hud.OnLogin = () =>
            {
                PullAuthFields();
                StartCoroutine(Login());
            };
            _hud.OnGuest = () => StartCoroutine(GuestLogin());
            _hud.OnClan = null;
            _hud.OnChat = null;
            _hud.OnPvp = null;
            _hud.OnTournament = null;
            _hud.OnOpenClan = () =>
            {
                _hud.ShowClanSheet(true);
                StartCoroutine(_social.RefreshMyClan(summary =>
                {
                    if (_hud != null) _hud.SetClanRoster(summary);
                }));
            };
            _hud.OnClanCreate = () => StartCoroutine(ClanCreateAndRefresh());
            _hud.OnClanJoin = () => StartCoroutine(ClanJoinAndRefresh());
            _hud.OnClanLeave = () => StartCoroutine(ClanLeaveAndRefresh());
            _hud.OnClanRefresh = () => StartCoroutine(_social.RefreshMyClan(summary =>
            {
                if (_hud != null) _hud.SetClanRoster(summary);
            }));
            _hud.OnOpenChat = () =>
            {
                _hud.ShowChatSheet(true);
                StartCoroutine(_social.RefreshChatHistory(summary =>
                {
                    if (_hud != null) _hud.SetChatHistory(summary);
                }));
            };
            _hud.OnChatSend = () => StartCoroutine(ChatSendAndRefresh());
            _hud.OnChatClanSend = () => StartCoroutine(ChatClanSendAndRefresh());
            _hud.OnChatRefresh = () => StartCoroutine(_social.RefreshChatHistory(summary =>
            {
                if (_hud != null) _hud.SetChatHistory(summary);
            }));
            _hud.OnOpenPvp = () =>
            {
                _hud.ShowPvpSheet(true);
                _hud.SetPvpResult("Jang yoki Qayta bosing.");
            };
            _hud.OnPvpFight = () => StartCoroutine(_social.FindPvpWithResult(summary =>
            {
                if (_hud != null) _hud.SetPvpResult(summary);
            }));
            _hud.OnPvpRematch = () => StartCoroutine(_social.FindPvpWithResult(summary =>
            {
                if (_hud != null) _hud.SetPvpResult(summary);
            }));
            _hud.OnOpenTournament = () =>
            {
                _hud.ShowTournamentSheet(true);
                StartCoroutine(_social.RefreshTournamentBracket(summary =>
                {
                    if (_hud != null) _hud.SetTournamentBracket(summary);
                }));
            };
            _hud.OnTournamentJoin = () => StartCoroutine(_social.RefreshTournamentBracket(summary =>
            {
                if (_hud != null) _hud.SetTournamentBracket(summary);
            }));
            _hud.OnTournamentRefresh = () => StartCoroutine(_social.RefreshTournamentBracket(summary =>
            {
                if (_hud != null) _hud.SetTournamentBracket(summary);
            }));
            _hud.OnRewardedAd = () => StartCoroutine(_social.ClaimRewardedAd((g, m, d) =>
            {
                if (g > 0) _gold = g;
                if (m > 0) _mana = m;
                if (d > 0) _diamond = d;
                MiniAudio.PlayCollect();
                RefreshHud();
            }));
            _hud.OnManualSave = () =>
            {
                EnsureSaveSystem();
                SyncDeviceSettingsToSave();
                var result = _save.ManualSave();
                SetStatus(result.Success
                    ? "Qurilma saqlandi (r" + _save.Revision + ") · iqtisod serverda"
                    : "Saqlash xato: " + result.Message);
            };
            _hud.OnOpenAchievements = () =>
            {
                if (_hud != null)
                {
                    _hud.SetAchievements(_achievements);
                    _hud.ShowAchievementsSheet(true);
                }
            };
            _hud.OnOpenUnlocks = () =>
            {
                if (_hud == null) return;
                _hud.SetUnlockBody(BuildUnlockSheetText());
                _hud.ShowUnlockSheet(true);
            };
            _hud.OnClaimAchievement = id => StartCoroutine(ClaimAchievement(id));
            _hud.OnRetryLoad = () =>
            {
                if (!SessionStore.HasSession)
                {
                    SetStatus("Avval Guest/Login qiling");
                    return;
                }
                StartCoroutine(LoadPlayerState());
            };
            _hud.SetAuthFields(_username, _displayName, _email, _password);
            RefreshHud();
        }

        void EnsureBuildingSystem()
        {
            if (_buildingGrid == null)
            {
                // FieldVisualBuilder may already have built one under Village/Gameplay — reuse it.
                _buildingGrid = UnityEngine.Object.FindFirstObjectByType<BuildingGrid>();
                if (_buildingGrid == null)
                {
                    var parent = _fieldRoot != null ? _fieldRoot : transform;
                    _buildingGrid = BuildingGrid.Build(parent, gridSize, cellSize, FieldCenter);
                }
            }

            if (_buildings == null)
            {
                _buildings = GetComponent<BuildingSystem>();
                if (_buildings == null) _buildings = gameObject.AddComponent<BuildingSystem>();
                _buildings.Configure(_api, () => SessionStore.Token, () => SessionStore.PlayerId, _buildingGrid);
                _buildings.StatusChanged += msg => SetStatus(msg);
                _buildings.StateChanged += OnBuildingStateChanged;
                _buildings.PreviewCellChanged += valid =>
                {
                    MiniAudio.PlaySnap();
                    if (_hud != null && _buildings.IsPlacing)
                        _hud.SetPlacementValid(valid);
                };
                _buildings.MutationSucceeded += () =>
                {
                    if (_hud != null) _hud.SetPlacementMode(false);
                    if (_troopInput != null) _troopInput.SetEnabled(true);
                    StartCoroutine(LoadPlayerState());
                };
            }

            if (_placementInput == null)
            {
                _placementInput = GetComponent<PlacementInputDriver>();
                if (_placementInput == null) _placementInput = gameObject.AddComponent<PlacementInputDriver>();
            }

            var cam = UnityEngine.Camera.main;
            _placementInput.Bind(_buildings, cam, () => _selectedBuildingId);
            _placementInput.OnEmptyTap = ClearBuildingSelection;

            WireCameraPanBlock();
        }

        void ClearBuildingSelection()
        {
            if (string.IsNullOrEmpty(_selectedBuildingId)) return;
            if (_buildings != null && (_buildings.IsPlacing || _buildings.IsRelocating)) return;
            _selectedBuildingId = null;
            _pendingDestroyId = null;
            BuildingSelectFx.Clear();
            RefreshHud();
            SetStatus("Tanlov bekor");
        }

        void WireCameraPanBlock()
        {
            if (_cocCamera == null)
            {
                var cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    _cocCamera = cam.GetComponent<CoCCameraController>();
                    if (_cocCamera == null) _cocCamera = cam.gameObject.AddComponent<CoCCameraController>();
                }
            }

            if (_cocCamera != null)
            {
                _cocCamera.BlocksPan = () =>
                    (_placementInput != null && _placementInput.BlocksCameraPan) ||
                    (_buildings != null && _buildings.BlocksCameraPan);
            }
        }

        /// <summary>While dragging a building, keep the mesh under the finger on the grid.</summary>
        void SyncRelocateVisual()
        {
            if (_buildings == null || !_buildings.IsRelocating) return;
            var id = _buildings.RelocatingBuildingId;
            if (string.IsNullOrEmpty(id)) return;
            if (!_buildings.TryGet(id, out var inst)) return;
            if (!_buildingViews.TryGetValue(id, out var go) || go == null) return;

            var world = ResolveBuildingWorldPos(inst.Type, inst.Anchor.X, inst.Anchor.Z);
            // Slight lift while dragging so the building reads as “picked up”.
            world.y += 0.18f;
            go.transform.position = world;
            var marker = go.GetComponent<BuildingMarker>();
            if (marker != null)
            {
                marker.gridX = inst.Anchor.X;
                marker.gridZ = inst.Anchor.Z;
            }
        }

        void OnBuildingStateChanged()
        {
            SyncRelocateVisual();
            if (_hud == null || _buildings == null) return;
            if (_buildings.IsPlacing)
                _hud.SetPlacementValid(_buildings.Session.IsValid);
        }

        void BeginPlace(string buildingType)
        {
            EnsureBuildingSystem();
            if (_buildings == null) return;

            if (!IsPlaceUnlocked(buildingType))
            {
                var need = buildingType == "barracks" ? 2 : 1;
                SetStatus(buildingType == "barracks"
                    ? "Kazarma uchun Qal’a L2 kerak — avval Yangila"
                    : "Kon uchun Qal’a L" + need + " kerak");
                MiniAudio.PlayError();
                return;
            }

            if (_buildings.BeginPlacement(buildingType))
            {
                var canRotate = BuildingDefinitionCatalog.GetOrDefault(buildingType).CanRotate;
                if (_hud != null)
                {
                    _hud.SetPlacementMode(true, canRotate);
                    _hud.SetPlacementValid(true);
                }
                if (_troopInput != null) _troopInput.SetEnabled(false);
            }
        }

        bool IsPlaceUnlocked(string buildingType)
        {
            if (_placeableUnlocks != null)
            {
                for (var i = 0; i < _placeableUnlocks.Length; i++)
                {
                    if (_placeableUnlocks[i] == buildingType) return true;
                }
            }

            if (buildingType == "gold_mine") return _castleLevel >= 1;
            if (buildingType == "barracks") return _castleLevel >= 2;
            return false;
        }

        IEnumerator ConfirmPlaceAndReload()
        {
            if (_buildings == null) yield break;
            SetBusy(true);
            yield return _buildings.ConfirmPlacement();
            SetBusy(false);
            if (_hud != null) _hud.SetPlacementMode(_buildings.IsPlacing);
            if (_troopInput != null) _troopInput.SetEnabled(!_buildings.IsPlacing);
            if (!_buildings.IsPlacing)
            {
                FunnelAnalytics.Instance?.Track("place_building");
                yield return LoadPlayerState();
                // Bounce the most recently selected / newest building.
                if (!string.IsNullOrEmpty(_selectedBuildingId) &&
                    _buildingViews.TryGetValue(_selectedBuildingId, out var go) && go != null)
                {
                    WorldFeedback.PlaceDrop(go.transform);
                    WorldFeedback.PlaceBurst(go.transform.position);
                }
            }
        }

        void EnsureTroopSystem()
        {
            if (_troops == null)
            {
                _troops = GetComponent<TroopSystem>();
                if (_troops == null) _troops = gameObject.AddComponent<TroopSystem>();
                var world = ResolveGameplayRoot();
                _troops.Configure(world != null ? world : transform, null, gridSize, cellSize);
                _troops.StatusChanged += msg => SetStatus(msg);
            }

            if (_troopInput == null)
            {
                _troopInput = GetComponent<TroopInputDriver>();
                if (_troopInput == null) _troopInput = gameObject.AddComponent<TroopInputDriver>();
                _troopInput.Bind(_troops, UnityEngine.Camera.main);
            }
        }

        void EnsureSaveSystem()
        {
            if (_save == null)
            {
                _save = GetComponent<SaveSystem>();
                if (_save == null) _save = gameObject.AddComponent<SaveSystem>();
                _save.Configure(encrypt: true, cloudMirror: true, autoInterval: 45f);
                _save.StatusChanged += msg => Debug.Log("[Save] " + msg);
                if (_save.DeviceSettings != null)
                    MiniAudio.Muted = _save.DeviceSettings.muted;
            }

            if (_api != null && _save != null)
                _save.BindHttpCloud(_api, () => SessionStore.Token);
        }

        void SyncDeviceSettingsToSave()
        {
            EnsureSaveSystem();
            if (_save == null) return;
            var settings = _save.DeviceSettings ?? new DeviceSettingsPayload();
            settings.muted = MiniAudio.Muted;
            _save.SetDeviceSettings(settings);
        }

        Vector3 ResolveTroopGatherCenter()
        {
            foreach (var view in _buildingViews.Values)
            {
                if (view == null) continue;
                var m = view.GetComponent<BuildingMarker>();
                if (m != null && m.buildingType == "barracks")
                    return view.transform.position;
            }

            return FieldCenter + new Vector3(cellSize * 2f, 0f, 0f);
        }

        void SyncTroopVisuals()
        {
            EnsureTroopSystem();
            if (_troops == null) return;
            _troops.SyncArmyCount("barbarian", _barbarianCount, ResolveTroopGatherCenter());
            RefreshAiPathObstacles();
            if (_troopInput != null)
                _troopInput.SetEnabled(_buildings == null || !_buildings.IsPlacing);
        }

        void RefreshAiPathObstacles()
        {
            if (_troops == null) return;
            var blocked = new System.Collections.Generic.List<Vector3>(16);
            foreach (var view in _buildingViews.Values)
            {
                if (view == null) continue;
                blocked.Add(view.transform.position);
            }

            _troops.RefreshPathObstacles(blocked);
        }

        void EnsureSocial()
        {
            if (_social != null) return;
            _social = GetComponent<SocialPillarsController>();
            if (_social == null) _social = gameObject.AddComponent<SocialPillarsController>();
            _social.Bind(_api, SetStatus, SetBusy, ExtractError);
        }

        void PullAuthFields()
        {
            if (_hud == null) return;
            _username = _hud.Username;
            _displayName = _hud.DisplayName;
            _email = _hud.Email;
            _password = _hud.Password;
        }

        void SetScreen(UiScreen screen)
        {
            _screen = screen;
            EnsureHud();
            if (_hud == null) return;
            _hud.ShowAuth(screen == UiScreen.Auth);
            _hud.ShowGame(screen == UiScreen.Game);
            _hud.ShowResult(screen == UiScreen.Result);
            RefreshHud();
        }

        void RefreshHud()
        {
            if (_hud == null) return;
            _hud.SetBusy(_busy);
            _hud.SetStatus(_status);
            _hud.SetResources(_gold, _mana, _diamond, _barbarianCount + _archerCount, _housingUsed, _housingMax);
            _hud.SetAchievements(_achievements);
            _hud.SetTrainTroopUi(_trainTroopType, CanTrainTroop("archer"));
            if (_screen == UiScreen.Result)
                _hud.SetResultMessage(_resultMessage);

            if (!string.IsNullOrEmpty(_selectedBuildingId) &&
                _buildingViews.TryGetValue(_selectedBuildingId, out var sel) && sel != null)
            {
                var m = sel.GetComponent<BuildingMarker>();
                var label = m != null ? PrettyType(m.buildingType) + " L" + m.level : _selectedBuildingId;
                var construct = _buildings != null ? _buildings.DescribeConstruction(_selectedBuildingId) : null;
                if (!string.IsNullOrEmpty(construct)) label += " · " + construct;
                if (m != null && _buildings != null &&
                    _buildings.TryGet(_selectedBuildingId, out var inst) && inst.IsDamaged)
                    label += " · damaged";
                _hud.SetSelected(label);
            }
            else
            {
                _hud.SetSelected(null);
            }

            if (_hud != null && _buildings != null)
                _hud.SetPlacementMode(_buildings.IsPlacing);

            SyncConstructionWorldBar();
        }

        void SyncConstructionWorldBar()
        {
            if (string.IsNullOrEmpty(_selectedBuildingId) ||
                !_buildingViews.TryGetValue(_selectedBuildingId, out var go) || go == null)
                return;

            if (_buildings == null ||
                !_buildings.TryGet(_selectedBuildingId, out var inst) ||
                !inst.IsUnderConstruction)
            {
                ConstructionWorldBar.Clear(go.transform);
                return;
            }

            var left = inst.ConstructionSecondsLeft;
            var total = Mathf.Max(left, 1);
            ConstructionWorldBar.Attach(go.transform, total, left);
        }

        static string PrettyType(string type)
        {
            if (type == "gold_mine") return "Kon";
            if (type == "barracks") return "Kazarma";
            if (type == "castle") return "Qasr";
            return type ?? "?";
        }

        static void DestroyLooseSceneCastles()
        {
            // Root-only scan — avoids FindObjectsByType over every Transform in the scene.
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                // Never destroy runtime-spawned buildings (BuildingMarker) or the live camera/light.
                if (go.GetComponent<BuildingMarker>() != null) continue;
                if (go.GetComponent<UnityEngine.Camera>() != null) continue;
                if (go.GetComponent<Light>() != null && go.name.Contains("Directional")) continue;

                var n = go.name;
                // Scene leftovers fight the runtime Village (duplicate field/border + foreground trees).
                if (n == "UzbekCastle" || n.StartsWith("UzbekCastle") ||
                    n == "Castle_L1" || n.StartsWith("Castle_L1") ||
                    n == "Castle" || n.StartsWith("Castle (") ||
                    n == "BaseField_L1" || n == "NatureBorder_L1" ||
                    n == "BaseField" || n == "NatureBorder" ||
                    n == "Global Volume" || n == "Village" || n == "ReferenceVillage" ||
                    n.StartsWith("SM_Env_") || n.StartsWith("SM_Prop_") ||
                    n.StartsWith("Tree") || n.StartsWith("Rock"))
                {
                    Debug.Log("[MiniMvp] Removing loose scene object: " + n);
                    Destroy(go);
                }
            }
        }

        void BuildGround()
        {
            _fieldRoot = FieldVisualBuilder.Build(gridSize, cellSize);

            var cam = EnsureMainCamera();
            _cocCamera = cam.GetComponent<CoCCameraController>();
            if (_cocCamera == null) _cocCamera = cam.gameObject.AddComponent<CoCCameraController>();
            if (cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();

            // CoCCameraController owns pose; aspect-fit framing kills empty sky letterbox.
            var fit = CoCCameraController.FitOrthoForBase(FieldWorldSize, 1.08f);
            _cocCamera.Configure(FieldCenter, FieldWorldSize, 4f);
            _cocCamera.FocusBase(FieldCenter, fit);
            WireCameraPanBlock();

            BaseLightingSetup.Apply(FieldCenter);
            FieldVisualBuilder.FinalizeHierarchy();

            // Re-assert pose after hierarchy/lighting (no parent fight, focus never stuck at origin).
            _cocCamera.FocusBase(FieldCenter, fit);

            var village = GameObject.Find("Village");
            MobileVillageOptimize.Apply(village != null ? village.transform : null, cam);
        }

        static UnityEngine.Camera EnsureMainCamera()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<UnityEngine.Camera>();
                cam.tag = "MainCamera";
            }

            // Keep Main Camera as a scene root so world pose math stays unambiguous.
            if (cam.transform.parent != null)
                cam.transform.SetParent(null, true);

            // Strip leftover URP camera extras if any (project is Built-in).
            var behaviours = cam.GetComponents<Behaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;
                var tn = b.GetType().Name;
                if (tn == "UniversalAdditionalCameraData" || tn == "Volume")
                    Destroy(b);
            }

            // Sky color owned by BaseLightingSetup.Apply — do not override here.
            cam.allowHDR = false;

            return cam;
        }


        IEnumerator GuestLogin()
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            _username = "guest_" + id;
            _displayName = "Guest";
            // Strict email validators reject .local — use RFC example.com for guest sims.
            _email = _username + "@guest.example.com";
            _password = "GuestPass123!@#";
            if (_hud != null) _hud.SetAuthFields(_username, _displayName, _email, _password);
            SetStatus("Guest akkaunt yaratilmoqda...");
            yield return Register();
        }

        IEnumerator Register()
        {
            SetBusy(true);
            SetStatus("Registering...");
            var body = JsonObject(
                ("username", _username.Trim()),
                ("displayName", string.IsNullOrWhiteSpace(_displayName) ? _username.Trim() : _displayName.Trim()),
                ("email", _email.Trim()),
                ("password", _password)
            );

            yield return _api.PostJson("/api/v1/auth/register", body, null, null, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    MiniAudio.PlayError();
                    SetStatus("Ro‘yxat xato: " + ExtractError(text));
                    return;
                }

                var res = JsonUtility.FromJson<AuthResponse>(text);
                SessionStore.Save(res.playerId, res.token, res.refreshToken);
                EnsureSaveSystem();
                _save?.SyncFromCloud(res.playerId);
                SetScreen(UiScreen.Game);
                SetStatus("Ro‘yxat OK. Faqat qal’a — Keyingi: Kon → Tasdiq. ◆ Mana = askar.");
                _loginStreakClaimedThisSession = false;
                FunnelAnalytics.Instance?.Track("player_registered");
                FunnelAnalytics.Instance?.Track("session_start");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator Login()
        {
            SetBusy(true);
            SetStatus("Kirilmoqda...");
            var body = JsonObject(
                ("username", _username.Trim()),
                ("password", _password)
            );

            yield return _api.PostJson("/api/v1/auth/login", body, null, null, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    MiniAudio.PlayError();
                    SetStatus("Kirish xato: " + ExtractError(text));
                    return;
                }

                var res = JsonUtility.FromJson<AuthResponse>(text);
                SessionStore.Save(res.playerId, res.token, res.refreshToken);
                ApplyPlayerBalances(res.player);
                EnsureSaveSystem();
                _save?.SyncFromCloud(res.playerId);
                SetScreen(UiScreen.Game);
                SetStatus("Kirish OK.");
                _loginStreakClaimedThisSession = false;
                FunnelAnalytics.Instance?.Track("session_start");
                MaybeTrackD1Return();
                StartCoroutine(LoadPlayerState());
            });
        }

        void MaybeTrackD1Return()
        {
            var key = "kog_first_session_day";
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (!PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.SetString(key, today);
                PlayerPrefs.Save();
                return;
            }
            var first = PlayerPrefs.GetString(key, today);
            if (first != today)
                FunnelAnalytics.Instance?.Track("d1_return", ("firstDay", first));
        }

        IEnumerator LoadPlayerState()
        {
            var gen = ++_stateLoadGen;
            SetBusy(true);
            if (_hud != null) _hud.ClearError();
            SetStatus("Baza yuklanmoqda…");
            yield return _api.GetJson("/api/v1/player/state", SessionStore.Token, (code, text) =>
            {
                if (gen != _stateLoadGen) return;
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    MiniAudio.PlayError();
                    var msg = "Baza yuklanmadi: " + ExtractError(text);
                    SetStatus(msg);
                    if (_hud != null) _hud.ShowError(msg + " — Qayta urin ni bosing.", true);
                    return;
                }

                if (_hud != null) _hud.ClearError();
                var state = JsonUtility.FromJson<PlayerStateResponse>(text);
                if (state.player != null)
                {
                    _gold = state.player.gold;
                    _mana = state.player.mana;
                    _diamond = state.player.diamond;
                    _castleLevel = Math.Max(1, state.player.castleLevel);
                }

                if (state.unlocks != null)
                {
                    if (state.unlocks.placeable != null && state.unlocks.placeable.Length > 0)
                        _placeableUnlocks = state.unlocks.placeable;
                    _trainableTroops = state.unlocks.troops ?? System.Array.Empty<string>();
                    _unlockLabels = state.unlocks.labels ?? System.Array.Empty<string>();
                    _campaignMax = Math.Max(1, state.unlocks.campaignMax);
                    if (!CanTrainTroop(_trainTroopType))
                        _trainTroopType = "barbarian";
                }
                else
                {
                    _campaignMax = _castleLevel >= 4 ? 3 : (_castleLevel >= 3 ? 2 : 1);
                    _trainableTroops = _castleLevel >= 4
                        ? new[] { "barbarian", "archer" }
                        : (_castleLevel >= 2 ? new[] { "barbarian" } : System.Array.Empty<string>());
                }

                _campaigns = state.campaigns;

                _barbarianCount = 0;
                _archerCount = 0;
                if (state.troops != null)
                {
                    foreach (var troop in state.troops)
                    {
                        if (troop.type == "barbarian") _barbarianCount = troop.quantity;
                        else if (troop.type == "archer") _archerCount = troop.quantity;
                    }
                }

                if (state.housing != null && state.housing.max > 0)
                {
                    _housingUsed = state.housing.used;
                    _housingMax = state.housing.max;
                }
                else
                {
                    _housingUsed = _barbarianCount + _archerCount;
                    _housingMax = 50 + Math.Max(1, _castleLevel) * 50;
                }

                _achievements = state.achievements;

                var hasMine = false;
                var hasBarracks = false;
                var hasCastle = false;
                var buildingCount = 0;
                var lite = ToLite(state.buildings);
                BuildingViewSync.Sync(
                    _buildingViews,
                    lite,
                    SpawnBuilding,
                    go => { if (go != null) Destroy(go); });

                if (state.buildings != null)
                {
                    foreach (var building in state.buildings)
                    {
                        buildingCount++;
                        if (building.type == "castle")
                        {
                            hasCastle = true;
                            _selectedBuildingId = building.id;
                        }
                        if (building.type == "gold_mine") hasMine = true;
                        if (building.type == "barracks") hasBarracks = true;
                    }
                }

                EnsureBuildingSystem();
                _buildings?.SyncFromServer(state.buildings);
                if (!string.IsNullOrEmpty(_selectedBuildingId))
                    _buildings?.BindConstructionTimer(_selectedBuildingId);

                // Safety: empty base still shows a center castle so field never looks abandoned.
                if (!hasCastle)
                {
                    var cx = gridSize / 2;
                    var cz = gridSize / 2;
                    SpawnBuilding("local_castle_fallback", "castle", 1, cx, cz, 0);
                    _buildings?.RegisterLocalCastleFallback("local_castle_fallback", cx, cz);
                    _selectedBuildingId = "local_castle_fallback";
                    buildingCount++;
                    Debug.LogWarning("[MiniMvp] Server castle missing — spawned center fallback");
                }

                if (state.goal != null && !string.IsNullOrEmpty(state.goal.title))
                {
                    if (_hud != null) _hud.SetGoal(state.goal.title, state.goal.cta);
                    SetStatus(state.goal.title + (string.IsNullOrEmpty(state.goal.cta) ? "" : " · " + state.goal.cta));
                }
                else
                {
                    var hint = BuildNextStepHint(hasMine, hasBarracks, buildingCount);
                    if (_hud != null) _hud.SetGoal(hint, "");
                    SetStatus(hint);
                }

                if (state.training != null && state.training.pending)
                {
                    SetStatus("Askar tayyorlanmoqda… " + state.training.secondsLeft + "s (" +
                              state.training.quantity + " " + state.training.troopType + ")");
                    if (_trainWaitCo != null) StopCoroutine(_trainWaitCo);
                    _trainWaitCo = StartCoroutine(WaitTrainingThenReload(state.training.secondsLeft));
                }

                FrameCameraOnBase();
                if (!string.IsNullOrEmpty(_selectedBuildingId) &&
                    _buildingViews.TryGetValue(_selectedBuildingId, out var selGo))
                    BuildingSelectFx.Select(selGo);
                else
                    BuildingSelectFx.Clear();
                SyncTroopVisuals();
                RefreshHud();
                EnsureSaveSystem();
                _save.CapturePlayerState(state);
                if (!_loginStreakClaimedThisSession)
                    StartCoroutine(ClaimLoginStreakOnce());
            });
        }

        IEnumerator ClaimLoginStreakOnce()
        {
            if (_loginStreakClaimedThisSession) yield break;
            _loginStreakClaimedThisSession = true;
            if (string.IsNullOrEmpty(SessionStore.Token) || string.IsNullOrEmpty(SessionStore.PlayerId))
                yield break;

            var body = JsonObject(("playerId", SessionStore.PlayerId));
            yield return _api.PostJson(
                "/api/v1/rewards/login-streak",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    if (code < 200 || code >= 300) return;
                    var res = JsonUtility.FromJson<LoginStreakResponse>(text);
                    if (res == null) return;
                    if (!string.IsNullOrEmpty(res.message) &&
                        res.message.IndexOf("Already", StringComparison.OrdinalIgnoreCase) >= 0)
                        return;
                    if (res.goldRewarded <= 0 && res.diamondsRewarded <= 0) return;

                    _gold += res.goldRewarded;
                    _diamond += res.diamondsRewarded;
                    RefreshHud();
                    var msg = "Kunlik streak " + res.currentStreak + ": +" + res.goldRewarded + "●";
                    if (res.diamondsRewarded > 0) msg += " +" + res.diamondsRewarded + "◇";
                    SetStatus(msg);
                    WorldFeedback.FloatLabel(FieldCenter + Vector3.up * 1.4f, msg, new Color(0.95f, 0.82f, 0.25f));
                    MiniAudio.PlayCollect();
                });
        }

        IEnumerator ClaimAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId) ||
                string.IsNullOrEmpty(SessionStore.Token) ||
                string.IsNullOrEmpty(SessionStore.PlayerId))
                yield break;

            SetBusy(true);
            SetStatus("Yutuq olinmoqda…");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("achievementId", achievementId)
            );
            var ok = false;
            AchievementClaimResponse res = null;
            yield return _api.PostJson(
                "/api/v1/rewards/achievement/claim",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    SetBusy(false);
                    if (code < 200 || code >= 300)
                    {
                        MiniAudio.PlayError();
                        SetStatus("Yutuq: " + ExtractError(text));
                        return;
                    }

                    res = JsonUtility.FromJson<AchievementClaimResponse>(text);
                    ok = res != null && res.success;
                });

            if (!ok || res == null) yield break;

            _diamond += Math.Max(0, res.rewardDiamondsAwarded);
            SetStatus("Yutuq! +" + res.rewardDiamondsAwarded + "◇");
            WorldFeedback.FloatLabel(
                FieldCenter + Vector3.up * 1.5f,
                "+" + res.rewardDiamondsAwarded + "◇",
                new Color(0.85f, 0.75f, 1f));
            MiniAudio.PlayCollect();
            yield return LoadPlayerState();
            if (_hud != null)
            {
                _hud.SetAchievements(_achievements);
                _hud.ShowAchievementsSheet(true);
            }
        }

        IEnumerator ClanCreateAndRefresh()
        {
            yield return _social.CreateClan();
            yield return _social.RefreshMyClan(summary =>
            {
                if (_hud != null)
                {
                    _hud.SetClanRoster(summary);
                    _hud.ShowClanSheet(true);
                }
            });
        }

        IEnumerator ClanJoinAndRefresh()
        {
            var id = _hud != null ? _hud.ClanJoinId : "";
            yield return _social.JoinClan(id);
            yield return _social.RefreshMyClan(summary =>
            {
                if (_hud != null)
                {
                    _hud.SetClanRoster(summary);
                    _hud.ShowClanSheet(true);
                }
            });
        }

        IEnumerator ClanLeaveAndRefresh()
        {
            yield return _social.LeaveClan();
            if (_hud != null)
            {
                _hud.SetClanRoster("Klan yo‘q — Yaratish yoki ID bilan qo‘shiling");
                _hud.ShowClanSheet(true);
            }
        }

        IEnumerator ChatSendAndRefresh()
        {
            var draft = _hud != null ? _hud.ChatDraft : "";
            yield return _social.SendGlobalChat(draft);
            if (_hud != null && !string.IsNullOrEmpty(draft)) _hud.ChatDraft = "";
            yield return _social.RefreshChatHistory(summary =>
            {
                if (_hud != null)
                {
                    _hud.SetChatHistory(summary);
                    _hud.ShowChatSheet(true);
                }
            });
        }

        IEnumerator ChatClanSendAndRefresh()
        {
            var draft = _hud != null ? _hud.ChatDraft : "";
            yield return _social.SendClanChat(draft);
            if (_hud != null && !string.IsNullOrEmpty(draft)) _hud.ChatDraft = "";
            yield return _social.RefreshClanChatHistory(summary =>
            {
                if (_hud != null)
                {
                    _hud.SetChatHistory(summary);
                    _hud.ShowChatSheet(true);
                }
            });
        }

        IEnumerator WaitTrainingThenReload(int seconds)
        {
            var wait = Mathf.Clamp(seconds, 1, 120);
            yield return new WaitForSecondsRealtime(wait + 0.35f);
            _trainWaitCo = null;
            yield return LoadPlayerState();
        }

        static BuildingViewSync.BuildingDtoLite[] ToLite(BuildingDto[] buildings)
        {
            if (buildings == null || buildings.Length == 0)
                return Array.Empty<BuildingViewSync.BuildingDtoLite>();
            var lite = new BuildingViewSync.BuildingDtoLite[buildings.Length];
            for (var i = 0; i < buildings.Length; i++)
            {
                var b = buildings[i];
                if (b == null) continue;
                lite[i] = new BuildingViewSync.BuildingDtoLite
                {
                    Id = b.id,
                    Type = b.type,
                    Level = b.level,
                    GridX = b.gridX,
                    GridZ = b.gridZ,
                    RotationSteps = b.rotationSteps
                };
            }
            return lite;
        }

        void ApplyPlayerBalances(PlayerSummary player)
        {
            if (player == null) return;
            _gold = player.gold;
            _mana = player.mana;
            _diamond = player.diamond;
        }

        static string BuildNextStepHint(bool hasMine, bool hasBarracks, int buildingCount)
        {
            if (!hasMine) return "Baza OK (" + buildingCount + " bino). Keyingi: Kon → Tasdiq";
            if (!hasBarracks) return "Kon bor. Keyingi: Qal’a L2 → Kazarma";
            return "Kon+Kazarma bor. Keyingi: Yig‘ish → Askar (mana) → Reyd";
        }

        IEnumerator UpgradeSelected()
        {
            EnsureBuildingSystem();
            if (_buildings == null)
            {
                SetStatus("Building system yo'q");
                yield break;
            }

            var prevCastle = _castleLevel;
            SetBusy(true);
            yield return _buildings.UpgradeSelected(_selectedBuildingId);
            SetBusy(false);
            yield return LoadPlayerState();
            if (_castleLevel > prevCastle)
            {
                var labels = _unlockLabels != null && _unlockLabels.Length > 0
                    ? string.Join(" + ", _unlockLabels)
                    : (_castleLevel == 2 ? "Kazarma + Askar"
                        : _castleLevel == 3 ? "Lager 2"
                        : _castleLevel == 4 ? "Archer + Lager 3"
                        : ("L" + _castleLevel));
                SetStatus("Qal’a L" + _castleLevel + " — yangi: " + labels);
                WorldFeedback.FloatLabel(FieldCenter + Vector3.up * 2f,
                    "Castle L" + _castleLevel, new Color(1f, 0.92f, 0.4f));
                if (_hud != null)
                {
                    _hud.SetUnlockBody(BuildUnlockSheetText());
                    _hud.ShowUnlockSheet(true);
                }
            }
        }

        IEnumerator SpeedupSelected()
        {
            EnsureBuildingSystem();
            if (_buildings == null)
            {
                SetStatus("Building system yo'q");
                yield break;
            }

            var prevCastle = _castleLevel;
            SetBusy(true);
            yield return _buildings.SpeedupSelected(_selectedBuildingId);
            SetBusy(false);
            yield return LoadPlayerState();
            if (_castleLevel > prevCastle)
            {
                SetStatus("Tezkor — Qal’a L" + _castleLevel);
                if (_hud != null)
                {
                    _hud.SetUnlockBody(BuildUnlockSheetText());
                    _hud.ShowUnlockSheet(true);
                }
            }
        }

        IEnumerator DestroySelected()
        {
            EnsureBuildingSystem();
            if (_buildings == null) yield break;
            var id = _selectedBuildingId;
            SetBusy(true);
            yield return _buildings.DestroySelected(id);
            SetBusy(false);
            if (_selectedBuildingId == id) _selectedBuildingId = null;
            yield return LoadPlayerState();
        }

        IEnumerator RepairSelected()
        {
            EnsureBuildingSystem();
            if (_buildings == null) yield break;
            SetBusy(true);
            yield return _buildings.RepairSelected(_selectedBuildingId);
            SetBusy(false);
            yield return LoadPlayerState();
        }

        IEnumerator CancelUpgradeSelected()
        {
            EnsureBuildingSystem();
            if (_buildings == null) yield break;
            SetBusy(true);
            yield return _buildings.CancelUpgrade(_selectedBuildingId);
            SetBusy(false);
            yield return LoadPlayerState();
        }

        IEnumerator CollectGold()
        {
            SetBusy(true);
            SetStatus("Yig‘ilmoqda…");
            var beforeGold = _gold;
            var beforeMana = _mana;
            var body = JsonObject(("playerId", SessionStore.PlayerId));
            yield return _api.PostJson("/api/v1/resources/collect", body, SessionStore.Token,
                ApiClient.NewIdempotencyKey(), (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    MiniAudio.PlayError();
                    SetStatus("Yig‘ish xato: " + ExtractError(text));
                    return;
                }

                var collected = JsonUtility.FromJson<CollectResponse>(text);
                var goldDelta = collected != null ? collected.goldCollected : 0;
                var manaDelta = collected != null ? collected.manaCollected : 0;
                if (goldDelta > 0) _gold = beforeGold + goldDelta;
                if (manaDelta > 0) _mana = beforeMana + manaDelta;

                Vector3 floatPos = FieldCenter + Vector3.up;
                if (!string.IsNullOrEmpty(_selectedBuildingId) &&
                    _buildingViews.TryGetValue(_selectedBuildingId, out var view) && view != null)
                    floatPos = view.transform.position + Vector3.up * 1.1f;
                else
                {
                    foreach (var v in _buildingViews.Values)
                    {
                        var m = v != null ? v.GetComponent<BuildingMarker>() : null;
                        if (m != null && m.buildingType == "gold_mine")
                        {
                            floatPos = v.transform.position + Vector3.up * 1.1f;
                            break;
                        }
                    }
                }

                var parts = new List<string>(2);
                if (goldDelta > 0) parts.Add("+" + goldDelta + "●");
                if (manaDelta > 0) parts.Add("+" + manaDelta + "◆");
                var label = parts.Count > 0 ? string.Join(" ", parts) : "+0";
                if (collected != null && collected.dailyMultiplierTriggered)
                {
                    label = "Kunlik ×2! " + label;
                    WorldFeedback.FloatLabel(floatPos + Vector3.up * 0.55f, "Kunlik ×2!", new Color(0.35f, 0.95f, 0.55f));
                }
                WorldFeedback.FloatLabel(floatPos, label, new Color(1f, 0.85f, 0.2f));
                WorldFeedback.PlaceBurst(floatPos);
                MiniAudio.PlayCollect();
                RefreshHud();
                SetStatus(parts.Count > 0
                    ? (collected != null && collected.dailyMultiplierTriggered ? "Kunlik ×2! " : "") +
                      "Yig‘ildi " + string.Join(" ", parts) + " (oldingi ●" + beforeGold + " ◆" + beforeMana + ")"
                    : "Yig‘ish: hozircha 0");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator TrainTroops(int quantity)
        {
            if (!CanTrainTroop(_trainTroopType))
            {
                SetStatus(_trainTroopType == "archer"
                    ? "Archer uchun Qal’a L4 kerak"
                    : "Askar uchun Qal’a L2 + Kazarma kerak");
                MiniAudio.PlayError();
                yield break;
            }

            var troopLabel = _trainTroopType == "archer" ? "Archer" : "Askar";
            SetBusy(true);
            SetStatus(troopLabel + " navbatga… (◆ mana)");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("troopType", _trainTroopType),
                ("quantity", quantity.ToString())
            );

            TrainResponse res = null;
            var ok = false;
            yield return _api.PostJson("/api/v1/troops/train", body, SessionStore.Token,
                ApiClient.NewIdempotencyKey(), (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    MiniAudio.PlayError();
                    var err = ExtractError(text);
                    if (!string.IsNullOrEmpty(err) &&
                        err.IndexOf("housing", StringComparison.OrdinalIgnoreCase) >= 0)
                        SetStatus("Lager to‘la — " + err);
                    else
                        SetStatus(troopLabel + " xato: " + err);
                    return;
                }

                res = JsonUtility.FromJson<TrainResponse>(text);
                ok = true;
            });

            if (!ok || res == null) yield break;

            FunnelAnalytics.Instance?.Track("train_troop", ("troopType", _trainTroopType));

            if (res.totalCostMana > 0) _mana = Math.Max(0, _mana - res.totalCostMana);
            if (res.housing != null && res.housing.max > 0)
            {
                _housingUsed = res.housing.used;
                _housingMax = res.housing.max;
            }
            RefreshHud();

            if (res.training && res.trainSeconds > 0)
            {
                SetStatus(troopLabel + " tayyorlanmoqda… " + res.trainSeconds + "s (×" + res.pendingQuantity + ")");
                if (_trainWaitCo != null) StopCoroutine(_trainWaitCo);
                _trainWaitCo = StartCoroutine(WaitTrainingThenReload(res.trainSeconds));
                yield break;
            }

            if (_trainTroopType == "archer")
                _archerCount += res.trainedQuantity;
            else
                _barbarianCount += res.trainedQuantity;
            Vector3 floatPos = FieldCenter + Vector3.up;
            foreach (var v in _buildingViews.Values)
            {
                var m = v != null ? v.GetComponent<BuildingMarker>() : null;
                if (m != null && m.buildingType == "barracks")
                {
                    floatPos = v.transform.position + Vector3.up * 1.2f;
                    break;
                }
            }
            WorldFeedback.FloatLabel(floatPos, "+" + res.trainedQuantity + " ⚔", new Color(0.7f, 0.9f, 1f));
            MiniAudio.PlayTrainDone();
            SetStatus(troopLabel + " +" + res.trainedQuantity);
            SyncTroopVisuals();
            RefreshHud();
            yield return LoadPlayerState();
        }

        void ToggleTrainTroopType()
        {
            if (!CanTrainTroop("archer"))
            {
                _trainTroopType = "barbarian";
                RefreshHud();
                return;
            }

            _trainTroopType = _trainTroopType == "archer" ? "barbarian" : "archer";
            RefreshHud();
            SetStatus(_trainTroopType == "archer"
                ? "Train: Archer (L4 ochiq)"
                : "Train: Askar");
        }

        bool CanTrainTroop(string troopType)
        {
            if (string.IsNullOrEmpty(troopType)) return false;
            if (_trainableTroops != null)
            {
                for (var i = 0; i < _trainableTroops.Length; i++)
                {
                    if (_trainableTroops[i] == troopType) return true;
                }
            }

            if (troopType == "barbarian") return _castleLevel >= 2;
            if (troopType == "archer") return _castleLevel >= 4;
            return false;
        }

        string BuildUnlockSheetText()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("Hozir: Qal’a L").Append(_castleLevel).Append('\n');
            sb.Append("Kamp: ").Append(_campaignMax).Append("/3\n");
            if (_unlockLabels != null && _unlockLabels.Length > 0)
                sb.Append("Ochilgan: ").Append(string.Join(", ", _unlockLabels)).Append('\n');
            sb.Append('\n');
            sb.Append(MarkUnlock(1)).Append(" L1 — Kon\n");
            sb.Append(MarkUnlock(2)).Append(" L2 — Kazarma + Askar (+500●)\n");
            sb.Append(MarkUnlock(3)).Append(" L3 — Lager 2 (+1000● +500◆)\n");
            sb.Append(MarkUnlock(4)).Append(" L4 — Archer + Lager 3 (+50◇)\n");
            if (_castleLevel < 4)
                sb.Append("\nKeyingi: Qal’a L").Append(_castleLevel + 1);
            else
                sb.Append("\nArcher: Askar yonidagi tur tugmasi.");
            return sb.ToString();
        }

        static string MarkUnlock(int level, int castleLevel)
        {
            return castleLevel >= level ? "✓" : "·";
        }

        string MarkUnlock(int level)
        {
            return MarkUnlock(level, _castleLevel);
        }

        int PickRaidFortress()
        {
            var max = Math.Max(1, Math.Min(_campaignMax, 3));
            var bestCleared = 0;
            if (_campaigns != null)
            {
                foreach (var c in _campaigns)
                {
                    if (c != null && c.starsEarned > 0 && c.fortressId > bestCleared)
                        bestCleared = c.fortressId;
                }
            }

            var next = Math.Min(max, bestCleared + 1);
            return Math.Max(1, next);
        }

        IEnumerator StartRaid()
        {
            var army = _barbarianCount + _archerCount;
            if (army < 1)
            {
                SetStatus("Reyd uchun askar kerak — avval Askar ×10");
                MiniAudio.PlayError();
                yield break;
            }

            _raidFortressId = PickRaidFortress();
            // Soft-test: Camp 2/3 need more than 10 barbs for ≥1★ (see pveSimulator layouts).
            const int MaxRaidDeploy = 40;
            _raidDeployCount = Mathf.Clamp(army, 1, MaxRaidDeploy);

            SetBusy(true);
            SetStatus("Reyd Lager " + _raidFortressId + "…");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("fortressId", _raidFortressId.ToString())
            );

            var ok = false;
            yield return _api.PostJson("/api/v1/campaign/start", body, SessionStore.Token, ApiClient.NewIdempotencyKey(), (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    MiniAudio.PlayError();
                    SetStatus("Reyd start xato: " + ExtractError(text));
                    return;
                }

                ok = true;
                _raidStartedAt = Time.realtimeSinceStartup;
                _raidActive = true;
                if (_hud != null)
                {
                    _hud.SetRaidCompleteReady(false, 30);
                    _hud.SetRaidHud(_raidFortressId, _raidDeployCount, 100, 30);
                }
                SetStatus("Reyd: Lager " + _raidFortressId + " · " + _raidDeployCount + " askar · 30s");
            });

            if (!ok) yield break;

            FunnelAnalytics.Instance?.Track("raid_start", ("fortressId", _raidFortressId.ToString()));

            var from = FindBuildingWorldPos("barracks");
            if (from.sqrMagnitude < 0.01f) from = FieldCenter;
            var camp = FieldCenter + new Vector3(4.5f, 0f, 4.5f);
            RaidPresentationFx.PlayRaidStart(_cocCamera, from, camp);

            if (_raidCountdownCo != null) StopCoroutine(_raidCountdownCo);
            _raidCountdownCo = StartCoroutine(RaidCountdownThenComplete());
        }

        IEnumerator RaidCountdownThenComplete()
        {
            const float wait = 30f;
            var midCueDone = false;
            while (_raidActive)
            {
                var left = wait - (Time.realtimeSinceStartup - _raidStartedAt);
                if (left <= 0f) break;
                var elapsed = wait - Mathf.Max(0f, left);
                var secLeft = Mathf.CeilToInt(left);
                // Visual HP drain from deploy power (honest feedback; server still authoritative).
                var hp = Mathf.Clamp(100 - Mathf.RoundToInt((elapsed / wait) * (40 + _raidDeployCount * 5)), 5, 100);
                if (_hud != null)
                {
                    _hud.SetRaidCompleteReady(false, secLeft);
                    _hud.SetRaidHud(_raidFortressId, _raidDeployCount, hp, secLeft);
                }

                if (!midCueDone && left <= 15f)
                {
                    midCueDone = true;
                    RaidPresentationFx.PlayRaidMidPulse(FieldCenter + new Vector3(4.5f, 0f, 4.5f), hp);
                }

                SetStatus("Reyd L" + _raidFortressId + ": " + secLeft + "s · HP " + hp + "%");
                yield return new WaitForSecondsRealtime(0.25f);
            }

            if (!_raidActive) yield break;
            if (_hud != null)
            {
                _hud.SetRaidCompleteReady(true, 0);
                _hud.SetRaidHud(_raidFortressId, _raidDeployCount, 15, 0);
            }
            SetStatus("Reyd tayyor — Yakunla ✓ yoki avto…");
            yield return new WaitForSecondsRealtime(0.35f);
            if (_raidActive)
                yield return CompleteRaid();
        }

        IEnumerator CompleteRaid()
        {
            if (!_raidActive)
            {
                SetStatus("Avval Raid bosing");
                yield break;
            }

            var elapsed = Time.realtimeSinceStartup - _raidStartedAt;
            if (elapsed < 30f)
            {
                var left = Mathf.CeilToInt(30f - elapsed);
                if (_hud != null) _hud.SetRaidCompleteReady(false, left);
                SetStatus("Hali erta — yana " + left + " soniya");
                yield break;
            }

            var army = _barbarianCount + _archerCount;
            var deploy = Math.Max(1, Math.Min(_raidDeployCount, Math.Max(1, army)));
            var barbs = Math.Min(_barbarianCount, deploy);
            var archers = Math.Min(_archerCount, deploy - barbs);
            // Server awards serverStars; never claim above 0 here (anti-cheat rejects claim > server).
            var sb = new StringBuilder();
            sb.Append("{\"playerId\":\"").Append(SessionStore.PlayerId)
              .Append("\",\"fortressId\":").Append(_raidFortressId)
              .Append(",\"starsEarned\":0,\"deployTicks\":[");
            var wrote = 0;
            for (var i = 0; i < barbs; i++)
            {
                if (wrote++ > 0) sb.Append(',');
                sb.Append("{\"troopType\":\"barbarian\"}");
            }
            for (var i = 0; i < archers; i++)
            {
                if (wrote++ > 0) sb.Append(',');
                sb.Append("{\"troopType\":\"archer\"}");
            }
            sb.Append("]}");

            SetBusy(true);
            SetStatus("Reyd yakunlanmoqda…");
            CampaignCompleteResponse res = null;
            var ok = false;
            yield return _api.PostJson(
                "/api/v1/campaign/complete",
                sb.ToString(),
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    SetBusy(false);
                    if (code < 200 || code >= 300)
                    {
                        MiniAudio.PlayError();
                        SetStatus("Reyd yakun xato: " + ExtractError(text));
                        return;
                    }

                    res = JsonUtility.FromJson<CampaignCompleteResponse>(text);
                    ok = true;
                });

            if (!ok || res == null)
            {
                // Keep raid session so player can retry Yakunla without full restart.
                if (_hud != null) _hud.SetRaidCompleteReady(true, 0);
                yield break;
            }

            _raidActive = false;
            if (_raidCountdownCo != null)
            {
                StopCoroutine(_raidCountdownCo);
                _raidCountdownCo = null;
            }
            if (_hud != null)
            {
                _hud.SetRaidCompleteReady(false, 0);
                _hud.SetRaidHud(0, 0, 0);
            }

            FunnelAnalytics.Instance?.Track("raid_complete", ("fortressId", _raidFortressId.ToString()));

            _gold = res.goldBalance;
            var stars = res.battleResult != null ? res.battleResult.stars : res.starsEarned;
            var loot = res.loot != null ? res.loot.gold : 0;
            var won = stars > 0;
            var clearTag = res.firstClear ? " (birinchi)" : " (qayta)";
            _resultMessage = won
                ? "Lager " + _raidFortressId + clearTag +
                  "\n★ " + stars + "  ·  Askar −" + deploy +
                  "\nO‘lja: +" + loot + " ●" +
                  "\nBalans: " + _gold + " ●"
                : "Lager " + _raidFortressId +
                  "\nAskar −" + deploy +
                  "\nO‘lja: 0" +
                  "\nBalans: " + _gold + " ●";

            if (won)
            {
                yield return RaidPresentationFx.PlayRaidWin(_cocCamera, FieldCenter, stars, loot);
                MiniAudio.PlayRaidWin();
            }
            else
            {
                yield return RaidPresentationFx.PlayRaidLose(_cocCamera, FieldCenter);
                MiniAudio.PlayRaidLose();
            }

            SetScreen(UiScreen.Result);
            if (_hud != null) _hud.SetResultChips(stars, loot, won);
            SetStatus(won ? "Reyd yakunlandi" : "Reyd mag‘lubiyat");
            if (res.troopsConsumed != null)
            {
                foreach (var t in res.troopsConsumed)
                {
                    if (t == null) continue;
                    if (t.type == "barbarian")
                        _barbarianCount = Math.Max(0, _barbarianCount - t.quantity);
                    else if (t.type == "archer")
                        _archerCount = Math.Max(0, _archerCount - t.quantity);
                }
            }
            else
            {
                _barbarianCount = Math.Max(0, _barbarianCount - barbs);
                _archerCount = Math.Max(0, _archerCount - archers);
            }
            SyncTroopVisuals();
            RefreshHud();
        }

        Vector3 FindBuildingWorldPos(string type)
        {
            foreach (var view in _buildingViews.Values)
            {
                if (view == null) continue;
                var m = view.GetComponent<BuildingMarker>();
                if (m == null || m.buildingType != type) continue;
                return view.transform.position;
            }
            return Vector3.zero;
        }

        void SpawnBuilding(string id, string type, int level, int gridX, int gridZ, int rotationSteps = 0)
        {
            if (_buildingViews.ContainsKey(id))
            {
                Destroy(_buildingViews[id]);
                _buildingViews.Remove(id);
            }

            // Presentation: BuildingArtCatalog prefab (when PreferProcedural=false) else Soft-GO Factory pack.
            var cell = ResolveBuildingWorldPos(type, gridX, gridZ);
            GameObject go = BuildingArtCatalog.TryInstantiatePrefab(type, level);
            if (go != null)
            {
                var fp = BuildingArtCatalog.Footprint(type);
                BuildingFitUtil.FitToCell(go, cell, fp, forceUpright: type == "castle");
                if (type == "castle") BuildingFitUtil.OrientTowardCamera(go, cell);
                BuildingFitUtil.EnsureClickCollider(go);
                Debug.Log("[MiniMvp] Art prefab " + type + " at " + go.transform.position);
            }
            else
            {
                go = BuildingVisualFactory.Create(type, level, cell);
                Debug.Log("[MiniMvp] Soft-GO art pack " + type + " L" + level);
            }

            if (rotationSteps != 0)
                go.transform.rotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f) * go.transform.rotation;

            // Keep buildings under Village/Gameplay so scene cleanup never orphans them.
            var gameplay = ResolveGameplayRoot();
            if (gameplay != null)
            {
                var folder = gameplay.Find("Buildings");
                if (folder == null)
                {
                    var folderGo = new GameObject("Buildings");
                    folderGo.transform.SetParent(gameplay, false);
                    folder = folderGo.transform;
                }
                go.transform.SetParent(folder, true);
            }

            var marker = go.GetComponent<BuildingMarker>();
            if (marker == null) marker = go.AddComponent<BuildingMarker>();
            marker.buildingId = id;
            marker.buildingType = type;
            marker.level = level;
            marker.gridX = gridX;
            marker.gridZ = gridZ;
            marker.rotationSteps = rotationSteps;

            BuildingFitUtil.EnsureClickCollider(go);

            var click = go.GetComponent<BuildingClickRelay>();
            if (click == null) click = go.AddComponent<BuildingClickRelay>();
            click.onClick = () =>
            {
                if (_buildings != null && (_buildings.IsPlacing || _buildings.IsRelocating)) return;
                if (Time.unscaledTime < BuildingClickRelay.SuppressUntilTime) return;
                _pendingDestroyId = null;
                _selectedBuildingId = id;
                BuildingSelectFx.Select(go);
                MiniAudio.PlaySelect();
                _buildings?.BindConstructionTimer(id);
                if (_cocCamera != null)
                    _cocCamera.FocusSmooth(ResolveBuildingWorldPos(type, gridX, gridZ));
                var construct = _buildings != null ? _buildings.DescribeConstruction(id) : null;
                var label = "Selected: " + PrettyType(type) + " L" + level;
                if (!string.IsNullOrEmpty(construct)) label += " · " + construct;
                SetStatus(label);
            };

            _buildingViews[id] = go;
            if (id == _selectedBuildingId)
                BuildingSelectFx.Select(go);
        }

        void FrameCameraOnBase()
        {
            var focus = FieldCenter;

            foreach (var view in _buildingViews.Values)
            {
                if (view == null) continue;
                var marker = view.GetComponent<BuildingMarker>();
                if (marker == null || marker.buildingType != "castle") continue;
                var renderers = view.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0) break;
                var b = BuildingFitUtil.EncapsulateBounds(renderers);
                focus = new Vector3(b.center.x, 0f, b.center.z);
                break;
            }

            if (_fieldRoot != null)
                _fieldRoot.position = FieldCenter;

            if (_cocCamera == null)
            {
                var cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    _cocCamera = cam.GetComponent<CoCCameraController>();
                    if (_cocCamera == null) _cocCamera = cam.gameObject.AddComponent<CoCCameraController>();
                    _cocCamera.Configure(FieldCenter, FieldWorldSize, 3f);
                }
            }

            if (_cocCamera != null)
                _cocCamera.FocusBase(focus, CoCCameraController.FitOrthoForBase(FieldWorldSize, 1.05f));
        }

        Transform ResolveGameplayRoot()
        {
            if (_villageGameplay != null) return _villageGameplay;
            var go = GameObject.Find("Village/Gameplay");
            if (go != null) _villageGameplay = go.transform;
            return _villageGameplay;
        }

        void ClearBuildings()
        {
            foreach (var view in _buildingViews.Values)
            {
                if (view != null) Destroy(view);
            }
            _buildingViews.Clear();
        }

        void SetStatus(string message)
        {
            _status = message;
            Debug.Log("[MiniMvp] " + message);
            RefreshHud();
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            if (_hud != null) _hud.SetBusy(_busy);
        }

        static string ExtractError(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "backend javob bermadi — `npm run dev` ishga tushiring (http://127.0.0.1:3000)";
            try
            {
                var err = JsonUtility.FromJson<ApiError>(text);
                if (!string.IsNullOrEmpty(err.message)) return err.message;
                if (!string.IsNullOrEmpty(err.error)) return err.error;
            }
            catch
            {
                // ignore
            }

            return text.Length > 180 ? text.Substring(0, 180) : text;
        }

        static string JsonObject(params (string key, string value)[] pairs)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            for (var i = 0; i < pairs.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(pairs[i].key).Append('"').Append(':');
                if (int.TryParse(pairs[i].value, out _) || long.TryParse(pairs[i].value, out _))
                    sb.Append(pairs[i].value);
                else
                    sb.Append('"').Append(Escape(pairs[i].value)).Append('"');
            }

            sb.Append('}');
            return sb.ToString();
        }

        static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    public sealed class BuildingMarker : MonoBehaviour
    {
        public string buildingId;
        public string buildingType;
        public int level;
        public int gridX;
        public int gridZ;
        public int rotationSteps;
    }

    public sealed class BuildingClickRelay : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        /// <summary>Ignore clicks through this time (set after drag-release).</summary>
        public static float SuppressUntilTime;

        public System.Action onClick;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (Time.unscaledTime < SuppressUntilTime) return;
            onClick?.Invoke();
        }
    }
}

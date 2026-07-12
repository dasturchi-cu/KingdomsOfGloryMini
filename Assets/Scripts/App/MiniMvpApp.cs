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
        float _raidStartedAt = -999f;
        bool _raidActive;
        Coroutine _raidCountdownCo;
        string _pendingDestroyId;
        int _stateLoadGen;
        string _selectedBuildingId;
        bool _busy;
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

        GUIStyle _titleStyle;
        GUIStyle _statusStyle;
        GUIStyle _btnStyle;
        bool _stylesReady;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Keep releaseBaseUrl referenced so Inspector value is not stripped / CS0414-warned.
            _ = releaseBaseUrl;
            return string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:3000" : baseUrl.Trim();
#else
            var url = string.IsNullOrWhiteSpace(releaseBaseUrl) ? baseUrl : releaseBaseUrl;
            url = (url ?? string.Empty).Trim();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[MiniMvp] Release build refuses cleartext HTTP — set releaseBaseUrl to HTTPS");
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
            _api = new ApiClient(ResolveBaseUrl());
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
            _hud.OnUpgrade = () => StartCoroutine(UpgradeSelected());
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
            _hud.OnClan = () => StartCoroutine(_social.CreateClan());
            _hud.OnChat = () => StartCoroutine(_social.SendGlobalChat());
            _hud.OnPvp = () => StartCoroutine(_social.FindPvp());
            _hud.OnTournament = () => StartCoroutine(_social.JoinTournament());
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
                    ? "Saqlandi (r" + _save.Revision + ")"
                    : "Saqlash xato: " + result.Message);
            };
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
                var parent = _fieldRoot != null ? _fieldRoot : transform;
                _buildingGrid = BuildingGrid.Build(parent, gridSize, cellSize, FieldCenter);
            }

            if (_buildings == null)
            {
                _buildings = GetComponent<BuildingSystem>();
                if (_buildings == null) _buildings = gameObject.AddComponent<BuildingSystem>();
                _buildings.Configure(_api, () => SessionStore.Token, () => SessionStore.PlayerId, _buildingGrid);
                _buildings.StatusChanged += msg => SetStatus(msg);
                _buildings.MutationSucceeded += () =>
                {
                    if (_hud != null) _hud.SetPlacementMode(false);
                    if (_troopInput != null) _troopInput.SetEnabled(true);
                };
            }

            if (_placementInput == null)
            {
                _placementInput = GetComponent<PlacementInputDriver>();
                if (_placementInput == null) _placementInput = gameObject.AddComponent<PlacementInputDriver>();
                var cam = UnityEngine.Camera.main;
                _placementInput.Bind(_buildings, cam);
            }
        }

        void BeginPlace(string buildingType)
        {
            EnsureBuildingSystem();
            if (_buildings == null) return;
            if (_buildings.BeginPlacement(buildingType))
            {
                if (_hud != null) _hud.SetPlacementMode(true);
                if (_troopInput != null) _troopInput.SetEnabled(false);
            }
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
                yield return LoadPlayerState();
                // Bounce the most recently selected / newest building.
                if (!string.IsNullOrEmpty(_selectedBuildingId) &&
                    _buildingViews.TryGetValue(_selectedBuildingId, out var go) && go != null)
                    WorldFeedback.PlaceDrop(go.transform);
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
            _hud.SetResources(_gold, _mana, _diamond, _barbarianCount);
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

            // CoCCameraController owns pose; app only supplies field center + framing size.
            _cocCamera.Configure(FieldCenter, FieldWorldSize, 5.5f);
            _cocCamera.FocusBase(FieldCenter, FieldWorldSize * 0.55f);

            BaseLightingSetup.Apply(FieldCenter);
            FieldVisualBuilder.FinalizeHierarchy();

            // Re-assert pose after hierarchy/lighting (no parent fight, focus never stuck at origin).
            _cocCamera.FocusBase(FieldCenter, FieldWorldSize * 0.55f);

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


        void EnsureStyles()
        {
            if (_stylesReady) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.95f, 0.6f) }
            };
            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _stylesReady = true;
        }

        void OnGUI()
        {
            // Auth + Game + Result all use MiniMvpHud (uGUI). OnGUI kept empty for hot-reload safety.
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
                SetStatus("Ro‘yxat OK. Endi Kon → Tasdiq.");
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
                StartCoroutine(LoadPlayerState());
            });
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
                }

                _barbarianCount = 0;
                if (state.troops != null)
                {
                    foreach (var troop in state.troops)
                    {
                        if (troop.type == "barbarian") _barbarianCount = troop.quantity;
                    }
                }

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
                    _selectedBuildingId = "local_castle_fallback";
                    buildingCount++;
                    Debug.LogWarning("[MiniMvp] Server castle missing — spawned center fallback");
                }

                SetStatus(BuildNextStepHint(hasMine, hasBarracks, buildingCount));
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
            });
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
            if (!hasBarracks) return "Kon bor. Keyingi: Kazarma → Tasdiq";
            return "Kon+Kazarma bor. Keyingi: Yig‘ish → Askar → Reyd";
        }

        IEnumerator UpgradeSelected()
        {
            EnsureBuildingSystem();
            if (_buildings == null)
            {
                SetStatus("Building system yo'q");
                yield break;
            }

            SetBusy(true);
            yield return _buildings.UpgradeSelected(_selectedBuildingId);
            SetBusy(false);
            yield return LoadPlayerState();
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
                WorldFeedback.FloatLabel(floatPos, label, new Color(1f, 0.85f, 0.2f));
                WorldFeedback.PlaceBurst(floatPos);
                MiniAudio.PlayCollect();
                RefreshHud();
                SetStatus(parts.Count > 0
                    ? "Yig‘ildi " + label + " (oldingi ●" + beforeGold + " ◆" + beforeMana + ")"
                    : "Yig‘ish: hozircha 0");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator TrainTroops(int quantity)
        {
            SetBusy(true);
            SetStatus("Askar tayyorlanmoqda…");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("troopType", "barbarian"),
                ("quantity", quantity.ToString())
            );

            yield return _api.PostJson("/api/v1/troops/train", body, SessionStore.Token,
                ApiClient.NewIdempotencyKey(), (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    MiniAudio.PlayError();
                    SetStatus("Askar xato: " + ExtractError(text) + " (avval Kazarma + mana)");
                    return;
                }

                var res = JsonUtility.FromJson<TrainResponse>(text);
                _barbarianCount += res.trainedQuantity;
                if (res.totalCostMana > 0) _mana = Math.Max(0, _mana - res.totalCostMana);
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
                SetStatus("Askar +" + res.trainedQuantity + " · jami " + _barbarianCount);
                SyncTroopVisuals();
                RefreshHud();
            });
        }

        IEnumerator StartRaid()
        {
            SetBusy(true);
            SetStatus("Reyd boshlanmoqda…");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("fortressId", "1")
            );

            var ok = false;
            yield return _api.PostJson("/api/v1/campaign/start", body, SessionStore.Token, null, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    MiniAudio.PlayError();
                    SetStatus("Reyd start xato: " + ExtractError(text) + " (askar kerak)");
                    return;
                }

                ok = true;
                _raidStartedAt = Time.realtimeSinceStartup;
                _raidActive = true;
                if (_hud != null) _hud.SetRaidCompleteReady(false, 30);
                SetStatus("Raid boshlandi — 30 soniya…");
            });

            if (!ok) yield break;

            // Client juice only — server session already started.
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
            while (_raidActive)
            {
                var left = wait - (Time.realtimeSinceStartup - _raidStartedAt);
                if (left <= 0f) break;
                if (_hud != null) _hud.SetRaidCompleteReady(false, Mathf.CeilToInt(left));
                SetStatus("Reyd: " + Mathf.CeilToInt(left) + "s…");
                yield return new WaitForSecondsRealtime(0.25f);
            }

            if (!_raidActive) yield break;
            if (_hud != null) _hud.SetRaidCompleteReady(true, 0);
            SetStatus("Reyd tayyor — Yakunla yoki avto…");
            yield return new WaitForSecondsRealtime(0.35f);
            if (_raidActive)
                yield return CompleteRaid();
        }

        IEnumerator CompleteRaid()
        {
            if (!_raidActive && Time.realtimeSinceStartup - _raidStartedAt > 120f)
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

            _raidActive = false;
            if (_raidCountdownCo != null)
            {
                StopCoroutine(_raidCountdownCo);
                _raidCountdownCo = null;
            }
            if (_hud != null) _hud.SetRaidCompleteReady(false, 0);

            SetBusy(true);
            SetStatus("Completing raid...");
            // Payload unchanged — presentation does not alter stars/troops/server math.
            var body =
                "{\"playerId\":\"" + SessionStore.PlayerId +
                "\",\"fortressId\":1,\"starsEarned\":1,\"deployTicks\":[{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"}]}";

            CampaignCompleteResponse res = null;
            var ok = false;
            yield return _api.PostJson(
                "/api/v1/campaign/complete",
                body,
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

            if (!ok || res == null) yield break;

            _gold = res.goldBalance;
            var stars = res.battleResult != null ? res.battleResult.stars : res.starsEarned;
            var loot = res.loot != null ? res.loot.gold : 0;
            var won = stars > 0;
            _resultMessage = won
                ? "G‘alaba!\n★ " + stars + "\nLoot: +" + loot + " ●\nBalans: " + _gold
                : "Mag‘lubiyat\nLoot: 0\nBalans: " + _gold;

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
                if (_buildings != null && _buildings.IsPlacing) return;
                _pendingDestroyId = null;
                _selectedBuildingId = id;
                BuildingSelectFx.Select(go);
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
                _cocCamera.FocusBase(focus, FieldWorldSize * 0.62f);
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
        public System.Action onClick;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            onClick?.Invoke();
        }

        void OnMouseDown()
        {
            onClick?.Invoke();
        }
    }
}

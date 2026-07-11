using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using KoG.MiniMvp.Camera;
using KoG.MiniMvp.Lighting;
using KoG.MiniMvp.Network;
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
        int _barbarianCount;
        float _raidStartedAt = -999f;
        string _selectedBuildingId;
        bool _busy;
        Transform _fieldRoot;
        CoCCameraController _cocCamera;
        MiniMvpHud _hud;

        GUIStyle _titleStyle;
        GUIStyle _statusStyle;
        GUIStyle _btnStyle;
        bool _stylesReady;

        /// <summary>World center of the playable checkerboard (castle sits here).</summary>
        Vector3 FieldCenter => new Vector3((gridSize - 1) * cellSize * 0.5f, 0f, (gridSize - 1) * cellSize * 0.5f);

        float FieldWorldSize => gridSize * cellSize;

        Vector3 GridToWorld(int gridX, int gridZ) => new Vector3(gridX * cellSize, 0f, gridZ * cellSize);

        string ResolveBaseUrl()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            EnsureHud();
            if (SessionStore.HasSession)
            {
                SetScreen(UiScreen.Game);
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
            _hud.OnPlaceMine = () => StartCoroutine(PlaceBuilding("gold_mine"));
            _hud.OnPlaceBarracks = () => StartCoroutine(PlaceBuilding("barracks"));
            _hud.OnCollect = () => StartCoroutine(CollectGold());
            _hud.OnTrain = () => StartCoroutine(TrainTroops(10));
            _hud.OnUpgrade = () => StartCoroutine(UpgradeSelected());
            _hud.OnStartRaid = () => StartCoroutine(StartRaid());
            _hud.OnCompleteRaid = () => StartCoroutine(CompleteRaid());
            _hud.OnLogout = () =>
            {
                SessionStore.Clear();
                ClearBuildings();
                BuildingSelectFx.Clear();
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
            _hud.SetAuthFields(_username, _displayName, _email, _password);
            RefreshHud();
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
            _hud.SetResources(_gold, _barbarianCount);
            if (_screen == UiScreen.Result)
                _hud.SetResultMessage(_resultMessage);

            if (!string.IsNullOrEmpty(_selectedBuildingId) &&
                _buildingViews.TryGetValue(_selectedBuildingId, out var sel) && sel != null)
            {
                var m = sel.GetComponent<BuildingMarker>();
                _hud.SetSelected(m != null ? PrettyType(m.buildingType) + " L" + m.level : _selectedBuildingId);
            }
            else
            {
                _hud.SetSelected(null);
            }
        }

        static string PrettyType(string type)
        {
            if (type == "gold_mine") return "Gold Mine";
            if (type == "barracks") return "Barracks";
            if (type == "castle") return "Castle";
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
            _email = _username + "@guest.local";
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
                    SetStatus("Register failed: " + ExtractError(text));
                    return;
                }

                var res = JsonUtility.FromJson<AuthResponse>(text);
                SessionStore.Save(res.playerId, res.token, res.refreshToken);
                SetScreen(UiScreen.Game);
                SetStatus("Registered. Endi Place Mine bosing.");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator Login()
        {
            SetBusy(true);
            SetStatus("Logging in...");
            var body = JsonObject(
                ("username", _username.Trim()),
                ("password", _password)
            );

            yield return _api.PostJson("/api/v1/auth/login", body, null, null, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    SetStatus("Login failed: " + ExtractError(text));
                    return;
                }

                var res = JsonUtility.FromJson<AuthResponse>(text);
                SessionStore.Save(res.playerId, res.token, res.refreshToken);
                if (res.player != null) _gold = res.player.gold;
                SetScreen(UiScreen.Game);
                SetStatus("Logged in.");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator LoadPlayerState()
        {
            SetBusy(true);
            SetStatus("Loading base...");
            yield return _api.GetJson("/api/v1/player/state", SessionStore.Token, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    SetStatus("State load failed: " + ExtractError(text));
                    return;
                }

                var state = JsonUtility.FromJson<PlayerStateResponse>(text);
                ClearBuildings();
                if (state.player != null) _gold = state.player.gold;

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
                if (state.buildings != null)
                {
                    foreach (var building in state.buildings)
                    {
                        buildingCount++;
                        SpawnBuilding(building.id, building.type, building.level, building.gridX, building.gridZ);
                        if (building.type == "castle")
                        {
                            hasCastle = true;
                            _selectedBuildingId = building.id;
                        }
                        if (building.type == "gold_mine") hasMine = true;
                        if (building.type == "barracks") hasBarracks = true;
                    }
                }

                // Safety: empty base still shows a center castle so field never looks abandoned.
                if (!hasCastle)
                {
                    var cx = gridSize / 2;
                    var cz = gridSize / 2;
                    SpawnBuilding("local_castle_fallback", "castle", 1, cx, cz);
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
                RefreshHud();
            });
        }

        static string BuildNextStepHint(bool hasMine, bool hasBarracks, int buildingCount)
        {
            if (!hasMine) return "Base OK (" + buildingCount + " bino). Keyingi: Place Mine";
            if (!hasBarracks) return "Mine bor. Keyingi: Place Barracks";
            return "Mine+Barracks bor. Keyingi: Collect → Train → Raid (Place qayta bosilmasin)";
        }

        IEnumerator PlaceBuilding(string buildingType)
        {
            foreach (var view in _buildingViews.Values)
            {
                var marker = view != null ? view.GetComponent<BuildingMarker>() : null;
                if (marker != null && marker.buildingType == buildingType)
                {
                    SetStatus(buildingType + " allaqachon bor — qayta qo'yilmaydi (limit 1). Keyingi qadamga o'ting.");
                    yield break;
                }
            }

            var cell = FindFreeCell();
            if (cell == null)
            {
                SetStatus("No free cell");
                PlacePreviewFx.Show(FieldCenter, cellSize, false);
                yield break;
            }

            var previewWorld = GridToWorld(cell.Value.x, cell.Value.y);
            PlacePreviewFx.Show(previewWorld, cellSize, true);

            SetBusy(true);
            SetStatus("Placing " + buildingType + "...");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("buildingType", buildingType),
                ("gridX", cell.Value.x.ToString()),
                ("gridZ", cell.Value.y.ToString())
            );

            yield return _api.PostJson(
                "/api/v1/buildings/place",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    SetBusy(false);
                    PlacePreviewFx.Hide();
                    if (code < 200 || code >= 300)
                    {
                        var err = ExtractError(text);
                        if (err.IndexOf("limit", StringComparison.OrdinalIgnoreCase) >= 0)
                            SetStatus(buildingType + " limit: allaqachon 1 ta bor. Collect/Train/Raid qiling.");
                        else
                            SetStatus("Place failed: " + err);
                        StartCoroutine(LoadPlayerState());
                        return;
                    }

                    var res = JsonUtility.FromJson<PlaceBuildingResponse>(text);
                    _gold = res.goldBalance;
                    SpawnBuilding(res.building.id, res.building.type, res.building.level, res.building.gridX, res.building.gridZ);
                    _selectedBuildingId = res.building.id;
                    var world = GridToWorld(res.building.gridX, res.building.gridZ);
                    WorldFeedback.PlaceBurst(world);
                    BuildingSelectFx.Select(_buildingViews[res.building.id]);
                    if (_cocCamera != null) _cocCamera.FocusSmooth(world);
                    SetStatus("OK: " + PrettyType(buildingType) + " qo'yildi");
                    RefreshHud();
                });
        }

        IEnumerator CollectGold()
        {
            SetBusy(true);
            SetStatus("Collecting...");
            var before = _gold;
            var body = JsonObject(("playerId", SessionStore.PlayerId));
            yield return _api.PostJson("/api/v1/resources/collect", body, SessionStore.Token, null, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    SetStatus("Collect failed: " + ExtractError(text));
                    return;
                }

                // Float near selected mine or field center.
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

                WorldFeedback.FloatLabel(floatPos, "+GOLD", new Color(1f, 0.85f, 0.2f));
                WorldFeedback.PlaceBurst(floatPos);
                SetStatus("Collected (+ from " + before + "). Yangilanmoqda...");
                StartCoroutine(LoadPlayerState());
            });
        }

        IEnumerator TrainTroops(int quantity)
        {
            SetBusy(true);
            SetStatus("Training...");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("troopType", "barbarian"),
                ("quantity", quantity.ToString())
            );

            yield return _api.PostJson("/api/v1/troops/train", body, SessionStore.Token, null, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    SetStatus("Train failed: " + ExtractError(text) + " (avval Barracks + gold kerak)");
                    return;
                }

                var res = JsonUtility.FromJson<TrainResponse>(text);
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
                SetStatus("Trained " + res.trainedQuantity + " · jami " + _barbarianCount);
                RefreshHud();
            });
        }

        IEnumerator UpgradeSelected()
        {
            if (string.IsNullOrEmpty(_selectedBuildingId))
            {
                SetStatus("Avval kub (bino) ustiga bosing yoki Place qiling");
                yield break;
            }

            SetBusy(true);
            SetStatus("Upgrading...");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("buildingId", _selectedBuildingId)
            );

            yield return _api.PostJson(
                "/api/v1/buildings/upgrade",
                body,
                SessionStore.Token,
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    SetBusy(false);
                    if (code < 200 || code >= 300)
                    {
                        SetStatus("Upgrade failed: " + ExtractError(text));
                        return;
                    }

                    SetStatus("Upgrade started");
                    StartCoroutine(LoadPlayerState());
                });
        }

        IEnumerator StartRaid()
        {
            SetBusy(true);
            SetStatus("Starting raid...");
            var body = JsonObject(
                ("playerId", SessionStore.PlayerId),
                ("fortressId", "1")
            );

            yield return _api.PostJson("/api/v1/campaign/start", body, SessionStore.Token, null, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    SetStatus("Raid start failed: " + ExtractError(text) + " (askar kerak)");
                    return;
                }

                _raidStartedAt = Time.realtimeSinceStartup;
                SetStatus("Raid boshlandi. 30 soniya kutib, Complete Raid bosing.");
            });
        }

        IEnumerator CompleteRaid()
        {
            var elapsed = Time.realtimeSinceStartup - _raidStartedAt;
            if (elapsed < 30f)
            {
                SetStatus("Hali erta — yana " + Mathf.CeilToInt(30f - elapsed) + " soniya kuting");
                yield break;
            }

            SetBusy(true);
            SetStatus("Completing raid...");
            var body =
                "{\"playerId\":\"" + SessionStore.PlayerId +
                "\",\"fortressId\":1,\"starsEarned\":1,\"deployTicks\":[{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"},{\"troopType\":\"barbarian\"}]}";

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
                        SetStatus("Raid complete failed: " + ExtractError(text));
                        return;
                    }

                    var res = JsonUtility.FromJson<CampaignCompleteResponse>(text);
                    _gold = res.goldBalance;
                    var stars = res.battleResult != null ? res.battleResult.stars : res.starsEarned;
                    var loot = res.loot != null ? res.loot.gold : 0;
                    _resultMessage = "G'alaba!\nStars: " + stars + "\nLoot: +" + loot + " gold\nBalance: " + _gold;
                    SetScreen(UiScreen.Result);
                    SetStatus("Raid complete");
                    RefreshHud();
                });
        }

        Vector2Int? FindFreeCell()
        {
            var occupied = new HashSet<string>();
            foreach (var view in _buildingViews.Values)
            {
                var marker = view.GetComponent<BuildingMarker>();
                if (marker != null) occupied.Add(marker.gridX + ":" + marker.gridZ);
            }

            for (var z = 0; z < gridSize; z++)
            {
                for (var x = 0; x < gridSize; x++)
                {
                    // Reserve map center for future castle / HQ — keep empty until gameplay places it.
                    if (x == gridSize / 2 && z == gridSize / 2) continue;
                    var key = x + ":" + z;
                    if (!occupied.Contains(key)) return new Vector2Int(x, z);
                }
            }

            return null;
        }

        void SpawnBuilding(string id, string type, int level, int gridX, int gridZ)
        {
            if (_buildingViews.ContainsKey(id))
            {
                Destroy(_buildingViews[id]);
                _buildingViews.Remove(id);
            }

            GameObject go;
            if (type == "castle")
            {
                // Castle.prefab has Tripo -90° axis fix baked in. CastleMesh.fbx alone lies flat.
                var prefab = Resources.Load<GameObject>("Buildings/Castle");
                if (prefab == null) prefab = Resources.Load<GameObject>("Buildings/CastleMesh");
                if (prefab != null)
                {
                    go = Instantiate(prefab);
                    go.name = type + "_" + level;
                    go.SetActive(true);
                    UrpMaterialUtil.RemapToUrp(go);
                    BuildingFitUtil.ApplyCastleAlbedoIfMissing(go);
                    var cell = GridToWorld(gridX, gridZ);
                    BuildingFitUtil.FitToCell(go, cell, 3.8f, forceUpright: true);
                    BuildingFitUtil.OrientTowardCamera(go, cell);
                    BuildingFitUtil.EnsureClickCollider(go);

                    var rends = go.GetComponentsInChildren<Renderer>(true);
                    var hasMesh = false;
                    if (rends != null)
                    {
                        foreach (var r in rends)
                        {
                            var mf = r.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null) { hasMesh = true; break; }
                        }
                    }
                    if (!hasMesh)
                    {
                        Debug.LogWarning("[MiniMvp] Castle mesh missing after load — greybox fallback");
                        Destroy(go);
                        go = BuildingVisualFactory.CreateGreybox(type, level, GridToWorld(gridX, gridZ));
                    }
                    else
                    {
                        SetStatus("Castle L1 3D yuklandi");
                        Debug.Log("[MiniMvp] Castle 3D at " + go.transform.position + " scale=" + go.transform.localScale);
                    }
                }
                else
                {
                    Debug.LogWarning("[MiniMvp] Buildings/Castle missing — greybox");
                    go = BuildingVisualFactory.CreateGreybox(type, level, GridToWorld(gridX, gridZ));
                }
            }
            else
            {
                go = BuildingVisualFactory.Create(type, level, GridToWorld(gridX, gridZ));
            }

            // Keep buildings under Village/Gameplay so scene cleanup never orphans them.
            var gameplay = GameObject.Find("Village/Gameplay");
            if (gameplay != null)
            {
                var folder = gameplay.transform.Find("Buildings");
                if (folder == null)
                {
                    var folderGo = new GameObject("Buildings");
                    folderGo.transform.SetParent(gameplay.transform, false);
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

            BuildingFitUtil.EnsureClickCollider(go);

            var click = go.GetComponent<BuildingClickRelay>();
            if (click == null) click = go.AddComponent<BuildingClickRelay>();
            click.onClick = () =>
            {
                _selectedBuildingId = id;
                BuildingSelectFx.Select(go);
                if (_cocCamera != null)
                    _cocCamera.FocusSmooth(GridToWorld(gridX, gridZ));
                SetStatus("Selected: " + PrettyType(type) + " L" + level);
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

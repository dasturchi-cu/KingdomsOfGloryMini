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
        [Tooltip("Editor/dev: http://127.0.0.1:3000. Release builds should use HTTPS.")]
        [SerializeField] string baseUrl = "http://127.0.0.1:3000";

        [Header("Grid")]
        [SerializeField] int gridSize = 15;
        [SerializeField] float cellSize = 1.2f;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (FindFirstObjectByType<MiniMvpApp>() != null) return;
            var go = new GameObject("MiniMvpApp");
            go.AddComponent<MiniMvpApp>();
        }

        void Awake()
        {
            _api = new ApiClient(baseUrl);
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
            }
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
        }

        static void DestroyLooseSceneCastles()
        {
            // Root-only scan — avoids FindObjectsByType over every Transform in the scene.
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                // Scene-ga qo'lda tashlangan AI prefablar — runtime spawn bilan dublikat.
                // Never destroy runtime-spawned buildings (BuildingMarker).
                if (go.GetComponent<BuildingMarker>() != null) continue;
                var n = go.name;
                if (n == "UzbekCastle" || n.StartsWith("UzbekCastle") ||
                    n == "Castle_L1" || n.StartsWith("Castle_L1") ||
                    n == "Castle" || n.StartsWith("Castle (") ||
                    n == "BaseField_L1" || n == "NatureBorder_L1" ||
                    n == "BaseField" || n == "NatureBorder")
                {
                    Debug.Log("[MiniMvp] Removing loose scene object: " + n);
                    Destroy(go);
                }
            }
        }

        void BuildGround()
        {
            _fieldRoot = FieldVisualBuilder.Build(gridSize, cellSize);

            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<UnityEngine.Camera>();
                cam.tag = "MainCamera";
            }

            _cocCamera = cam.GetComponent<CoCCameraController>();
            if (_cocCamera == null) _cocCamera = cam.gameObject.AddComponent<CoCCameraController>();
            if (cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            _cocCamera.Configure(FieldCenter, FieldWorldSize, 3f);
            _cocCamera.FocusBase(FieldCenter, FieldWorldSize * 0.78f);
            BaseLightingSetup.Apply(FieldCenter);
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
                var buildingCount = 0;
                if (state.buildings != null)
                {
                    foreach (var building in state.buildings)
                    {
                        buildingCount++;
                        SpawnBuilding(building.id, building.type, building.level, building.gridX, building.gridZ);
                        if (building.type == "castle") _selectedBuildingId = building.id;
                        if (building.type == "gold_mine") hasMine = true;
                        if (building.type == "barracks") hasBarracks = true;
                    }
                }

                SetStatus(BuildNextStepHint(hasMine, hasBarracks, buildingCount));
                FrameCameraOnBase();
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
                yield break;
            }

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
                    SetStatus("OK: " + buildingType + " qo'yildi. Gold=" + _gold);
                });
        }

        IEnumerator CollectGold()
        {
            SetBusy(true);
            SetStatus("Collecting...");
            var body = JsonObject(("playerId", SessionStore.PlayerId));
            yield return _api.PostJson("/api/v1/resources/collect", body, SessionStore.Token, null, (code, text) =>
            {
                SetBusy(false);
                if (code < 200 || code >= 300)
                {
                    SetStatus("Collect failed: " + ExtractError(text));
                    return;
                }

                SetStatus("Collected. State yangilanmoqda...");
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
                SetStatus("Trained " + res.trainedQuantity + " barbarians. Total=" + _barbarianCount);
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
                    if (x == 7 && z == 7) continue;
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
                    ApplyCastleAlbedoIfMissing(go);
                    FitBuildingToCell(go, gridX, gridZ, 3.8f, forceUpright: true);
                    OrientCastleTowardCamera(go, gridX, gridZ);
                    EnsureClickCollider(go);

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

            var marker = go.GetComponent<BuildingMarker>();
            if (marker == null) marker = go.AddComponent<BuildingMarker>();
            marker.buildingId = id;
            marker.buildingType = type;
            marker.level = level;
            marker.gridX = gridX;
            marker.gridZ = gridZ;

            EnsureClickCollider(go);

            var click = go.GetComponent<BuildingClickRelay>();
            if (click == null) click = go.AddComponent<BuildingClickRelay>();
            click.onClick = () =>
            {
                _selectedBuildingId = id;
                SetStatus("Selected: " + type + " L" + level);
            };

            _buildingViews[id] = go;
        }

        /// <summary>
        /// Scales and plants a prefab so its footprint fits one grid cell and sits on the ground.
        /// forceUpright: always pick the tallest orientation (Tripo FBX often lies flat).
        /// </summary>
        void FitBuildingToCell(GameObject go, int gridX, int gridZ, float targetFootprint, bool forceUpright = false)
        {
            go.transform.localScale = Vector3.one;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                go.transform.position = GridToWorld(gridX, gridZ);
                go.transform.localScale = Vector3.one * 0.8f;
                return;
            }

            foreach (var r in renderers)
            {
                r.enabled = true;
                r.gameObject.SetActive(true);
            }

            // Prefer child-baked -90° (Castle.prefab). Rotate root only when still flat / forced search.
            var upright = EncapsulateBounds(renderers);
            var footprint0 = Mathf.Max(upright.size.x, upright.size.z, 0.01f);
            var alreadyUpright = upright.size.y >= footprint0 * 0.85f;

            // Always score orientations for castle — picks tallest (avoids double -90 on prefab).
            if (forceUpright || !alreadyUpright)
            {
                var candidates = new[]
                {
                    Quaternion.identity,
                    Quaternion.Euler(-90f, 0f, 0f),
                    Quaternion.Euler(-90f, 90f, 0f),
                    Quaternion.Euler(-90f, -90f, 0f),
                    Quaternion.Euler(-90f, 180f, 0f),
                    Quaternion.Euler(90f, 0f, 0f),
                };

                var bestRot = Quaternion.identity;
                var bestScore = float.NegativeInfinity;
                foreach (var rot in candidates)
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, rot);
                    var b = EncapsulateBounds(renderers);
                    var fp = Mathf.Max(b.size.x, b.size.z, 0.01f);
                    var score = b.size.y - fp * 0.15f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRot = rot;
                    }
                }

                go.transform.SetPositionAndRotation(Vector3.zero, bestRot);
                Debug.Log("[MiniMvp] Castle rot=" + bestRot.eulerAngles + " hScore=" + bestScore.ToString("F2") + " wasUpright=" + alreadyUpright);
            }

            var bounds = EncapsulateBounds(renderers);
            var sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            var scale = targetFootprint / sizeXZ;
            scale = Mathf.Clamp(scale, 0.5f, 25f);
            go.transform.localScale = Vector3.one * scale;

            bounds = EncapsulateBounds(renderers);
            sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            if (Mathf.Abs(sizeXZ - targetFootprint) > 0.05f)
            {
                go.transform.localScale *= targetFootprint / sizeXZ;
                bounds = EncapsulateBounds(renderers);
            }

            // Final sanity: if still flatter than tall, force -90° X once more.
            if (forceUpright && bounds.size.y < Mathf.Max(bounds.size.x, bounds.size.z) * 0.75f)
            {
                var e = go.transform.eulerAngles;
                go.transform.rotation = Quaternion.Euler(e.x - 90f, e.y, e.z);
                bounds = EncapsulateBounds(renderers);
                Debug.LogWarning("[MiniMvp] Castle still flat — forced extra -90 X");
            }

            var cell = GridToWorld(gridX, gridZ);
            var delta = cell - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            go.transform.position += delta;
            Debug.Log("[MiniMvp] Castle fit h=" + bounds.size.y.ToString("F2") + " fp=" + Mathf.Max(bounds.size.x, bounds.size.z).ToString("F2") + " scale=" + go.transform.localScale.x.ToString("F2"));
        }

        /// <summary>
        /// CoC-style: castle faces the camera (front door toward viewer), slightly diagonal.
        /// </summary>
        void OrientCastleTowardCamera(GameObject go, int gridX, int gridZ)
        {
            var cam = UnityEngine.Camera.main;
            var cell = GridToWorld(gridX, gridZ);
            var toCam = (cam != null ? cam.transform.position : cell + new Vector3(12f, 20f, -12f)) - cell;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;

            // Face camera; +25° = "sal qiya" 3/4 view (not flat front-on).
            var yaw = Quaternion.LookRotation(toCam.normalized).eulerAngles.y + 25f;
            var e = go.transform.eulerAngles;
            go.transform.rotation = Quaternion.Euler(e.x, yaw, e.z);

            // Re-plant after yaw (mesh bounds shift on XZ).
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;
            var bounds = EncapsulateBounds(renderers);
            var delta = cell - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            go.transform.position += delta;
        }

        static void ApplyCastleAlbedoIfMissing(GameObject go)
        {
            var albedo = Resources.Load<Texture2D>("Buildings/CastleMeshTextures/Color_5f773bf5-b6c0-45d4-bd53-2e31d222e9f7");
            if (albedo == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null) continue;
                var next = new Material[shared.Length];
                var changed = false;
                for (var i = 0; i < shared.Length; i++)
                {
                    var m = shared[i];
                    if (m == null) { next[i] = null; continue; }
                    if (m.mainTexture != null)
                    {
                        next[i] = m;
                        continue;
                    }
                    // Clone once into URP cache path via Remap — here just assign albedo on a copy.
                    var copy = new Material(m);
                    copy.mainTexture = albedo;
                    if (copy.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", albedo);
                    if (copy.HasProperty("_MainTex")) copy.SetTexture("_MainTex", albedo);
                    next[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = next;
            }
        }

        static Bounds EncapsulateBounds(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
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
                var b = EncapsulateBounds(renderers);
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
                _cocCamera.FocusBase(focus, FieldWorldSize * 0.78f);
        }

        static void EnsureClickCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var box = go.AddComponent<BoxCollider>();
            if (renderers != null && renderers.Length > 0)
            {
                var b = EncapsulateBounds(renderers);
                var localCenter = go.transform.InverseTransformPoint(b.center);
                var lossy = go.transform.lossyScale;
                box.center = localCenter;
                box.size = new Vector3(
                    Mathf.Max(b.size.x / Mathf.Max(lossy.x, 0.001f), 0.5f),
                    Mathf.Max(b.size.y / Mathf.Max(lossy.y, 0.001f), 0.5f),
                    Mathf.Max(b.size.z / Mathf.Max(lossy.z, 0.001f), 0.5f));
            }
            else
            {
                box.size = new Vector3(2.5f, 2.5f, 2.5f);
                box.center = new Vector3(0f, 1.2f, 0f);
            }
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
            if (string.IsNullOrEmpty(text)) return "unknown error";
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

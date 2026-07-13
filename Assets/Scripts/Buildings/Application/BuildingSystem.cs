using System;
using System.Collections;
using System.Collections.Generic;
using KoG.MiniMvp.Network;
using KoG.MiniMvp.World;
using KoG.MiniMvp.App;
using UnityEngine;

namespace KoG.MiniMvp.Buildings
{
    /// <summary>
    /// Production building orchestrator: grid occupancy, placement preview/rotate/cancel,
    /// upgrade / destroy / repair / construction timer. Server is authoritative for mutations.
    /// </summary>
    public sealed class BuildingSystem : MonoBehaviour
    {
        public event Action<string> StatusChanged;
        public event Action StateChanged;
        /// <summary>Fired when preview/relocate snaps to a new grid cell (SFX / haptic).</summary>
        public event Action<bool> PreviewCellChanged;
        /// <summary>Fired after a successful server mutation — host should reload player state.</summary>
        public event Action MutationSucceeded;

        ApiClient _api;
        Func<string> _token;
        Func<string> _playerId;
        BuildingGrid _grid;
        GridOccupancyMap _occupancy;
        readonly PlacementSession _session = new PlacementSession();
        readonly ConstructionTimer _timer = new ConstructionTimer();
        readonly Dictionary<string, BuildingInstance> _instances = new Dictionary<string, BuildingInstance>();
        static readonly Collider[] PhysicsValidationHits = new Collider[32];
        bool _busy;

        GameStateMachine _stateMachine;

        public bool IsPlacing => _stateMachine != null ? _stateMachine.CurrentState == GameState.Building : _session.IsActive;
        public bool IsRelocating => _stateMachine != null ? _stateMachine.CurrentState == GameState.Relocating : !string.IsNullOrEmpty(_relocateBuildingId);
        public bool IsBusy => _busy;
        public bool BlocksCameraPan => IsPlacing || IsRelocating;
        public PlacementSession Session => _session;
        public string RelocatingBuildingId => _relocateBuildingId;
        public IReadOnlyDictionary<string, BuildingInstance> Instances => _instances;

        string _relocateBuildingId;
        GridCoord _relocateOrigin;
        BuildingFootprint _relocateFootprint;
        string _relocateType;
        bool _relocateValid;

        public void Configure(ApiClient api, Func<string> token, Func<string> playerId, BuildingGrid grid, GameStateMachine stateMachine = null)
        {
            _api = api;
            _token = token;
            _playerId = playerId;
            _grid = grid;
            _stateMachine = stateMachine;
            _occupancy = new GridOccupancyMap(grid != null ? grid.GridSize : 20);
        }

        public void SyncFromServer(BuildingDto[] buildings)
        {
            _instances.Clear();
            _occupancy?.Clear();
            if (buildings == null) return;

            for (var i = 0; i < buildings.Length; i++)
            {
                var dto = buildings[i];
                if (dto == null || string.IsNullOrEmpty(dto.id)) continue;
                var inst = new BuildingInstance
                {
                    Id = dto.id,
                    Type = dto.type,
                    Level = dto.level,
                    Anchor = new GridCoord(dto.gridX, dto.gridZ),
                    RotationSteps = dto.rotationSteps,
                    IsUnderConstruction = dto.isUnderConstruction,
                    IsDamaged = dto.isDamaged,
                    ConstructionSecondsLeft = dto.constructionSecondsLeft
                };
                _instances[inst.Id] = inst;
                _occupancy.Occupy(inst.Id, inst.Anchor, inst.Footprint);
            }

            StateChanged?.Invoke();
        }

        /// <summary>Visual-only castle when server omits one — occupy center so placement cannot overlap.</summary>
        public void RegisterLocalCastleFallback(string id, int gridX, int gridZ)
        {
            if (_occupancy == null || string.IsNullOrEmpty(id)) return;
            if (_instances.ContainsKey(id)) return;
            var inst = new BuildingInstance
            {
                Id = id,
                Type = "castle",
                Level = 1,
                Anchor = new GridCoord(gridX, gridZ),
                RotationSteps = 0,
                IsUnderConstruction = false,
                IsDamaged = false,
                ConstructionSecondsLeft = 0
            };
            _instances[inst.Id] = inst;
            _occupancy.Occupy(inst.Id, inst.Anchor, inst.Footprint);
            StateChanged?.Invoke();
        }

        public bool TryGet(string buildingId, out BuildingInstance inst) =>
            _instances.TryGetValue(buildingId, out inst);

        public string DescribeConstruction(string buildingId)
        {
            if (!_instances.TryGetValue(buildingId, out var inst) || !inst.IsUnderConstruction)
                return null;

            if (_timer.TryGetRemaining(buildingId, out var left))
                return left <= 0 ? "Qurilish tayyor — Yangila (tugatish)" : "Qurilish: " + left + "s";

            if (inst.ConstructionSecondsLeft > 0)
            {
                _timer.SyncFromServer(buildingId, inst.ConstructionSecondsLeft);
                return "Qurilish: " + inst.ConstructionSecondsLeft + "s";
            }

            return "Qurilish davom etmoqda";
        }

        public void BindConstructionTimer(string buildingId)
        {
            if (!_instances.TryGetValue(buildingId, out var inst) || !inst.IsUnderConstruction)
            {
                _timer.Clear();
                return;
            }

            _timer.SyncFromServer(buildingId, Math.Max(1, inst.ConstructionSecondsLeft));
        }

        public bool BeginPlacement(string buildingType)
        {
            if (_busy || _occupancy == null || _grid == null)
            {
                Emit("Building system not ready");
                return false;
            }

            if (string.IsNullOrEmpty(buildingType))
            {
                Emit("Building type missing");
                return false;
            }

            foreach (var pair in _instances)
            {
                if (pair.Value.Type == buildingType)
                {
                    Emit(Pretty(buildingType) + " allaqachon bor");
                    return false;
                }
            }

            if (_stateMachine != null)
            {
                if (!_stateMachine.CanTransitionTo(GameState.Building))
                {
                    Emit("Hozir bino qurib bo'lmaydi");
                    return false;
                }
                _stateMachine.TransitionTo(GameState.Building);
            }

            var def = BuildingDefinitionCatalog.GetOrDefault(buildingType);
            var footprint = def.DefaultFootprint;
            FindCastle(out var castle, out var keepOut);
            if (!PlacementRules.TryFindFreeNearCenter(
                    _occupancy, buildingType, footprint, castle, keepOut, out var anchor))
            {
                Emit("Bo'sh katak yo'q");
                PlacePreviewFx.Hide();
                if (_stateMachine != null) _stateMachine.TransitionTo(GameState.Idle);
                return false;
            }

            _session.Begin(buildingType, anchor, footprint, true);
            RefreshPreview();
            Emit("Joylashtirish: tasdiqlang / aylantiring / bekor");
            StateChanged?.Invoke();
            return true;
        }

        public void CancelPlacement()
        {
            if (!_session.IsActive) return;
            _session.Cancel();
            PlacePreviewFx.Hide();
            Emit("Joylashtirish bekor");
            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(GameState.Idle);
            }
            StateChanged?.Invoke();
        }

        public void RotatePlacement()
        {
            if (!_session.IsActive) return;
            var def = BuildingDefinitionCatalog.GetOrDefault(_session.BuildingType);
            if (!def.CanRotate)
            {
                Emit("Bu bino aylanmaydi");
                KoG.MiniMvp.Audio.MiniAudio.PlayError();
                return;
            }

            _session.Rotate(1);
            _session.Revalidate((a, f) => Validate(a, f, _session.BuildingType, null));
            if (!_session.IsValid)
            {
                // Keep rotation but try to nudge to nearest valid with same rotation.
                FindCastle(out var castle, out var keepOut);
                if (PlacementRules.TryFindFreeNearCenter(
                        _occupancy, _session.BuildingType, _session.Footprint, castle, keepOut, out var found))
                    _session.SetAnchor(found, true);
            }

            RefreshPreview();
            StateChanged?.Invoke();
        }

        public void MovePreviewToWorld(Vector3 world)
        {
            if (!_session.IsActive || _grid == null) return;

            // Clamp dynamically so that the entire footprint is guaranteed to stay within the grid boundaries
            int x = Mathf.Clamp(Mathf.RoundToInt(world.x / _grid.CellSize), 0, _grid.GridSize - _session.Footprint.OccupiedWidth);
            int z = Mathf.Clamp(Mathf.RoundToInt(world.z / _grid.CellSize), 0, _grid.GridSize - _session.Footprint.OccupiedDepth);

            var anchor = new GridCoord(x, z);
            var valid = Validate(anchor, _session.Footprint, _session.BuildingType, null);
            if (!_session.WouldChangeAnchor(anchor, valid)) return;
            var cellMoved = !anchor.Equals(_session.Anchor);
            _session.SetAnchor(anchor, valid);
            RefreshPreview();
            if (cellMoved) PreviewCellChanged?.Invoke(valid);
            StateChanged?.Invoke();
        }

        /// <summary>Start CoC-style drag relocate for any building (castle, mine, barracks, future types).</summary>
        public bool BeginRelocate(string buildingId)
        {
            if (_busy || _occupancy == null || _grid == null) return false;
            if (_session.IsActive || IsRelocating) return false;
            if (!_instances.TryGetValue(buildingId, out var inst)) return false;

            if (_stateMachine != null)
            {
                if (!_stateMachine.CanTransitionTo(GameState.Relocating)) return false;
                _stateMachine.TransitionTo(GameState.Relocating);
            }

            _relocateBuildingId = buildingId;
            _relocateOrigin = inst.Anchor;
            _relocateFootprint = inst.Footprint;
            _relocateType = inst.Type;
            _occupancy.Free(buildingId);
            // While castle is mid-relocate, keep-out uses its preview anchor (SetAnchorPreview).
            _relocateValid = Validate(inst.Anchor, _relocateFootprint, _relocateType, _relocateBuildingId);
            RefreshRelocatePreview(inst.Anchor);
            Emit("Sudrab joylang — qo‘yib yuboring");
            StateChanged?.Invoke();
            return true;
        }

        public void MoveRelocateToWorld(Vector3 world)
        {
            if (!IsRelocating || _grid == null) return;

            // Clamp dynamically so that the entire footprint is guaranteed to stay within the grid boundaries
            int x = Mathf.Clamp(Mathf.RoundToInt(world.x / _grid.CellSize), 0, _grid.GridSize - _relocateFootprint.OccupiedWidth);
            int z = Mathf.Clamp(Mathf.RoundToInt(world.z / _grid.CellSize), 0, _grid.GridSize - _relocateFootprint.OccupiedDepth);

            var anchor = new GridCoord(x, z);
            var valid = Validate(anchor, _relocateFootprint, _relocateType, _relocateBuildingId);
            var prevAnchor = default(GridCoord);
            var hadPrev = _instances.TryGetValue(_relocateBuildingId, out var inst);
            if (hadPrev) prevAnchor = inst.Anchor;
            if (hadPrev && prevAnchor.Equals(anchor) && _relocateValid == valid) return;

            var cellMoved = !hadPrev || !prevAnchor.Equals(anchor);
            _relocateValid = valid;
            RefreshRelocatePreview(anchor);
            // Stash desired anchor on the instance temporarily for visual sync.
            if (hadPrev) inst.SetAnchorPreview(anchor);
            if (cellMoved) PreviewCellChanged?.Invoke(valid);
            StateChanged?.Invoke();
        }

        public void CancelRelocate()
        {
            if (!IsRelocating) return;
            if (_instances.TryGetValue(_relocateBuildingId, out var inst))
            {
                inst.SetAnchorPreview(_relocateOrigin);
                _occupancy.Occupy(_relocateBuildingId, _relocateOrigin, _relocateFootprint);
            }
            // Sync visual while still IsRelocating, then clear.
            StateChanged?.Invoke();
            ClearRelocate();
            PlacePreviewFx.Hide();
            Emit("Ko‘chirish bekor");
            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(GameState.Selecting);
            }
            StateChanged?.Invoke();
        }

        public IEnumerator ConfirmRelocate()
        {
            if (!IsRelocating)
                yield break;

            if (!_instances.TryGetValue(_relocateBuildingId, out var inst))
            {
                ClearRelocate();
                if (_stateMachine != null) _stateMachine.TransitionTo(GameState.Idle);
                yield break;
            }

            var target = inst.Anchor;
            if (!_relocateValid)
            {
                Emit("Bu katak band — qaytarildi");
                KoG.MiniMvp.Audio.MiniAudio.PlayError();
                PlacePreviewFx.RejectPulse();
                inst.SetAnchorPreview(_relocateOrigin);
                _occupancy.Occupy(_relocateBuildingId, _relocateOrigin, _relocateFootprint);
                StateChanged?.Invoke();
                ClearRelocate();
                PlacePreviewFx.Hide();
                if (_stateMachine != null) _stateMachine.TransitionTo(GameState.Selecting);
                StateChanged?.Invoke();
                yield break;
            }

            if (target.X == _relocateOrigin.X && target.Z == _relocateOrigin.Z)
            {
                _occupancy.Occupy(_relocateBuildingId, _relocateOrigin, _relocateFootprint);
                StateChanged?.Invoke();
                ClearRelocate();
                PlacePreviewFx.Hide();
                if (_stateMachine != null) _stateMachine.TransitionTo(GameState.Selecting);
                StateChanged?.Invoke();
                yield break;
            }

            if (_api == null)
            {
                Emit("API yo‘q");
                CancelRelocate();
                yield break;
            }

            var buildingId = _relocateBuildingId;
            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(GameState.Busy);
            }
            _busy = true;
            Emit("Ko‘chirilmoqda...");
            var body = BuildingJson.Object(
                ("playerId", _playerId()),
                ("buildingId", buildingId),
                ("gridX", target.X.ToString()),
                ("gridZ", target.Z.ToString())
            );

            yield return _api.PostJson(
                "/api/v1/buildings/move",
                body,
                _token(),
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        Emit("Ko‘chirish xato: " + BuildingJson.ExtractError(text));
                        if (_instances.TryGetValue(buildingId, out var failInst))
                            failInst.SetAnchorPreview(_relocateOrigin);
                        _occupancy.Occupy(buildingId, _relocateOrigin, _relocateFootprint);
                        StateChanged?.Invoke();
                        ClearRelocate();
                        PlacePreviewFx.Hide();
                        if (_stateMachine != null) _stateMachine.TransitionTo(GameState.Selecting);
                        StateChanged?.Invoke();
                        return;
                    }

                    if (_instances.TryGetValue(buildingId, out var okInst))
                    {
                        okInst.CommitAnchor(target);
                        _occupancy.Occupy(buildingId, target, okInst.Footprint);
                    }
                    ClearRelocate();
                    PlacePreviewFx.Hide();
                    KoG.MiniMvp.Audio.MiniAudio.PlayPlace();
                    Emit("Joyiga qo‘yildi");
                    if (_stateMachine != null) _stateMachine.TransitionTo(GameState.Selecting);
                    MutationSucceeded?.Invoke();
                    StateChanged?.Invoke();
                });
        }

        void ClearRelocate()
        {
            _relocateBuildingId = null;
            _relocateType = null;
            _relocateValid = false;
        }

        void RefreshRelocatePreview(GridCoord anchor)
        {
            if (_grid == null) return;
            var world = _grid.GridToWorld(anchor.X, anchor.Z);
            PlacePreviewFx.ShowFootprint(
                world,
                _grid.CellSize,
                _relocateFootprint.Width,
                _relocateFootprint.Depth,
                _relocateFootprint.YawDegrees,
                _relocateValid,
                _relocateType);
        }

        void RefreshPreview()
        {
            if (!_session.IsActive || _grid == null)
            {
                PlacePreviewFx.Hide();
                return;
            }

            var world = _grid.GridToWorld(_session.Anchor.X, _session.Anchor.Z);
            PlacePreviewFx.ShowFootprint(
                world,
                _grid.CellSize,
                _session.Footprint.Width,
                _session.Footprint.Depth,
                _session.Footprint.YawDegrees,
                _session.IsValid,
                _session.BuildingType);
        }

        public IEnumerator ConfirmPlacement()
        {
            if (!_session.IsActive)
            {
                Emit("Joylashtirish aktiv emas");
                yield break;
            }

            if (!_session.IsValid)
            {
                Emit("Bu katak band yoki taqiqlangan");
                KoG.MiniMvp.Audio.MiniAudio.PlayError();
                PlacePreviewFx.RejectPulse();
                yield break;
            }

            if (_api == null)
            {
                Emit("API yo'q");
                yield break;
            }

            var type = _session.BuildingType;
            var anchor = _session.Anchor;
            var rotation = _session.Footprint.RotationSteps;
            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(GameState.Busy);
            }
            _busy = true;
            Emit("Placing " + type + "...");

            var body = BuildingJson.Object(
                ("playerId", _playerId()),
                ("buildingType", type),
                ("gridX", anchor.X.ToString()),
                ("gridZ", anchor.Z.ToString()),
                ("rotationSteps", rotation.ToString())
            );

            yield return _api.PostJson(
                "/api/v1/buildings/place",
                body,
                _token(),
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        Emit("Place failed: " + BuildingJson.ExtractError(text));
                        KoG.MiniMvp.Audio.MiniAudio.PlayError();
                        PlacePreviewFx.RejectPulse();
                        RefreshPreview();
                        if (_stateMachine != null) _stateMachine.TransitionTo(GameState.Building);
                        return;
                    }

                    _session.Cancel();
                    PlacePreviewFx.Hide();
                    KoG.MiniMvp.Audio.MiniAudio.PlayPlace();
                    Emit("OK: " + Pretty(type) + " qo'yildi");
                    if (_stateMachine != null) _stateMachine.TransitionTo(GameState.Idle);
                    MutationSucceeded?.Invoke();
                    StateChanged?.Invoke();
                });
        }

        public IEnumerator UpgradeSelected(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                Emit("Avval binoni tanlang");
                yield break;
            }

            _busy = true;
            Emit("Upgrading...");
            var body = BuildingJson.Object(
                ("playerId", _playerId()),
                ("buildingId", buildingId)
            );

            yield return _api.PostJson(
                "/api/v1/buildings/upgrade",
                body,
                _token(),
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        Emit("Upgrade failed: " + BuildingJson.ExtractError(text));
                        return;
                    }

                    if (text != null && (text.IndexOf("\"finished\":true", StringComparison.Ordinal) >= 0
                        || text.IndexOf("finishedEarly", StringComparison.Ordinal) >= 0))
                    {
                        var done = JsonUtility.FromJson<UpgradeResponse>(text);
                        Emit("Qurilish tugadi — L" + Math.Max(1, done.nextLevel));
                        _timer.Clear();
                    }
                    else if (text != null && text.IndexOf("upgradeSeconds", StringComparison.Ordinal) >= 0)
                    {
                        var res = JsonUtility.FromJson<UpgradeResponse>(text);
                        Emit("Yangilandi (" + res.upgradeSeconds + "s) — kuting yoki Tezkor ◇");
                        _timer.SyncFromServer(buildingId, Math.Max(1, res.upgradeSeconds));
                    }
                    else
                        Emit("Yangilash boshlandi");

                    MutationSucceeded?.Invoke();
                    StateChanged?.Invoke();
                });
        }

        public IEnumerator SpeedupSelected(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                Emit("Avval binoni tanlang");
                yield break;
            }

            _busy = true;
            Emit("Tezkor ◇…");
            var body = BuildingJson.Object(
                ("playerId", _playerId()),
                ("buildingId", buildingId)
            );

            yield return _api.PostJson(
                "/api/v1/buildings/speedup",
                body,
                _token(),
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        Emit("Tezkor xato: " + BuildingJson.ExtractError(text));
                        return;
                    }

                    _timer.Clear();
                    Emit("Tezkor tugadi");
                    MutationSucceeded?.Invoke();
                    StateChanged?.Invoke();
                });
        }

        public IEnumerator CancelUpgrade(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                Emit("Avval binoni tanlang");
                yield break;
            }

            _busy = true;
            Emit("Cancel upgrade...");
            var body = BuildingJson.Object(
                ("playerId", _playerId()),
                ("buildingId", buildingId)
            );

            yield return _api.PostJson(
                "/api/v1/buildings/cancel-upgrade",
                body,
                _token(),
                null,
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        Emit("Cancel failed: " + BuildingJson.ExtractError(text));
                        return;
                    }

                    _timer.Clear();
                    Emit("Upgrade bekor qilindi");
                    MutationSucceeded?.Invoke();
                    StateChanged?.Invoke();
                });
        }

        public IEnumerator DestroySelected(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                Emit("Avval binoni tanlang");
                yield break;
            }

            if (_instances.TryGetValue(buildingId, out var inst))
            {
                var def = BuildingDefinitionCatalog.GetOrDefault(inst.Type);
                if (!def.CanDestroy)
                {
                    Emit("Castle o'chirilmaydi");
                    yield break;
                }
            }

            _busy = true;
            Emit("Destroying...");
            var body = BuildingJson.Object(
                ("playerId", _playerId()),
                ("buildingId", buildingId)
            );

            yield return _api.PostJson(
                "/api/v1/buildings/destroy",
                body,
                _token(),
                ApiClient.NewIdempotencyKey(),
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        Emit("Destroy failed: " + BuildingJson.ExtractError(text));
                        return;
                    }

                    _occupancy.Free(buildingId);
                    _instances.Remove(buildingId);
                    Emit("Bino o'chirildi");
                    MutationSucceeded?.Invoke();
                    StateChanged?.Invoke();
                });
        }

        public IEnumerator RepairSelected(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                Emit("Avval binoni tanlang");
                yield break;
            }

            if (_instances.TryGetValue(buildingId, out var inst) && !inst.IsDamaged)
            {
                Emit("Bino shikastlanmagan");
                yield break;
            }

            _busy = true;
            Emit("Repairing...");
            var body = BuildingJson.Object(
                ("playerId", _playerId()),
                ("buildingId", buildingId)
            );

            yield return _api.PostJson(
                "/api/v1/buildings/repair",
                body,
                _token(),
                null,
                (code, text) =>
                {
                    _busy = false;
                    if (code < 200 || code >= 300)
                    {
                        Emit("Repair failed: " + BuildingJson.ExtractError(text));
                        return;
                    }

                    Emit("Ta'mirlandi");
                    MutationSucceeded?.Invoke();
                    StateChanged?.Invoke();
                });
        }

        public Vector3 AnchorToWorld(GridCoord anchor)
        {
            if (_grid == null) return new Vector3(anchor.X, 0f, anchor.Z);
            return _grid.GridToWorld(anchor.X, anchor.Z);
        }

        bool Validate(GridCoord anchor, BuildingFootprint footprint, string buildingType, string ignoreBuildingId)
        {
            FindCastle(out var castle, out var keepOut);
            if (buildingType == "castle" && keepOut <= 0)
            {
                keepOut = BuildingDefinitionCatalog.GetOrDefault("castle").KeepOutChebyshev;
            }
            
            // 1. Grid Validation
            if (!PlacementRules.CanPlace(_occupancy, buildingType, anchor, footprint, castle, keepOut))
                return false;

            // 2. Physics Collision Validation
            if (_grid == null) return true;
            
            float cx = _grid.GridToWorld(anchor.X, anchor.Z).x;
            float cz = _grid.GridToWorld(anchor.X, anchor.Z).z;
            
            Vector3 center = new Vector3(
                cx + (footprint.OccupiedWidth * _grid.CellSize) / 2f, 
                5f, 
                cz + (footprint.OccupiedDepth * _grid.CellSize) / 2f
            );
            
            Vector3 halfExtents = new Vector3(
                (footprint.OccupiedWidth * _grid.CellSize) / 2f - 0.1f, 
                5f, 
                (footprint.OccupiedDepth * _grid.CellSize) / 2f - 0.1f
            );
            
            int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, PhysicsValidationHits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = PhysicsValidationHits[i];
                var hitObj = hit.gameObject;
                string hitName = hitObj.name;
                
                // Ignore standard terrain/ground
                if (hitName == "Ground" || hitName == "Terrain" || hitName == "Plane" || hitName.Contains("Ground") || hitName.Contains("Terrain")) 
                    continue;
                    
                // Ignore building being relocated
                if (!string.IsNullOrEmpty(ignoreBuildingId))
                {
                    var marker = hit.GetComponentInParent<BuildingMarker>();
                    if (marker != null && marker.buildingId == ignoreBuildingId)
                        continue;
                }

                // Ignore placement visualizers
                if (hit.GetComponentInParent<KoG.MiniMvp.World.PlacePreviewFx>() != null) 
                    continue;

                // Explicit block list or any other collider blocks placement
                return false;
            }

            return true;
        }

        void FindCastle(out GridCoord? castle, out int keepOut)
        {
            castle = null;
            keepOut = 0;
            foreach (var pair in _instances)
            {
                if (pair.Value.Type != "castle") continue;
                castle = pair.Value.Anchor;
                keepOut = BuildingDefinitionCatalog.GetOrDefault("castle").KeepOutChebyshev;
                return;
            }
        }

        void Emit(string msg)
        {
            StatusChanged?.Invoke(msg);
        }

        static string Pretty(string type)
        {
            if (type == "gold_mine") return "Gold Mine";
            if (type == "barracks") return "Barracks";
            if (type == "castle") return "Castle";
            return type;
        }
    }

    static class BuildingJson
    {
        public static string Object(params (string key, string value)[] pairs)
        {
            var sb = new System.Text.StringBuilder(128);
            sb.Append('{');
            for (var i = 0; i < pairs.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(pairs[i].key).Append("\":");
                if (int.TryParse(pairs[i].value, out _) || long.TryParse(pairs[i].value, out _) ||
                    pairs[i].value == "true" || pairs[i].value == "false")
                    sb.Append(pairs[i].value);
                else
                    sb.Append('"').Append(Escape(pairs[i].value)).Append('"');
            }

            sb.Append('}');
            return sb.ToString();
        }

        public static string ExtractError(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "backend javob bermadi";
            try
            {
                var err = JsonUtility.FromJson<ApiError>(text);
                if (!string.IsNullOrEmpty(err.message)) return err.message;
                if (!string.IsNullOrEmpty(err.error)) return err.error;
            }
            catch
            {
                // fall through
            }

            return text.Length > 120 ? text.Substring(0, 120) : text;
        }

        static string Escape(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

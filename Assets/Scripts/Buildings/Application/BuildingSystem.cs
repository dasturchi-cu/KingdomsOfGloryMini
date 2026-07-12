using System;
using System.Collections;
using System.Collections.Generic;
using KoG.MiniMvp.Network;
using KoG.MiniMvp.World;
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
        bool _busy;

        public bool IsPlacing => _session.IsActive;
        public bool IsBusy => _busy;
        public PlacementSession Session => _session;
        public IReadOnlyDictionary<string, BuildingInstance> Instances => _instances;

        public void Configure(ApiClient api, Func<string> token, Func<string> playerId, BuildingGrid grid)
        {
            _api = api;
            _token = token;
            _playerId = playerId;
            _grid = grid;
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

        public bool TryGet(string buildingId, out BuildingInstance inst) =>
            _instances.TryGetValue(buildingId, out inst);

        public string DescribeConstruction(string buildingId)
        {
            if (!_instances.TryGetValue(buildingId, out var inst) || !inst.IsUnderConstruction)
                return null;

            if (_timer.TryGetRemaining(buildingId, out var left))
                return left <= 0 ? "Qurilish tayyor — Upgrade bosing" : "Qurilish: " + left + "s";

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

            var def = BuildingDefinitionCatalog.GetOrDefault(buildingType);
            var footprint = def.DefaultFootprint;
            FindCastle(out var castle, out var keepOut);
            if (!PlacementRules.TryFindFreeNearCenter(
                    _occupancy, buildingType, footprint, castle, keepOut, out var anchor))
            {
                Emit("Bo'sh katak yo'q");
                PlacePreviewFx.Hide();
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
            StateChanged?.Invoke();
        }

        public void RotatePlacement()
        {
            if (!_session.IsActive) return;
            _session.Rotate(1);
            _session.Revalidate((a, f) => Validate(a, f, _session.BuildingType));
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
            if (!_grid.WorldToGrid(world, out var x, out var z))
            {
                x = Mathf.Clamp(Mathf.RoundToInt(world.x / _grid.CellSize), 0, _grid.GridSize - 1);
                z = Mathf.Clamp(Mathf.RoundToInt(world.z / _grid.CellSize), 0, _grid.GridSize - 1);
            }

            var anchor = new GridCoord(x, z);
            var valid = Validate(anchor, _session.Footprint, _session.BuildingType);
            _session.SetAnchor(anchor, valid);
            RefreshPreview();
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
                        RefreshPreview();
                        return;
                    }

                    _session.Cancel();
                    PlacePreviewFx.Hide();
                    KoG.MiniMvp.Audio.MiniAudio.PlayPlace();
                    Emit("OK: " + Pretty(type) + " qo'yildi");
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

                    if (text != null && text.IndexOf("finishedEarly", StringComparison.Ordinal) >= 0)
                        Emit("Upgrade tayyor (instant)");
                    else if (text != null && text.IndexOf("upgradeSeconds", StringComparison.Ordinal) >= 0)
                    {
                        var res = JsonUtility.FromJson<UpgradeResponse>(text);
                        Emit("Upgrade boshlandi (" + res.upgradeSeconds + "s) — yana Upgrade = tugatish");
                        _timer.SyncFromServer(buildingId, Math.Max(1, res.upgradeSeconds));
                    }
                    else
                        Emit("Upgrade boshlandi");

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

        bool Validate(GridCoord anchor, BuildingFootprint footprint, string buildingType)
        {
            FindCastle(out var castle, out var keepOut);
            return PlacementRules.CanPlace(_occupancy, buildingType, anchor, footprint, castle, keepOut);
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
                _session.Footprint.OccupiedWidth,
                _session.Footprint.OccupiedDepth,
                _session.Footprint.YawDegrees,
                _session.IsValid);
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

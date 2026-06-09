using System.Runtime.InteropServices;
using NumVec2 = System.Numerics.Vector2;
using POE2Radar.Core;
using POE2Radar.Core.Cheats;
using POE2Radar.Core.Game;
using POE2Radar.Core.Pathfinding;
using static POE2Radar.Core.Pathfinding.ExplorationTracker;
using POE2Radar.Overlay.Input;
using POE2Radar.Overlay.Native;
using POE2Radar.Overlay.Automation;
using POE2Radar.Overlay.Web;

#pragma warning disable CA1416

namespace POE2Radar.Overlay;

public sealed class RadarApp : IDisposable
{
    private const int TargetHz = 144;
    private const int WorldHz = 30;

    private readonly ProcessHandle _process;
    private readonly MemoryReader _reader;
    private readonly Poe2Live _live;
    private readonly Poe2Atlas _atlas;
    private readonly CheatManager _cheats;
    private readonly OverlayWindow _window;
    private readonly OverlayRenderer _renderer;
    private readonly WatchedEntities _watched;
    private readonly HiddenEntities _hidden;
    private readonly PathingTargets _pathing;
    private readonly AutoRuleEngine _autoRules;
    private readonly ApiServer _api;
    private readonly RadarSettings _radarSettings;
    private readonly EntityNameResolver _entityNames;
    private readonly GameDataService _gameData;
    private SettingsForm? _settingsForm;
    private volatile RadarState _state = RadarState.Empty;

    private DateTime _worldAt = DateTime.MinValue;
    private List<Poe2Live.EntityDot> _entities = new();
    private IReadOnlyList<Poe2Live.Landmark> _landmarks = Array.Empty<Poe2Live.Landmark>();
    private Poe2Live.TerrainData? _terrain;
    private uint _areaHash;
    private nint _lastAreaInstance;
    private nint _gameHwnd;
    private volatile bool _shutdown;

    private DateTime _nextKeyAt = DateTime.MinValue;
    private List<(int X, int Y)>? _pathPoints;
    private NumVec2 _lastPathPlayerGrid;
    private string _lastPathTarget = "";
    private string? _pathTargetName;
    private string? _manualPathPattern;
    private (int X, int Y)? _manualPathGridTarget;
    private readonly List<(float ScreenX, float ScreenY, string Metadata)> _entityScreenPos = new();
    private readonly ExplorationTracker _exploration = new();
    private string? _inspectedEntity;
    private string? _inspectedMeta;
    private DateTime _inspectedAt;
    private readonly List<(float ScreenX, float ScreenY, float GridX, float GridY, string Name)> _landmarkScreenPos = new();

    private const int LifeVk = 0x31, ManaVk = 0x32;
    private static readonly TimeSpan LifeCooldown = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan ManaCooldown = TimeSpan.FromMilliseconds(2000);
    private bool _autoFlask = true;
    private DateTime _lifeFiredAt = DateTime.MinValue, _manaFiredAt = DateTime.MinValue;
    private DateTime _nextToggleAt = DateTime.MinValue;
    private float _hpPct = 100f, _manaPct = 100f;
    private string _flaskNote = "";
    private string _areaCode = "", _charName = "";
    private string? _areaName;
    private int _areaAct;
    private bool _isTown;
    private int _charLevel;
    private float[]? _cameraMatrix;
    private bool _overlayVisible = true;
    private List<Poe2Atlas.AtlasNodeLive> _atlasNodes = new();
    private readonly object _atlasLock = new();
    private readonly HashSet<string> _atlasPinned = new(StringComparer.Ordinal);
    private List<AtlasMark> _atlasMarks = new();

    private DateTime _nextCheatKeyAt = DateTime.MinValue;
    private static readonly (int Vk, string Name)[] CheatKeys =
    [
        (0x70, "NoAtlasFog"),        // F1
        (0x71, "RevealMap"),         // F2
        (0x72, "InfiniteZoom"),      // F3
        (0x73, "EnemyHealthBars"),   // F4
        (0x74, "PlayerLightRadius"), // F5
    ];

    public void RequestShutdown() => _shutdown = true;

    public RadarApp(ProcessHandle process, MemoryReader reader, nint gameStateSlot)
    {
        _process = process;
        _reader = reader;
        _live = new Poe2Live(reader, gameStateSlot);
        _atlas = new Poe2Atlas(reader);
        _cheats = new CheatManager(process, reader);
        Console.WriteLine("\nScanning cheat patterns...");
        _cheats.ScanAndResolve();
        Console.WriteLine("Hotkeys: F1-F5 cheats, F8 flask, F9 settings, F10 overlay, F11 web dashboard\n");
        _window = OverlayWindow.Create();
        _renderer = new OverlayRenderer(_window);
        var configDir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "config");
        _radarSettings = RadarSettings.Load(Path.Combine(configDir, "radar_settings.json"));
        _watched = new WatchedEntities(Path.Combine(configDir, "watched_entities.json"));
        _hidden = new HiddenEntities(Path.Combine(configDir, "hidden_entities.json"));
        _pathing = new PathingTargets(Path.Combine(configDir, "pathing_targets.json"));
        _autoRules = new AutoRuleEngine(Path.Combine(configDir, "auto_rules.json"));
        _entityNames = new EntityNameResolver(Path.Combine(configDir, "entity_names.json"));
        _gameData = new GameDataService(configDir);
        ComponentFieldReader? inspector = null;
        var idaOffsetsPath = Path.Combine(configDir, "OtIdaOffsets.json");
        if (File.Exists(idaOffsetsPath))
        {
            try
            {
                inspector = new ComponentFieldReader(idaOffsetsPath, _live, _reader);
                Console.WriteLine($"Inspector loaded: {inspector.ComponentNames.Count} components from OtIdaOffsets.json");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Inspector disabled: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Inspector disabled: config\\OtIdaOffsets.json not found");
        }

        _api = new ApiServer(() => _state, _watched, _hidden, _radarSettings, _pathing, _autoRules, inspector, _entityNames, _gameData,
            GetAtlasDashboard, SetAtlasPins);
        try { _api.Start(); Console.WriteLine("API on http://localhost:7777 (/state, /entities, /api/inspect)"); }
        catch (Exception ex) { Console.Error.WriteLine($"API server disabled: {ex.Message}"); }
    }

    public void Run()
    {
        var targetMs = 1000 / TargetHz;
        _gameHwnd = OverlayNative.FindWindowForProcess(_process.ProcessId);
        while (!_shutdown)
        {
            if (_gameHwnd == 0) _gameHwnd = OverlayNative.FindWindowForProcess(_process.ProcessId);
            if (_gameHwnd != 0) _window.TrackGameWindow(_gameHwnd);
            if (!_window.PumpMessages()) break;
            Tick();
            Thread.Sleep(targetMs);
        }
    }

    private void Tick()
    {
        HandleCalibrationKeys();
        HandleCheatKeys();
        HandleSettingsToggle();
        HandleAltClick();
        HandleShiftInspect();

        var inGame = _live.TryResolve(out var inGameState, out var areaInstance, out var localPlayer);
        var player = NumVec2.Zero;
        POE2Radar.Core.Game.Vector3? playerWorld = null;
        var map = default(Poe2Live.MapUi);
        Poe2Live.AtlasSnapshot? atlas = null;
        IReadOnlyList<Poe2Atlas.AtlasNodeLive>? atlasNodes = null;
        var areaLevel = 0;

        if (inGame)
        {
            if (areaInstance != _lastAreaInstance) { _terrain = null; _lastAreaInstance = areaInstance; }
            _areaHash = _live.AreaHash(areaInstance);
            areaLevel = _live.AreaLevel(areaInstance);

            playerWorld = _live.PlayerWorld(localPlayer);
            player = playerWorld is { } pw
                ? new NumVec2(pw.X / Poe2.WorldToGridRatio, pw.Y / Poe2.WorldToGridRatio)
                : _live.PlayerGrid(localPlayer) ?? NumVec2.Zero;
            _exploration.Update(player.X, player.Y, areaInstance);
            map = _live.ReadMap(inGameState, areaInstance);
            _areaCode = _live.AreaCode(areaInstance);
            var area = _gameData.GetArea(_areaCode);
            _areaName = area?.Name;
            _areaAct = area?.Act ?? 0;
            _isTown = area?.Town ?? false;
            _charName = _live.PlayerName(localPlayer);
            _charLevel = _live.PlayerLevel(localPlayer);
            _cameraMatrix = _live.CameraMatrix(inGameState);
            if (_radarSettings.ShowAtlasNodes)
            {
                _atlasNodes = _atlas.ReadNodes(inGameState);
                atlasNodes = _atlasNodes;
                _atlasMarks = BuildAtlasMarks(_atlasNodes);
                if (_atlasNodes.Count == 0 && _live.TryReadAtlasSnapshot(inGameState, out var atlasSnapshot))
                    atlas = atlasSnapshot;
            }
            else if (_atlasMarks.Count != 0)
            {
                _atlasMarks = new();
            }
            TickAutoFlask(localPlayer);

            var now = DateTime.UtcNow;
            if ((now - _worldAt).TotalMilliseconds >= 1000.0 / WorldHz)
            {
                _worldAt = now;
                _terrain ??= _live.Terrain(areaInstance);
                _entities = _live.Entities(areaInstance);
                _landmarks = _live.Landmarks(areaInstance);
                UpdatePath(player);
            }
        }

        _state = new RadarState(inGame, _areaHash, areaLevel, map.IsVisible, map.Zoom, player, _entities, _landmarks,
            _hpPct, _manaPct, _autoFlask, _flaskNote, _areaCode, _charName, _charLevel,
            _areaName, _areaAct, _isTown, _gameData.GetArea(_areaCode)?.Waypoint ?? false,
            map.ShiftX, map.ShiftY,
            _live.GameMinimap.Available, _live.GameMinimap.ShiftX, _live.GameMinimap.ShiftY, _live.GameMinimap.Zoom);

        var ctx = new RenderContext(
            InGame: inGame,
            Active: _gameHwnd != 0 && GetForegroundWindow() == _gameHwnd,
            WindowWidth: _window.Width,
            WindowHeight: _window.Height,
            PlayerGrid: player,
            Map: map,
            Entities: _entities,
            Landmarks: _landmarks,
            AreaHash: _areaHash,
            Terrain: _terrain,
            ScaleMul: _radarSettings.ScaleMul,
            OffsetX: _radarSettings.OffsetX,
            OffsetY: _radarSettings.OffsetY,
            HpPct: _hpPct,
            ManaPct: _manaPct,
            FlaskNote: _flaskNote,
            AreaCode: _areaCode,
            CharLevel: _charLevel,
            CameraMatrix: _cameraMatrix,
            CheatStatus: _cheats.GetStatus(),
            Radar: _radarSettings,
            OverlayVisible: _overlayVisible,
            Watched: _watched,
            PathPoints: _pathPoints,
            EntityScreenPositions: _entityScreenPos,
            LandmarkScreenPositions: _landmarkScreenPos,
            Exploration: _exploration,
            InspectedName: _inspectedEntity,
            InspectedMeta: _inspectedMeta,
            PathTargetName: _pathTargetName,
            EntityNames: _entityNames,
            AreaName: _areaName,
            AreaAct: _areaAct,
            IsTown: _isTown,
            CharName: _charName,
            MapPins: _gameData.GetPins(_areaCode),
            GameData: _gameData,
            GameMinimap: _live.GameMinimap,
            Hidden: _hidden,
            PlayerWorld: playerWorld,
            Atlas: atlas,
            AtlasNodes: atlasNodes,
            AtlasMarks: _atlasMarks,
            AtlasLoadingText: _radarSettings.ShowAtlasNodes && _atlas.LastPanelOpen && _atlas.LoadProgress is > 0f and < 1f
                ? _atlas.LoadStatus
                : null,
            AtlasLoadingProgress: _atlas.LoadProgress);
        _renderer.Render(ctx);
    }

    private List<AtlasMark> BuildAtlasMarks(IReadOnlyList<Poe2Atlas.AtlasNodeLive> nodes)
    {
        if (nodes.Count == 0) return new List<AtlasMark>();

        HashSet<string> pinned;
        lock (_atlasLock) pinned = new HashSet<string>(_atlasPinned, StringComparer.Ordinal);
        var track = new HashSet<string>(_radarSettings.AtlasHighlightTags ?? new(), StringComparer.OrdinalIgnoreCase);
        var arrow = new HashSet<string>(_radarSettings.AtlasArrowTags ?? new(), StringComparer.OrdinalIgnoreCase);

        var marks = new List<AtlasMark>(Math.Min(nodes.Count, 256));
        foreach (var n in nodes)
        {
            var selected = pinned.Contains(AtlasNodeKey(n.Element));
            var matchedTrack = MatchAtlasRule(track, n);
            var matchedArrow = MatchAtlasRule(arrow, n);
            var semantic = InferAtlasSemantic(n);
            var drawNormalNode = n.Visible;
            var drawDebugNode = _radarSettings.AtlasDrawAll &&
                (n.Visible || _radarSettings.AtlasShowHiddenNodes);
            if (!selected && matchedTrack == null && matchedArrow == null && !drawNormalNode && !drawDebugNode)
                continue;

            var matched = matchedTrack ?? matchedArrow ?? semantic.Label;
            var label = matched ?? AtlasNodeLabel(n);
            var color = selected
                ? _radarSettings.AtlasWaypointColor
                : matched != null && _radarSettings.AtlasHighlightColors.TryGetValue(matched, out var configured)
                    ? configured
                    : semantic.Color;

            marks.Add(new AtlasMark(
                n.X, n.Y,
                Selected: selected || matchedTrack != null,
                HasContent: n.HasContent,
                Visited: n.Visited,
                Unlocked: n.Unlocked,
                Biome: n.Biome,
                IconType: n.IconType,
                Label: label,
                Color: color,
                Arrow: (selected && _radarSettings.AtlasShowWaypointArrows) || matchedArrow != null));
        }

        return marks;
    }

    private static string? MatchAtlasRule(HashSet<string> rules, in Poe2Atlas.AtlasNodeLive node)
    {
        if (rules.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(node.MapName) && rules.Contains(node.MapName)) return node.MapName;
        foreach (var tag in node.Tags)
            if (rules.Contains(tag)) return tag;
        return null;
    }

    private static (string? Label, string Color) InferAtlasSemantic(in Poe2Atlas.AtlasNodeLive node)
    {
        string hay = ((node.MapName ?? "") + " " + string.Join(' ', node.Tags)).ToLowerInvariant();
        if (hay.Contains("citadel")) return ("Citadel", "#e0b341");
        if (hay.Contains("boss")) return ("Boss", "#ff4040");
        if (hay.Contains("breach")) return ("Breach", "#b05cff");
        if (hay.Contains("ritual")) return ("Ritual", "#ff4d6d");
        if (hay.Contains("delirium")) return ("Delirium", "#c8c8c8");
        if (hay.Contains("expedition")) return ("Expedition", "#26e6d9");
        if (hay.Contains("corrupt")) return ("Corrupted", "#ff66ff");
        if (hay.Contains("tower")) return ("Tower", "#66aaff");
        if (node.HasContent) return (node.Tags.Count > 0 ? node.Tags[0] : "Content", "#ff9e42");
        if (node.Visited) return ("Visited", "#ff66ff");
        return ("Map", "#6ee888");
    }

    private object GetAtlasDashboard()
    {
        HashSet<string> pinned;
        lock (_atlasLock) pinned = new HashSet<string>(_atlasPinned, StringComparer.Ordinal);

        var nodes = _atlasNodes;
        return new
        {
            open = nodes.Count > 0,
            total = nodes.Count,
            pinned = pinned.ToArray(),
            highlightTags = _radarSettings.AtlasHighlightTags,
            arrowTags = _radarSettings.AtlasArrowTags,
            highlightColors = _radarSettings.AtlasHighlightColors,
            allTags = nodes.SelectMany(n => n.Tags)
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => new { tag = g.Key, count = g.Count() }),
            allMaps = nodes.Where(n => !string.IsNullOrWhiteSpace(n.MapName))
                .GroupBy(n => n.MapName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g => new { tag = g.Key, count = g.Count() }),
            nodeList = nodes
                .OrderByDescending(n => pinned.Contains(AtlasNodeKey(n.Element)))
                .ThenByDescending(n => n.Visible)
                .ThenByDescending(n => n.HasContent)
                .ThenBy(n => AtlasNodeLabel(n))
                .Take(2000)
                .Select(n => new
                {
                    el = AtlasNodeKey(n.Element),
                    id = n.Id,
                    map = n.MapName,
                    tags = n.Tags,
                    label = AtlasNodeLabel(n),
                    visible = n.Visible,
                    visited = n.Visited,
                    unlocked = n.Unlocked,
                    hasContent = n.HasContent,
                    biome = n.Biome,
                    icon = n.IconType,
                    x = (int)n.X,
                    y = (int)n.Y,
                    pinned = pinned.Contains(AtlasNodeKey(n.Element)),
                }),
        };
    }

    private void SetAtlasPins(IReadOnlyList<string> pins)
    {
        lock (_atlasLock)
        {
            _atlasPinned.Clear();
            foreach (var pin in pins)
            {
                if (!string.IsNullOrWhiteSpace(pin))
                    _atlasPinned.Add(pin.Trim());
            }
        }
    }

    private static string AtlasNodeKey(nint element) => $"0x{element.ToInt64():X}";

    private static string AtlasNodeLabel(Poe2Atlas.AtlasNodeLive node)
    {
        if (!string.IsNullOrWhiteSpace(node.MapName)) return node.MapName;
        return node.Tags.Count > 0 ? node.Tags[0] : $"Node {node.Id}";
    }

    private void HandleSettingsToggle()
    {
        // F7 cycle pathing target, Escape = back to auto-nearest
        if (Down(0x1B) && DateTime.UtcNow >= _nextToggleAt && (_manualPathPattern != null || _manualPathGridTarget != null))
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            _manualPathPattern = null;
            _manualPathGridTarget = null;
            _pathPoints = null;
            _lastPathTarget = "";
            _pathTargetName = null;
            _lastPathPlayerGrid = NumVec2.Zero;
            Console.WriteLine("\nPath: back to auto-nearest");
        }
        if (Down(0x76) && DateTime.UtcNow >= _nextToggleAt)
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            var next = _pathing.CycleNext();
            if (next != null)
            {
                _manualPathPattern = next.Pattern;
                _manualPathGridTarget = null;
                _pathPoints = null;
                _lastPathTarget = "";
                _pathTargetName = null;
                _lastPathPlayerGrid = NumVec2.Zero;
                Console.WriteLine($"\nPath target: {next.Label} ({next.Pattern})");
            }
        }
        // F11 open web dashboard
        if (Down(0x7A) && DateTime.UtcNow >= _nextToggleAt)
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(500);
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:7777") { UseShellExecute = true }); }
            catch { }
        }
        // F10 toggle overlay visibility
        if (Down(0x79) && DateTime.UtcNow >= _nextToggleAt)
        {
            _overlayVisible = !_overlayVisible;
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            Console.WriteLine($"\nOverlay: {(_overlayVisible ? "VISIBLE" : "HIDDEN")}");
        }
        if (Down(0x78) && DateTime.UtcNow >= _nextToggleAt) // F9
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(_cheats, _radarSettings);
                _settingsForm.Show();
            }
            else if (_settingsForm.Visible)
            {
                _settingsForm.Hide();
            }
            else
            {
                _settingsForm.SyncState();
                _settingsForm.Show();
            }
        }
    }

    private void TickAutoFlask(nint localPlayer)
    {
        if (_live.PlayerVitals(localPlayer) is not { } v) return;
        _hpPct = v.HpPct; _manaPct = v.ManaPct;

        if (!_autoFlask) { _flaskNote = "OFF (F8)"; return; }
        if (GetForegroundWindow() != _gameHwnd) { _flaskNote = "paused"; return; }
        _flaskNote = "armed";

        // Count nearby enemies for rule conditions
        var playerGrid = _live.PlayerGrid(localPlayer) ?? System.Numerics.Vector2.Zero;
        var nearbyCount = 0;
        foreach (var e in _entities)
        {
            if (e.Category != Poe2Live.EntityCategory.Monster || !e.IsAlive) continue;
            if ((e.Grid - playerGrid).Length() < 60f) nearbyCount++;
        }

        foreach (var rule in _autoRules.Rules)
        {
            if (!_autoRules.Evaluate(rule, _hpPct, _manaPct, nearbyCount)) continue;
            SendInputNative.Tap((ushort)rule.Key);
            _autoRules.MarkFired(rule);
            _flaskNote = $"{rule.Name}";
        }
    }

    private void HandleShiftInspect()
    {
        var shiftHeld = Down(0x10); // VK_SHIFT
        if (!shiftHeld && !Down(0x12)) // neither Shift nor Alt
            _window.SetInteractive(false);
        if (shiftHeld)
            _window.SetInteractive(true);

        if (!shiftHeld || !_window.HasClick) return;
        _window.ConsumeClick();

        if (_cameraMatrix == null || _cameraMatrix.Length < 16) return;

        var cx = _window.ClickX;
        var cy = _window.ClickY;
        var W = (float)_window.Width;
        var H = (float)_window.Height;
        var m = _cameraMatrix;

        string? bestMeta = null;
        string? bestName = null;
        var bestDist = 40f * 40f;

        foreach (var e in _entities)
        {
            var w = e.World;
            var cw = w.X * m[3] + w.Y * m[7] + w.Z * m[11] + m[15];
            if (cw <= 0.001f) continue;
            var px = w.X * m[0] + w.Y * m[4] + w.Z * m[8] + m[12];
            var py = w.X * m[1] + w.Y * m[5] + w.Z * m[9] + m[13];
            var sx = (px / cw / 2f + 0.5f) * W;
            var sy = (0.5f - py / cw / 2f) * H;

            var dx = sx - cx;
            var dy = sy - cy;
            var d2 = dx * dx + dy * dy;
            if (d2 < bestDist)
            {
                bestDist = d2;
                bestMeta = e.Metadata;
                var parts = e.Metadata.Split('/');
                bestName = $"{e.Category} | {parts[^1].Split('@')[0]} | {e.Rarity}" +
                    (e.HpMax > 0 ? $" | HP {e.HpCur}/{e.HpMax}" : "") +
                    (e.Poi ? " | POI" : "") +
                    (e.IsFriendly ? " | Friendly" : "");
            }
        }

        if (bestMeta != null)
        {
            _inspectedEntity = bestName;
            _inspectedMeta = bestMeta;
            _inspectedAt = DateTime.UtcNow;
            Console.WriteLine($"\nInspect: {bestMeta}");
            Console.WriteLine($"  {bestName}");
        }
    }

    private void HandleAltClick()
    {
        var altHeld = Down(0x12); // VK_MENU (Alt)
        if (altHeld) _window.SetInteractive(true);

        if (!altHeld || !_window.HasClick) return;
        _window.ConsumeClick();

        var cx = _window.ClickX;
        var cy = _window.ClickY;
        var bestDist = 25f * 25f;

        // Check landmarks first (larger click target since they have text labels)
        string? bestLandmark = null;
        float bestLmGx = 0, bestLmGy = 0;
        foreach (var (sx, sy, gx, gy, name) in _landmarkScreenPos)
        {
            var dx = cx - sx;
            var dy = cy - sy;
            if (dx >= -15 && dx <= 300 && dy >= -25 && dy <= 25)
            {
                var d2 = dx * dx + dy * dy;
                if (d2 < bestDist) { bestDist = d2; bestLandmark = name; bestLmGx = gx; bestLmGy = gy; }
            }
        }

        if (bestLandmark != null)
        {
            _manualPathPattern = null;
            _manualPathGridTarget = ((int)bestLmGx, (int)bestLmGy);
            _pathPoints = null;
            _lastPathTarget = "";
            _pathTargetName = bestLandmark;
            _lastPathPlayerGrid = NumVec2.Zero;
            Console.WriteLine($"\nAlt+click nav to landmark: {bestLandmark} grid=({(int)bestLmGx},{(int)bestLmGy})");
            return;
        }

        // Check entities
        string? bestMeta = null;
        bestDist = 35f * 35f;
        foreach (var (sx, sy, meta) in _entityScreenPos)
        {
            var dx = sx - cx;
            var dy = sy - cy;
            var d2 = dx * dx + dy * dy;
            if (d2 < bestDist) { bestDist = d2; bestMeta = meta; }
        }

        if (bestMeta != null)
        {
            var shortName = bestMeta.Split('/')[^1].Split('@')[0];
            _manualPathPattern = shortName;
            _manualPathGridTarget = null;
            _pathPoints = null;
            _lastPathTarget = "";
            _pathTargetName = shortName;
            _lastPathPlayerGrid = NumVec2.Zero;
            Console.WriteLine($"\nAlt+click nav: {shortName}");
        }
    }

    private void UpdatePath(NumVec2 playerGrid)
    {
        if (!_radarSettings.ShowPath || _terrain == null)
        {
            _pathPoints = null;
            _pathTargetName = null;
            return;
        }

        int destX, destY;
        string cacheKey;

        // Priority: 1) Alt+click grid target, 2) F7/Alt+click entity pattern, 3) auto-nearest
        if (_manualPathGridTarget is { } gridTarget)
        {
            destX = gridTarget.X;
            destY = gridTarget.Y;
            cacheKey = $"grid:{destX},{destY}";
            _pathTargetName ??= "Custom waypoint";
        }
        else
        {
            string? targetPattern = _manualPathPattern;

            if (targetPattern == null)
            {
                if (_pathing.All.Count == 0) { _pathPoints = null; _pathTargetName = null; return; }
                var entityInfo = _entities
                    .Select(e => (e.Metadata, Distance: (e.Grid - playerGrid).Length(), e.IsAlive))
                    .ToList();
                targetPattern = _pathing.FindNearestPattern(entityInfo!);
            }

            if (targetPattern == null) { _pathPoints = null; _pathTargetName = null; return; }
            _pathTargetName = _pathing.All.FirstOrDefault(e =>
                string.Equals(e.Pattern, targetPattern, StringComparison.OrdinalIgnoreCase))?.Label ?? targetPattern;

            Poe2Live.EntityDot? closest = null;
            var closestDist = float.MaxValue;
            foreach (var e in _entities)
            {
                if (!e.IsAlive && e.HpMax > 0) continue;
                if (!e.Metadata.Contains(targetPattern, StringComparison.OrdinalIgnoreCase)) continue;
                var d = (e.Grid - playerGrid).Length();
                if (d < closestDist) { closestDist = d; closest = e; }
            }

            if (closest == null)
            {
                if (_manualPathPattern != null) return; // keep last path visible
                _pathPoints = null; _pathTargetName = null; return;
            }
            destX = (int)closest.Value.Grid.X;
            destY = (int)closest.Value.Grid.Y;
            cacheKey = $"entity:{targetPattern}:{destX},{destY}";
        }

        var playerMoved = (playerGrid - _lastPathPlayerGrid).Length() > 3f;
        var targetChanged = cacheKey != _lastPathTarget;
        if (!playerMoved && !targetChanged && _pathPoints != null) return;

        _lastPathPlayerGrid = playerGrid;
        _lastPathTarget = cacheKey;

        var t = _terrain;
        var px = (int)playerGrid.X;
        var py = (int)playerGrid.Y;

        if (destX < 0 || destX >= t.Width || destY < 0 || destY >= t.Height)
        {
            Console.WriteLine($"  Path dest out of bounds: ({destX},{destY}) grid=({t.Width}x{t.Height})");
            _pathPoints = null;
            return;
        }

        var result = AStarPathfinder.FindPath(t.Walkable, t.Width, t.Height, px, py, destX, destY);
        if (result != null)
        {
            _pathPoints = AStarPathfinder.Simplify(result.Value.Points);
            Console.WriteLine($"  Path found: {result.Value.Points.Count} pts, {result.Value.GridDistance:F0} dist, {result.Value.NodesVisited} visited");
        }
        else
        {
            Console.WriteLine($"  Path FAILED: ({px},{py}) -> ({destX},{destY})");
            _pathPoints = null;
        }
    }

    private void HandleCalibrationKeys()
    {
        if (Down(0x77) && DateTime.UtcNow >= _nextToggleAt)
        {
            _autoFlask = !_autoFlask;
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            Console.WriteLine($"\nAuto-flask: {(_autoFlask ? "ON" : "OFF")}");
        }
        if (DateTime.UtcNow < _nextKeyAt) return;
        var changed = true;
        if (Down(0x21)) _radarSettings.ScaleMul *= 1.03f;
        else if (Down(0x22)) _radarSettings.ScaleMul /= 1.03f;
        else if (Down(0x25)) _radarSettings.OffsetX -= 4;
        else if (Down(0x27)) _radarSettings.OffsetX += 4;
        else if (Down(0x26)) _radarSettings.OffsetY -= 4;
        else if (Down(0x28)) _radarSettings.OffsetY += 4;
        else if (Down(0x24)) { _radarSettings.ScaleMul = 1f; _radarSettings.OffsetX = 0; _radarSettings.OffsetY = 0; }
        else changed = false;
        if (changed)
        {
            _nextKeyAt = DateTime.UtcNow.AddMilliseconds(40);
            Console.Write($"\rcalib: scaleMul={_radarSettings.ScaleMul:F3} off=({_radarSettings.OffsetX:F0},{_radarSettings.OffsetY:F0})        ");
        }
    }

    private void HandleCheatKeys()
    {
        if (DateTime.UtcNow < _nextCheatKeyAt) return;
        foreach (var (vk, name) in CheatKeys)
        {
            if (!Down(vk)) continue;
            _cheats.Toggle(name);
            _nextCheatKeyAt = DateTime.UtcNow.AddMilliseconds(300);
            break;
        }
    }

    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    public void Dispose()
    {
        _cheats.RestoreAll();
        _settingsForm?.Dispose();
        _api.Dispose();
        _renderer.Dispose();
        _window.Dispose();
    }
}

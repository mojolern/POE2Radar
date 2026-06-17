using System.Diagnostics;
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
using POE2Radar.Overlay.Pricing;
using POE2Radar.Overlay.Navigation;
using Path = System.IO.Path;

#pragma warning disable CA1416

namespace POE2Radar.Overlay;

public sealed class RadarApp : IDisposable
{
    private const int WorldHz = 30;
    private const int MaxSoftwareRenderHz = 60;

    private readonly ProcessHandle _process;
    private readonly MemoryReader _reader;
    private readonly Poe2Live _live;
    private readonly MemoryReader _renderReader;
    private readonly Poe2Live _renderLive;
    private readonly MemoryReader _apiReader;
    private readonly Poe2Live _apiLive;
    private readonly Poe2Live _terrainLive;
    private readonly Poe2Live _landmarkLive;
    private readonly Poe2Atlas _atlas;
    private readonly Poe2Runeforge _runeforge;
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
    private readonly DisplayRules _displayRules;
    private readonly ModCatalog _modCatalog;
    private readonly PriceBook _priceBook;
    private SettingsForm? _settingsForm;
    private volatile RadarState _state = RadarState.Empty;
    private volatile WorldFrame _worldFrame = WorldFrame.Empty;
    private Thread? _worldThread;
    private float _worldMs;

    private List<Poe2Live.EntityDot> _entities = new();
    private IReadOnlyList<Poe2Live.Landmark> _landmarks = Array.Empty<Poe2Live.Landmark>();
    private Poe2Live.TerrainData? _terrain;
    private Task<(nint Area, Poe2Live.TerrainData? Data, double ElapsedMs)>? _terrainLoadTask;
    private Task<(nint Area, Poe2Live.Landmark[] Data, double ElapsedMs)>? _landmarkLoadTask;
    private nint _terrainLoadedArea;
    private nint _landmarksLoadedArea;
    private uint _areaHash;
    private nint _lastAreaInstance;
    private nint _gameHwnd;
    private bool _mapWasVisible;
    private volatile bool _shutdown;
    private long _perfWindowStarted = Stopwatch.GetTimestamp();
    private int _perfFrameCount;
    private double _perfTickTotalMs;
    private double _perfTickMaxMs;
    private double _perfRenderTotalMs;
    private double _perfRenderMaxMs;
    private double _perfDrawTotalMs;
    private double _perfDrawMaxMs;
    private double _perfEndDrawTotalMs;
    private double _perfEndDrawMaxMs;
    private double _perfPresentTotalMs;
    private double _perfPresentMaxMs;
    private double _perfFogTotalMs;
    private double _perfFogMaxMs;

    private DateTime _nextKeyAt = DateTime.MinValue;
    private List<(int X, int Y)>? _pathPoints;
    private string _lastPathTarget = "";
    private readonly object _pathLock = new();
    private readonly BackgroundReplanner _replanner = new();
    private RouteTracker _routeTracker = new();
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
    private (int X, int Y)? _atlasStartGrid;
    private (int X, int Y)? _atlasGoalGrid;
    private ((int X, int Y) Start, (int X, int Y) Goal)? _loggedAtlasRoute;
    private List<AtlasMark> _atlasMarks = new();
    private IReadOnlyList<ItemLabel> _itemLabels = Array.Empty<ItemLabel>();
    private IReadOnlyList<RuneLabel> _runeLabels = Array.Empty<RuneLabel>();

    private sealed record WorldFrame(
        bool InGame,
        uint AreaHash,
        int AreaLevel,
        IReadOnlyList<Poe2Live.EntityDot> Entities,
        IReadOnlyList<Poe2Live.Landmark> Landmarks,
        Poe2Live.TerrainData? Terrain,
        string AreaCode,
        string? AreaName,
        int AreaAct,
        bool IsTown,
        bool HasWaypoint,
        int CharLevel,
        IReadOnlyList<(int X, int Y)>? PathPoints,
        string? PathTargetName,
        IReadOnlyList<ItemLabel> ItemLabels,
        IReadOnlyList<RuneLabel> RuneLabels,
        IReadOnlyList<Poe2Atlas.AtlasNodeLive> AtlasNodes,
        IReadOnlyList<AtlasMark> AtlasMarks,
        NumVec2? AtlasRouteStart,
        NumVec2? AtlasRouteEnd,
        IReadOnlyList<NumVec2> AtlasRoute,
        string? AtlasLoadingText,
        float AtlasLoadingProgress)
    {
        public static readonly WorldFrame Empty = new(
            false, 0, 0,
            Array.Empty<Poe2Live.EntityDot>(),
            Array.Empty<Poe2Live.Landmark>(),
            null, "", null, 0, false, false, 0, null, null,
            Array.Empty<ItemLabel>(), Array.Empty<RuneLabel>(),
            Array.Empty<Poe2Atlas.AtlasNodeLive>(), Array.Empty<AtlasMark>(),
            null, null, Array.Empty<NumVec2>(), null, 0);
    }

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
        _renderReader = new MemoryReader(process);
        _renderLive = new Poe2Live(_renderReader, gameStateSlot);
        _apiReader = new MemoryReader(process);
        _apiLive = new Poe2Live(_apiReader, gameStateSlot);
        _terrainLive = new Poe2Live(reader, gameStateSlot);
        _landmarkLive = new Poe2Live(reader, gameStateSlot);
        _atlas = new Poe2Atlas(reader);
        _runeforge = new Poe2Runeforge(reader);
        _cheats = new CheatManager(process, reader);
        Console.WriteLine("\nScanning cheat patterns...");
        _cheats.ScanAndResolve();
        Console.WriteLine("Hotkeys: F1-F5 cheats, F8 flask, F9 settings, F10 overlay / Atlas tile, F11 web dashboard\n");
        Console.WriteLine("         F10 with Atlas open = dump hovered map/content/biome and set route START -> END; third press resets\n");
        _window = OverlayWindow.Create();
        _renderer = new OverlayRenderer(_window);
        var configDir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "config");
        _radarSettings = RadarSettings.Load(Path.Combine(configDir, "radar_settings.json"));
        if (_radarSettings.FpsCap > MaxSoftwareRenderHz)
        {
            Console.WriteLine(
                $"Overlay FPS cap reduced from {_radarSettings.FpsCap} to {MaxSoftwareRenderHz}: " +
                "the layered-window compositor is software-rendered.");
            _radarSettings.FpsCap = MaxSoftwareRenderHz;
            _radarSettings.Save();
        }
        _watched = new WatchedEntities(Path.Combine(configDir, "watched_entities.json"));
        _displayRules = new DisplayRules(Path.Combine(configDir, "display_rules.json"));
        if (_displayRules.Count == 0)
            _displayRules.Replace(DisplayRules.BuildDefault(
                _radarSettings.Styles,
                _radarSettings.ShowMonsters,
                _watched.All.Values));
        _modCatalog = new ModCatalog(Path.Combine(configDir, "known_mods.json"));
        _priceBook = new PriceBook(
            Path.Combine(configDir, "price_cache.json"),
            _radarSettings.GroundItems.League);
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
                inspector = new ComponentFieldReader(idaOffsetsPath, _apiLive, _apiReader);
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
            _displayRules, _modCatalog, GetPriceStatus,
            GetAtlasDashboard, SetAtlasPins);
        try { _api.Start(); Console.WriteLine("API on http://localhost:7777 (/state, /entities, /api/inspect)"); }
        catch (Exception ex) { Console.Error.WriteLine($"API server disabled: {ex.Message}"); }
    }

    public void Run()
    {
        _gameHwnd = OverlayNative.FindWindowForProcess(_process.ProcessId);
        _worldThread = new Thread(WorldLoop)
        {
            IsBackground = true,
            Name = "POE2Radar.World",
        };
        _worldThread.Start();

        var nextFrame = Stopwatch.GetTimestamp();
        while (!_shutdown)
        {
            if (_gameHwnd == 0) _gameHwnd = OverlayNative.FindWindowForProcess(_process.ProcessId);
            if (_gameHwnd != 0) _window.TrackGameWindow(_gameHwnd);
            if (!_window.PumpMessages()) break;
            Tick();

            var targetHz = Math.Clamp(_radarSettings.FpsCap, 15, MaxSoftwareRenderHz);
            var frameTicks = Math.Max(1L, Stopwatch.Frequency / targetHz);
            nextFrame += frameTicks;
            var now = Stopwatch.GetTimestamp();
            if (now > nextFrame)
                nextFrame = now;
            WaitUntil(nextFrame);
        }
        _shutdown = true;
        _worldThread.Join(1500);
    }

    private void WorldLoop()
    {
        var budgetTicks = Math.Max(1L, Stopwatch.Frequency / WorldHz);
        var nextTick = Stopwatch.GetTimestamp();
        while (!_shutdown)
        {
            var started = Stopwatch.GetTimestamp();
            try { UpdateWorldFrame(); }
            catch (Exception ex) { Console.Error.WriteLine($"World tick error: {ex.Message}"); }
            _worldMs = (float)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            nextTick += budgetTicks;
            var now = Stopwatch.GetTimestamp();
            if (now > nextTick) nextTick = now;
            WaitUntil(nextTick);
        }
    }

    private static void WaitUntil(long deadline)
    {
        while (true)
        {
            var remaining = deadline - Stopwatch.GetTimestamp();
            if (remaining <= 0) return;

            var remainingMs = remaining * 1000.0 / Stopwatch.Frequency;
            if (remainingMs > 2.0)
                Thread.Sleep(Math.Max(1, (int)remainingMs - 1));
            else
                Thread.Yield();
        }
    }

    private void UpdateWorldFrame()
    {
        if (!_live.TryResolve(out var inGameState, out var areaInstance, out var localPlayer))
        {
            _entities = new();
            _atlasNodes = new();
            _atlasMarks = new();
            _worldFrame = WorldFrame.Empty;
            return;
        }

        if (areaInstance != _lastAreaInstance)
        {
            _terrain = null;
            _landmarks = Array.Empty<Poe2Live.Landmark>();
            _terrainLoadedArea = 0;
            _landmarksLoadedArea = 0;
            _lastAreaInstance = areaInstance;
            lock (_pathLock)
            {
                _lastPathTarget = "";
                _routeTracker = new RouteTracker();
            }
            BeginStaticAreaLoads(areaInstance);
        }
        PollStaticAreaLoads(areaInstance);

        _areaHash = _live.AreaHash(areaInstance);
        var areaLevel = _live.AreaLevel(areaInstance);
        var playerWorld = _live.PlayerWorld(localPlayer);
        var player = playerWorld is { } pw
            ? new NumVec2(pw.X / Poe2.WorldToGridRatio, pw.Y / Poe2.WorldToGridRatio)
            : _live.PlayerGrid(localPlayer) ?? NumVec2.Zero;
        _exploration.Update(player.X, player.Y, areaInstance);

        _areaCode = _live.AreaCode(areaInstance);
        var area = _gameData.GetArea(_areaCode);
        _areaName = area?.Name;
        _areaAct = area?.Act ?? 0;
        _isTown = area?.Town ?? false;
        _charLevel = _live.PlayerLevel(localPlayer);

        if (_radarSettings.ShowAtlasNodes)
        {
            _atlasNodes = _atlas.ReadNodes(inGameState);
            _atlasMarks = BuildAtlasMarks(_atlasNodes);
        }
        else
        {
            _atlasNodes = new();
            _atlasMarks = new();
        }
        var (atlasRouteStart, atlasRouteEnd, atlasRoute) = BuildAtlasRoute(_atlasNodes);

        _entities = _live.Entities(areaInstance, _radarSettings.ShowPreloadedMechanicLocations);
        _modCatalog.Observe(_entities);
        _priceBook.SetLeagueOverride(_radarSettings.GroundItems.League);
        _priceBook.RefreshIfDue();
        _itemLabels = BuildItemLabels(_entities);
        _runeLabels = BuildRuneLabels(inGameState);
        UpdatePath(player);

        _worldFrame = new WorldFrame(
            true,
            _areaHash,
            areaLevel,
            _entities,
            _landmarks,
            _terrain,
            _areaCode,
            _areaName,
            _areaAct,
            _isTown,
            area?.Waypoint ?? false,
            _charLevel,
            _pathPoints?.ToArray(),
            _pathTargetName,
            _itemLabels,
            _runeLabels,
            _atlasNodes,
            _atlasMarks,
            atlasRouteStart,
            atlasRouteEnd,
            atlasRoute,
            _radarSettings.ShowAtlasNodes && _atlas.LastPanelOpen && _atlas.LoadProgress is > 0f and < 1f
                ? _atlas.LoadStatus
                : null,
            _atlas.LoadProgress);
    }

    private void Tick()
    {
        var tickStarted = Stopwatch.GetTimestamp();
        HandleCheatKeys();
        HandleSettingsToggle();
        HandleAltClick();
        HandleShiftInspect();

        var snap = _worldFrame;
        var inGame = _renderLive.TryResolve(out var inGameState, out var areaInstance, out var localPlayer);
        var player = NumVec2.Zero;
        POE2Radar.Core.Game.Vector3? playerWorld = null;
        var map = default(Poe2Live.MapUi);
        uint liveAreaHash = 0;

        if (inGame)
        {
            liveAreaHash = _renderLive.AreaHash(areaInstance);
            playerWorld = _renderLive.PlayerWorld(localPlayer);
            player = playerWorld is { } pw
                ? new NumVec2(pw.X / Poe2.WorldToGridRatio, pw.Y / Poe2.WorldToGridRatio)
                : _renderLive.PlayerGrid(localPlayer) ?? NumVec2.Zero;
            map = _renderLive.ReadMap(inGameState, areaInstance);
            if (string.IsNullOrWhiteSpace(_charName))
                _charName = _renderLive.PlayerName(localPlayer);
            _cameraMatrix = _renderLive.CameraMatrix(inGameState);
            TickAutoFlask(_renderLive, localPlayer, snap.Entities);
        }

        HandleCalibrationKeys();
        if (map.IsVisible && !_mapWasVisible)
            _renderer.ResetMapTracking();
        _mapWasVisible = map.IsVisible;

        var worldFresh = inGame && snap.InGame && snap.AreaHash == liveAreaHash;
        var entities = worldFresh ? snap.Entities : Array.Empty<Poe2Live.EntityDot>();
        var landmarks = worldFresh ? snap.Landmarks : Array.Empty<Poe2Live.Landmark>();
        var terrain = worldFresh ? snap.Terrain : null;
        var pathPoints = worldFresh ? snap.PathPoints : null;
        var itemLabels = worldFresh ? snap.ItemLabels : Array.Empty<ItemLabel>();
        var runeLabels = worldFresh ? snap.RuneLabels : Array.Empty<RuneLabel>();
        var atlasNodes = worldFresh ? snap.AtlasNodes : Array.Empty<Poe2Atlas.AtlasNodeLive>();
        var atlasMarks = worldFresh ? snap.AtlasMarks : Array.Empty<AtlasMark>();
        var atlasRouteStart = worldFresh ? snap.AtlasRouteStart : null;
        var atlasRouteEnd = worldFresh ? snap.AtlasRouteEnd : null;
        var atlasRoute = worldFresh ? snap.AtlasRoute : Array.Empty<NumVec2>();

        var minimap = _renderLive.GameMinimap;
        _state = new RadarState(inGame, snap.AreaHash, snap.AreaLevel, map.IsVisible, map.Zoom, player, snap.Entities, snap.Landmarks,
            _hpPct, _manaPct, _autoFlask, _flaskNote, snap.AreaCode, _charName, snap.CharLevel,
            snap.AreaName, snap.AreaAct, snap.IsTown, snap.HasWaypoint,
            map.ShiftX, map.ShiftY,
            minimap.Available, minimap.ShiftX, minimap.ShiftY, minimap.Zoom);

        var ctx = new RenderContext(
            InGame: inGame,
            Active: _gameHwnd != 0 && GetForegroundWindow() == _gameHwnd,
            WindowWidth: _window.Width,
            WindowHeight: _window.Height,
            PlayerGrid: player,
            Map: map,
            Entities: entities,
            Landmarks: landmarks,
            AreaHash: liveAreaHash,
            Terrain: terrain,
            ScaleMul: _radarSettings.ScaleMul,
            OffsetX: _radarSettings.OffsetX,
            OffsetY: _radarSettings.OffsetY,
            HpPct: _hpPct,
            ManaPct: _manaPct,
            FlaskNote: _flaskNote,
            AreaCode: snap.AreaCode,
            CharLevel: snap.CharLevel,
            CameraMatrix: _cameraMatrix,
            CheatStatus: _cheats.GetStatus(),
            Radar: _radarSettings,
            OverlayVisible: _overlayVisible,
            Watched: _watched,
            PathPoints: pathPoints?.ToList(),
            EntityScreenPositions: _entityScreenPos,
            LandmarkScreenPositions: _landmarkScreenPos,
            Exploration: _exploration,
            InspectedName: _inspectedEntity,
            InspectedMeta: _inspectedMeta,
            PathTargetName: snap.PathTargetName,
            EntityNames: _entityNames,
            AreaName: snap.AreaName,
            AreaAct: snap.AreaAct,
            IsTown: snap.IsTown,
            CharName: _charName,
            MapPins: _gameData.GetPins(snap.AreaCode),
            GameData: _gameData,
            GameMinimap: minimap,
            Hidden: _hidden,
            DisplayRules: _displayRules,
            PlayerWorld: playerWorld,
            ItemLabels: itemLabels,
            RuneLabels: runeLabels,
            AtlasNodes: atlasNodes,
            AtlasMarks: atlasMarks,
            AtlasRouteStart: atlasRouteStart,
            AtlasRouteEnd: atlasRouteEnd,
            AtlasRoute: atlasRoute,
            AtlasLoadingText: worldFresh ? snap.AtlasLoadingText : null,
            AtlasLoadingProgress: worldFresh ? snap.AtlasLoadingProgress : 0);
        var renderStarted = Stopwatch.GetTimestamp();
        _renderer.Render(ctx);
        TrackPerformance(tickStarted, Stopwatch.GetElapsedTime(renderStarted).TotalMilliseconds);
    }

    private IReadOnlyList<ItemLabel> BuildItemLabels(IReadOnlyList<Poe2Live.EntityDot> entities)
    {
        var settings = _radarSettings.GroundItems;
        if (!settings.Enabled || !_priceBook.IsLoaded) return Array.Empty<ItemLabel>();

        var categories = new HashSet<string>(
            settings.Categories ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        var result = new List<ItemLabel>();
        foreach (var entity in entities)
        {
            if (string.IsNullOrWhiteSpace(entity.ItemArt)) continue;
            var price = _priceBook.TryByArtAndName(entity.ItemArt, entity.ItemName);
            if (price is not { } value || value.LowConfidence(settings.MinQuantity)) continue;
            var group = PriceCategoryGroup(value.Category);
            if (!categories.Contains(group)) continue;
            if (entity.Rarity == Poe2Live.Rarity.Unique && value.Exalted < settings.UniqueMinEx) continue;
            var stackCount = Math.Max(1, entity.ItemStackCount);
            var highExalted = value.HighExalted * stackCount;
            result.Add(new ItemLabel(
                entity.World,
                value.Name,
                FormatStackedPrice(value, stackCount),
                highExalted >= settings.HighlightMinEx,
                entity.Rarity == Poe2Live.Rarity.Unique && !entity.ItemIdentified));
        }
        return result;
    }

    private string FormatStackedPrice(PriceResult value, int stackCount)
    {
        stackCount = Math.Max(1, stackCount);
        if (stackCount == 1) return _priceBook.Format(value);
        if (!value.IsRange) return _priceBook.Format(value.Exalted * stackCount);
        return $"{_priceBook.Format(value.LowExalted * stackCount)}-{_priceBook.Format(value.HighExalted * stackCount)}";
    }

    private object GetPriceStatus() => new
    {
        loaded = _priceBook.IsLoaded,
        league = _priceBook.League,
        count = _priceBook.ItemCount,
        status = _priceBook.Status,
        exPerDivine = _priceBook.ExPerDivine,
        divPerExalted = _priceBook.DivPerExalted,
        exPerChaos = _priceBook.ExPerChaos,
        primaryCurrency = _priceBook.PrimaryCurrency,
        secondaryCurrency = _priceBook.SecondaryCurrency,
        hardcore = _priceBook.Hardcore,
        lastFetchUtc = _priceBook.LastFetchUtc,
    };

    private IReadOnlyList<RuneLabel> BuildRuneLabels(nint inGameState)
    {
        var settings = _radarSettings.GroundItems;
        if (!settings.Enabled || !settings.ShowRuneforgePrices || !_priceBook.IsLoaded)
            return Array.Empty<RuneLabel>();

        var rows = _runeforge.ReadRewards(inGameState, _window.Width, _window.Height);
        if (rows.Count == 0) return Array.Empty<RuneLabel>();
        var result = new List<RuneLabel>(rows.Count);
        foreach (var row in rows)
        {
            var price = _priceBook.TryByName(row.Name);
            if (price is not { } value || value.LowConfidence(settings.MinQuantity)) continue;
            var total = value.Exalted * Math.Max(1, row.Count);
            var color = total >= settings.HighlightMinEx ? 0xFFFFCC33u : 0xFFEAEAEAu;
            result.Add(new RuneLabel(
                row.X, row.Y, row.W, row.H,
                _priceBook.Format(total),
                color));
        }
        return result;
    }

    private static string PriceCategoryGroup(string category)
    {
        if (category.StartsWith("Unique", StringComparison.OrdinalIgnoreCase)) return "Uniques";
        if (category.Contains("Rune", StringComparison.OrdinalIgnoreCase)) return "Runes";
        if (category.Contains("Essence", StringComparison.OrdinalIgnoreCase)) return "Essences";
        return category;
    }

    private void BeginStaticAreaLoads(nint areaInstance)
    {
        if (_terrainLoadedArea != areaInstance &&
            (_terrainLoadTask is null or { IsCompleted: true }))
        {
            _terrainLoadTask = Task.Run(() =>
            {
                var started = Stopwatch.GetTimestamp();
                var data = _terrainLive.Terrain(areaInstance);
                return (areaInstance, data, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            });
        }

        if (_landmarksLoadedArea != areaInstance &&
            (_landmarkLoadTask is null or { IsCompleted: true }))
        {
            _landmarkLoadTask = Task.Run(() =>
            {
                var started = Stopwatch.GetTimestamp();
                var data = _landmarkLive.Landmarks(areaInstance).ToArray();
                return (areaInstance, data, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            });
        }
    }

    private void PollStaticAreaLoads(nint areaInstance)
    {
        if (_terrainLoadTask is { IsCompleted: true } terrainTask)
        {
            _terrainLoadTask = null;
            try
            {
                var result = terrainTask.GetAwaiter().GetResult();
                if (result.Area == areaInstance)
                {
                    _terrainLoadedArea = areaInstance;
                    _terrain = result.Data;
                    if (_radarSettings.ShowPerformanceDiagnostics)
                        Console.WriteLine($"perf: terrain loaded {result.Data?.Width ?? 0}x{result.Data?.Height ?? 0} in {result.ElapsedMs:F1}ms");
                }
            }
            catch (Exception ex)
            {
                if (_radarSettings.ShowPerformanceDiagnostics)
                    Console.WriteLine($"perf: terrain load failed: {ex.Message}");
            }
        }

        if (_landmarkLoadTask is { IsCompleted: true } landmarkTask)
        {
            _landmarkLoadTask = null;
            try
            {
                var result = landmarkTask.GetAwaiter().GetResult();
                if (result.Area == areaInstance)
                {
                    _landmarksLoadedArea = areaInstance;
                    _landmarks = result.Data;
                    if (_radarSettings.ShowPerformanceDiagnostics)
                        Console.WriteLine($"perf: landmarks loaded {result.Data.Length} in {result.ElapsedMs:F1}ms");
                }
            }
            catch (Exception ex)
            {
                if (_radarSettings.ShowPerformanceDiagnostics)
                    Console.WriteLine($"perf: landmark load failed: {ex.Message}");
            }
        }

        if (_terrainLoadedArea != areaInstance && _terrainLoadTask is null)
            BeginStaticAreaLoads(areaInstance);
        if (_landmarksLoadedArea != areaInstance && _landmarkLoadTask is null)
            BeginStaticAreaLoads(areaInstance);
    }

    private void TrackPerformance(long tickStarted, double renderMs)
    {
        if (!_radarSettings.ShowPerformanceDiagnostics)
        {
            _perfWindowStarted = Stopwatch.GetTimestamp();
            _perfFrameCount = 0;
            _perfTickTotalMs = _perfTickMaxMs = 0;
            _perfRenderTotalMs = _perfRenderMaxMs = 0;
            _perfDrawTotalMs = _perfDrawMaxMs = 0;
            _perfEndDrawTotalMs = _perfEndDrawMaxMs = 0;
            _perfPresentTotalMs = _perfPresentMaxMs = 0;
            _perfFogTotalMs = _perfFogMaxMs = 0;
            return;
        }

        var tickMs = Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds;
        _perfFrameCount++;
        _perfTickTotalMs += tickMs;
        _perfTickMaxMs = Math.Max(_perfTickMaxMs, tickMs);
        _perfRenderTotalMs += renderMs;
        _perfRenderMaxMs = Math.Max(_perfRenderMaxMs, renderMs);
        _perfDrawTotalMs += _renderer.LastDrawMs;
        _perfDrawMaxMs = Math.Max(_perfDrawMaxMs, _renderer.LastDrawMs);
        _perfEndDrawTotalMs += _renderer.LastEndDrawMs;
        _perfEndDrawMaxMs = Math.Max(_perfEndDrawMaxMs, _renderer.LastEndDrawMs);
        _perfPresentTotalMs += _renderer.LastPresentMs;
        _perfPresentMaxMs = Math.Max(_perfPresentMaxMs, _renderer.LastPresentMs);
        _perfFogTotalMs += _renderer.LastFogMs;
        _perfFogMaxMs = Math.Max(_perfFogMaxMs, _renderer.LastFogMs);

        var elapsed = Stopwatch.GetElapsedTime(_perfWindowStarted);
        if (elapsed.TotalSeconds < 5 || _perfFrameCount == 0) return;

        Console.WriteLine(
            $"perf: fps={_perfFrameCount / elapsed.TotalSeconds:F1} " +
            $"tick={_perfTickTotalMs / _perfFrameCount:F2}/{_perfTickMaxMs:F2}ms " +
            $"render={_perfRenderTotalMs / _perfFrameCount:F2}/{_perfRenderMaxMs:F2}ms " +
            $"draw={_perfDrawTotalMs / _perfFrameCount:F2}/{_perfDrawMaxMs:F2}ms " +
            $"end={_perfEndDrawTotalMs / _perfFrameCount:F2}/{_perfEndDrawMaxMs:F2}ms " +
            $"present={_perfPresentTotalMs / _perfFrameCount:F2}/{_perfPresentMaxMs:F2}ms " +
            $"fog={_perfFogTotalMs / _perfFrameCount:F2}/{_perfFogMaxMs:F2}ms " +
            $"fogCells={_renderer.LastFogRects}/{_renderer.LastFogSamples} " +
            $"world={_worldMs:F2}ms " +
            $"entities={_live.LastReturnedEntityCount} " +
            $"entityScan={_live.LastEntityScanMs:F2}ms awake={_live.LastAwakeMapSize} " +
            $"sleepScan={_live.LastSleepingScanMs:F2}ms sleeping={_live.LastSleepingMapSize}");

        _perfWindowStarted = Stopwatch.GetTimestamp();
        _perfFrameCount = 0;
        _perfTickTotalMs = _perfTickMaxMs = 0;
        _perfRenderTotalMs = _perfRenderMaxMs = 0;
        _perfDrawTotalMs = _perfDrawMaxMs = 0;
        _perfEndDrawTotalMs = _perfEndDrawMaxMs = 0;
        _perfPresentTotalMs = _perfPresentMaxMs = 0;
        _perfFogTotalMs = _perfFogMaxMs = 0;
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
            var label = matched != null && _radarSettings.AtlasRuleLabels.TryGetValue(matched, out var alias) && !string.IsNullOrWhiteSpace(alias)
                ? alias
                : matched ?? AtlasNodeLabel(n);
            var color = selected
                ? _radarSettings.AtlasWaypointColor
                : matched != null && _radarSettings.AtlasHighlightColors.TryGetValue(matched, out var configured)
                    ? configured
                    : semantic.Color;

            marks.Add(new AtlasMark(
                n.X + n.W * 0.5f, n.Y + n.H * 0.5f,
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

    private (NumVec2? Start, NumVec2? End, IReadOnlyList<NumVec2> Route) BuildAtlasRoute(
        IReadOnlyList<Poe2Atlas.AtlasNodeLive> nodes)
    {
        if (nodes.Count == 0) return (null, null, Array.Empty<NumVec2>());

        HashSet<string> pinned;
        (int X, int Y)? startGrid;
        (int X, int Y)? manualGoalGrid;
        lock (_atlasLock)
        {
            pinned = new HashSet<string>(_atlasPinned, StringComparer.Ordinal);
            startGrid = _atlasStartGrid;
            manualGoalGrid = _atlasGoalGrid;
        }

        Poe2Atlas.AtlasNodeLive? goalNode = null;
        foreach (var node in nodes)
        {
            if (manualGoalGrid is { } manualGoal)
            {
                if (node.Grid != manualGoal) continue;
            }
            else
            {
                if (!pinned.Contains(AtlasNodeKey(node.Element))) continue;
            }
            goalNode = node;
            break;
        }
        if (goalNode is not { } goal) return (null, null, Array.Empty<NumVec2>());

        var gridToPos = new Dictionary<(int X, int Y), NumVec2>(nodes.Count);
        foreach (var node in nodes)
            gridToPos[node.Grid] = new NumVec2(node.X + node.W * 0.5f, node.Y + node.H * 0.5f);

        var end = gridToPos.GetValueOrDefault(goal.Grid);
        if (startGrid is not { } start || !gridToPos.TryGetValue(start, out var startPos))
            return (null, end, Array.Empty<NumVec2>());

        var path = _atlas.FindPath(start, goal.Grid);
        if (_loggedAtlasRoute != (start, goal.Grid))
        {
            _loggedAtlasRoute = (start, goal.Grid);
            Console.WriteLine($"[atlas route] {start}->{goal.Grid}: " +
                (path == null
                    ? $"NO graph path (graph has {_atlas.GraphNodeCount} nodes; start in graph={_atlas.GraphHas(start)}, goal in graph={_atlas.GraphHas(goal.Grid)})"
                    : $"{path.Count} hops"));
        }
        if (path == null || path.Count == 0)
            return (startPos, end, Array.Empty<NumVec2>());

        var route = new List<NumVec2>(path.Count);
        foreach (var hop in path)
            if (gridToPos.TryGetValue(hop, out var pos))
                route.Add(pos);

        return (startPos, end, route);
    }

    private void AtlasRoutePick()
    {
        if (!GetCursorPos(out var pt))
        {
            Console.WriteLine("\n[atlas tile] cursor unavailable.");
            return;
        }

        var nodes = _atlasNodes;
        if (nodes.Count == 0)
        {
            Console.WriteLine("\n[atlas tile] no nodes loaded yet. Open the Atlas and wait for the node layer.");
            return;
        }

        var (scale, offset) = AtlasProjectionForPick(nodes);
        var curX = (pt.X - offset.X) / Math.Max(0.0001f, scale);
        var curY = (pt.Y - offset.Y) / Math.Max(0.0001f, scale);

        Poe2Atlas.AtlasNodeLive? bestIn = null;
        Poe2Atlas.AtlasNodeLive? bestAny = null;
        var bestInDist = double.MaxValue;
        var bestAnyDist = double.MaxValue;
        foreach (var node in nodes)
        {
            if (!float.IsFinite(node.X) || !float.IsFinite(node.Y)) continue;
            var cx = node.X + node.W * 0.5f;
            var cy = node.Y + node.H * 0.5f;
            var dx = curX - cx;
            var dy = curY - cy;
            var dist = dx * dx + dy * dy;
            if (dist < bestAnyDist)
            {
                bestAnyDist = dist;
                bestAny = node;
            }

            var hw = Math.Max(node.W, 40f) * 0.5f;
            var hh = Math.Max(node.H, 40f) * 0.5f;
            if (Math.Abs(dx) <= hw && Math.Abs(dy) <= hh && dist < bestInDist)
            {
                bestInDist = dist;
                bestIn = node;
            }
        }

        if ((bestIn ?? bestAny) is not { } picked)
        {
            Console.WriteLine("\n[atlas tile] no tile under cursor.");
            return;
        }

        var mapCode = AtlasRawMapCode(picked);
        var content = picked.Tags.Count > 0 ? string.Join(", ", picked.Tags) : "(none)";
        Console.WriteLine($"\n[atlas tile] \"{picked.MapName}\"  code={mapCode}  grid={picked.Grid}  biome={picked.Biome}");
        Console.WriteLine($"             content: {content}");
        Console.WriteLine($"             web-UI filters -> Map: \"{picked.MapName}\"" +
                          (picked.Tags.Count > 0 ? $"   Content: {content}" : ""));

        string stage;
        lock (_atlasLock)
        {
            if (_atlasStartGrid is null)
            {
                _atlasStartGrid = picked.Grid;
                _atlasGoalGrid = null;
                stage = $"START = {picked.Grid} '{picked.MapName}'";
            }
            else if (_atlasGoalGrid is null)
            {
                _atlasGoalGrid = picked.Grid;
                stage = $"END = {picked.Grid} '{picked.MapName}'";
            }
            else
            {
                _atlasStartGrid = null;
                _atlasGoalGrid = null;
                _loggedAtlasRoute = null;
                stage = "route RESET";
            }
        }
        Console.WriteLine($"[atlas route] {stage}");
    }

    private (float Scale, NumVec2 Offset) AtlasProjectionForPick(IReadOnlyList<Poe2Atlas.AtlasNodeLive> nodes)
    {
        var zooms = nodes.Select(n => n.Scale).Where(float.IsFinite).OrderBy(v => v).ToArray();
        var zoom = zooms.Length == 0 ? 1f : zooms[zooms.Length / 2];
        var scale = (_window.Height > 0 ? _window.Height / 1600f : 1080f / 1600f) * zoom;
        if (_radarSettings.AtlasAutoAlign == false)
            scale = _window.Height > 0 ? _window.Height / 1080f : 1f;
        scale *= Math.Clamp(_radarSettings.AtlasScale, 0.25f, 4f);
        return (scale, new NumVec2(_radarSettings.AtlasOffsetX, _radarSettings.AtlasOffsetY));
    }

    private static string AtlasRawMapCode(in Poe2Atlas.AtlasNodeLive node)
    {
        foreach (var candidate in node.MapCandidates)
        {
            var colon = candidate.IndexOf(':');
            var value = colon >= 0 ? candidate[(colon + 1)..] : candidate;
            if (value.StartsWith("Map", StringComparison.Ordinal)) return value;
        }
        return "";
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
            graphNodes = _atlas.GraphNodeCount,
            currentGrid = _atlas.CurrentNodeGrid(),
            highlightTags = _radarSettings.AtlasHighlightTags,
            arrowTags = _radarSettings.AtlasArrowTags,
            highlightColors = _radarSettings.AtlasHighlightColors,
            ruleLabels = _radarSettings.AtlasRuleLabels,
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
                    mapSource = n.MapSource,
                    mapCandidates = n.MapCandidates,
                    tags = n.Tags,
                    label = AtlasNodeLabel(n),
                    visible = n.Visible,
                    visited = n.Visited,
                    unlocked = n.Unlocked,
                    hasContent = n.HasContent,
                    biome = n.Biome,
                    icon = n.IconType,
                    gridX = n.GridX,
                    gridY = n.GridY,
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
            lock (_pathLock)
            {
                _manualPathPattern = null;
                _manualPathGridTarget = null;
                _lastPathTarget = "";
            }
            Console.WriteLine("\nPath: back to auto-nearest");
        }
        if (Down(0x76) && DateTime.UtcNow >= _nextToggleAt)
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            var next = _pathing.CycleNext();
            if (next != null)
            {
                lock (_pathLock)
                {
                    _manualPathPattern = next.Pattern;
                    _manualPathGridTarget = null;
                    _lastPathTarget = "";
                }
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
        // F10: Atlas tile inspector/route picker while Atlas is open; otherwise toggle overlay visibility.
        if (Down(0x79) && DateTime.UtcNow >= _nextToggleAt)
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            if (_radarSettings.ShowAtlasNodes && _atlas.LastPanelOpen)
            {
                AtlasRoutePick();
            }
            else
            {
                _overlayVisible = !_overlayVisible;
                Console.WriteLine($"\nOverlay: {(_overlayVisible ? "VISIBLE" : "HIDDEN")}");
            }
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

    private void TickAutoFlask(
        Poe2Live live,
        nint localPlayer,
        IReadOnlyList<Poe2Live.EntityDot> entities)
    {
        if (live.PlayerVitals(localPlayer) is not { } v) return;
        _hpPct = v.HpPct; _manaPct = v.ManaPct;

        if (!_autoFlask) { _flaskNote = "OFF (F8)"; return; }
        if (GetForegroundWindow() != _gameHwnd) { _flaskNote = "paused"; return; }
        _flaskNote = "armed";

        // Count nearby enemies for rule conditions
        var playerGrid = live.PlayerGrid(localPlayer) ?? System.Numerics.Vector2.Zero;
        var nearbyCount = 0;
        foreach (var e in entities)
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
            lock (_pathLock)
            {
                _manualPathPattern = null;
                _manualPathGridTarget = ((int)bestLmGx, (int)bestLmGy);
                _lastPathTarget = "";
            }
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
            lock (_pathLock)
            {
                _manualPathPattern = shortName;
                _manualPathGridTarget = null;
                _lastPathTarget = "";
            }
            Console.WriteLine($"\nAlt+click nav: {shortName}");
        }
    }

    private void UpdatePath(NumVec2 playerGrid)
    {
        lock (_pathLock) UpdatePathCore(playerGrid);
    }

    private void UpdatePathCore(NumVec2 playerGrid)
    {
        if (!_radarSettings.ShowPath || _terrain == null)
        {
            _pathPoints = null;
            _pathTargetName = null;
            _lastPathTarget = "";
            _routeTracker = new RouteTracker();
            return;
        }

        int destX, destY;
        string cacheKey;

        // Priority: 1) Alt+click grid target, 2) F7/Alt+click entity pattern, 3) auto-nearest
        if (_manualPathGridTarget is { } gridTarget)
        {
            destX = gridTarget.X;
            destY = gridTarget.Y;
            cacheKey = $"{_areaHash}:grid:{destX},{destY}";
            _pathTargetName ??= "Custom waypoint";
        }
        else
        {
            string? targetPattern = _manualPathPattern;
            string? resolvedTargetName = null;
            Poe2Live.EntityDot? closest = null;
            var closestDist = float.MaxValue;

            if (targetPattern == null)
            {
                foreach (var e in _entities)
                {
                    if (e.Category == Poe2Live.EntityCategory.Monster && !e.IsAlive) continue;
                    var rule = _displayRules.Resolve(e);
                    if (rule?.Navigable != true) continue;
                    var d = (e.Grid - playerGrid).Length();
                    if (d >= closestDist) continue;
                    closestDist = d;
                    closest = e;
                    targetPattern = e.Metadata;
                    resolvedTargetName = rule.Label ?? rule.Name;
                }

                if (closest == null && _pathing.All.Count > 0)
                {
                    var entityInfo = _entities
                        .Select(e => (e.Metadata, Distance: (e.Grid - playerGrid).Length(), e.IsAlive))
                        .ToList();
                    targetPattern = _pathing.FindNearestPattern(entityInfo!);
                }
            }

            if (targetPattern == null) { _pathPoints = null; _pathTargetName = null; return; }
            _pathTargetName = resolvedTargetName ?? _pathing.All.FirstOrDefault(e =>
                string.Equals(e.Pattern, targetPattern, StringComparison.OrdinalIgnoreCase))?.Label ?? targetPattern;

            if (closest == null)
            {
                foreach (var e in _entities)
                {
                    if (e.Category == Poe2Live.EntityCategory.Monster && !e.IsAlive) continue;
                    if (!e.Metadata.Contains(targetPattern, StringComparison.OrdinalIgnoreCase)) continue;
                    var d = (e.Grid - playerGrid).Length();
                    if (d < closestDist) { closestDist = d; closest = e; }
                }
            }

            if (closest == null)
            {
                if (_manualPathPattern != null) return; // keep last path visible
                _pathPoints = null; _pathTargetName = null; return;
            }
            destX = (int)closest.Value.Grid.X;
            destY = (int)closest.Value.Grid.Y;
            cacheKey = $"{_areaHash}:entity:{targetPattern}:{destX},{destY}";
        }

        var t = _terrain;
        var px = (int)playerGrid.X;
        var py = (int)playerGrid.Y;

        if (px < 0 || px >= t.Width || py < 0 || py >= t.Height ||
            destX < 0 || destX >= t.Width || destY < 0 || destY >= t.Height)
        {
            Console.WriteLine($"  Path dest out of bounds: ({destX},{destY}) grid=({t.Width}x{t.Height})");
            _pathPoints = null;
            return;
        }

        if (cacheKey != _lastPathTarget)
        {
            _lastPathTarget = cacheKey;
            _routeTracker = new RouteTracker();
            _pathPoints = null;
        }

        if (_replanner.TryDrainResults(out var results))
        {
            foreach (var result in results)
            {
                if (result.TargetId != _lastPathTarget) continue;
                _routeTracker.ApplyResult(result.Waypoints, new NumVec2(result.Goal.x, result.Goal.y));
            }
        }

        var goal = new NumVec2(destX, destY);
        _routeTracker.Maintain(playerGrid);
        if (!_routeTracker.ReplanInFlight && _routeTracker.ShouldReplan(playerGrid, goal))
        {
            _routeTracker.MarkReplanRequested(playerGrid);
            _replanner.Enqueue(new BackgroundReplanner.Request(
                cacheKey, t, (px, py), (destX, destY)));
        }

        var current = _routeTracker.CurrentPoints;
        _pathPoints = current.Count == 0
            ? null
            : current.Select(p => (X: p.x, Y: p.y)).ToList();
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
        var manualOffset = Down(0x11); // Ctrl keeps calibration available without fighting PoE map panning.
        if (Down(0x21)) _radarSettings.ScaleMul *= 1.03f;
        else if (Down(0x22)) _radarSettings.ScaleMul /= 1.03f;
        else if (manualOffset && Down(0x25)) _radarSettings.OffsetX -= 4;
        else if (manualOffset && Down(0x27)) _radarSettings.OffsetX += 4;
        else if (manualOffset && Down(0x26)) _radarSettings.OffsetY -= 4;
        else if (manualOffset && Down(0x28)) _radarSettings.OffsetY += 4;
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

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct POINT
    {
        public readonly int X;
        public readonly int Y;
    }

    public void Dispose()
    {
        _modCatalog.Flush();
        _replanner.Dispose();
        _cheats.RestoreAll();
        _settingsForm?.Dispose();
        _api.Dispose();
        _renderer.Dispose();
        _window.Dispose();
    }
}

using POE2Radar.Core.Cheats;
using POE2Radar.Core.Game;
using NumVec2 = System.Numerics.Vector2;

namespace POE2Radar.Overlay;

public readonly record struct AtlasMark(
    float X,
    float Y,
    bool Selected,
    bool HasContent,
    bool Visited,
    bool Unlocked,
    int Biome,
    int IconType,
    string? Label = null,
    string? Color = null,
    bool Arrow = false);

public readonly record struct ItemLabel(
    POE2Radar.Core.Game.Vector3 World,
    string Name,
    string Value,
    bool Highlight,
    bool ShowName);

public readonly record struct RuneLabel(
    float X,
    float Y,
    float W,
    float H,
    string Text,
    uint Color);

public readonly record struct RitualLabel(
    float X,
    float Y,
    float W,
    float H,
    string Text,
    uint Color,
    bool Highlight);

public readonly record struct MonolithReward(
    string Name,
    int Count,
    double Ex,
    int Size,
    string Runes);

public sealed record MonolithMarker(
    NumVec2 Grid,
    int Holes,
    bool IsUnique,
    bool Collected,
    string AnchorName,
    double BestEx,
    string BestName,
    uint Color,
    IReadOnlyList<MonolithReward> Rewards);

public sealed record RenderContext(
    bool InGame,
    bool Active,
    int WindowWidth,
    int WindowHeight,
    NumVec2 PlayerGrid,
    Poe2Live.MapUi Map,
    IReadOnlyList<Poe2Live.EntityDot> Entities,
    IReadOnlyList<Poe2Live.Landmark> Landmarks,
    uint AreaHash,
    Poe2Live.TerrainData? Terrain,
    float ScaleMul,
    float OffsetX,
    float OffsetY,
    float HpPct,
    float ManaPct,
    string FlaskNote,
    string AreaCode,
    int CharLevel,
    float[]? CameraMatrix,
    IReadOnlyDictionary<string, CheatInfo>? CheatStatus = null,
    RadarSettings? Radar = null,
    bool OverlayVisible = true,
    POE2Radar.Overlay.Web.WatchedEntities? Watched = null,
    List<(int X, int Y)>? PathPoints = null,
    List<(float ScreenX, float ScreenY, string Metadata)>? EntityScreenPositions = null,
    List<(float ScreenX, float ScreenY, float GridX, float GridY, string Name)>? LandmarkScreenPositions = null,
    POE2Radar.Core.Pathfinding.ExplorationTracker? Exploration = null,
    string? InspectedName = null,
    string? InspectedMeta = null,
    string? ZoneGuideTitle = null,
    string? ZoneGuideNotes = null,
    bool ShowZoneGuide = false,
    string? PathTargetName = null,
    POE2Radar.Overlay.Web.EntityNameResolver? EntityNames = null,
    string? AreaName = null,
    int AreaAct = 0,
    bool IsTown = false,
    string? CharName = null,
    IReadOnlyList<POE2Radar.Overlay.Web.MapPin>? MapPins = null,
    POE2Radar.Overlay.Web.GameDataService? GameData = null,
    Poe2Live.MinimapUi GameMinimap = default,
    POE2Radar.Overlay.Web.HiddenEntities? Hidden = null,
    POE2Radar.Overlay.Web.DisplayRules? DisplayRules = null,
    POE2Radar.Core.Game.Vector3? PlayerWorld = null,
    IReadOnlyList<ItemLabel>? ItemLabels = null,
    IReadOnlyList<RuneLabel>? RuneLabels = null,
    IReadOnlyList<RitualLabel>? RitualRewards = null,
    IReadOnlyList<MonolithMarker>? Monoliths = null,
    bool ShowMonolithPanel = true,
    IReadOnlyList<Poe2Atlas.AtlasNodeLive>? AtlasNodes = null,
    IReadOnlyList<AtlasMark>? AtlasMarks = null,
    NumVec2? AtlasRouteStart = null,
    NumVec2? AtlasRouteEnd = null,
    IReadOnlyList<NumVec2>? AtlasRoute = null,
    string? AtlasLoadingText = null,
    float AtlasLoadingProgress = 0f);

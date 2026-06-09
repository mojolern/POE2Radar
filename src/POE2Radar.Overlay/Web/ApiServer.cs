using System.Net;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Text.Json;
using POE2Radar.Core.Game;
using POE2Radar.Overlay.Automation;

namespace POE2Radar.Overlay.Web;

public sealed class ApiServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<RadarState> _state;
    private readonly WatchedEntities _watched;
    private readonly HiddenEntities _hidden;
    private readonly PathingTargets _pathing;
    private readonly AutoRuleEngine _autoRules;
    private readonly RadarSettings _settings;
    private readonly ComponentFieldReader? _inspector;
    private readonly EntityNameResolver _entityNames;
    private readonly GameDataService _gameData;
    private readonly Func<object>? _atlasProvider;
    private readonly Action<IReadOnlyList<string>>? _setAtlasPins;
    private volatile bool _running;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WatchedEntities Watched => _watched;

    public ApiServer(
        Func<RadarState> state,
        WatchedEntities watched,
        HiddenEntities hidden,
        RadarSettings settings,
        PathingTargets pathing,
        AutoRuleEngine autoRules,
        ComponentFieldReader? inspector,
        EntityNameResolver entityNames,
        GameDataService gameData,
        Func<object>? atlasProvider = null,
        Action<IReadOnlyList<string>>? setAtlasPins = null,
        int port = 7777)
    {
        _state = state;
        _watched = watched;
        _hidden = hidden;
        _pathing = pathing;
        _autoRules = autoRules;
        _settings = settings;
        _inspector = inspector;
        _entityNames = entityNames;
        _gameData = gameData;
        _atlasProvider = atlasProvider;
        _setAtlasPins = setAtlasPins;
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        _listener.Start();
        _running = true;
        new Thread(Loop) { IsBackground = true, Name = "ApiThread" }.Start();
    }

    private void Loop()
    {
        while (_running)
        {
            HttpListenerContext ctx;
            try { ctx = _listener.GetContext(); }
            catch { return; }
            try { Handle(ctx); }
            catch (Exception ex) { TryWrite(ctx, 500, "application/json", JsonSerializer.Serialize(new { error = ex.Message }, Json)); }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        var method = ctx.Request.HttpMethod;
        var q = ctx.Request.QueryString;
        var s = _state();

        switch (path)
        {
            case "/":
                WriteHtml(ctx, DashboardHtml.Page);
                break;

            case "/health":
                WriteJson(ctx, new { ok = true, inGame = s.InGame });
                break;

            case "/state":
            {
                var counts = s.Entities.GroupBy(e => e.Category).ToDictionary(g => g.Key.ToString(), g => g.Count());
                WriteJson(ctx, new
                {
                    s.InGame, areaCode = s.AreaCode, areaHash = s.AreaHash, areaLevel = s.AreaLevel,
                    areaName = s.AreaName, act = s.Act, isTown = s.IsTown, hasWaypoint = s.HasWaypoint,
                    mapVisible = s.MapVisible, zoom = s.Zoom,
                    map = new { visible = s.MapVisible, shiftX = s.MapShiftX, shiftY = s.MapShiftY, zoom = s.Zoom },
                    minimap = new { available = s.GameMinimapAvailable, shiftX = s.GameMinimapShiftX, shiftY = s.GameMinimapShiftY, zoom = s.GameMinimapZoom },
                    hpPct = s.HpPct, manaPct = s.ManaPct, autoFlask = s.AutoFlask, flask = s.FlaskNote,
                    player = new { x = s.Player.X, y = s.Player.Y, name = s.CharName, level = s.CharLevel },
                    entityCount = s.Entities.Count, counts,
                });
                break;
            }

            case "/landmarks":
            {
                var list = s.Landmarks.OrderBy(l => Dist(l.Center, s.Player)).Select(l => new
                {
                    name = l.Name, path = l.Path, tiles = l.TileCount,
                    x = l.Center.X, y = l.Center.Y, dist = (int)Dist(l.Center, s.Player),
                });
                WriteJson(ctx, list);
                break;
            }

            case "/entities":
            {
                var category = q["category"];
                var aliveOnly = string.Equals(q["alive"], "true", StringComparison.OrdinalIgnoreCase);
                _ = float.TryParse(q["radius"], out var radius);
                _ = int.TryParse(q["limit"], out var limit);
                if (limit <= 0) limit = 500;

                IEnumerable<Poe2Live.EntityDot> q2 = s.Entities;
                if (!string.IsNullOrEmpty(category))
                    q2 = q2.Where(e => string.Equals(e.Category.ToString(), category, StringComparison.OrdinalIgnoreCase));
                if (aliveOnly) q2 = q2.Where(e => e.HpCur > 0);
                if (radius > 0) q2 = q2.Where(e => Dist(e.Grid, s.Player) <= radius);

                var list = q2.OrderBy(e => Dist(e.Grid, s.Player)).Take(limit).Select(e => new
                {
                    addr = $"0x{e.Address.ToInt64():X}",
                    id = e.Id, category = e.Category.ToString(), metadata = e.Metadata,
                    name = _entityNames.ResolveOrShorten(e.Metadata),
                    poi = e.Poi, friendly = e.IsFriendly, rarity = e.Rarity.ToString(),
                    x = e.Grid.X, y = e.Grid.Y, hpCur = e.HpCur, hpMax = e.HpMax,
                    alive = e.IsAlive, dist = (int)Dist(e.Grid, s.Player),
                    boss = e.IsBoss, league = e.League.ToString(), locked = e.IsLocked, large = e.IsLarge,
                    watched = _watched.IsWatched(e.Metadata),
                });
                WriteJson(ctx, list);
                break;
            }

            case "/api/watched":
            {
                if (method == "GET")
                {
                    WriteJson(ctx, _watched.All.Values.ToList());
                }
                else if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    var entry = JsonSerializer.Deserialize<WatchedEntry>(body, Json);
                    if (entry != null)
                    {
                        _watched.Add(entry.Pattern, entry.Label, entry.Color, entry.Size);
                        WriteJson(ctx, new { ok = true });
                    }
                    else WriteJson(ctx, new { error = "bad json" }, 400);
                }
                else if (method == "PUT")
                {
                    var body = ReadBody(ctx);
                    var patch = JsonSerializer.Deserialize<WatchedEntry>(body, Json);
                    if (patch != null)
                    {
                        _watched.Update(patch.Pattern, patch.Label, patch.Color, patch.Enabled, patch.Size);
                        WriteJson(ctx, new { ok = true });
                    }
                    else WriteJson(ctx, new { error = "bad json" }, 400);
                }
                else if (method == "DELETE")
                {
                    var pattern = q["pattern"];
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        _watched.Remove(pattern);
                        WriteJson(ctx, new { ok = true });
                    }
                    else WriteJson(ctx, new { error = "missing pattern" }, 400);
                }
                break;
            }

            case "/api/hidden":
            {
                if (method == "GET")
                {
                    WriteJson(ctx, _hidden.All.Order(StringComparer.OrdinalIgnoreCase).ToArray());
                }
                else if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body, Json);
                    if (data != null && data.TryGetValue("pattern", out var pattern))
                    {
                        _hidden.Add(pattern);
                        WriteJson(ctx, new { ok = true });
                    }
                    else WriteJson(ctx, new { error = "missing pattern" }, 400);
                }
                else if (method == "DELETE")
                {
                    var pattern = q["pattern"];
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        _hidden.Remove(pattern);
                        WriteJson(ctx, new { ok = true });
                    }
                    else WriteJson(ctx, new { error = "missing pattern" }, 400);
                }
                break;
            }

            case "/api/settings":
            {
                if (method == "GET")
                    WriteJson(ctx, _settings);
                else if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    var patch = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
                    if (patch != null) ApplySettings(patch);
                    WriteJson(ctx, _settings);
                }
                break;
            }

            case "/api/settings/reset":
            {
                if (method == "POST")
                {
                    _settings.ResetToDefaults();
                    WriteJson(ctx, new { ok = true });
                }
                else WriteJson(ctx, new { error = "method not allowed" }, 405);
                break;
            }

            case "/api/watched/export":
            {
                var json = JsonSerializer.Serialize(_watched.All.Values.ToList(), Json);
                ctx.Response.AddHeader("Content-Disposition", "attachment; filename=watched_entities.json");
                TryWrite(ctx, 200, "application/json", json);
                break;
            }

            case "/api/watched/import":
            {
                if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<WatchedEntry>>(body, Json);
                        var added = 0;
                        if (list != null)
                        {
                            foreach (var e in list)
                            {
                                if (!string.IsNullOrEmpty(e.Pattern))
                                {
                                    _watched.Add(e.Pattern, e.Label ?? e.Pattern.Split('/')[^1], e.Color ?? "#ff5555");
                                    added++;
                                }
                            }
                        }
                        WriteJson(ctx, new { ok = true, imported = added, total = _watched.All.Count });
                    }
                    catch (Exception ex) { WriteJson(ctx, new { error = ex.Message }, 400); }
                }
                break;
            }

            case "/api/pathing":
            {
                if (method == "GET")
                    WriteJson(ctx, new { targets = _pathing.All, current = _pathing.CurrentIndex });
                else if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    var entry = JsonSerializer.Deserialize<PathingEntry>(body, Json);
                    if (entry != null) { _pathing.Add(entry.Pattern, entry.Label); WriteJson(ctx, new { ok = true }); }
                    else WriteJson(ctx, new { error = "bad json" }, 400);
                }
                else if (method == "PUT")
                {
                    var body = ReadBody(ctx);
                    var entry = JsonSerializer.Deserialize<PathingEntry>(body, Json);
                    if (entry != null) { _pathing.Update(entry.Pattern, entry.Label, entry.Enabled); WriteJson(ctx, new { ok = true }); }
                    else WriteJson(ctx, new { error = "bad json" }, 400);
                }
                else if (method == "DELETE")
                {
                    var pattern = q["pattern"];
                    if (!string.IsNullOrEmpty(pattern)) { _pathing.Remove(pattern); WriteJson(ctx, new { ok = true }); }
                    else WriteJson(ctx, new { error = "missing pattern" }, 400);
                }
                break;
            }

            case "/api/pathing/cycle":
            {
                var next = _pathing.CycleNext();
                WriteJson(ctx, new { ok = true, current = next?.Pattern, label = next?.Label });
                break;
            }

            case "/api/rules":
            {
                if (method == "GET")
                    WriteJson(ctx, new { enabled = _autoRules.Enabled, rules = _autoRules.Rules });
                else if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    var rule = JsonSerializer.Deserialize<AutoRule>(body, Json);
                    if (rule != null) { _autoRules.Add(rule); WriteJson(ctx, new { ok = true }); }
                    else WriteJson(ctx, new { error = "bad json" }, 400);
                }
                else if (method == "PUT")
                {
                    var body = ReadBody(ctx);
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
                    if (data != null && data.TryGetValue("index", out var idxEl))
                    {
                        var idx = idxEl.GetInt32();
                        if (data.TryGetValue("enabled", out var enEl))
                            _autoRules.Enabled = enEl.GetBoolean();
                        if (data.TryGetValue("rule", out var ruleEl))
                        {
                            var rule = JsonSerializer.Deserialize<AutoRule>(ruleEl.GetRawText(), Json);
                            if (rule != null) _autoRules.Update(idx, rule);
                        }
                        WriteJson(ctx, new { ok = true });
                    }
                    else WriteJson(ctx, new { error = "missing index" }, 400);
                }
                else if (method == "DELETE")
                {
                    var idxStr = q["index"];
                    if (int.TryParse(idxStr, out var idx)) { _autoRules.Remove(idx); WriteJson(ctx, new { ok = true }); }
                    else WriteJson(ctx, new { error = "missing index" }, 400);
                }
                break;
            }

            case "/api/rules/toggle":
            {
                _autoRules.Enabled = !_autoRules.Enabled;
                WriteJson(ctx, new { enabled = _autoRules.Enabled });
                break;
            }

            case "/api/database":
            {
                WriteJson(ctx, LoadEntityDatabase());
                break;
            }

            case "/api/gamedata/areas":
            {
                WriteJson(ctx, _gameData.SearchAreas(q["search"]).Select(a => new
                {
                    code = a.Code, name = a.Name, act = a.Act, level = a.Level, town = a.Town, waypoint = a.Waypoint,
                }));
                break;
            }

            case "/api/gamedata/buffs":
            {
                _ = int.TryParse(q["limit"], out var limit);
                WriteJson(ctx, _gameData.SearchBuffs(q["search"], limit).Select(b => new
                {
                    id = b.Id, name = b.Name, description = b.Description,
                }));
                break;
            }

            case "/api/gamedata/pins":
            {
                WriteJson(ctx, new { area = s.AreaCode, pins = _gameData.GetPins(s.AreaCode) });
                break;
            }

            case "/api/atlas":
            {
                if (method == "GET")
                    WriteJson(ctx, _atlasProvider?.Invoke() ?? new { open = false, total = 0, nodeList = Array.Empty<object>() });
                else
                    WriteJson(ctx, new { error = "method not allowed" }, 405);
                break;
            }

            case "/api/atlas-pins":
            {
                if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body, Json);
                    var pins = new List<string>();
                    if (data != null && data.TryGetValue("pins", out var pinsEl) && pinsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var pinEl in pinsEl.EnumerateArray())
                        {
                            if (pinEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(pinEl.GetString()))
                                pins.Add(pinEl.GetString()!.Trim());
                            else if (pinEl.ValueKind == JsonValueKind.Number && pinEl.TryGetInt64(out var numeric))
                                pins.Add($"0x{numeric:X}");
                        }
                    }
                    _setAtlasPins?.Invoke(pins);
                    WriteJson(ctx, new { ok = true, count = pins.Count });
                }
                else WriteJson(ctx, new { error = "method not allowed" }, 405);
                break;
            }

            case "/api/inspect/components":
            {
                HandleInspectComponents(ctx);
                break;
            }

            case "/api/inspect/schema":
            {
                HandleInspectSchema(ctx, q["component"]);
                break;
            }

            case "/api/inspect":
            {
                HandleInspectEntity(ctx, q["entity"], q["component"]);
                break;
            }

            default:
                WriteJson(ctx, new { error = "not found", path }, 404);
                break;
        }
    }

    private void HandleInspectComponents(HttpListenerContext ctx)
    {
        if (_inspector == null)
        {
            WriteJson(ctx, new { error = "inspector not loaded - place OtIdaOffsets.json in config/" }, 503);
            return;
        }

        WriteJson(ctx, _inspector.Components.Select(c => new
        {
            name = c.Key,
            fieldCount = c.Value.Fields.Count,
            byteSetter = c.Value.ByteSetter,
            intSetter = c.Value.IntSetter,
            floatSetter = c.Value.FloatSetter,
            anchor = c.Value.StringAnchor,
        }));
    }

    private void HandleInspectSchema(HttpListenerContext ctx, string? componentName)
    {
        if (_inspector == null)
        {
            WriteJson(ctx, new { error = "inspector not loaded - place OtIdaOffsets.json in config/" }, 503);
            return;
        }

        if (string.IsNullOrWhiteSpace(componentName))
        {
            WriteJson(ctx, _inspector.ComponentNames);
            return;
        }

        if (!_inspector.Components.TryGetValue(componentName, out var cdef))
        {
            WriteJson(ctx, new { error = "unknown component" }, 404);
            return;
        }

        WriteJson(ctx, new
        {
            name = cdef.Name,
            byteSetter = cdef.ByteSetter,
            intSetter = cdef.IntSetter,
            floatSetter = cdef.FloatSetter,
            anchor = cdef.StringAnchor,
            notes = cdef.ComponentNotes,
            fields = cdef.Fields.Select(f => new
            {
                name = f.Name,
                offset = $"0x{f.Offset:X}",
                type = f.Type,
                verified = f.Verified,
                notes = f.Notes,
            }),
        });
    }

    private void HandleInspectEntity(HttpListenerContext ctx, string? entityAddress, string? componentName)
    {
        if (_inspector == null)
        {
            WriteJson(ctx, new { error = "inspector not loaded - place OtIdaOffsets.json in config/" }, 503);
            return;
        }

        if (string.IsNullOrWhiteSpace(entityAddress) || !TryParseAddress(entityAddress, out var entity))
        {
            WriteJson(ctx, new { error = "entity parameter required (hex address)" }, 400);
            return;
        }

        if (!string.IsNullOrWhiteSpace(componentName))
        {
            WriteJson(ctx, new
            {
                entity = $"0x{entity:X}",
                component = componentName,
                fields = _inspector.ReadComponent(entity, componentName),
            });
            return;
        }

        WriteJson(ctx, new
        {
            entity = $"0x{entity:X}",
            components = _inspector.ReadAllComponents(entity),
        });
    }

    private static bool TryParseAddress(string text, out nint address)
    {
        address = 0;
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];

        if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;

        address = (nint)value;
        return true;
    }

    private void ApplySettings(Dictionary<string, JsonElement> patch)
    {
        foreach (var (key, val) in patch)
        {
            var prop = typeof(RadarSettings).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            if (prop == null) continue;
            try
            {
                if (prop.PropertyType == typeof(float))
                    prop.SetValue(_settings, val.GetSingle());
                else if (prop.PropertyType == typeof(bool))
                    prop.SetValue(_settings, val.GetBoolean());
                else if (prop.PropertyType == typeof(string))
                    prop.SetValue(_settings, val.GetString());
                else if (prop.PropertyType == typeof(int))
                    prop.SetValue(_settings, val.GetInt32());
                else
                {
                    var parsed = JsonSerializer.Deserialize(val.GetRawText(), prop.PropertyType, Json);
                    if (parsed != null) prop.SetValue(_settings, parsed);
                }
            }
            catch { }
        }
        _settings.Save();
    }

    private static string[]? _dbCache;
    private static string[] LoadEntityDatabase()
    {
        if (_dbCache != null) return _dbCache;
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.Contains("entity_database"));
            if (name == null) return _dbCache = [];
            using var stream = asm.GetManifestResourceStream(name)!;
            _dbCache = JsonSerializer.Deserialize<string[]>(stream) ?? [];
        }
        catch { _dbCache = []; }
        return _dbCache;
    }

    private static float Dist(System.Numerics.Vector2 a, System.Numerics.Vector2 b) => (a - b).Length();

    private static string ReadBody(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
        return reader.ReadToEnd();
    }

    private static void WriteJson(HttpListenerContext ctx, object data, int status = 200)
        => TryWrite(ctx, status, "application/json", JsonSerializer.Serialize(data, Json));

    private static void WriteHtml(HttpListenerContext ctx, string html)
        => TryWrite(ctx, 200, "text/html; charset=utf-8", html);

    private static void TryWrite(HttpListenerContext ctx, int status, string contentType, string body)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }
        catch { }
    }

    public void Dispose()
    {
        _running = false;
        try { _listener.Stop(); } catch { }
        _listener.Close();
    }
}

public sealed record RadarState(
    bool InGame, uint AreaHash, int AreaLevel, bool MapVisible, float Zoom,
    System.Numerics.Vector2 Player,
    IReadOnlyList<Poe2Live.EntityDot> Entities,
    IReadOnlyList<Poe2Live.Landmark> Landmarks,
    float HpPct, float ManaPct, bool AutoFlask, string FlaskNote,
    string AreaCode, string CharName, int CharLevel,
    string? AreaName = null, int Act = 0, bool IsTown = false, bool HasWaypoint = false,
    float MapShiftX = 0, float MapShiftY = 0,
    bool GameMinimapAvailable = false, float GameMinimapShiftX = 0, float GameMinimapShiftY = 0, float GameMinimapZoom = 0)
{
    public static readonly RadarState Empty =
        new(false, 0, 0, false, 0, System.Numerics.Vector2.Zero,
            Array.Empty<Poe2Live.EntityDot>(), Array.Empty<Poe2Live.Landmark>(), 100, 100, false, "", "", "", 0);
}

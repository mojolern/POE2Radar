using System.Text.Json;

namespace POE2Radar.Overlay.Web;

public sealed record WorldArea(string Code, string Name, int Act, int Level, bool Town, bool Waypoint);
public sealed record BuffInfo(string Id, string Name, string? Description);
public sealed record MapPin(string Name, string Type);

public sealed class EntityNameResolver
{
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);

    public EntityNameResolver(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(filePath));
            if (map is null) return;
            foreach (var (key, value) in map)
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    _names[key] = value;
        }
        catch { }
    }

    public string ResolveOrShorten(string metadata)
    {
        if (_names.TryGetValue(metadata, out var exact)) return exact;
        var shortName = metadata.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? metadata;
        return shortName.EndsWith(".tdt", StringComparison.OrdinalIgnoreCase) ? shortName[..^4] : shortName;
    }
}

public sealed class GameDataService
{
    private readonly Dictionary<string, WorldArea> _areas = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BuffInfo> _buffs = [];
    private readonly Dictionary<string, List<MapPin>> _pins = new(StringComparer.OrdinalIgnoreCase);

    public GameDataService(string configDir)
    {
        LoadAreas(Path.Combine(configDir, "world_areas.json"));
        LoadBuffs(Path.Combine(configDir, "buff_names.json"));
        LoadPins(Path.Combine(configDir, "map_pins.json"));
    }

    public WorldArea? GetArea(string codeOrName)
    {
        if (_areas.TryGetValue(codeOrName, out var area)) return area;
        return _areas.Values.FirstOrDefault(a =>
            string.Equals(a.Name, codeOrName, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<WorldArea> SearchAreas(string? search)
    {
        var q = search ?? "";
        return _areas.Values
            .Where(a => q.Length == 0 ||
                a.Code.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Act)
            .ThenBy(a => a.Level)
            .ThenBy(a => a.Name)
            .Take(500)
            .ToArray();
    }

    public IReadOnlyList<BuffInfo> SearchBuffs(string? search, int limit)
    {
        var q = search ?? "";
        if (limit <= 0) limit = 500;
        return _buffs
            .Where(b => q.Length == 0 ||
                b.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (b.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(limit)
            .ToArray();
    }

    public IReadOnlyList<MapPin> GetPins(string? areaCode)
    {
        if (areaCode is not null && _pins.TryGetValue(areaCode, out var pins)) return pins;
        return Array.Empty<MapPin>();
    }

    private void LoadAreas(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var item in EnumerateObjects(doc.RootElement))
            {
                var code = ReadString(item, "code", "id", "Id", "Code") ?? "";
                var name = ReadString(item, "name", "Name") ?? code;
                if (string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name)) code = name;
                if (string.IsNullOrWhiteSpace(code)) continue;
                var area = new WorldArea(code, name, ReadInt(item, "act", "Act"), ReadInt(item, "level", "Level", "areaLevel"),
                    ReadBool(item, "town", "isTown", "IsTown"), ReadBool(item, "waypoint", "hasWaypoint", "HasWaypoint"));
                _areas[code] = area;
            }
        }
        catch { }
    }

    private void LoadBuffs(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var item in EnumerateObjects(doc.RootElement))
            {
                var id = ReadString(item, "id", "Id", "key") ?? "";
                var name = ReadString(item, "name", "Name") ?? id;
                if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name)) id = name;
                if (string.IsNullOrWhiteSpace(id)) continue;
                _buffs.Add(new BuffInfo(id, name, ReadString(item, "description", "Description", "desc")));
            }
        }
        catch { }
    }

    private void LoadPins(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            foreach (var area in doc.RootElement.EnumerateObject())
            {
                var pins = new List<MapPin>();
                foreach (var item in EnumerateObjects(area.Value))
                {
                    var name = ReadString(item, "name", "Name", "label", "Label") ?? "";
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    pins.Add(new MapPin(name, ReadString(item, "type", "Type") ?? "pin"));
                }
                if (pins.Count > 0) _pins[area.Name] = pins;
            }
        }
        catch { }
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object) yield return item;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                    yield return prop.Value;
            }
        }
    }

    private static string? ReadString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
            if (item.TryGetProperty(name, out var value))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return null;
    }

    private static int ReadInt(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)) return i;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out i)) return i;
        }
        return 0;
    }

    private static bool ReadBool(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var b)) return b;
        }
        return false;
    }
}

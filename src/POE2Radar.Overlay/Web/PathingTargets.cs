using System.Text.Json;

namespace POE2Radar.Overlay.Web;

public sealed class PathingTargets
{
    private readonly string _filePath;
    private readonly List<PathingEntry> _entries = new();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public IReadOnlyList<PathingEntry> All => _entries;
    public int CurrentIndex { get; set; }

    public PathingTargets(string filePath)
    {
        _filePath = filePath;
        Load();
        if (_entries.Count == 0) LoadDefaults();
    }

    public PathingEntry? Current => _entries.Count > 0 && CurrentIndex < _entries.Count
        ? _entries[CurrentIndex] : null;

    public PathingEntry? CycleNext()
    {
        if (_entries.Count == 0) return null;
        var enabled = _entries.Where(e => e.Enabled).ToList();
        if (enabled.Count == 0) return null;

        var curPattern = Current?.Pattern;
        var idx = enabled.FindIndex(e => e.Pattern == curPattern);
        idx = (idx + 1) % enabled.Count;

        var target = enabled[idx];
        CurrentIndex = _entries.IndexOf(target);
        return target;
    }

    public string? FindNearestPattern(IReadOnlyList<(string Metadata, float Distance, bool Alive)> entities)
    {
        var enabled = _entries.Where(e => e.Enabled).ToList();
        if (enabled.Count == 0) return null;

        string? bestPattern = null;
        var bestDist = float.MaxValue;

        foreach (var entry in enabled)
        {
            foreach (var (meta, dist, alive) in entities)
            {
                if (!alive && dist > 0) continue;
                if (!meta.Contains(entry.Pattern, StringComparison.OrdinalIgnoreCase)) continue;
                if (dist < bestDist) { bestDist = dist; bestPattern = entry.Pattern; }
            }
        }
        return bestPattern;
    }

    public void Add(string pattern, string label)
    {
        if (_entries.Any(e => e.Pattern.Equals(pattern, StringComparison.OrdinalIgnoreCase))) return;
        _entries.Add(new PathingEntry(pattern, label, true));
        Save();
    }

    public void Remove(string pattern)
    {
        _entries.RemoveAll(e => e.Pattern.Equals(pattern, StringComparison.OrdinalIgnoreCase));
        if (CurrentIndex >= _entries.Count) CurrentIndex = 0;
        Save();
    }

    public void Update(string pattern, string? label = null, bool? enabled = null)
    {
        var idx = _entries.FindIndex(e => e.Pattern.Equals(pattern, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;
        var e = _entries[idx];
        _entries[idx] = e with { Label = label ?? e.Label, Enabled = enabled ?? e.Enabled };
        Save();
    }

    public void Reorder(List<string> patterns)
    {
        var map = _entries.ToDictionary(e => e.Pattern, StringComparer.OrdinalIgnoreCase);
        _entries.Clear();
        foreach (var p in patterns)
            if (map.TryGetValue(p, out var e)) _entries.Add(e);
        foreach (var e in map.Values)
            if (!_entries.Contains(e)) _entries.Add(e);
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var list = JsonSerializer.Deserialize<List<PathingEntry>>(File.ReadAllText(_filePath), Json);
            if (list != null) _entries.AddRange(list);
        }
        catch { }
    }

    private void LoadDefaults()
    {
        _entries.AddRange([
            new("AreaTransition", "Area Exit", true),
            new("Waypoint", "Waypoint", true),
            new("Checkpoint", "Checkpoint", true),
            new("QuestChest", "Quest Chest", true),
            new("QuestObject", "Quest Object", true),
            new("Shrine", "Shrine", false),
            new("Strongbox", "Strongbox", false),
        ]);
        Save();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_entries, Json));
        }
        catch { }
    }
}

public sealed record PathingEntry(string Pattern, string Label, bool Enabled);

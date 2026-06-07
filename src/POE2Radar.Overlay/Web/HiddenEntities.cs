using System.Text.Json;
using System.Text.RegularExpressions;

namespace POE2Radar.Overlay.Web;

public sealed class HiddenEntities
{
    private readonly string _filePath;
    private readonly HashSet<string> _patterns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Regex> _regexCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public HiddenEntities(string filePath)
    {
        _filePath = filePath;
        Load();
        if (_patterns.Count == 0) LoadDefaults();
    }

    public IReadOnlyCollection<string> All => _patterns;

    public bool IsHidden(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var pattern in _patterns)
        {
            if (IsGlob(pattern))
            {
                if (GetOrCreateRegex(pattern).IsMatch(text)) return true;
            }
            else if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public void Add(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return;
        if (_patterns.Add(pattern.Trim())) Save();
    }

    public void Remove(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return;
        if (_patterns.Remove(pattern))
        {
            _regexCache.Remove(pattern);
            Save();
        }
    }

    public void Clear()
    {
        _patterns.Clear();
        _regexCache.Clear();
        Save();
    }

    private static bool IsGlob(string pattern) => pattern.Contains('*') || pattern.Contains('?');

    private Regex GetOrCreateRegex(string pattern)
    {
        if (_regexCache.TryGetValue(pattern, out var cached)) return cached;
        var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        _regexCache[pattern] = regex;
        return regex;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_filePath), Json);
            if (list is null) return;
            foreach (var pattern in list.Where(p => !string.IsNullOrWhiteSpace(p)))
                _patterns.Add(pattern);
        }
        catch { }
    }

    private void LoadDefaults()
    {
        _patterns.Add("AbyssCrack");
        Save();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_patterns.Order().ToList(), Json));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to save hidden entities: " + ex.Message);
        }
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace POE2Radar.Overlay;

public static partial class IconLibrary
{
    public static string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "icons");

    public static IReadOnlyList<IconDef> Ordered => Library.Value.Ordered;
    public static IReadOnlyDictionary<string, IconDef> Map => Library.Value.Map;

    public static bool Contains(string? name) => !string.IsNullOrEmpty(name) && Map.ContainsKey(name);

    public static string? Canonical(string? name) =>
        !string.IsNullOrEmpty(name) && Map.TryGetValue(name, out var def) ? def.Name : null;

    private static (IReadOnlyList<IconDef> Ordered, IReadOnlyDictionary<string, IconDef> Map) Load()
    {
        var builtins = BuiltIns();
        try
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.CreateDirectory(Directory);
                foreach (var icon in builtins)
                    File.WriteAllText(Path.Combine(Directory, icon.Name + ".svg"), ToSvg(icon));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Icon library materialize failed: " + ex.Message);
        }

        var disk = new Dictionary<string, IconDef>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                foreach (var path in System.IO.Directory.EnumerateFiles(Directory, "*.svg"))
                {
                    var def = TryParseSvgFile(path);
                    if (def is not null) disk[def.Name] = def;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Icon library load failed: " + ex.Message);
        }

        var ordered = new List<IconDef>();
        var map = new Dictionary<string, IconDef>(StringComparer.OrdinalIgnoreCase);
        foreach (var builtin in builtins)
        {
            var def = disk.Remove(builtin.Name, out var custom) ? custom : builtin;
            ordered.Add(def);
            map[def.Name] = def;
        }
        foreach (var def in disk.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            ordered.Add(def);
            map[def.Name] = def;
        }
        return (ordered, map);
    }

    private static IconDef? TryParseSvgFile(string path)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name)) return null;
            var text = File.ReadAllText(path);

            var vx = 0f;
            var vy = 0f;
            var vw = 24f;
            var vh = 24f;
            var vb = ViewBoxRegex().Match(text);
            if (vb.Success)
            {
                var parts = vb.Groups[1].Value.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var c) &&
                    float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) &&
                    c > 0 && d > 0)
                {
                    vx = a; vy = b; vw = c; vh = d;
                }
            }

            var paths = PathRegex().Matches(text)
                .Select(m => m.Groups[1].Value.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            return paths.Length == 0 ? null : new IconDef(name, vx, vy, vw, vh, paths);
        }
        catch
        {
            return null;
        }
    }

    private static string ToSvg(IconDef icon)
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"").Append(icon.ViewBox).AppendLine("\">");
        foreach (var path in icon.Paths)
            sb.Append("  <path d=\"").Append(path).AppendLine("\" />");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static List<IconDef> BuiltIns() =>
    [
        One("Circle", "M12 0 C18.627 0 24 5.373 24 12 C24 18.627 18.627 24 12 24 C5.373 24 0 18.627 0 12 C0 5.373 5.373 0 12 0 Z"),
        One("Square", "M1.2 1.2 L22.8 1.2 L22.8 22.8 L1.2 22.8 Z"),
        One("Triangle", "M12 0 L22.392 18 L1.608 18 Z"),
        One("Diamond", "M12 0 L24 12 L12 24 L0 12 Z"),
        One("Plus", "M7.68 0 L16.32 0 L16.32 7.68 L24 7.68 L24 16.32 L16.32 16.32 L16.32 24 L7.68 24 L7.68 16.32 L0 16.32 L0 7.68 L7.68 7.68 Z"),
        One("Star", "M12 0 L14.962 7.922 L23.413 8.292 L16.793 13.557 L19.053 21.708 L12 17.04 L4.947 21.708 L7.207 13.557 L0.587 8.292 L9.038 7.922 Z"),
        One("Hexagon", "M12 0 L22.392 6 L22.392 18 L12 24 L1.608 18 L1.608 6 Z"),
        One("Pentagon", "M12 0 L23.413 8.292 L19.053 21.708 L4.947 21.708 L0.587 8.292 Z"),
        One("TriangleDown", "M12 24 L1.608 6 L22.392 6 Z"),
        One("ArrowUp", "M12 1 L22 13 L16 13 L16 23 L8 23 L8 13 L2 13 Z"),
        One("Cross", "M6 2.5 L12 8.5 L18 2.5 L21.5 6 L15.5 12 L21.5 18 L18 21.5 L12 15.5 L6 21.5 L2.5 18 L8.5 12 L2.5 6 Z"),
        One("Heart", "M12 21 C12 21 2.5 14.5 2.5 8 C2.5 4.9 4.9 2.5 7.8 2.5 C9.7 2.5 11.2 3.6 12 5 C12.8 3.6 14.3 2.5 16.2 2.5 C19.1 2.5 21.5 4.9 21.5 8 C21.5 14.5 12 21 12 21 Z"),
        One("Droplet", "M12 1.5 C12 1.5 4.5 11 4.5 16 C4.5 20.1 7.9 23 12 23 C16.1 23 19.5 20.1 19.5 16 C19.5 11 12 1.5 12 1.5 Z"),
        One("Gem", "M12 1.5 L20.5 9 L12 22.5 L3.5 9 Z"),
        One("Ring", "M12 1 C18.075 1 23 5.925 23 12 C23 18.075 18.075 23 12 23 C5.925 23 1 18.075 1 12 C1 5.925 5.925 1 12 1 Z M12 6 C15.314 6 18 8.686 18 12 C18 15.314 15.314 18 12 18 C8.686 18 6 15.314 6 12 C6 8.686 8.686 6 12 6 Z"),
        One("Shield", "M12 1.5 L20.5 4.5 L20.5 11 C20.5 16.3 16.7 21 12 22.5 C7.3 21 3.5 16.3 3.5 11 L3.5 4.5 Z"),
        One("Exclamation", "M10.4 2.5 L13.6 2.5 L13 14.5 L11 14.5 Z M12 17.5 C13.105 17.5 14 18.395 14 19.5 C14 20.605 13.105 21.5 12 21.5 C10.895 21.5 10 20.605 10 19.5 C10 18.395 10.895 17.5 12 17.5 Z"),
    ];

    private static IconDef One(string name, string path) => new(name, 0, 0, 24, 24, [path]);

    private static readonly Lazy<(IReadOnlyList<IconDef> Ordered, IReadOnlyDictionary<string, IconDef> Map)> Library = new(Load);

    [GeneratedRegex("viewBox\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ViewBoxRegex();

    [GeneratedRegex("<path\\b[^>]*?\\bd\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PathRegex();
}

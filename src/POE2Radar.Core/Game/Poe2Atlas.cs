using System.Reflection;
using System.Text.Json;
using POE2Radar.Core;

namespace POE2Radar.Core.Game;

/// <summary>
/// Lightweight live Atlas node reader based on the Sikaka upstream implementation.
/// Atlas nodes are UiElements under one canvas; their +0x118 relative position is already
/// updated by the game for pan, while +0x130 is the live Atlas zoom.
/// </summary>
public sealed class Poe2Atlas
{
    private const int UiChildrenEnd = Poe2.UiElement.Children + 8;
    private const int UiParent = 0xB8;
    private const int UiRelativePos = 0x118;
    private const int UiSizeW = 0x288;
    private const int UiSizeH = 0x28C;
    private const int AtlasMapNodeId = 0x300;
    private const int AtlasContent = 0x310;
    private const int AtlasState = 0x32C;
    private const int AtlasBiome = 0x32E;
    private const int AtlasFlags = 0x32F;
    private const int AtlasCompletion = 0x339;

    private readonly MemoryReader _reader;
    private readonly object _nodeLock = new();
    private readonly Dictionary<nint, ResolvedAtlasInfo> _tagCache = new();

    private nint _nodeVtable;
    private nint _nodeCanvas;
    private int _nodeRetry;
    private int _hiddenTicks;

    private static readonly string[] NoTags = Array.Empty<string>();
    private static readonly string[] NoCandidates = Array.Empty<string>();
    private static readonly Lazy<IReadOnlyDictionary<string, string>> AreaNames = new(LoadAreaNames);

    public Poe2Atlas(MemoryReader reader) => _reader = reader;

    public readonly record struct AtlasNodeLive(
        nint Element,
        uint Id,
        uint Content,
        byte State,
        byte Biome,
        byte Flags,
        byte Completion,
        float X,
        float Y,
        float W,
        float H,
        float Scale,
        bool Visible,
        int IconType,
        string MapName,
        string MapSource,
        IReadOnlyList<string> MapCandidates,
        IReadOnlyList<string> Tags)
    {
        public bool Unlocked => (Flags & 0x01) != 0;
        public bool Visited => (Flags & 0x02) != 0;
        public bool HasContent => Content != 0 || Tags.Count > 0;
    }

    public bool AllTagsResolved { get; private set; }
    public bool LastPanelOpen { get; private set; }
    public string LoadStatus { get; private set; } = "";
    public float LoadProgress { get; private set; }

    public List<AtlasNodeLive> ReadNodes(nint inGameState)
    {
        var nodes = new List<AtlasNodeLive>();
        var uiRoot = Ptr(inGameState + Poe2.InGameState.UiRoot);
        if (uiRoot == 0)
        {
            SetIdle();
            return nodes;
        }

        lock (_nodeLock)
        {
            if (_nodeCanvas != 0 && _nodeVtable != 0)
            {
                if (HierarchicallyVisible(_nodeCanvas))
                {
                    _hiddenTicks = 0;
                    if (ReadCanvasNodes(_nodeCanvas, nodes)) return nodes;
                }
                else
                {
                    var liveSelf = Ptr(_nodeCanvas + Poe2.UiElement.Self) == _nodeCanvas;
                    if (liveSelf && ++_hiddenTicks % 150 != 0) return nodes;
                    Invalidate();
                }
            }

            if (!AtlasPanelOpen(uiRoot))
            {
                _nodeRetry = 0;
                SetIdle();
                return nodes;
            }

            LastPanelOpen = true;
            LoadStatus = "Atlas: locating node layer";
            LoadProgress = 0.12f;
            if (_nodeRetry > 0 && _nodeRetry++ % 5 != 0) return nodes;
            _nodeRetry++;

            if (!DetectNodeClass(uiRoot)) return nodes;
            _nodeRetry = 0;
            if (HierarchicallyVisible(_nodeCanvas)) ReadCanvasNodes(_nodeCanvas, nodes);
            return nodes;
        }
    }

    private bool ReadCanvasNodes(nint canvas, List<AtlasNodeLive> outNodes)
    {
        var first = Ptr(canvas + Poe2.UiElement.Children);
        if (first == 0 || !_reader.TryReadStruct<nint>(canvas + UiChildrenEnd, out var last))
        {
            Invalidate();
            return false;
        }

        var count = ((long)last - (long)first) / 8;
        if (count is <= 0 or > 20000)
        {
            Invalidate();
            return false;
        }

        var matched = 0;
        var resolveBudget = _tagCache.Count < 512 ? 240 : 120;
        var allCached = true;
        var resolvedCount = 0;
        for (long i = 0; i < count; i++)
        {
            var el = Ptr(first + (nint)(i * 8));
            if (el == 0 || Ptr(el) != _nodeVtable) continue;
            matched++;

            _reader.TryReadStruct<uint>(el + AtlasMapNodeId, out var id);
            _reader.TryReadStruct<uint>(el + AtlasContent, out var content);
            _reader.TryReadStruct<byte>(el + AtlasState, out var state);
            _reader.TryReadStruct<byte>(el + AtlasBiome, out var biome);
            _reader.TryReadStruct<byte>(el + AtlasFlags, out var flags);
            _reader.TryReadStruct<byte>(el + AtlasCompletion, out var completion);
            _reader.TryReadStruct<float>(el + UiRelativePos, out var x);
            _reader.TryReadStruct<float>(el + UiRelativePos + 4, out var y);
            _reader.TryReadStruct<float>(el + UiSizeW, out var w);
            _reader.TryReadStruct<float>(el + UiSizeH, out var h);
            _reader.TryReadStruct<float>(el + Poe2.AtlasUi.LayerZoom, out var scale);
            _reader.TryReadStruct<uint>(el + Poe2.UiElement.Flags, out var uiFlags);
            var visible = ((uiFlags >> Poe2.UiElement.FlagVisibleBit) & 1) != 0;

            var iconType = 0;
            var d = el;
            for (var lvl = 0; lvl < 5 && d != 0; lvl++)
            {
                if (_reader.TryReadStruct<uint>(d + AtlasContent, out var c) && c is > 0 and < 256)
                {
                    iconType = (int)c;
                    break;
                }
                d = Ptr(Ptr(d + Poe2.UiElement.Children));
            }

            if (!_tagCache.TryGetValue(el, out var resolved))
            {
                if (resolveBudget > 0)
                {
                    resolved = ResolveTags(el);
                    _tagCache[el] = resolved;
                    resolveBudget--;
                    resolvedCount++;
                }
                else
                {
                    resolved = new ResolvedAtlasInfo("", NoTags, "deferred", NoCandidates);
                    allCached = false;
                }
            }
            else
            {
                resolvedCount++;
            }

            outNodes.Add(new AtlasNodeLive(
                el, id, content, state, biome, flags, completion,
                x, y, w, h, scale, visible, iconType,
                resolved.Map, resolved.MapSource, resolved.MapCandidates, resolved.Content));
        }

        if (matched < 8)
        {
            Invalidate();
            return false;
        }

        AllTagsResolved = allCached;
        LastPanelOpen = true;
        if (allCached)
        {
            LoadStatus = $"Atlas ready: {outNodes.Count} nodes";
            LoadProgress = 1f;
        }
        else
        {
            var progress = matched <= 0 ? 0.25f : 0.25f + MathF.Min(0.7f, resolvedCount / (float)matched * 0.7f);
            LoadStatus = $"Atlas: resolving labels {resolvedCount}/{matched}";
            LoadProgress = progress;
        }
        return true;
    }

    private sealed record ResolvedAtlasInfo(string Map, string[] Content, string MapSource, string[] MapCandidates);

    private ResolvedAtlasInfo ResolveTags(nint el)
    {
        var map = ResolveMapName(el);

        var tags = new List<string>(4);
        var row = Ptr(el + AtlasContent);
        if (row == 0) return new ResolvedAtlasInfo(map.Name, NoTags, map.Source, map.Candidates);

        var contentRow = Ptr(row + 0x38);
        if (contentRow != 0)
        {
            var name = ReadDisplayName(contentRow);
            if (LooksLikeName(name)) tags.Add(name.Trim());
        }

        var stats = Ptr(row + 0x50);
        if (stats != 0)
        {
            Span<byte> buffer = stackalloc byte[0x400];
            var n = _reader.TryReadBytes(stats, buffer);
            for (var o = 0; o + 8 <= n; o += 8)
            {
                var p = (nint)BitConverter.ToInt64(buffer[o..]);
                if (!IsCanon(p)) continue;
                const string prefix = "map_atlas_node_has_";
                var s = ReadAscii(p, 64);
                if (!s.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var pp = Ptr(p);
                    s = pp == 0 ? "" : ReadAscii(pp, 64);
                }
                if (!s.StartsWith(prefix, StringComparison.Ordinal)) continue;

                var mechanic = TitleCase(s[prefix.Length..].Replace('_', ' '));
                if (mechanic.Length > 0 && !tags.Contains(mechanic))
                    tags.Add(mechanic);
            }
        }

        return new ResolvedAtlasInfo(
            map.Name,
            tags.Count == 0 ? NoTags : tags.ToArray(),
            map.Source,
            map.Candidates);
    }

    private string ReadDisplayName(nint row)
    {
        ReadOnlySpan<int> offsets = [0x30, 0x28, 0x20, 0x18, 0x10, 0x08, 0x00];
        foreach (var offset in offsets)
        {
            var p = Ptr(row + offset);
            if (p == 0) continue;

            var s = _reader.ReadStringUtf16(p, 80);
            if (LooksLikeName(s)) return s.Trim();

            s = ReadAscii(p, 80);
            if (LooksLikeName(s)) return s.Trim();

            var pp = Ptr(p);
            if (pp == 0) continue;

            s = _reader.ReadStringUtf16(pp, 80);
            if (LooksLikeName(s)) return s.Trim();

            s = ReadAscii(pp, 80);
            if (LooksLikeName(s)) return s.Trim();
        }
        return "";
    }

    private (string Name, string Source, string[] Candidates) ResolveMapName(nint el)
    {
        var mapRow = Ptr(el + AtlasMapNodeId);
        if (mapRow == 0) return ("", "no-map-row", NoCandidates);

        var w = Ptr(mapRow);
        var code = w != 0 ? _reader.ReadStringUtf16(w, 80) : "";
        if (!code.StartsWith("Map", StringComparison.Ordinal))
        {
            var w2 = Ptr(w);
            code = w2 != 0 ? _reader.ReadStringUtf16(w2, 80) : code;
        }
        if (code.StartsWith("Map", StringComparison.Ordinal))
        {
            var display = FriendlyMapName(code);
            return (display, display.Equals(Prettify(code), StringComparison.Ordinal) ? "map-code" : "area-table", [code, display]);
        }

        // Some atlas rows do not expose a Map* code through the simple first-field path.
        // Sikaka's research probe found that the display title can still be reached by
        // scanning nearby row pointers, so use that as a conservative fallback.
        return ScanDisplayTitle(mapRow);
    }

    private (string Name, string Source, string[] Candidates) ScanDisplayTitle(nint row)
    {
        var best = "";
        var bestScore = int.MinValue;
        var candidates = new List<string>(12);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var off = -0x40; off < 0x180; off += 8)
        {
            var p = Ptr(row + off);
            if (p == 0) continue;

            foreach (var s in CandidateStrings(p))
            {
                var trimmed = s.Trim();
                if (seen.Add(trimmed) && candidates.Count < 12)
                    candidates.Add($"+0x{off:X}:{trimmed}");
                if (!LooksLikeMapTitle(trimmed)) continue;
                var score = ScoreMapTitle(trimmed);
                if (score <= bestScore) continue;
                best = trimmed;
                bestScore = score;
            }
        }
        return (best, best.Length > 0 ? "row-scan" : "not-found", candidates.ToArray());
    }

    private IEnumerable<string> CandidateStrings(nint p)
    {
        var addrs = new[] { p, Ptr(p), Ptr(p + 0x20), Ptr(p + 0x08), Ptr(p + 0x10), Ptr(p + 0x30) };
        foreach (var a in addrs)
        {
            if (a == 0) continue;

            var s = _reader.ReadStringUtf16(a, 80);
            if (LooksLikeName(s)) yield return s;

            s = ReadAscii(a, 80);
            if (LooksLikeName(s)) yield return s;
        }
    }

    private static bool LooksLikeMapTitle(string s)
    {
        if (!LooksLikeName(s)) return false;
        s = s.Trim();
        if (s.Length < 4) return false;
        if (s.StartsWith("Map", StringComparison.Ordinal)) return false;
        if (s.Contains('/') || s.Contains('\\') || s.Contains('_')) return false;
        if (s.Contains("atlas", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("map_atlas_node", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("Players in area", StringComparison.OrdinalIgnoreCase)) return false;
        return s.Any(char.IsLetter) && s.Count(char.IsDigit) <= 2;
    }

    private static int ScoreMapTitle(string s)
    {
        var score = 0;
        if (s.Contains(' ')) score += 8;
        if (s.Any(char.IsLower)) score += 4;
        if (s.Any(char.IsUpper)) score += 2;
        if (s.Length is >= 6 and <= 32) score += 3;
        if (s.Equals(s.ToUpperInvariant(), StringComparison.Ordinal)) score -= 8;

        ReadOnlySpan<string> contentWords =
        [
            "Boss", "Breach", "Ritual", "Delirium", "Expedition", "Corrupted",
            "Content", "Biome", "Players in area", "Powerful Map Boss"
        ];
        foreach (var word in contentWords)
            if (s.Contains(word, StringComparison.OrdinalIgnoreCase)) score -= 20;
        return score;
    }

    private bool AtlasPanelOpen(nint uiRoot)
    {
        var first = Ptr(uiRoot + Poe2.UiElement.Children);
        if (first == 0) return false;
        var panel = Ptr(first + (nint)(Poe2.AtlasUi.RootChildIndex * 8));
        if (panel == 0 || Ptr(panel + Poe2.UiElement.Self) != panel) return false;
        return _reader.TryReadStruct<uint>(panel + Poe2.UiElement.Flags, out var flags) &&
            ((flags >> Poe2.UiElement.FlagVisibleBit) & 1) != 0;
    }

    private bool HierarchicallyVisible(nint element)
    {
        var cur = element;
        var guard = 0;
        while (cur != 0 && guard++ < 16)
        {
            if (!_reader.TryReadStruct<uint>(cur + Poe2.UiElement.Flags, out var flags)) return false;
            if (((flags >> Poe2.UiElement.FlagVisibleBit) & 1) == 0) return false;
            var parent = Ptr(cur + UiParent);
            if (parent == cur) break;
            cur = parent;
        }
        return true;
    }

    private bool DetectNodeClass(nint uiRoot)
    {
        var parentRoot = Ptr(uiRoot + UiParent);
        var root = parentRoot != 0 ? parentRoot : uiRoot;
        var queue = new Queue<nint>();
        var visited = new HashSet<nint>();
        var byVtable = new Dictionary<nint, List<nint>>();
        queue.Enqueue(root);

        while (queue.Count > 0 && visited.Count < 200000)
        {
            var el = queue.Dequeue();
            if (el == 0 || !visited.Add(el) || Ptr(el + Poe2.UiElement.Self) != el) continue;
            var vtable = Ptr(el);
            if (vtable != 0)
                (byVtable.TryGetValue(vtable, out var list) ? list : byVtable[vtable] = new()).Add(el);

            var first = Ptr(el + Poe2.UiElement.Children);
            if (first == 0 || !_reader.TryReadStruct<nint>(el + UiChildrenEnd, out var last))
                continue;
            var count = ((long)last - (long)first) / 8;
            if (count is <= 0 or > 16384) continue;
            for (long i = 0; i < count; i++)
                queue.Enqueue(Ptr(first + (nint)(i * 8)));
        }

        nint bestVtable = 0;
        var bestCount = 0;
        var bestBiomes = 0;
        nint fallbackVtable = 0;
        var fallbackBiomes = 0;

        foreach (var (vtable, list) in byVtable)
        {
            if (list.Count < 50) continue;
            var biomes = new HashSet<int>();
            var widths = new Dictionary<int, int>();
            foreach (var el in list.Take(400))
            {
                if (_reader.TryReadStruct<byte>(el + AtlasBiome, out var biome) && biome is >= 1 and <= 12)
                    biomes.Add(biome);
                if (_reader.TryReadStruct<float>(el + UiSizeW, out var w))
                {
                    var iw = (int)w;
                    widths[iw] = widths.GetValueOrDefault(iw) + 1;
                }
            }

            if (biomes.Count > fallbackBiomes)
            {
                fallbackBiomes = biomes.Count;
                fallbackVtable = vtable;
            }

            var modalWidth = widths.Count == 0 ? 0 : widths.OrderByDescending(k => k.Value).First().Key;
            if (modalWidth is >= 28 and <= 56 && biomes.Count >= 3 && list.Count > bestCount)
            {
                bestCount = list.Count;
                bestVtable = vtable;
                bestBiomes = biomes.Count;
            }
        }

        if (bestVtable == 0)
        {
            bestVtable = fallbackVtable;
            bestBiomes = fallbackBiomes;
        }
        if (bestVtable == 0 || bestBiomes < 3) return false;

        _nodeVtable = bestVtable;
        var parentCount = new Dictionary<nint, int>();
        foreach (var el in byVtable[bestVtable])
        {
            var parent = Ptr(el + UiParent);
            if (parent != 0) parentCount[parent] = parentCount.GetValueOrDefault(parent) + 1;
        }

        if (parentCount.Count == 0) return false;
        _nodeCanvas = parentCount.OrderByDescending(k => k.Value).First().Key;
        return _nodeCanvas != 0;
    }

    private void Invalidate()
    {
        _nodeCanvas = 0;
        _nodeVtable = 0;
        _hiddenTicks = 0;
        _nodeRetry = 0;
        _tagCache.Clear();
        AllTagsResolved = false;
    }

    private void SetIdle()
    {
        LastPanelOpen = false;
        LoadStatus = "";
        LoadProgress = 0f;
        AllTagsResolved = false;
    }

    private string ReadAscii(nint addr, int max)
    {
        Span<byte> bytes = stackalloc byte[max];
        var n = _reader.TryReadBytes(addr, bytes);
        var sb = new System.Text.StringBuilder(n);
        for (var i = 0; i < n; i++)
        {
            var c = bytes[i];
            if (c is >= 0x20 and < 0x7f) sb.Append((char)c);
            else break;
        }
        return sb.ToString();
    }

    private static string Prettify(string code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        var s = code;
        if (s.StartsWith("Map", StringComparison.Ordinal)) s = s[3..];
        s = s.Replace("UberBoss_", "")
            .Replace("PrecursorTower", "Tower ")
            .Replace("Unique", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"(?<=\D)\d{1,2}(?=_|$)", "");
        s = s.Replace("_", " ");

        var sb = new System.Text.StringBuilder(s.Length + 8);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (i > 0 && char.IsUpper(c) &&
                (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))) &&
                sb.Length > 0 && sb[^1] != ' ')
                sb.Append(' ');
            sb.Append(c);
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static string FriendlyMapName(string code)
    {
        if (AreaNames.Value.TryGetValue(code, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        return Prettify(code);
    }

    private static IReadOnlyDictionary<string, string> LoadAreaNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var res = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("world_areas.json", StringComparison.OrdinalIgnoreCase));
            if (res == null) return names;
            using var stream = asm.GetManifestResourceStream(res);
            if (stream == null) return names;
            using var doc = JsonDocument.Parse(stream);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                if (!prop.Value.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) continue;
                var name = n.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    names[prop.Name] = name;
            }
        }
        catch
        {
            // Missing or stale embedded area data should degrade to Prettify(code), not break reads.
        }
        return names;
    }

    private static string TitleCase(string s)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
            parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        return string.Join(' ', parts);
    }

    private static bool LooksLikeName(string s)
        => s.Length is >= 3 and <= 64 && s[0] is >= ' ' and < (char)0x7f;

    private static bool IsCanon(nint p)
        => (ulong)p >= 0x10000 && (ulong)p <= 0x7FFFFFFFFFFF;

    private nint Ptr(nint addr)
    {
        if (!_reader.TryReadStruct<nint>(addr, out var p)) return 0;
        return IsCanon(p) ? p : 0;
    }
}

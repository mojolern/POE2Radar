using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace POE2Radar.Overlay.Pricing;

/// <summary>
/// Result of a price lookup. <see cref="Exalted"/> is the item's value in Exalted Orbs (PoE2's base
/// economy unit). <see cref="Quantity"/> is the listing count / trade volume — a confidence signal:
/// low-volume rows are often mislisted (a 2-listing leveling unique priced at 5000 div).
/// </summary>
public readonly record struct PriceResult(string Name, double Exalted, int Quantity, string Category)
{
    public bool LowConfidence(int minQty) => Quantity < minQty;
    public double MinExalted { get; init; }
    public double MaxExalted { get; init; }
    public bool IsRange => MinExalted > 0 && MaxExalted > 0 && Math.Abs(MaxExalted - MinExalted) > 0.01;
    public double LowExalted => MinExalted > 0 ? MinExalted : Exalted;
    public double HighExalted => MaxExalted > 0 ? MaxExalted : Exalted;
}

public sealed record PriceLeagueInfo(string Value, bool IsCurrent, bool Hardcore);

/// <summary>
/// Centralized price source, ported in spirit from the user's PoE1 NinjaPriceService and built around
/// the live <b>poe.ninja</b> PoE2 economy API (the gold-standard source, estimated off the official trade
/// API). Fetches the current league's currency-like + unique prices, converts everything to Exalted, and
/// indexes them by normalized name AND by 2D-art basename (the bridge to in-game item reads: a dropped
/// item's RenderItem .dds basename equals poe.ninja's icon basename), caches to disk, and refreshes on a
/// TTL. Reads are lock-free off volatile snapshots; the whole index is swapped atomically.
///
/// <para>Two poe.ninja endpoints, both returning all rows in one (un-paginated) response:</para>
/// <list type="bullet">
///   <item><b>exchange</b> (<c>.../exchange/current/overview?type=…</c>) — fungible/currency-like rows.
///   <c>items[]</c> maps id→name/image, <c>lines[]</c> carries the price; joined by id.</item>
///   <item><b>stash item</b> (<c>.../stash/current/item/overview?type=Unique…</c>) — uniques, with
///   name/icon/price/listingCount all inline on each <c>lines[]</c> row.</item>
/// </list>
///
/// <para>Price unit: every line's <c>primaryValue</c> is in <b>Divine Orbs</b> (validated against known
/// anchors — Divine=1, Exalted≈0.0066, and the dirt-cheap unique floor ≈0.0017 div), and
/// <c>core.rates.exalted</c> is Exalted-per-Divine, so <c>ex = primaryValue × rates.exalted</c> uniformly.</para>
///
/// <para>poe.ninja has no clean league-list endpoint, so the current league NAME is still discovered via
/// poe2scout's lightweight <c>/Leagues</c> (its <c>Value</c> strings match what poe.ninja expects, e.g.
/// "Runes of Aldur"); the divine/chaos RATES come from poe.ninja itself so prices stay self-consistent.</para>
///
/// <para>This is the recyclable data source: ground-loot valuation, runeforge rewards, expedition choices,
/// ritual/vendor overlays all consume <see cref="TryByArt"/> / <see cref="TryByName"/>.</para>
/// </summary>
public sealed class PriceBook
{
    // poe.ninja "stash item" unique types (icon → art basename + price inline per line).
    private static readonly string[] UniqueTypes =
        { "UniqueWeapons", "UniqueArmours", "UniqueAccessories", "UniqueFlasks", "UniqueJewels",
          "UniqueTablets", "PrecursorTablets" };

    // poe.ninja "exchange" currency-like types. Superset of the old poe2scout set — includes
    // SoulCores (poe2scout's "ultimatum"), UncutGems (Uncut Skill/Spirit/Support Gem by level) and
    // LineageSupportGems (named, tradeable). NOTE: individual CUT active skill gems (e.g. "Rain of
    // Blades") are not a traded market on poe.ninja or anywhere — only UncutGems carry a value.
    private static readonly string[] ExchangeTypes =
        { "Currency", "Runes", "Fragments", "Essences", "Expedition", "Verisium", "Breach", "Ritual",
          "Delirium", "UncutGems", "Abyss", "SoulCores", "LineageSupportGems", "Idols" };

    private const string NinjaExchange = "https://poe.ninja/poe2/api/economy/exchange/current/overview";
    private const string NinjaStashItem = "https://poe.ninja/poe2/api/economy/stash/current/item/overview";

    private static readonly HttpClient Http = CreateHttp();
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _cachePath;
    private readonly object _gate = new();

    // Atomically-swapped snapshots (volatile; lock-free reads on the tick/render thread).
    private volatile Dictionary<string, PricedItem> _byArt = new(StringComparer.OrdinalIgnoreCase);
    private volatile Dictionary<string, PriceRange> _byArtRange = new(StringComparer.OrdinalIgnoreCase);
    private volatile Dictionary<string, PricedItem> _byName = new(StringComparer.OrdinalIgnoreCase);

    private volatile bool _fetching;
    private DateTime _lastFetchUtc = DateTime.MinValue;
    private string _league = "";
    private string? _leagueOverride;
    private volatile string? _detectedLeague;
    private static IReadOnlyList<PriceLeagueInfo>? s_leagueOptions;
    private static DateTime s_leagueOptionsFetchedUtc = DateTime.MinValue;

    public double ExPerDivine { get; private set; } = 1;
    public double ExPerChaos { get; private set; } = 1;
    public double DivPerExalted => ExPerDivine > 0 ? 1.0 / ExPerDivine : 0;
    public bool IsLoaded => _byName.Count > 0 || _byArt.Count > 0;
    public int ItemCount => _byArt.Count + _byName.Count;
    public string League => _league;
    public bool Hardcore => _league.StartsWith("HC ", StringComparison.OrdinalIgnoreCase);
    public string PrimaryCurrency { get; private set; } = "divine";
    public string SecondaryCurrency { get; private set; } = "exalted";
    public string Status { get; private set; } = "not started";
    public DateTime LastFetchUtc => _lastFetchUtc;
    public int RefreshIntervalMinutes { get; set; } = 30;

    private sealed record PricedItem(string Name, double Exalted, int Quantity, string Category);
    private sealed record PriceRange(string Name, double MinExalted, double MaxExalted, int Quantity, string Category);

    public PriceBook(string cachePath, string? leagueOverride = null)
    {
        _cachePath = cachePath;
        _leagueOverride = string.IsNullOrWhiteSpace(leagueOverride) ? null : leagueOverride.Trim();
        TryLoadCache();
    }

    private static HttpClient CreateHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("POE2Radar-PriceBook");
        return c;
    }

    /// <summary>Set/clear a manual league (e.g. from settings); triggers a refresh if it changed.</summary>
    public void SetLeagueOverride(string? league)
    {
        var v = string.IsNullOrWhiteSpace(league) ? null : league.Trim();
        if (v == _leagueOverride) return;
        _leagueOverride = v;
        _lastFetchUtc = DateTime.MinValue; // force the next RefreshIfDue to re-fetch
    }

    /// <summary>Set/clear the league read from game memory. Manual dashboard override still wins.</summary>
    public void SetDetectedLeague(string? league)
    {
        var v = string.IsNullOrWhiteSpace(league) ? null : league.Trim();
        if (v == _detectedLeague) return;
        _detectedLeague = v;
        if (_leagueOverride == null) _lastFetchUtc = DateTime.MinValue;
    }

    /// <summary>Call periodically (cheap when not due). Kicks a background fetch when stale and not already running.</summary>
    public void RefreshIfDue()
    {
        if (_fetching) return;
        if (DateTime.UtcNow - _lastFetchUtc < TimeSpan.FromMinutes(RefreshIntervalMinutes)) return;
        _fetching = true;
        _ = Task.Run(FetchAsync);
    }

    public void ForceRefresh()
    {
        if (_fetching) return;
        _fetching = true;
        _ = Task.Run(FetchAsync);
    }

    /// <summary>Look up a unique by its 2D-art basename (e.g. "Earthbound") — the in-game item read key.</summary>
    public PriceResult? TryByArt(string? artBasename)
    {
        return TryByArt(artBasename, allowRange: true);
    }

    private PriceResult? TryByArt(string? artBasename, bool allowRange)
    {
        if (string.IsNullOrWhiteSpace(artBasename)) return null;
        var art = artBasename.Trim();
        if (allowRange && _byArtRange.TryGetValue(art, out var r))
            return new PriceResult(r.Name, r.MaxExalted, r.Quantity, r.Category)
            {
                MinExalted = r.MinExalted,
                MaxExalted = r.MaxExalted,
            };
        return _byArt.TryGetValue(art, out var p)
            ? new PriceResult(p.Name, p.Exalted, p.Quantity, p.Category) : null;
    }

    public PriceResult? TryByArtAndName(string? artBasename, string? itemName)
    {
        var cleanName = CleanItemLabel(itemName);
        var byName = TryByName(cleanName);
        if (byName is { } exact) return exact;

        // Shared 2D art is common. Only use a tiered price range when the
        // item name itself looks like a tiered variant family.
        var allowRange = string.IsNullOrWhiteSpace(cleanName) || IsTieredVariantName(cleanName);
        return TryByArt(artBasename, allowRange);
    }

    /// <summary>Look up any priced item (unique or currency) by display name.</summary>
    public PriceResult? TryByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _byName.TryGetValue(Normalize(name), out var p)
            ? new PriceResult(p.Name, p.Exalted, p.Quantity, p.Category) : null;
    }

    /// <summary>Convert an Exalted value to a short display string in the largest sensible unit.</summary>
    public string Format(double ex)
    {
        if (ExPerDivine > 1 && ex >= ExPerDivine) return $"{ex / ExPerDivine:0.##} div";
        return $"{ex:0.##} ex";
    }

    public string Format(PriceResult result)
    {
        if (!result.IsRange) return Format(result.Exalted);
        return $"{Format(result.LowExalted)}-{Format(result.HighExalted)}";
    }

    public double DivineToExalted(double divine) => DivineToExalted(divine, ExPerDivine);
    public double ExaltedToDivine(double exalted) => ExPerDivine > 0 ? exalted / ExPerDivine : 0;

    public static async Task<IReadOnlyList<PriceLeagueInfo>> GetLeagueOptionsAsync()
    {
        if (s_leagueOptions is { Count: > 0 } cached &&
            DateTime.UtcNow - s_leagueOptionsFetchedUtc < TimeSpan.FromMinutes(30))
            return cached;

        try
        {
            var json = await Http.GetStringAsync("https://poe2scout.com/api/poe2/Leagues").ConfigureAwait(false);
            var leagues = JsonSerializer.Deserialize<List<ScoutLeague>>(json, Json) ?? new();
            var options = leagues
                .Where(l => !string.IsNullOrWhiteSpace(l.Value))
                .Select(l => new PriceLeagueInfo(
                    l.Value.Trim(),
                    l.IsCurrent,
                    l.Value.StartsWith("HC", StringComparison.OrdinalIgnoreCase)))
                .DistinctBy(l => l.Value, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(l => l.IsCurrent)
                .ThenBy(l => l.Hardcore)
                .ThenBy(l => l.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            s_leagueOptions = options;
            s_leagueOptionsFetchedUtc = DateTime.UtcNow;
            return options;
        }
        catch
        {
            return s_leagueOptions ?? Array.Empty<PriceLeagueInfo>();
        }
    }

    // ── fetch ────────────────────────────────────────────────────────────────

    private async Task FetchAsync()
    {
        try
        {
            Status = "fetching…";
            var league = await ResolveLeagueAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(league)) { Status = "no league"; return; }
            var lg = Uri.EscapeDataString(league);

            var byArt = new Dictionary<string, PricedItem>(StringComparer.OrdinalIgnoreCase);
            var byArtFamilies = new Dictionary<string, List<PricedItem>>(StringComparer.OrdinalIgnoreCase);
            var byName = new Dictionary<string, PricedItem>(StringComparer.OrdinalIgnoreCase);

            // Rates default to "not yet known"; the first overview response that carries core.rates sets
            // them, and ApplyRates fixes ExPerDivine/ExPerChaos for the rest of the fetch + conversions.
            double exPerDivine = 0;

            foreach (var type in ExchangeTypes)
                await FetchExchangeAsync(lg, type, byArt, byArtFamilies, byName, () => exPerDivine, r => exPerDivine = r).ConfigureAwait(false);
            foreach (var type in UniqueTypes)
                await FetchUniquesAsync(lg, type, byArt, byName, () => exPerDivine, r => exPerDivine = r).ConfigureAwait(false);

            if (byArt.Count == 0 && byName.Count == 0) { Status = "fetch returned no rows"; return; }

            _byArt = byArt;
            _byArtRange = BuildTieredRanges(byArtFamilies);
            _byName = byName;
            _league = league;
            _lastFetchUtc = DateTime.UtcNow;
            Status = $"loaded {byName.Count} by name + {byArt.Count} by art + {_byArtRange.Count} ranges for '{league}'";
            SaveCache();
        }
        catch (Exception ex) { Status = $"fetch failed: {ex.Message}"; }
        finally { _fetching = false; }
    }

    /// <summary>Discover the current league name. Manual override wins; otherwise prefer the league read
    /// from game memory, then fall back to poe2scout's current softcore league.</summary>
    private async Task<string> ResolveLeagueAsync()
    {
        if (_leagueOverride != null) return _leagueOverride;
        var detected = _detectedLeague;
        try
        {
            var leagues = await GetLeagueOptionsAsync().ConfigureAwait(false);
            if (detected != null)
            {
                var match = leagues.FirstOrDefault(l => string.Equals(l.Value, detected, StringComparison.OrdinalIgnoreCase));
                return match?.Value ?? detected;
            }

            var pick = leagues.FirstOrDefault(l => l.IsCurrent && !l.Hardcore)
                       ?? leagues.FirstOrDefault(l => l.IsCurrent)
                       ?? leagues.FirstOrDefault();
            return pick?.Value ?? "";
        }
        catch { return detected ?? ""; }
    }

    // Convert poe.ninja core.rates → our Exalted-per-Divine / Exalted-per-Chaos. rates are "units per
    // Divine": exalted = ex/div directly; chaos = chaos/div, so ex/chaos = (ex/div)/(chaos/div).
    private void ApplyRates(NinjaCore? core)
    {
        if (core?.Rates == null) return;
        if (!string.IsNullOrWhiteSpace(core.Primary)) PrimaryCurrency = core.Primary.Trim();
        if (!string.IsNullOrWhiteSpace(core.Secondary)) SecondaryCurrency = core.Secondary.Trim();
        if (core.Rates.TryGetValue("exalted", out var exPerDiv) && exPerDiv > 0)
        {
            ExPerDivine = exPerDiv;
            if (core.Rates.TryGetValue("chaos", out var chaosPerDiv) && chaosPerDiv > 0)
                ExPerChaos = exPerDiv / chaosPerDiv;
        }
    }

    private async Task FetchExchangeAsync(string leagueEscaped, string type,
        Dictionary<string, PricedItem> byArt,
        Dictionary<string, List<PricedItem>> byArtFamilies,
        Dictionary<string, PricedItem> byName,
        Func<double> getRate, Action<double> setRate)
    {
        try
        {
            var url = $"{NinjaExchange}?league={leagueEscaped}&type={type}";
            var data = JsonSerializer.Deserialize<NinjaOverview>(await Http.GetStringAsync(url).ConfigureAwait(false), Json);
            if (data?.Lines == null) return;
            if (getRate() <= 0) { ApplyRates(data.Core); setRate(ExPerDivine); }
            var rate = getRate();
            if (rate <= 0) return; // can't price without a rate

            // items[] maps id → name/image; join the priced lines[] to it by id.
            var meta = new Dictionary<string, NinjaItem>(StringComparer.Ordinal);
            if (data.Items != null)
                foreach (var it in data.Items)
                    if (!string.IsNullOrEmpty(it.Id)) meta[it.Id] = it;

            foreach (var ln in data.Lines)
            {
                if (ln.Id.ValueKind != JsonValueKind.String) continue; // exchange ids are strings
                var id = ln.Id.GetString();
                if (string.IsNullOrEmpty(id) || !meta.TryGetValue(id, out var m)) continue;
                if (string.IsNullOrWhiteSpace(m.Name) || ln.PrimaryValue <= 0) continue;
                var ex = DivineToExalted(ln.PrimaryValue, rate);
                var qty = (int)Math.Clamp(ln.VolumePrimaryValue ?? 0, 0, int.MaxValue);
                var item = new PricedItem(m.Name.Trim(), ex, qty, type);
                Upsert(byName, Normalize(m.Name), item);
                var art = ArtBasenameFromIcon(m.Image);
                if (art != null)
                {
                    UpsertExchangeArt(byArt, art, item);
                    AddExchangeArtFamily(byArtFamilies, art, item);
                }
            }
        }
        catch { /* a missing/empty category is fine — skip it */ }
    }

    private async Task FetchUniquesAsync(string leagueEscaped, string type,
        Dictionary<string, PricedItem> byArt, Dictionary<string, PricedItem> byName,
        Func<double> getRate, Action<double> setRate)
    {
        try
        {
            var url = $"{NinjaStashItem}?league={leagueEscaped}&type={type}";
            var data = JsonSerializer.Deserialize<NinjaOverview>(await Http.GetStringAsync(url).ConfigureAwait(false), Json);
            if (data?.Lines == null) return;
            if (getRate() <= 0) { ApplyRates(data.Core); setRate(ExPerDivine); }
            var rate = getRate();
            if (rate <= 0) return;

            foreach (var ln in data.Lines)
            {
                if (string.IsNullOrWhiteSpace(ln.Name) || ln.PrimaryValue <= 0) continue;
                var ex = DivineToExalted(ln.PrimaryValue, rate);
                var item = new PricedItem(ln.Name.Trim(), ex, ln.ListingCount ?? 0, type);
                Upsert(byName, Normalize(ln.Name), item);
                var art = ArtBasenameFromIcon(ln.Icon);
                if (art != null) Upsert(byArt, art, item);
            }
        }
        catch { }
    }

    // Keep the higher-value listing on a key collision (shared art across variants → show the best).
    private static double DivineToExalted(double divine, double exPerDivine) => divine * exPerDivine;

    private static void UpsertExchangeArt(Dictionary<string, PricedItem> map, string key, PricedItem item)
    {
        if (IsSharedTieredCurrencyArt(item)) return;
        Upsert(map, key, item);
    }

    private static bool IsSharedTieredCurrencyArt(PricedItem item) =>
        item.Category.Equals("Currency", StringComparison.OrdinalIgnoreCase) &&
        IsTieredVariantName(item.Name);

    private static void AddExchangeArtFamily(Dictionary<string, List<PricedItem>> map, string key, PricedItem item)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<PricedItem>();
            map[key] = list;
        }
        list.Add(item);
    }

    private static Dictionary<string, PriceRange> BuildTieredRanges(Dictionary<string, List<PricedItem>> families)
    {
        var result = new Dictionary<string, PriceRange>(StringComparer.OrdinalIgnoreCase);
        foreach (var (art, items) in families)
        {
            var currencyItems = items
                .Where(i => i.Category.Equals("Currency", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (currencyItems.Length == 0 ||
                !currencyItems.Any(i => IsTieredVariantName(i.Name)) ||
                currencyItems.Any(i => !IsTieredVariantName(i.Name)))
                continue;

            var range = currencyItems
                .GroupBy(i => TierFamilyName(i.Name), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1 && g.Any(i => IsTieredVariantName(i.Name)))
                .Select(g =>
                {
                    var variants = g
                        .GroupBy(i => Normalize(i.Name), StringComparer.OrdinalIgnoreCase)
                        .Select(g2 => g2.OrderByDescending(i => i.Quantity).First())
                        .OrderBy(i => i.Exalted)
                        .ToArray();
                    return variants.Length < 2
                        ? null
                        : new PriceRange(
                            $"{g.Key} variants",
                            variants.First().Exalted,
                            variants.Last().Exalted,
                            variants.Max(i => i.Quantity),
                            variants.First().Category);
                })
                .Where(r => r != null)
                .OrderByDescending(r => r!.MaxExalted - r.MinExalted)
                .FirstOrDefault();

            if (range != null) result[art] = range;
        }
        return result;
    }

    private static bool IsTieredVariantName(string name) =>
        name.StartsWith("Lesser ", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Greater ", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Perfect ", StringComparison.OrdinalIgnoreCase);

    private static string TierFamilyName(string name)
    {
        foreach (var prefix in new[] { "Lesser ", "Greater ", "Perfect " })
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return name[prefix.Length..].Trim();
        return name.Trim();
    }

    private static void Upsert(Dictionary<string, PricedItem> map, string key, PricedItem item)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!map.TryGetValue(key, out var cur) || item.Exalted > cur.Exalted) map[key] = item;
    }

    /// <summary>poe.ninja icon → basename. ".../<hash>/Earthbound.png" → "Earthbound" (handles both the
    /// absolute web.poecdn.com unique icons and the relative "/gen/image/.../X.png" currency images).</summary>
    private static string? ArtBasenameFromIcon(string? iconUrl)
    {
        if (string.IsNullOrWhiteSpace(iconUrl)) return null;
        var noQuery = iconUrl.Split('?')[0];
        var seg = noQuery.Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(seg)) return null;
        var dot = seg.LastIndexOf('.');
        var name = dot > 0 ? seg[..dot] : seg;
        return name.Length >= 2 ? name : null;
    }

    private static string Normalize(string s)
    {
        var cleaned = s.Trim()
            .Replace('\u00A0', ' ')
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201B', '\'')
            .Replace('\u2032', '\'');
        return string.Join(' ', cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? CleanItemLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim();
        var x = s.IndexOf('x');
        var times = s.IndexOf('×');
        if (times > 0 && (x < 0 || times < x)) x = times;
        if (x > 0 && int.TryParse(s[..x].Trim(), out _))
            s = s[(x + 1)..].Trim();
        return s;
    }

    // ── disk cache ─────────────────────────────────────────────────────────────

    private sealed class CacheDto
    {
        public string League { get; set; } = "";
        public DateTime FetchedUtc { get; set; }
        public double ExPerDivine { get; set; }
        public double ExPerChaos { get; set; }
        public string PrimaryCurrency { get; set; } = "divine";
        public string SecondaryCurrency { get; set; } = "exalted";
        public Dictionary<string, PricedItem> ByArt { get; set; } = new();
        public Dictionary<string, PriceRange> ByArtRanges { get; set; } = new();
        public Dictionary<string, PricedItem> ByName { get; set; } = new();
    }

    private void TryLoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) { Status = "no cache; will fetch"; return; }
            var dto = JsonSerializer.Deserialize<CacheDto>(File.ReadAllText(_cachePath), Json);
            if (dto == null) return;
            // Honor a configured league: a cache for a different league shouldn't be used.
            if (_leagueOverride != null && !string.Equals(dto.League, _leagueOverride, StringComparison.OrdinalIgnoreCase)) return;
            var byArt = new Dictionary<string, PricedItem>(dto.ByArt, StringComparer.OrdinalIgnoreCase);
            var removedStaleCurrencyArt = false;
            foreach (var key in byArt.Where(kv => IsSharedTieredCurrencyArt(kv.Value)).Select(kv => kv.Key).ToArray())
            {
                byArt.Remove(key);
                removedStaleCurrencyArt = true;
            }
            _byArt = byArt;
            _byArtRange = new Dictionary<string, PriceRange>(dto.ByArtRanges ?? new(), StringComparer.OrdinalIgnoreCase);
            _byName = new Dictionary<string, PricedItem>(dto.ByName, StringComparer.OrdinalIgnoreCase);
            _league = dto.League;
            _lastFetchUtc = dto.FetchedUtc;
            if (dto.ExPerDivine > 0) ExPerDivine = dto.ExPerDivine;
            if (dto.ExPerChaos > 0) ExPerChaos = dto.ExPerChaos;
            if (!string.IsNullOrWhiteSpace(dto.PrimaryCurrency)) PrimaryCurrency = dto.PrimaryCurrency;
            if (!string.IsNullOrWhiteSpace(dto.SecondaryCurrency)) SecondaryCurrency = dto.SecondaryCurrency;
            if (removedStaleCurrencyArt)
            {
                _lastFetchUtc = DateTime.MinValue;
                Status = $"cache: {ItemCount} entries for '{_league}'; refreshing stale currency art";
            }
            else if (_byArtRange.Count == 0)
            {
                _lastFetchUtc = DateTime.MinValue;
                Status = $"cache: {ItemCount} entries for '{_league}'; refreshing tiered price ranges";
            }
            else Status = $"cache: {ItemCount} entries for '{_league}'";
        }
        catch (Exception ex) { Status = $"cache load failed: {ex.Message}"; }
    }

    private void SaveCache()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var dto = new CacheDto
            {
                League = _league, FetchedUtc = _lastFetchUtc, ExPerDivine = ExPerDivine, ExPerChaos = ExPerChaos,
                PrimaryCurrency = PrimaryCurrency, SecondaryCurrency = SecondaryCurrency,
                ByArt = new(_byArt), ByArtRanges = new(_byArtRange), ByName = new(_byName),
            };
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(dto, Json));
        }
        catch { }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────────

    // poe2scout /Leagues — used only to discover the current league name.
    private sealed class ScoutLeague
    {
        public string Value { get; set; } = "";
        public bool IsCurrent { get; set; }
    }

    // poe.ninja overview (both exchange + stash item share this shape).
    private sealed class NinjaOverview
    {
        public NinjaCore? Core { get; set; }
        public List<NinjaLine>? Lines { get; set; }
        public List<NinjaItem>? Items { get; set; }   // exchange only (id→name/image); empty for uniques
    }
    private sealed class NinjaCore
    {
        public Dictionary<string, double>? Rates { get; set; }   // "units per Divine": exalted, chaos
        public string? Primary { get; set; }
        public string? Secondary { get; set; }
    }
    private sealed class NinjaItem
    {
        public string Id { get; set; } = "";       // string id ("exalted", "divine", …) on exchange
        public string Name { get; set; } = "";
        public string? Image { get; set; }
    }
    private sealed class NinjaLine
    {
        // id is a string on the exchange endpoint and a number on the uniques endpoint — read raw and
        // branch on ValueKind (uniques carry name/icon inline so their id is unused).
        public JsonElement Id { get; set; }
        public string? Name { get; set; }            // uniques: display name
        public string? Icon { get; set; }            // uniques: absolute art url
        public double PrimaryValue { get; set; }     // value in Divine Orbs (× rates.exalted → Exalted)
        public double? VolumePrimaryValue { get; set; } // exchange: trade volume (confidence)
        public int? ListingCount { get; set; }       // uniques: listing count (confidence)
    }
}

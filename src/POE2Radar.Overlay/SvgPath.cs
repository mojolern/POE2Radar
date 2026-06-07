using System.Globalization;
using System.Numerics;

namespace POE2Radar.Overlay;

internal static class SvgPath
{
    public enum SegKind { Line, Cubic, Quad }

    public readonly record struct Seg(SegKind Kind, Vector2 C1, Vector2 C2, Vector2 End);

    public sealed class SvgFigure
    {
        public Vector2 Start;
        public readonly List<Seg> Segs = [];
        public bool Closed;
    }

    public static List<SvgFigure> Parse(string? d)
    {
        var figs = new List<SvgFigure>();
        if (string.IsNullOrWhiteSpace(d)) return figs;

        var i = 0;
        var cmd = '\0';
        var cur = Vector2.Zero;
        var start = Vector2.Zero;
        var prevCubic = Vector2.Zero;
        var prevQuad = Vector2.Zero;
        var hadCubic = false;
        var hadQuad = false;
        SvgFigure? fig = null;

        void EnsureFig()
        {
            if (fig is not null) return;
            fig = new SvgFigure { Start = cur };
            figs.Add(fig);
        }

        while (i < d.Length)
        {
            SkipSep(d, ref i);
            if (i >= d.Length) break;
            if (char.IsLetter(d[i]))
            {
                cmd = d[i++];
                SkipSep(d, ref i);
            }
            else if (cmd == '\0')
            {
                i++;
                continue;
            }

            var up = char.ToUpperInvariant(cmd);
            var rel = char.IsLower(cmd);
            switch (up)
            {
                case 'M':
                    if (!ReadPoint(d, ref i, cur, rel, out cur)) { i++; break; }
                    start = cur;
                    fig = new SvgFigure { Start = cur };
                    figs.Add(fig);
                    cmd = rel ? 'l' : 'L';
                    hadCubic = hadQuad = false;
                    break;
                case 'L':
                    if (!ReadPoint(d, ref i, cur, rel, out var lp)) { i++; break; }
                    EnsureFig();
                    fig!.Segs.Add(new Seg(SegKind.Line, default, default, lp));
                    cur = lp;
                    hadCubic = hadQuad = false;
                    break;
                case 'H':
                    if (!TryReadFloat(d, ref i, out var x)) { i++; break; }
                    var hp = new Vector2(rel ? cur.X + x : x, cur.Y);
                    EnsureFig();
                    fig!.Segs.Add(new Seg(SegKind.Line, default, default, hp));
                    cur = hp;
                    hadCubic = hadQuad = false;
                    break;
                case 'V':
                    if (!TryReadFloat(d, ref i, out var y)) { i++; break; }
                    var vp = new Vector2(cur.X, rel ? cur.Y + y : y);
                    EnsureFig();
                    fig!.Segs.Add(new Seg(SegKind.Line, default, default, vp));
                    cur = vp;
                    hadCubic = hadQuad = false;
                    break;
                case 'C':
                    if (!ReadPoint(d, ref i, cur, rel, out var c1) ||
                        !ReadPoint(d, ref i, cur, rel, out var c2) ||
                        !ReadPoint(d, ref i, cur, rel, out var cp)) { i++; break; }
                    EnsureFig();
                    fig!.Segs.Add(new Seg(SegKind.Cubic, c1, c2, cp));
                    prevCubic = c2;
                    cur = cp;
                    hadCubic = true;
                    hadQuad = false;
                    break;
                case 'S':
                    if (!ReadPoint(d, ref i, cur, rel, out var sc2) ||
                        !ReadPoint(d, ref i, cur, rel, out var sp)) { i++; break; }
                    var sc1 = hadCubic ? cur + (cur - prevCubic) : cur;
                    EnsureFig();
                    fig!.Segs.Add(new Seg(SegKind.Cubic, sc1, sc2, sp));
                    prevCubic = sc2;
                    cur = sp;
                    hadCubic = true;
                    hadQuad = false;
                    break;
                case 'Q':
                    if (!ReadPoint(d, ref i, cur, rel, out var q1) ||
                        !ReadPoint(d, ref i, cur, rel, out var qp)) { i++; break; }
                    EnsureFig();
                    fig!.Segs.Add(new Seg(SegKind.Quad, q1, default, qp));
                    prevQuad = q1;
                    cur = qp;
                    hadQuad = true;
                    hadCubic = false;
                    break;
                case 'T':
                    if (!ReadPoint(d, ref i, cur, rel, out var tp)) { i++; break; }
                    var tq = hadQuad ? cur + (cur - prevQuad) : cur;
                    EnsureFig();
                    fig!.Segs.Add(new Seg(SegKind.Quad, tq, default, tp));
                    prevQuad = tq;
                    cur = tp;
                    hadQuad = true;
                    hadCubic = false;
                    break;
                case 'Z':
                    if (fig is not null)
                    {
                        fig.Closed = true;
                        cur = start;
                    }
                    fig = null;
                    hadCubic = hadQuad = false;
                    break;
                default:
                    i++;
                    break;
            }
        }
        return figs;
    }

    private static void SkipSep(string s, ref int i)
    {
        while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
    }

    private static bool TryReadFloat(string s, ref int i, out float value)
    {
        value = 0;
        SkipSep(s, ref i);
        var start = i;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
        var any = false;
        while (i < s.Length && char.IsDigit(s[i])) { i++; any = true; }
        if (i < s.Length && s[i] == '.')
        {
            i++;
            while (i < s.Length && char.IsDigit(s[i])) { i++; any = true; }
        }
        if (any && i < s.Length && (s[i] == 'e' || s[i] == 'E'))
        {
            var e = i++;
            if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
            var exp = i;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (exp == i) i = e;
        }
        if (!any)
        {
            i = start;
            return false;
        }
        return float.TryParse(s.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool ReadPoint(string s, ref int i, Vector2 cur, bool rel, out Vector2 point)
    {
        point = default;
        if (!TryReadFloat(s, ref i, out var x) || !TryReadFloat(s, ref i, out var y)) return false;
        point = rel ? new Vector2(cur.X + x, cur.Y + y) : new Vector2(x, y);
        return true;
    }
}

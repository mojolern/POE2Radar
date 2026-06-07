using System.Globalization;

namespace POE2Radar.Overlay;

public sealed record IconDef(string Name, float VbX, float VbY, float VbW, float VbH, IReadOnlyList<string> Paths)
{
    public string ViewBox =>
        string.Join(" ", [Fmt(VbX), Fmt(VbY), Fmt(VbW), Fmt(VbH)]);

    private static string Fmt(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

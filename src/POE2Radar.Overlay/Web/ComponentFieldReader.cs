using System.Globalization;
using System.Text.Json;
using POE2Radar.Core;
using POE2Radar.Core.Game;

namespace POE2Radar.Overlay.Web;

public sealed class ComponentFieldReader
{
    private readonly Dictionary<string, ComponentDef> _components;
    private readonly Poe2Live _live;
    private readonly MemoryReader _reader;

    public ComponentFieldReader(string jsonPath, Poe2Live live, MemoryReader reader)
    {
        _live = live;
        _reader = reader;
        _components = ComponentFieldLoader.Load(jsonPath);
    }

    public IReadOnlyList<string> ComponentNames => _components.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyDictionary<string, ComponentDef> Components => _components;

    public Dictionary<string, object?>? ReadComponent(nint entity, string componentName)
    {
        if (!_components.TryGetValue(componentName, out var def))
            return null;

        var comp = _live.ResolveComponentAddress(entity, componentName);
        if (comp == 0)
            return null;

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in def.Fields)
            result[field.Name] = ReadField(comp, field);

        return result;
    }

    public Dictionary<string, object?> ReadAllComponents(nint entity)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, def) in _components)
        {
            var comp = _live.ResolveComponentAddress(entity, name);
            if (comp == 0)
                continue;

            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in def.Fields)
                fields[field.Name] = ReadField(comp, field);

            result[name] = fields;
        }

        return result;
    }

    public object? ReadField(nint componentBase, ComponentFieldDef field)
    {
        var addr = componentBase + field.Offset;
        var type = field.Type.Trim().ToLowerInvariant();

        if (type.StartsWith("bit", StringComparison.OrdinalIgnoreCase))
            return ReadBitfield(addr, type);

        return type switch
        {
            "byte" => _reader.TryReadStruct<byte>(addr, out var b) ? b : null,
            "sbyte" => _reader.TryReadStruct<sbyte>(addr, out var sb) ? sb : null,
            "short" => _reader.TryReadStruct<short>(addr, out var s) ? s : null,
            "word" or "ushort" => _reader.TryReadStruct<ushort>(addr, out var us) ? us : null,
            "int" => _reader.TryReadStruct<int>(addr, out var i) ? i : null,
            "uint" or "dword" => _reader.TryReadStruct<uint>(addr, out var ui) ? ui : null,
            "long" => _reader.TryReadStruct<long>(addr, out var l) ? l : null,
            "ulong" or "qword" => _reader.TryReadStruct<ulong>(addr, out var ul) ? ul : null,
            "float" => _reader.TryReadStruct<float>(addr, out var f) ? f : null,
            "double" => _reader.TryReadStruct<double>(addr, out var d) ? d : null,
            "bool" => _reader.TryReadStruct<byte>(addr, out var bb) ? bb != 0 : null,
            "ptr" or "pointer" => ReadPointer(addr),
            "vector2" => ReadVector2(addr),
            "vector3" => ReadVector3(addr),
            "vitalstruct" => ReadVitalStruct(addr),
            "utf8" or "string" => ReadUtf8Pointer(addr),
            "utf16" or "wstring" => ReadUtf16Pointer(addr),
            _ => _reader.TryReadStruct<int>(addr, out var v) ? v : null,
        };
    }

    private object? ReadPointer(nint addr)
    {
        return _reader.TryReadStruct<nint>(addr, out var p) ? $"0x{p:X}" : null;
    }

    private object? ReadVector2(nint addr)
    {
        if (!_reader.TryReadStruct<float>(addr, out var x))
            return null;
        _reader.TryReadStruct<float>(addr + 4, out var y);
        return new { x, y };
    }

    private object? ReadVector3(nint addr)
    {
        if (!_reader.TryReadStruct<float>(addr, out var x))
            return null;
        _reader.TryReadStruct<float>(addr + 4, out var y);
        _reader.TryReadStruct<float>(addr + 8, out var z);
        return new { x, y, z };
    }

    private object? ReadVitalStruct(nint addr)
    {
        if (!_reader.TryReadStruct<VitalStruct>(addr, out var vital))
            return null;
        return new
        {
            current = vital.Current,
            max = vital.Max,
            reserved = vital.ReservedFlat,
            reservedPct = vital.ReservedFraction,
            regen = vital.Regen,
            valid = vital.LooksValid(),
        };
    }

    private object? ReadBitfield(nint addr, string type)
    {
        if (!_reader.TryReadStruct<byte>(addr, out var b))
            return null;

        var bitText = type.Replace("bit", "", StringComparison.OrdinalIgnoreCase).Replace("+", "", StringComparison.Ordinal);
        return int.TryParse(bitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bitIndex)
            ? (b >> bitIndex) & 1
            : b;
    }

    private object? ReadUtf8Pointer(nint addr)
    {
        if (!_reader.TryReadStruct<nint>(addr, out var ptr) || ptr == 0)
            return null;
        var value = _reader.ReadStringUtf8(ptr);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private object? ReadUtf16Pointer(nint addr)
    {
        if (!_reader.TryReadStruct<nint>(addr, out var ptr) || ptr == 0)
            return null;
        var value = _reader.ReadStringUtf16(ptr);
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

public sealed class ComponentDef
{
    public string Name { get; init; } = "";
    public string? ByteSetter { get; init; }
    public string? IntSetter { get; init; }
    public string? FloatSetter { get; init; }
    public string? StringAnchor { get; init; }
    public string? ComponentNotes { get; init; }
    public List<ComponentFieldDef> Fields { get; init; } = [];
}

public sealed class ComponentFieldDef
{
    public string Name { get; init; } = "";
    public int Offset { get; init; }
    public string Type { get; init; } = "int";
    public bool Verified { get; init; }
    public string? Notes { get; init; }
}

internal static class ComponentFieldLoader
{
    public static Dictionary<string, ComponentDef> Load(string jsonPath)
    {
        using var stream = File.OpenRead(jsonPath);
        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        var result = new Dictionary<string, ComponentDef>(StringComparer.OrdinalIgnoreCase);
        foreach (var componentProp in doc.RootElement.EnumerateObject())
        {
            if (componentProp.Name.StartsWith('_') || componentProp.Value.ValueKind != JsonValueKind.Object)
                continue;

            if (!componentProp.Value.TryGetProperty("fields", out var fieldsElement) ||
                fieldsElement.ValueKind != JsonValueKind.Object)
                continue;

            var fields = new List<ComponentFieldDef>();
            foreach (var fieldProp in fieldsElement.EnumerateObject())
            {
                if (fieldProp.Value.ValueKind != JsonValueKind.Object)
                    continue;

                fields.Add(new ComponentFieldDef
                {
                    Name = fieldProp.Name,
                    Offset = ReadOffset(fieldProp.Value, "offset"),
                    Type = ReadString(fieldProp.Value, "type") ?? "int",
                    Verified = ReadBool(fieldProp.Value, "verified"),
                    Notes = ReadString(fieldProp.Value, "notes"),
                });
            }

            result[componentProp.Name] = new ComponentDef
            {
                Name = componentProp.Name,
                ByteSetter = ReadString(componentProp.Value, "byte_setter"),
                IntSetter = ReadString(componentProp.Value, "int_setter"),
                FloatSetter = ReadString(componentProp.Value, "float_setter"),
                StringAnchor = ReadString(componentProp.Value, "string_anchor"),
                ComponentNotes = ReadString(componentProp.Value, "component_notes") ?? ReadString(componentProp.Value, "notes"),
                Fields = fields,
            };
        }

        return result;
    }

    private static string? ReadString(JsonElement obj, string name)
    {
        return obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool ReadBool(JsonElement obj, string name)
    {
        return obj.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
    }

    private static int ReadOffset(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            return intValue;

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) ? hex : 0;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec) ? dec : 0;
    }
}

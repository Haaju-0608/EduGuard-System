using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EduGuardProject.Helpers;

public static class FieldSelectionHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static object? ApplyFields(object? data, string? fields)
    {
        if (data == null || string.IsNullOrWhiteSpace(fields))
            return data;

        var allowed = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(f => f.ToLowerInvariant())
            .ToHashSet();

        if (data is IEnumerable enumerable and not string)
        {
            return enumerable.Cast<object>().Select(item => FilterObject(item, allowed)).ToList();
        }

        return FilterObject(data, allowed);
    }

    private static Dictionary<string, object?> FilterObject(object item, HashSet<string> allowed)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!allowed.Contains(prop.Name.ToLowerInvariant()))
                continue;

            result[ToCamelCase(prop.Name)] = prop.GetValue(item);
        }

        return result;
    }

    private static string ToCamelCase(string name) =>
        char.ToLowerInvariant(name[0]) + name[1..];
}

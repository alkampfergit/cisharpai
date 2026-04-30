using System.Text;
using System.Text.Json;

namespace Cisharpai;

/// <summary>
/// Deeply merges a JSON override document into a base JSON document.
/// Object properties are merged recursively; all other value kinds
/// (arrays, strings, numbers, booleans, null) in the override replace
/// the corresponding base value.
/// </summary>
public static class JsonDeepMerge
{
    public static string Merge(string baseJson, JsonElement overrides)
    {
        if (overrides.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Overrides must be a JSON object.", nameof(overrides));

        using var baseDoc = JsonDocument.Parse(baseJson);

        if (baseDoc.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Base JSON must be an object.", nameof(baseJson));

        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            MergeObjects(writer, baseDoc.RootElement, overrides);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void MergeObjects(
        Utf8JsonWriter writer,
        JsonElement baseElement,
        JsonElement overrideElement)
    {
        writer.WriteStartObject();

        // Write all base properties, recursively merging when override has the same key.
        foreach (var prop in baseElement.EnumerateObject())
        {
            if (overrideElement.TryGetProperty(prop.Name, out var overrideValue))
            {
                writer.WritePropertyName(prop.Name);

                if (prop.Value.ValueKind == JsonValueKind.Object &&
                    overrideValue.ValueKind == JsonValueKind.Object)
                {
                    MergeObjects(writer, prop.Value, overrideValue);
                }
                else
                {
                    overrideValue.WriteTo(writer);
                }
            }
            else
            {
                prop.WriteTo(writer);
            }
        }

        // Write override properties that don't exist in the base.
        foreach (var prop in overrideElement.EnumerateObject())
        {
            if (!baseElement.TryGetProperty(prop.Name, out _))
            {
                prop.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }
}

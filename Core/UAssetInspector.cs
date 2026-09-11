using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.Core;

public static class UAssetInspector
{
    public static RawExportInfo ReadLocalizationExport(string jsonPath, string sourceLabel)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
        var root = doc.RootElement;

        if (!root.TryGetProperty("Exports", out var exports) || exports.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"{sourceLabel}: UAssetGUI JSON has no Exports array");

        JsonElement? candidate = null;
        var exportCount = exports.GetArrayLength();

        foreach (var export in exports.EnumerateArray())
        {
            if (export.ValueKind != JsonValueKind.Object)
                continue;

            var objectName = export.TryGetProperty("ObjectName", out var nameElement)
                ? nameElement.ToString()
                : string.Empty;

            var hasData = export.TryGetProperty("Data", out var dataElement)
                          && dataElement.ValueKind == JsonValueKind.String;

            if (hasData && objectName.Contains("LocalizationDatabase", StringComparison.OrdinalIgnoreCase))
            {
                if (candidate is not null)
                    throw new InvalidDataException($"{sourceLabel}: more than one raw LocalizationDatabase export was found");
                candidate = export;
            }
        }

        if (candidate is null)
            throw new InvalidDataException($"{sourceLabel}: no raw LocalizationDatabase export was found");

        var selected = candidate.Value;
        var base64 = selected.GetProperty("Data").GetString()
                     ?? throw new InvalidDataException($"{sourceLabel}: RawExport Data is empty");

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(base64);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"{sourceLabel}: invalid RawExport Base64", ex);
        }

        var serialSize = ReadInt64Like(selected, "SerialSize", sourceLabel);
        var serialOffset = ReadInt64Like(selected, "SerialOffset", sourceLabel);

        var importsClass = false;
        if (root.TryGetProperty("Imports", out var imports) && imports.ValueKind == JsonValueKind.Array)
        {
            foreach (var import in imports.EnumerateArray())
            {
                if (import.ValueKind != JsonValueKind.Object)
                    continue;
                if (import.TryGetProperty("ObjectName", out var objectName)
                    && string.Equals(objectName.ToString(), "ModLocalizationDatabaseDataAsset", StringComparison.Ordinal))
                {
                    importsClass = true;
                    break;
                }
            }
        }

        return new RawExportInfo
        {
            Payload = payload,
            SerialSize = serialSize,
            SerialOffset = serialOffset,
            ExportCount = exportCount,
            ImportsLocalizationDatabaseClass = importsClass,
        };
    }


    public static string DetectInternalPackagePath(string jsonPath, string virtualPath, string sourceLabel)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
        var normalized = virtualPath.Replace('\\', '/');
        var assetStem = Path.GetFileNameWithoutExtension(normalized);
        var suffix = "/" + assetStem;
        var candidates = new HashSet<string>(StringComparer.Ordinal);

        static void Walk(JsonElement element, string suffix, HashSet<string> candidates)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                {
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value)
                        && value.StartsWith("/", StringComparison.Ordinal)
                        && value.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        candidates.Add(value);
                    }
                    break;
                }
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        Walk(item, suffix, candidates);
                    break;
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                        Walk(property.Value, suffix, candidates);
                    break;
            }
        }

        Walk(doc.RootElement, suffix, candidates);
        if (candidates.Count != 1)
        {
            var details = candidates.Count == 0
                ? "(none)"
                : string.Join(Environment.NewLine, candidates.OrderBy(x => x, StringComparer.Ordinal));
            throw new InvalidDataException(
                $"{sourceLabel}: expected exactly one internal Unreal package path ending in '{suffix}', " +
                $"found {candidates.Count}:{Environment.NewLine}{details}"
            );
        }

        return candidates.Single();
    }

    private static long ReadInt64Like(JsonElement obj, string property, string sourceLabel)
    {
        if (!obj.TryGetProperty(property, out var value))
            throw new InvalidDataException($"{sourceLabel}: RawExport has no {property}");

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number))
            return number;

        throw new InvalidDataException($"{sourceLabel}: invalid RawExport {property}");
    }
}

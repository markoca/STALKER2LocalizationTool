using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

public static class UAssetInspector
{
    public static RawExportInfo ReadLocalizationExport(
        string jsonPath,
        string sourceLabel,
        string legacyUexpPath)
    {
        using var stream = new FileStream(
            jsonPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.SequentialScan
        );
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Exports", out var exports)
            || exports.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"{sourceLabel}: UAssetGUI JSON has no Exports array"
            );
        }

        JsonElement? candidate = null;
        var exportCount = exports.GetArrayLength();
        foreach (var export in exports.EnumerateArray())
        {
            if (export.ValueKind != JsonValueKind.Object)
                continue;

            var objectName = export.TryGetProperty(
                "ObjectName",
                out var nameElement)
                ? nameElement.ToString()
                : string.Empty;

            // UAssetGUI represents unmapped assets as Base64 RawExport and
            // mapped assets as a structured property array.
            var hasData = export.TryGetProperty("Data", out var dataElement)
                          && (dataElement.ValueKind == JsonValueKind.String
                              || dataElement.ValueKind == JsonValueKind.Array);

            if (hasData
                && objectName.Contains(
                    "LocalizationDatabase",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (candidate is not null)
                {
                    throw new InvalidDataException(
                        $"{sourceLabel}: more than one "
                        + "LocalizationDatabase export was found"
                    );
                }

                candidate = export;
            }
        }

        if (candidate is null)
        {
            throw new InvalidDataException(
                $"{sourceLabel}: no LocalizationDatabase export with Base64 or mapped Data was found"
            );
        }

        var selected = candidate.Value;
        var serialSize = ReadInt64Like(selected, "SerialSize", sourceLabel);
        var serialOffset = ReadInt64Like(selected, "SerialOffset", sourceLabel);
        var data = selected.GetProperty("Data");
        var isMapped = data.ValueKind == JsonValueKind.Array;
        byte[] payload;

        if (!isMapped)
        {
            try
            {
                // Decode the Base64 JSON token without allocating an intermediate string.
                payload = data.GetBytesFromBase64();
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            {
                throw new InvalidDataException(
                    $"{sourceLabel}: invalid RawExport Base64", ex);
            }
        }
        else
        {
            // Modern mappings deserialize ModLocalizationDatabaseDataAsset into
            // a property array. The original bytes are still present, untouched,
            // in retoc\u0027s pristine .uexp. Reading them preserves the exact binary
            // layout required by LocalizationDatabaseCodec and PackagePatcher.
            if (exportCount != 1)
                throw new InvalidDataException(
                    $"{sourceLabel}: mapped LocalizationDatabase requires exactly one export (found {exportCount})");
            if (serialSize <= 0 || serialSize > int.MaxValue)
                throw new InvalidDataException(
                    $"{sourceLabel}: invalid mapped export SerialSize {serialSize}");
            if (!File.Exists(legacyUexpPath))
                throw new FileNotFoundException(
                    $"{sourceLabel}: pristine .uexp required for mapped export", legacyUexpPath);

            var uexpBytes = File.ReadAllBytes(legacyUexpPath);
            if (uexpBytes.LongLength < serialSize)
                throw new InvalidDataException(
                    $"{sourceLabel}: mapped export SerialSize {serialSize} exceeds pristine .uexp length {uexpBytes.LongLength}");

            // retoc to-legacy places the sole export at the beginning of .uexp.
            // Never attempt to reconstruct binary data from UAssetGUI JSON.
            payload = uexpBytes.AsSpan(0, checked((int)serialSize)).ToArray();
            try
            {
                _ = LocalizationDatabaseCodec.Parse(payload, sourceLabel);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
            {
                throw new InvalidDataException(
                    $"{sourceLabel}: mapped export raw bytes in pristine .uexp are not a valid LocalizationDatabase", ex);
            }
        }

        if (serialSize != payload.LongLength)
            throw new InvalidDataException(
                $"{sourceLabel}: SerialSize {serialSize} differs from payload length {payload.LongLength}");


        var importsClass = false;
        if (root.TryGetProperty("Imports", out var imports)
            && imports.ValueKind == JsonValueKind.Array)
        {
            foreach (var import in imports.EnumerateArray())
            {
                if (import.ValueKind != JsonValueKind.Object)
                    continue;

                if (import.TryGetProperty(
                        "ObjectName",
                        out var objectName)
                    && string.Equals(
                        objectName.ToString(),
                        "ModLocalizationDatabaseDataAsset",
                        StringComparison.Ordinal))
                {
                    importsClass = true;
                    break;
                }
            }
        }

        var internalPackagePath = DetectInternalPackagePath(
            root,
            sourceLabel,
            sourceLabel
        );

        return new RawExportInfo
        {
            Payload = payload,
            UsedMappedExport = isMapped,
            SerialSize = serialSize,
            SerialOffset = serialOffset,
            ExportCount = exportCount,
            ImportsLocalizationDatabaseClass = importsClass,
            InternalPackagePath = internalPackagePath,
        };
    }

    private static string DetectInternalPackagePath(
        JsonElement root,
        string virtualPath,
        string sourceLabel)
    {
        var normalized = virtualPath.Replace('\\', '/');
        var assetStem = Path.GetFileNameWithoutExtension(normalized);
        var suffix = "/" + assetStem;
        var candidates = new HashSet<string>(StringComparer.Ordinal);

        static void Walk(
            JsonElement element,
            string suffix,
            HashSet<string> candidates)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                {
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value)
                        && value.StartsWith('/')
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
                    {
                        // RawExport.Data is often by far the largest token in this
                        // document. It is Base64 binary, never an Unreal package path.
                        // Materializing it as a string can allocate hundreds of MiB.
                        if (string.Equals(
                                property.Name,
                                "Data",
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        Walk(property.Value, suffix, candidates);
                    }
                    break;
            }
        }

        Walk(root, suffix, candidates);

        if (candidates.Count != 1)
        {
            var details = candidates.Count == 0
                ? "(none)"
                : string.Join(
                    Environment.NewLine,
                    candidates.OrderBy(x => x, StringComparer.Ordinal)
                );

            throw new InvalidDataException(
                $"{sourceLabel}: expected exactly one internal Unreal "
                + $"package path ending in '{suffix}', found "
                + $"{candidates.Count}:{Environment.NewLine}{details}"
            );
        }

        return candidates.Single();
    }

    private static long ReadInt64Like(
        JsonElement obj,
        string property,
        string sourceLabel)
    {
        if (!obj.TryGetProperty(property, out var value))
        {
            throw new InvalidDataException(
                $"{sourceLabel}: RawExport has no {property}"
            );
        }

        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), out number))
        {
            return number;
        }

        throw new InvalidDataException(
            $"{sourceLabel}: invalid RawExport {property}"
        );
    }
}

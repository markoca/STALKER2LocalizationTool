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

        var exportCount = exports.GetArrayLength();
        var candidates = new List<(JsonElement Value, string Name, int Index)>();
        var descriptions = new List<string>();
        var exportIndex = 0;

        foreach (var export in exports.EnumerateArray())
        {
            if (export.ValueKind != JsonValueKind.Object)
            {
                exportIndex++;
                continue;
            }

            var objectName = export.TryGetProperty("ObjectName", out var nameElement)
                ? nameElement.ToString()
                : string.Empty;
            var dataKind = export.TryGetProperty("Data", out var dataElement)
                ? dataElement.ValueKind
                : JsonValueKind.Undefined;

            // A fully mapped export may be an object or an array, whereas
            // unmapped RawExport.Data is a Base64 string.
            descriptions.Add($"#{exportIndex}: {objectName} (Data={dataKind})");
            if (objectName.Contains("LocalizationDatabase", StringComparison.OrdinalIgnoreCase)
                && dataKind is JsonValueKind.String or JsonValueKind.Array or JsonValueKind.Object)
            {
                candidates.Add((export, objectName, exportIndex));
            }

            exportIndex++;
        }

        var assetStem = Path.GetFileNameWithoutExtension(sourceLabel.Replace('\\', '/'));
        var matchedByName = candidates
            .Where(item => string.Equals(item.Name, assetStem, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Class-default objects and related exports can also contain the
        // substring "LocalizationDatabase". Only the export whose ObjectName
        // equals the asset stem identifies the actual localization asset.
        // Do not pick an arbitrary export when names remain ambiguous.
        var eligible = matchedByName.Count == 1 ? matchedByName : candidates;
        if (eligible.Count != 1)
        {
            var reason = eligible.Count == 0
                ? "no matching LocalizationDatabase export"
                : $"ambiguous LocalizationDatabase exports ({eligible.Count})";
            throw new InvalidDataException(
                $"{sourceLabel}: {reason}. Expected ObjectName '{assetStem}'. "
                + $"Available exports: {string.Join("; ", descriptions)}"
            );
        }

        var candidate = eligible[0];
        var selected = candidate.Value;
        var serialSize = ReadInt64Like(selected, "SerialSize", sourceLabel);
        var serialOffset = ReadInt64Like(selected, "SerialOffset", sourceLabel);
        var data = selected.GetProperty("Data");
        var isMapped = data.ValueKind != JsonValueKind.String;
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
            // in the pristine .uexp extracted by retoc. Reading them preserves the exact binary
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

            // SerialOffset values are package-relative. In the retoc legacy
            // output the lowest non-empty export offset starts at .uexp[0].
            // Account for preceding exports instead of assuming this one is
            // always the first export.
            var offsets = ReadSerializedExports(exports, sourceLabel);
            var firstOffset = offsets.Min(item => item.Offset);
            var relativeOffset = serialOffset - firstOffset;
            if (relativeOffset < 0 || relativeOffset > uexpBytes.LongLength
                || serialSize > uexpBytes.LongLength - relativeOffset)
            {
                throw new InvalidDataException(
                    $"{sourceLabel}: mapped export at relative offset {relativeOffset} "
                    + $"with SerialSize {serialSize} is outside pristine .uexp "
                    + $"({uexpBytes.LongLength} bytes)");
            }

            // Preserve exact pristine binary contents. Never reverse-engineer
            // or reserialize the UAssetGUI mapped JSON property representation.
            payload = uexpBytes.AsSpan(checked((int)relativeOffset), checked((int)serialSize)).ToArray();
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


        var serializedExports = ReadSerializedExports(exports, sourceLabel);
        var selectedIsLast = serializedExports.All(item =>
            item.Offset <= serialOffset
            && (item.Offset < serialOffset || item.Size == serialSize));

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
            IsLastSerializedExport = selectedIsLast,
            ImportsLocalizationDatabaseClass = importsClass,
            InternalPackagePath = internalPackagePath,
        };
    }

    private static List<(long Offset, long Size)> ReadSerializedExports(
        JsonElement exports, string sourceLabel)
    {
        var result = new List<(long Offset, long Size)>();
        foreach (var export in exports.EnumerateArray())
        {
            if (export.ValueKind != JsonValueKind.Object
                || !export.TryGetProperty("SerialOffset", out _)
                || !export.TryGetProperty("SerialSize", out _))
                continue;

            var size = ReadInt64Like(export, "SerialSize", sourceLabel);
            var offset = ReadInt64Like(export, "SerialOffset", sourceLabel);
            if (size < 0 || offset < 0)
                throw new InvalidDataException(
                    $"{sourceLabel}: invalid export offset/size {offset}/{size}");
            if (size > 0)
                result.Add((offset, size));
        }

        if (result.Count == 0)
            throw new InvalidDataException(
                $"{sourceLabel}: no exports with non-empty serialized data");
        return result;
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

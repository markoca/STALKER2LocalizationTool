namespace STALKER2LocalizationTool.Core;

public static class PathUtil
{
    public static string MakeSafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
            sb.Append(invalid.Contains(c) ? '_' : c);

        var result = Regex.Replace(sb.ToString(), @"\s+", " ").Trim(' ', '.');
        return string.IsNullOrWhiteSpace(result) ? "Localization" : result;
    }

    public static string MakeModId(string name)
    {
        return MakeSafeName(name);
    }

    public static string NormalizeVirtualPath(string virtualPath)
    {
        var value = virtualPath.Replace('\\', '/').Trim();
        while (value.StartsWith("../", StringComparison.Ordinal))
            value = value[3..];
        value = value.TrimStart('/');
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Could not normalize virtual asset path: {virtualPath}");
        return value.Replace('/', Path.DirectorySeparatorChar);
    }

    public static string NormalizePakPath(string pakPath)
    {
        var value = pakPath.Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Could not normalize PAK path: {pakPath}");
        return value.Replace('/', Path.DirectorySeparatorChar);
    }

    public static string InferModName(string modsRoot, string sourcePath)
    {
        var relative = Path.GetRelativePath(modsRoot, sourcePath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length > 1)
            return parts[0];

        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var patterns = new[]
        {
            // Current MODS localization source-family suffixes. Keep these before the
            // generic _P rule so e.g. SomeModB_P groups as SomeMod rather than SomeModB.
            @"(?i)_OC_50$",
            @"(?i)-OverrideContent$",
            @"(?i)_OC$",
            @"(?i)_NC$",
            @"(?i)B_P$",

            // Older/verbose naming remains understood for grouping compatibility, even
            // though MODS scanning itself is filtered to the explicit source suffixes.
            @"(?i)Stalker2-Windows-(NewContent|OverrideContent)$",
            @"(?i)-Windows-(NewContent|OverrideContent)$",
            @"(?i)[_-](NewContent|OverrideContent)$",
            @"(?i)_P$",
        };
        foreach (var pattern in patterns)
            stem = Regex.Replace(stem, pattern, string.Empty);
        stem = stem.TrimEnd('_', '-', ' ');
        return string.IsNullOrWhiteSpace(stem) ? Path.GetFileNameWithoutExtension(sourcePath) : stem;
    }

    public static bool IsSupportedModLocalizationContainer(string sourcePath)
    {
        return GetModLocalizationContainerPriority(sourcePath) < int.MaxValue;
    }

    public static bool IsOverrideModLocalizationContainer(string sourcePath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        return stem.EndsWith("_OC_50", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("_OC", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("-OverrideContent", StringComparison.OrdinalIgnoreCase);
    }

    public static int GetModLocalizationContainerPriority(string sourcePath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);

        // OverrideContent is authoritative. _OC_50 is intentionally the most specific
        // form, followed by the explicit verbose name and the normal _OC suffix.
        if (stem.EndsWith("_OC_50", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (stem.EndsWith("-OverrideContent", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (stem.EndsWith("_OC", StringComparison.OrdinalIgnoreCase))
            return 2;

        // Fallback families are scanned only when no OverrideContent family exists.
        if (stem.EndsWith("_NC", StringComparison.OrdinalIgnoreCase))
            return 10;
        if (stem.EndsWith("B_P", StringComparison.OrdinalIgnoreCase))
            return 20;

        return int.MaxValue;
    }

    public static string GetModLocalizationContainerKind(string sourcePath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        if (stem.EndsWith("_OC_50", StringComparison.OrdinalIgnoreCase))
            return "OverrideContent (_OC_50)";
        if (stem.EndsWith("-OverrideContent", StringComparison.OrdinalIgnoreCase))
            return "OverrideContent (-OverrideContent)";
        if (stem.EndsWith("_OC", StringComparison.OrdinalIgnoreCase))
            return "OverrideContent (_OC)";
        if (stem.EndsWith("_NC", StringComparison.OrdinalIgnoreCase))
            return "NewContent (_NC)";
        if (stem.EndsWith("B_P", StringComparison.OrdinalIgnoreCase))
            return "B_P fallback";
        return "unsupported";
    }

    public static string GetModLocalizationSourceFamilyKey(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        return Path.Combine(directory, stem);
    }


    public static string GetZenPackageIdentity(string zenChunkId)
    {
        var value = zenChunkId.Trim();
        if (value.Length == 24 && value.All(Uri.IsHexDigit))
            return value[..16].ToLowerInvariant();

        // Be conservative for malformed or future chunk-ID formats: do not collapse
        // anything unless it is the expected 24-character hexadecimal Zen ID.
        return value.ToLowerInvariant();
    }

    public static bool IsOverrideContentContainer(string sourceContainerRelativePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourceContainerRelativePath);
        return IsOverrideModLocalizationContainer(sourceContainerRelativePath)
               || fileName.Contains("OverrideContent", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNewContentContainer(string sourceContainerRelativePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourceContainerRelativePath);
        return fileName.EndsWith("_NC", StringComparison.OrdinalIgnoreCase)
               || fileName.Contains("NewContent", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBaseContentAlias(string virtualPath)
    {
        var p = virtualPath.Replace('\\', '/');
        return p.StartsWith("../../../Stalker2/Content/", StringComparison.OrdinalIgnoreCase)
               || p.StartsWith("Stalker2/Content/", StringComparison.OrdinalIgnoreCase);
    }
}

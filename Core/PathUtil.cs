namespace LocalizationWorkbench.Core;

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
        var value = NormalizeVirtualPathForComparison(virtualPath);
        return value.Replace('/', Path.DirectorySeparatorChar);
    }

    public static string NormalizeVirtualPathForComparison(string virtualPath)
    {
        var value = virtualPath.Replace('\\', '/').Trim();
        while (value.StartsWith("../", StringComparison.Ordinal))
            value = value[3..];
        value = value.TrimStart('/');
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Could not normalize virtual asset path: {virtualPath}");
        return value;
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
            // ZoneKit / common paired localization container conventions.
            @"(?i)Stalker2-Windows-(NewContent|OverrideContent)$",
            @"(?i)-Windows-(NewContent|OverrideContent)$",
            @"(?i)[_-](NewContent|OverrideContent)$",
            @"(?i)_OC_50$",
            @"(?i)_OC$",
            @"(?i)_NC$",
            @"(?i)-OverrideContent$",
            @"(?i)-NewContent$",
            @"(?i)_O$",
            @"(?i)_N$",
            @"(?i)[AB]_P$",
        };
        foreach (var pattern in patterns)
            stem = Regex.Replace(stem, pattern, string.Empty);
        stem = stem.TrimEnd('_', '-', ' ');
        return string.IsNullOrWhiteSpace(stem) ? Path.GetFileNameWithoutExtension(sourcePath) : stem;
    }

    public static bool IsSupportedModLocalizationContainer(string sourcePath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        return stem.EndsWith("_OC_50", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("_OC", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("_NC", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("-OverrideContent", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("-NewContent", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("Stalker2-Windows-OverrideContent", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("Stalker2-Windows-NewContent", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("_O", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("_N", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("A_P", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("B_P", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsOverrideModLocalizationContainer(string sourcePath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        return stem.EndsWith("_OC_50", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("_OC", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("-OverrideContent", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("Stalker2-Windows-OverrideContent", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("_O", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("B_P", StringComparison.OrdinalIgnoreCase);
    }

    public static int GetModLocalizationContainerPriority(string sourcePath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        if (stem.EndsWith("_OC_50", StringComparison.OrdinalIgnoreCase)) return 0;
        if (stem.EndsWith("-OverrideContent", StringComparison.OrdinalIgnoreCase)) return 1;
        if (stem.EndsWith("Stalker2-Windows-OverrideContent", StringComparison.OrdinalIgnoreCase)) return 1;
        if (stem.EndsWith("_OC", StringComparison.OrdinalIgnoreCase)) return 2;
        if (stem.EndsWith("_O", StringComparison.OrdinalIgnoreCase)) return 3;
        if (stem.EndsWith("B_P", StringComparison.OrdinalIgnoreCase)) return 4;
        if (stem.EndsWith("_NC", StringComparison.OrdinalIgnoreCase)) return 10;
        if (stem.EndsWith("-NewContent", StringComparison.OrdinalIgnoreCase)) return 11;
        if (stem.EndsWith("Stalker2-Windows-NewContent", StringComparison.OrdinalIgnoreCase)) return 11;
        if (stem.EndsWith("_N", StringComparison.OrdinalIgnoreCase)) return 12;
        if (stem.EndsWith("A_P", StringComparison.OrdinalIgnoreCase)) return 13;
        return int.MaxValue;
    }

    public static string GetModLocalizationContainerKind(string sourcePath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        if (stem.EndsWith("_OC_50", StringComparison.OrdinalIgnoreCase)) return "OverrideContent (_OC_50)";
        if (stem.EndsWith("-OverrideContent", StringComparison.OrdinalIgnoreCase)
            || stem.EndsWith("Stalker2-Windows-OverrideContent", StringComparison.OrdinalIgnoreCase))
            return "OverrideContent";
        if (stem.EndsWith("_OC", StringComparison.OrdinalIgnoreCase)) return "OverrideContent (_OC)";
        if (stem.EndsWith("_O", StringComparison.OrdinalIgnoreCase)) return "OverrideContent (_O)";
        if (stem.EndsWith("B_P", StringComparison.OrdinalIgnoreCase)) return "paired B_P";
        if (stem.EndsWith("_NC", StringComparison.OrdinalIgnoreCase)) return "NewContent (_NC)";
        if (stem.EndsWith("-NewContent", StringComparison.OrdinalIgnoreCase)
            || stem.EndsWith("Stalker2-Windows-NewContent", StringComparison.OrdinalIgnoreCase))
            return "NewContent";
        if (stem.EndsWith("_N", StringComparison.OrdinalIgnoreCase)) return "NewContent (_N)";
        if (stem.EndsWith("A_P", StringComparison.OrdinalIgnoreCase)) return "paired A_P";
        return "unsupported";
    }

    public static string GetModLocalizationSourceFamilyKey(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var inferred = InferModName(directory.Length == 0 ? "." : directory, sourcePath);
        return Path.Combine(directory, inferred);
    }

    public static string GetZenPackageIdentity(string zenChunkId)
    {
        // Full 24-hex ExportBundleData chunk ID is authoritative. Keeping this helper
        // preserves call-site compatibility while deliberately avoiding prefix collapse.
        return zenChunkId.Trim().ToLowerInvariant();
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
               || fileName.EndsWith("_N", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith("A_P", StringComparison.OrdinalIgnoreCase)
               || fileName.Contains("NewContent", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBaseContentAlias(string virtualPath)
    {
        var p = NormalizeVirtualPathForComparison(virtualPath);
        return p.StartsWith("Stalker2/Content/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPluginContentAlias(string virtualPath)
    {
        var p = NormalizeVirtualPathForComparison(virtualPath);
        return p.StartsWith("Stalker2/Mods/", StringComparison.OrdinalIgnoreCase);
    }

    public static string DirectoryAliasPackagePathFromVirtualPath(string virtualPath)
    {
        var value = NormalizeVirtualPathForComparison(virtualPath);
        if (value.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            value = value[..^7];

        const string gamePrefix = "Stalker2/Content/";
        if (value.StartsWith(gamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var tail = value[gamePrefix.Length..];
            if (string.IsNullOrWhiteSpace(tail))
                throw new InvalidOperationException($"Invalid base-game virtual package path: {virtualPath}");
            return "/Game/" + tail;
        }

        const string modsPrefix = "Stalker2/Mods/";
        if (value.StartsWith(modsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = value[modsPrefix.Length..];
            const string marker = "/Content/";
            var markerIndex = rest.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex <= 0)
                throw new InvalidOperationException($"Invalid mod/plugin virtual package path: {virtualPath}");

            var plugin = rest[..markerIndex];
            var tail = rest[(markerIndex + marker.Length)..];
            if (string.IsNullOrWhiteSpace(plugin) || string.IsNullOrWhiteSpace(tail))
                throw new InvalidOperationException($"Invalid mod/plugin virtual package path: {virtualPath}");
            return $"/{plugin}/{tail}";
        }

        throw new InvalidOperationException($"Cannot derive Unreal package path from LocalizationDatabase path: {virtualPath}");
    }
}

using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.Core;

public sealed class LocalizationAliasGroupingResult
{
    public List<LocalizationAssetGroup> Assets { get; init; } = new();
    public int ExactChunkAliasesCollapsed { get; init; }
    public int PackagePrefixDuplicatesCollapsed { get; init; }
}

public static class LocalizationAliasGrouper
{
    public static LocalizationAliasGroupingResult Group(IEnumerable<LocalizationAlias> aliases)
    {
        var source = aliases.ToList();
        var distinctFullChunkCount = source
            .Select(alias => alias.ZenChunkId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var assets = source
            .GroupBy(alias => PathUtil.GetZenPackageIdentity(alias.ZenChunkId), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    // Prefer the actual base-game override path when NewContent and
                    // OverrideContent expose the same localization database package.
                    .OrderBy(alias => PathUtil.IsBaseContentAlias(alias.VirtualPath) ? 0 : 1)
                    .ThenBy(alias => PathUtil.IsOverrideContentContainer(alias.SourceUtocRelative) ? 0 : 1)
                    .ThenBy(alias => alias.SourceUtocRelative, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(alias => alias.VirtualPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(alias => alias.ZenChunkId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var canonical = ordered[0];
                return new LocalizationAssetGroup
                {
                    // Keep the complete chunk ID of the canonical source for extraction,
                    // build verification and retoc identity checks. The 16-character
                    // package prefix is used only for duplicate detection.
                    ZenChunkId = canonical.ZenChunkId.ToLowerInvariant(),
                    Canonical = canonical,
                    Aliases = ordered,
                };
            })
            .OrderBy(asset => asset.Canonical.VirtualPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.ZenChunkId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LocalizationAliasGroupingResult
        {
            Assets = assets,
            ExactChunkAliasesCollapsed = source.Count - distinctFullChunkCount,
            PackagePrefixDuplicatesCollapsed = distinctFullChunkCount - assets.Count,
        };
    }
}

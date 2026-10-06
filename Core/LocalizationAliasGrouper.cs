using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

public sealed class LocalizationAliasGroupingResult
{
    public List<LocalizationAssetGroup> Assets { get; init; } = new();
    public int ExactChunkAliasesCollapsed { get; init; }
}

public static class LocalizationAliasGrouper
{
    public static LocalizationAliasGroupingResult Group(IEnumerable<LocalizationAlias> aliases)
    {
        var source = aliases.ToList();

        // ExportBundleData chunk IDs are the authoritative package identity here.
        // Do NOT collapse only on the first 16 hex characters. The current launch.py
        // baseline groups aliases only when the complete 24-hex Zen chunk ID matches.
        var assets = source
            .GroupBy(alias => alias.ZenChunkId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(alias => PathUtil.IsBaseContentAlias(alias.VirtualPath) ? 0 : 1)
                    .ThenBy(alias => PathUtil.IsOverrideContentContainer(alias.SourceUtocRelative) ? 0 : 1)
                    .ThenBy(alias => alias.SourceUtocRelative, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(alias => alias.VirtualPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var canonical = ordered[0];
                return new LocalizationAssetGroup
                {
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
            ExactChunkAliasesCollapsed = source.Count - assets.Count,
        };
    }
}

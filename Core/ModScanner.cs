using STALKER2LocalizationTool.Models;
using STALKER2LocalizationTool.Services;

namespace STALKER2LocalizationTool.Core;

public sealed class ModScanner
{
    private readonly RetocService _retoc;
    private readonly string _modsRoot;
    private readonly string _cachedRoot;
    private readonly string _editableRoot;
    private readonly IReadOnlyList<BuildLanguage> _buildLanguages;
    private readonly Action<string>? _log;

    public ModScanner(
        RetocService retoc,
        string modsRoot,
        string cachedRoot,
        string editableRoot,
        IEnumerable<int> buildLanguageIds,
        Action<string>? log = null)
    {
        _retoc = retoc;
        _modsRoot = modsRoot;
        _cachedRoot = cachedRoot;
        _editableRoot = editableRoot;
        _buildLanguages = buildLanguageIds
            .Select(BuildLanguageCatalog.ById)
            .DistinctBy(language => language.Id)
            .ToList();
        _log = log;
    }

    public async Task<List<ModScanResult>> ScanAsync(
        IProgress<(int Current, int Total, string Message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_modsRoot))
            return new List<ModScanResult>();

        var allUtocs = Directory.EnumerateFiles(_modsRoot, "*.utoc", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // MODS is LocalizationDatabase-only. Traditional Game.locres PAKs belong
        // exclusively to the GAME workflow and are never opened or inspected here.
        // A companion .pak next to a selected .utoc may still be copied later as
        // retoc input, but its contents are not scanned as localization.
        var utocs = allUtocs
            .Where(PathUtil.IsSupportedModLocalizationContainer)
            .ToList();

        var ignoredSourceCount = allUtocs.Count - utocs.Count;
        _log?.Invoke(
            $"MODS source filter: {utocs.Count} IoStore eligible; " +
            $"ignored {ignoredSourceCount} .utoc file(s) outside _OC_50/_NC/B_P/_OC/-OverrideContent naming. " +
            "LOCRES/PAK scanning is disabled for MODS."
        );

        var groups = BuildGroups(utocs);

        // Select one source family per mod before scanning. OverrideContent wins; if no
        // OverrideContent family exists, _NC is the first fallback and B_P the second.
        foreach (var group in groups)
            SelectPreferredSourceFamily(group);

        var totalSources = groups.Sum(group => group.Containers.Count);
        var processed = 0;

        foreach (var group in groups)
        {
            var aliases = new List<LocalizationAlias>();
            var errors = new List<string>();

            foreach (var utoc in group.Containers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;
                progress?.Report((processed, totalSources, Path.GetFileName(utoc)));

                try
                {
                    aliases.AddRange(await _retoc.ListLocalizationAssetsAsync(utoc, _modsRoot, cancellationToken));
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(utoc)}: {ex.Message}");
                    _log?.Invoke($"IoStore scan failed for {utoc}: {ex.Message}");
                }
            }


            var aliasGrouping = LocalizationAliasGrouper.Group(aliases);
            group.Assets = aliasGrouping.Assets;

            // Safety net: source-family selection above normally means one IoStore source
            // reaches this point. If that selected OverrideContent source itself exposes more
            // than one logical LocalizationDatabase, collapse it deterministically to one DB.
            if (group.Assets.Count > 1)
            {
                var overrideAssets = group.Assets
                    .Where(asset => asset.Aliases.Any(alias =>
                        PathUtil.IsBaseContentAlias(alias.VirtualPath)
                        || PathUtil.IsOverrideContentContainer(alias.SourceUtocRelative)))
                    .OrderBy(asset => asset.Aliases.Any(alias =>
                        PathUtil.IsBaseContentAlias(alias.VirtualPath)) ? 0 : 1)
                    .ThenBy(asset => asset.Canonical.VirtualPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(asset => asset.ZenChunkId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (overrideAssets.Count > 0)
                {
                    var selected = overrideAssets[0];
                    var suppressedCount = group.Assets.Count - 1;
                    group.Assets = new List<LocalizationAssetGroup> { selected };

                    _log?.Invoke(
                        $"{group.ModName}: found multiple localization databases; " +
                        $"using one OverrideContent database only ({selected.Canonical.VirtualPath}) " +
                        $"and ignoring {suppressedCount} other database asset(s)."
                    );
                }
            }

            if (aliasGrouping.ExactChunkAliasesCollapsed > 0)
            {
                _log?.Invoke(
                    $"{group.ModName}: ignored {aliasGrouping.ExactChunkAliasesCollapsed} duplicate localization alias(es) for identical Zen chunks."
                );
            }

            if (aliasGrouping.PackagePrefixDuplicatesCollapsed > 0)
            {
                _log?.Invoke(
                    $"{group.ModName}: ignored {aliasGrouping.PackagePrefixDuplicatesCollapsed} duplicate localization database asset(s) " +
                    "sharing the same Zen package ID prefix (first 16 hex characters); OverrideContent/base-content alias preferred."
                );
            }

            group.LocresAssets.Clear(); // LOCRES is GAME-only.

            group.ScanError = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;

            if (group.HasLocalization)
            {
                // Fingerprint only the selected localization source family. Ignored source
                // families do not participate in extraction/build and therefore do not need
                // to invalidate the Editable workspace while the preferred family still exists.
                var relevantFiles = aliases
                    .Select(x => x.SourceUtoc)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var fingerprintLines = new List<string>();
                foreach (var path in relevantFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var hash = await HashUtil.Sha256FileAsync(path, cancellationToken);
                    fingerprintLines.Add(Path.GetRelativePath(_modsRoot, path).Replace('\\', '/') + "|" + hash);
                }
                group.SourceFingerprint = HashUtil.Sha256Text(string.Join("\n", fingerprintLines));
            }

            var manifestPath = Path.Combine(_cachedRoot, group.ModId, "manifest.json");
            group.NeedsExtraction = group.HasLocalization && !ManifestMatches(manifestPath, group.SourceFingerprint);
            group.EditableTranslationFile = _buildLanguages
                .Select(language => EditableScanner.FindTranslationFile(_editableRoot, group, language))
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            group.UiStatus = ResolveStatus(group, _buildLanguages.Count);
        }

        return groups
            .OrderBy(x => x.ModName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void SelectPreferredSourceFamily(ModScanResult group)
    {
        var allSources = group.Containers
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allSources.Count == 0)
            return;

        var families = allSources
            .GroupBy(PathUtil.GetModLocalizationSourceFamilyKey, StringComparer.OrdinalIgnoreCase)
            .Select(family => new
            {
                Key = family.Key,
                Sources = family.ToList(),
                Priority = family.Min(PathUtil.GetModLocalizationContainerPriority),
            })
            .OrderBy(family => family.Priority)
            .ThenBy(family => family.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = families[0];
        var selectedSet = selected.Sources.ToHashSet(StringComparer.OrdinalIgnoreCase);
        group.Containers = group.Containers.Where(selectedSet.Contains).ToList();

        if (families.Count > 1)
        {
            var representative = selected.Sources[0];
            _log?.Invoke(
                $"{group.ModName}: localization source selection -> {Path.GetFileNameWithoutExtension(representative)} " +
                $"[{PathUtil.GetModLocalizationContainerKind(representative)}]; ignored {families.Count - 1} other eligible source family/families."
            );
        }
    }

    private List<ModScanResult> BuildGroups(List<string> utocs)
    {
        var map = new Dictionary<string, ModScanResult>(StringComparer.OrdinalIgnoreCase);
        var assignedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in utocs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(_modsRoot, path);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string modName;
            string sourceRoot;
            string groupKey;

            if (parts.Length > 1)
            {
                modName = parts[0];
                sourceRoot = Path.Combine(_modsRoot, parts[0]);
                groupKey = "folder|" + parts[0];
            }
            else
            {
                modName = PathUtil.InferModName(_modsRoot, path);
                sourceRoot = _modsRoot;
                groupKey = "direct|" + modName;
            }

            if (!map.TryGetValue(groupKey, out var group))
            {
                var baseId = PathUtil.MakeModId(modName);
                var modId = baseId;
                var collision = 2;
                while (!assignedIds.Add(modId))
                    modId = $"{baseId}_{collision++}";

                group = new ModScanResult
                {
                    ModName = modName,
                    ModSourceRoot = sourceRoot,
                    ModId = modId,
                };
                map[groupKey] = group;
            }

            group.Containers.Add(path);
        }

        return map.Values.ToList();
    }

    private static bool ManifestMatches(string manifestPath, string fingerprint)
    {
        if (!File.Exists(manifestPath) || string.IsNullOrWhiteSpace(fingerprint))
            return false;
        try
        {
            var manifest = JsonUtil.Load<ExtractedManifest>(manifestPath);
            return manifest.SchemaVersion == AppConstants.ManifestSchemaVersion
                   && string.Equals(manifest.SourceFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static ModUiStatus ResolveStatus(ModScanResult mod, int selectedLanguageCount)
    {
        if (!string.IsNullOrWhiteSpace(mod.ScanError))
            return ModUiStatus.Error;
        if (!mod.HasLocalization)
            return ModUiStatus.NoLocalization;
        if (mod.NeedsExtraction)
            return ModUiStatus.NeedsExtraction;
        if (selectedLanguageCount == 0)
            return ModUiStatus.NoLanguageSelected;
        if (string.IsNullOrWhiteSpace(mod.EditableTranslationFile))
            return ModUiStatus.MissingTranslation;
        return ModUiStatus.Available;
    }
}

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

        progress?.Report((0, 100, "Starting MODS scan..."));

        var materializationRoot = Path.Combine(_cachedRoot, ".source_cache");
        var discoveryProgress = new Progress<(int Current, int Total, string Message)>(p =>
        {
            var fraction = p.Total <= 0 ? 0.0 : p.Current / (double)p.Total;
            var overall = Math.Clamp((int)Math.Round(fraction * 25.0), 0, 25);
            progress?.Report((overall, 100, p.Message));
        });

        var discoveredSources = await ModSourceDiscovery.DiscoverAsync(
            _modsRoot,
            materializationRoot,
            _log,
            discoveryProgress,
            cancellationToken
        );

        progress?.Report((25, 100, "Mod sources discovered. Scanning localization containers..."));

        var groups = BuildGroups(discoveredSources);

        _log?.Invoke(
            $"MODS source discovery: {groups.Count} source group(s), " +
            $"{groups.Sum(group => group.Containers.Count)} complete IoStore triplet(s). " +
            "Loose/extracted trees and ZIP/7z/RAR archives are supported; " +
            "container filename suffixes are no longer used as a localization whitelist."
        );

        var totalSources = groups.Sum(group => group.Containers.Count);
        var processed = 0;

        var completedGroups = 0;

        foreach (var group in groups)
        {
            var aliases = new List<LocalizationAlias>();
            var errors = new List<string>();

            foreach (var utoc in group.Containers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                var containerLabel = group.ContainerLabelsByPath.TryGetValue(utoc, out var label)
                    ? label
                    : Path.GetFileNameWithoutExtension(utoc);

                var scanFraction = totalSources <= 0 ? 1.0 : processed / (double)totalSources;
                var scanOverall = 25 + Math.Clamp(
                    (int)Math.Round(scanFraction * 55.0),
                    0,
                    55
                );
                progress?.Report((
                    scanOverall,
                    100,
                    $"Scanning {containerLabel}"
                ));

                try
                {
                    aliases.AddRange(
                        await _retoc.ListLocalizationAssetsAsync(
                            utoc,
                            _modsRoot,
                            cancellationToken
                        )
                    );
                }
                catch (Exception ex)
                {
                    errors.Add($"{containerLabel}: {ex.Message}");
                    _log?.Invoke(
                        $"IoStore scan failed for {group.SourceLabel} :: {containerLabel}: {ex.Message}"
                    );
                }
            }

            var aliasGrouping = LocalizationAliasGrouper.Group(aliases);
            group.Assets = aliasGrouping.Assets;

            if (aliasGrouping.ExactChunkAliasesCollapsed > 0)
            {
                _log?.Invoke(
                    $"{group.ModName}: ignored {aliasGrouping.ExactChunkAliasesCollapsed} " +
                    "duplicate localization alias(es) for identical full Zen chunks."
                );
            }

            group.LocresAssets.Clear(); // LOCRES is GAME-only.
            group.ScanError = errors.Count > 0
                ? string.Join(Environment.NewLine, errors)
                : null;

            if (group.HasLocalization)
            {
                // Fingerprint the physical source, not the materialized archive cache.
                // For loose sources this covers each complete pak/utoc/ucas trio;
                // for archive sources this hashes the original ZIP/7z/RAR itself.
                var fingerprintLines = new List<string>();

                foreach (var path in group.OriginalSourceFiles
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var hash = await HashUtil.Sha256FileAsync(path, cancellationToken);
                    var relative = Path.GetRelativePath(_modsRoot, path).Replace('\\', '/');
                    fingerprintLines.Add(relative + "|" + hash);
                }

                group.SourceFingerprint = HashUtil.Sha256Text(
                    string.Join("\n", fingerprintLines)
                );
            }

            var manifestPath = Path.Combine(_cachedRoot, group.ModId, "manifest.json");
            group.NeedsExtraction = group.HasLocalization
                                    && !ManifestMatches(manifestPath, group.SourceFingerprint);
            group.EditableTranslationFile = _buildLanguages
                .Select(language => EditableScanner.FindTranslationFile(_editableRoot, group, language))
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            group.UiStatus = ResolveStatus(group, _buildLanguages.Count);

            completedGroups++;
            var finalizeFraction = groups.Count == 0
                ? 1.0
                : completedGroups / (double)groups.Count;
            var finalizeOverall = 80 + Math.Clamp(
                (int)Math.Round(finalizeFraction * 20.0),
                0,
                20
            );
            progress?.Report((
                finalizeOverall,
                100,
                $"Finalizing {group.ModName}"
            ));
        }

        progress?.Report((100, 100, "MODS scan complete"));

        return groups
            .OrderBy(x => x.ModName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private List<ModScanResult> BuildGroups(
        IReadOnlyList<ModSourceDiscovery.SourceGroup> sources)
    {
        var results = new List<ModScanResult>();
        var assignedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources
                     .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase))
        {
            var baseId = PathUtil.MakeModId(source.Name);
            var modId = baseId;
            var collision = 2;

            while (!assignedIds.Add(modId))
                modId = $"{baseId}_{collision++}";

            var sourceRoot = source.Kind == "archive"
                ? Path.GetDirectoryName(Path.Combine(_modsRoot, source.Label)) ?? _modsRoot
                : source.Label == "<mods-root>"
                    ? _modsRoot
                    : Path.Combine(_modsRoot, source.Label);

            results.Add(new ModScanResult
            {
                ModName = source.Name,
                ModSourceRoot = sourceRoot,
                ModId = modId,
                SourceKind = source.Kind,
                SourceLabel = source.Label,
                Containers = source.Containers.ToList(),
                ContainerLabels = source.ContainerLabelsByPath.Values
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ContainerLabelsByPath = new Dictionary<string, string>(
                    source.ContainerLabelsByPath,
                    StringComparer.OrdinalIgnoreCase
                ),
                OriginalSourceFiles = source.OriginalSourceFiles.ToList(),
            });
        }

        return results;
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

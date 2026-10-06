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
        var scanCacheRoot = Path.Combine(_cachedRoot, ".scan_cache");
        var discoveryProgress = new InlineProgress<(int Current, int Total, string Message)>(p =>
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
            var fingerprintLines = new List<string>();

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

                try
                {
                    // Match the launch.py cache contract: UTOC is the authoritative
                    // change detector for one IoStore container. It is tiny compared
                    // with PAK/UCAS, so refreshes stay fast even for large mods.
                    progress?.Report((
                        scanOverall,
                        100,
                        $"Checking {containerLabel}"
                    ));

                    var utocHash = await HashUtil.Sha256FileAsync(utoc, cancellationToken);
                    fingerprintLines.Add(containerLabel + "|" + utocHash);

                    var cachedAliases = TryLoadContainerScanCache(
                        scanCacheRoot,
                        group,
                        utoc,
                        containerLabel,
                        utocHash
                    );

                    if (cachedAliases is not null)
                    {
                        aliases.AddRange(cachedAliases);
                        _log?.Invoke($"Scan cache reused: {group.ModName} :: {containerLabel}");
                    }
                    else
                    {
                        progress?.Report((
                            scanOverall,
                            100,
                            $"Scanning {containerLabel}"
                        ));

                        var scannedAliases = await _retoc.ListLocalizationAssetsAsync(
                            utoc,
                            _modsRoot,
                            cancellationToken
                        );
                        aliases.AddRange(scannedAliases);

                        SaveContainerScanCache(
                            scanCacheRoot,
                            group,
                            utoc,
                            containerLabel,
                            utocHash,
                            scannedAliases
                        );
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{containerLabel}: {ex.Message}");
                    _log?.Invoke(
                        $"IoStore scan failed for {group.SourceLabel} :: {containerLabel}: {ex.Message}"
                    );
                }
            }

            // Source identity is derived from the ordered UTOC hashes, just like the
            // launch pipeline. Do not hash large PAK/UCAS payloads on every refresh.
            group.SourceFingerprint = fingerprintLines.Count == 0
                ? string.Empty
                : HashUtil.Sha256Text(
                    string.Join(
                        "\n",
                        fingerprintLines.OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
                    )
                );

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

            NormalizeLegacyEditableFolders(group);

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

    private void NormalizeLegacyEditableFolders(ModScanResult mod)
    {
        if (string.IsNullOrWhiteSpace(_editableRoot)
            || !Directory.Exists(_editableRoot)
            || !string.Equals(
                mod.SourceKind,
                "archive",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var archiveDisplay = Path.GetFileNameWithoutExtension(
            mod.SourceLabel
        );
        if (string.IsNullOrWhiteSpace(archiveDisplay))
            return;

        var canonicalRoot = Path.Combine(
            _editableRoot,
            mod.ModId
        );

        var legacyRoots = Directory
            .EnumerateDirectories(
                _editableRoot,
                "*",
                SearchOption.TopDirectoryOnly
            )
            .Where(directory =>
            {
                var name = Path.GetFileName(directory);
                return !string.Equals(
                           directory,
                           canonicalRoot,
                           StringComparison.OrdinalIgnoreCase)
                       && (
                           string.Equals(
                               name,
                               archiveDisplay,
                               StringComparison.OrdinalIgnoreCase)
                           || name.StartsWith(
                               archiveDisplay + " — ",
                               StringComparison.OrdinalIgnoreCase)
                       );
            })
            .OrderBy(Directory.GetLastWriteTimeUtc)
            .ToList();

        if (legacyRoots.Count == 0)
            return;

        Directory.CreateDirectory(canonicalRoot);

        foreach (var legacyRoot in legacyRoots)
        {
            MergeEditableDirectory(
                legacyRoot,
                canonicalRoot
            );

            Directory.Delete(
                legacyRoot,
                recursive: true
            );

            _log?.Invoke(
                $"Merged legacy Editable folder into "
                + $"{mod.ModId}: {Path.GetFileName(legacyRoot)}"
            );
        }
    }

    private static void MergeEditableDirectory(
        string sourceRoot,
        string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(
                sourceRoot,
                directory
            );
            Directory.CreateDirectory(
                Path.Combine(destinationRoot, relative)
            );
        }

        foreach (var sourceFile in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(
                sourceRoot,
                sourceFile
            );
            var destinationFile = Path.Combine(
                destinationRoot,
                relative
            );

            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationFile)!
            );

            if (string.Equals(
                    Path.GetExtension(sourceFile),
                    ".json",
                    StringComparison.OrdinalIgnoreCase)
                && File.Exists(destinationFile)
                && string.Equals(
                    Path.GetDirectoryName(sourceFile),
                    sourceRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                MergeEditableJson(
                    sourceFile,
                    destinationFile
                );
                continue;
            }

            File.Copy(
                sourceFile,
                destinationFile,
                overwrite: true
            );
        }
    }

    private static void MergeEditableJson(
        string sourceFile,
        string destinationFile)
    {
        try
        {
            var destination = EditableScanner.LoadFlatTranslations(
                destinationFile
            );
            var source = EditableScanner.LoadFlatTranslations(
                sourceFile
            );

            foreach (var pair in source)
                destination[pair.Key] = pair.Value;

            JsonUtil.Save(
                destinationFile,
                destination
            );
        }
        catch
        {
            // Non-translation JSON (for example manifest.json) is metadata,
            // not user-authored localization. Prefer the newer source copy.
            File.Copy(
                sourceFile,
                destinationFile,
                overwrite: true
            );
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value) => _handler(value);
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

    private sealed class ContainerScanCache
    {
        public int Version { get; set; } = 1;
        public string SourceKind { get; set; } = string.Empty;
        public string SourceLabel { get; set; } = string.Empty;
        public string ContainerLabel { get; set; } = string.Empty;
        public string UtocSha256 { get; set; } = string.Empty;
        public List<LocalizationAlias> Aliases { get; set; } = new();
    }

    private static List<LocalizationAlias>? TryLoadContainerScanCache(
        string cacheRoot,
        ModScanResult group,
        string utoc,
        string containerLabel,
        string utocHash)
    {
        var cachePath = ContainerScanCachePath(
            cacheRoot,
            group,
            containerLabel
        );

        if (!File.Exists(cachePath))
            return null;

        try
        {
            var cache = JsonUtil.Load<ContainerScanCache>(cachePath);
            if (cache.Version != 1
                || !string.Equals(cache.SourceKind, group.SourceKind, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cache.SourceLabel, group.SourceLabel, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cache.ContainerLabel, containerLabel, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cache.UtocSha256, utocHash, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            foreach (var alias in cache.Aliases)
            {
                alias.SourceUtoc = utoc;
                alias.SourceUtocRelative = Path.GetRelativePath(group.ModSourceRoot, utoc);
            }

            return cache.Aliases;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveContainerScanCache(
        string cacheRoot,
        ModScanResult group,
        string utoc,
        string containerLabel,
        string utocHash,
        IReadOnlyList<LocalizationAlias> aliases)
    {
        try
        {
            var cachePath = ContainerScanCachePath(
                cacheRoot,
                group,
                containerLabel
            );

            var copy = aliases.Select(alias => new LocalizationAlias
            {
                ZenChunkId = alias.ZenChunkId,
                VirtualPath = alias.VirtualPath,
                SourceUtoc = utoc,
                SourceUtocRelative = alias.SourceUtocRelative,
            }).ToList();

            JsonUtil.Save(
                cachePath,
                new ContainerScanCache
                {
                    Version = 1,
                    SourceKind = group.SourceKind,
                    SourceLabel = group.SourceLabel,
                    ContainerLabel = containerLabel,
                    UtocSha256 = utocHash,
                    Aliases = copy,
                }
            );
        }
        catch
        {
            // Scan cache is purely an optimization. Never fail a scan because
            // the cache directory is unavailable or a cache write is interrupted.
        }
    }

    private static string ContainerScanCachePath(
        string cacheRoot,
        ModScanResult group,
        string containerLabel)
    {
        var identity = string.Join(
            "|",
            group.SourceKind,
            group.SourceLabel,
            containerLabel
        );

        return Path.Combine(
            cacheRoot,
            HashUtil.Sha256Text(identity) + ".json"
        );
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

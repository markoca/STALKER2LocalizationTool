using LocalizationWorkbench.Models;
using LocalizationWorkbench.Services;

namespace LocalizationWorkbench.Core;

public sealed class ModScanner
{
    private const int MaxParallelContainerScans = 2;
    private readonly RetocService _retoc;
    private readonly string _modsRoot;
    private readonly string _sourceRoot;
    private readonly string _translationsRoot;
    private readonly IReadOnlyList<BuildLanguage> _buildLanguages;
    private readonly Action<string>? _log;

    public ModScanner(
        RetocService retoc,
        string modsRoot,
        string sourceRoot,
        string translationsRoot,
        IEnumerable<int> buildLanguageIds,
        Action<string>? log = null)
    {
        _retoc = retoc;
        _modsRoot = modsRoot;
        _sourceRoot = sourceRoot;
        _translationsRoot = translationsRoot;
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
        var scanTimer = Stopwatch.StartNew();

        var materializationRoot = Path.Combine(_sourceRoot, ".source_cache");
        var scanCacheRoot = Path.Combine(_sourceRoot, ".scan_cache");
        var discoveryProgress = new InlineProgress<(int Current, int Total, string Message)>(p =>
        {
            var fraction = p.Total <= 0 ? 0.0 : p.Current / (double)p.Total;
            var overall = Math.Clamp((int)Math.Round(fraction * 25.0), 0, 25);
            progress?.Report((overall, 100, p.Message));
        });

        var discoveryTimer = Stopwatch.StartNew();
        var discoveredSources = await ModSourceDiscovery.DiscoverAsync(
            _modsRoot,
            materializationRoot,
            _log,
            discoveryProgress,
            cancellationToken
        );
        discoveryTimer.Stop();
        _log?.Invoke(
            $"MODS source discovery completed in "
            + $"{discoveryTimer.Elapsed.TotalSeconds:N1}s."
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
        var startedSources = 0;

        var jobs = groups
            .SelectMany(group => group.Containers.Select(
                (utoc, order) => new ContainerScanJob
                {
                    Group = group,
                    Utoc = utoc,
                    Order = order,
                }))
            .ToList();

        _log?.Invoke(
            $"MODS container scan parallelism: max={MaxParallelContainerScans}."
        );

        using var scanGate = new SemaphoreSlim(MaxParallelContainerScans);
        var archiveFallbackGates = groups
            .Where(group => string.Equals(
                group.SourceKind,
                "archive",
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                group => group,
                _ => new SemaphoreSlim(1, 1)
            );

        var scanTasks = jobs.Select(async job =>
        {
            await scanGate.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var group = job.Group;
                var utoc = job.Utoc;
                var containerLabel = group.ContainerLabelsByPath.TryGetValue(
                    utoc,
                    out var label)
                    ? label
                    : Path.GetFileNameWithoutExtension(utoc);

                var started = Interlocked.Increment(ref startedSources);
                var scanFraction = totalSources <= 0
                    ? 1.0
                    : started / (double)totalSources;
                var scanOverall = 25 + Math.Clamp(
                    (int)Math.Round(scanFraction * 55.0),
                    0,
                    55
                );

                progress?.Report((
                    scanOverall,
                    100,
                    $"Checking {containerLabel}"
                ));

                try
                {
                    // UTOC remains the authoritative change detector. Hashing only
                    // this small index avoids touching large PAK/UCAS payloads.
                    var hashTimer = Stopwatch.StartNew();
                    var utocHash = await HashUtil.Sha256FileAsync(
                        utoc,
                        cancellationToken
                    );
                    hashTimer.Stop();

                    var fingerprintLine = containerLabel + "|" + utocHash;
                    var cachedAliases = TryLoadContainerScanCache(
                        scanCacheRoot,
                        group,
                        utoc,
                        containerLabel,
                        utocHash
                    );

                    if (cachedAliases is not null)
                    {
                        _log?.Invoke(
                            $"Scan timing: {group.ModName} :: {containerLabel} | "
                            + $"hash={hashTimer.Elapsed.TotalSeconds:N1}s | cache=hit"
                        );

                        return new ContainerScanOutcome
                        {
                            Group = group,
                            Order = job.Order,
                            FingerprintLine = fingerprintLine,
                            Aliases = cachedAliases,
                            CacheHit = true,
                        };
                    }

                    progress?.Report((
                        scanOverall,
                        100,
                        $"Scanning {containerLabel}"
                    ));

                    var usedSparsePlaceholder =
                        string.Equals(
                            group.SourceKind,
                            "archive",
                            StringComparison.OrdinalIgnoreCase)
                        && ModSourceDiscovery
                            .HasArchiveScanUcasPlaceholder(utoc);

                    var sparseFallbackUsed = false;
                    var retocTimer = Stopwatch.StartNew();
                    List<LocalizationAlias> scannedAliases;

                    try
                    {
                        scannedAliases = await _retoc
                            .ListLocalizationAssetsAsync(
                                utoc,
                                _modsRoot,
                                cancellationToken
                            );
                        retocTimer.Stop();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception firstError)
                        when (usedSparsePlaceholder)
                    {
                        retocTimer.Stop();
                        _log?.Invoke(
                            $"Sparse UCAS fast path rejected for "
                            + $"{group.ModName} :: {containerLabel}; "
                            + $"retrying with real UCAS. "
                            + firstError.Message
                        );

                        var fallbackGate = archiveFallbackGates[group];
                        await fallbackGate.WaitAsync(cancellationToken);
                        try
                        {
                            await ModSourceDiscovery
                                .MaterializeArchiveScanUcasFallbackAsync(
                                    _modsRoot,
                                    materializationRoot,
                                    group.SourceLabel,
                                    new[] { utoc },
                                    _log,
                                    cancellationToken
                                );
                        }
                        finally
                        {
                            fallbackGate.Release();
                        }

                        sparseFallbackUsed = true;
                        retocTimer.Restart();
                        scannedAliases = await _retoc
                            .ListLocalizationAssetsAsync(
                                utoc,
                                _modsRoot,
                                cancellationToken
                            );
                        retocTimer.Stop();
                    }

                    SaveContainerScanCache(
                        scanCacheRoot,
                        group,
                        utoc,
                        containerLabel,
                        utocHash,
                        scannedAliases
                    );

                    var sparseStatus = !usedSparsePlaceholder
                        ? string.Empty
                        : sparseFallbackUsed
                            ? " | sparse-ucas=fallback"
                            : " | sparse-ucas=accepted";

                    _log?.Invoke(
                        $"Scan timing: {group.ModName} :: {containerLabel} | "
                        + $"hash={hashTimer.Elapsed.TotalSeconds:N1}s | "
                        + $"retoc={retocTimer.Elapsed.TotalSeconds:N1}s"
                        + sparseStatus
                    );

                    return new ContainerScanOutcome
                    {
                        Group = group,
                        Order = job.Order,
                        FingerprintLine = fingerprintLine,
                        Aliases = scannedAliases,
                        RetocScanned = true,
                        SparseAccepted =
                            usedSparsePlaceholder
                            && !sparseFallbackUsed,
                        SparseFallback = sparseFallbackUsed,
                    };
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log?.Invoke(
                        $"IoStore scan failed for {group.SourceLabel} :: "
                        + $"{containerLabel}: {ex.Message}"
                    );

                    return new ContainerScanOutcome
                    {
                        Group = group,
                        Order = job.Order,
                        Error = $"{containerLabel}: {ex.Message}",
                    };
                }
            }
            finally
            {
                scanGate.Release();
            }
        }).ToList();

        var scanOutcomes = await Task.WhenAll(scanTasks);
        var scanCacheHits = scanOutcomes.Count(outcome => outcome.CacheHit);
        var retocScans = scanOutcomes.Count(outcome => outcome.RetocScanned);
        var sparseAccepted = scanOutcomes.Count(
            outcome => outcome.SparseAccepted
        );
        var sparseFallbacks = scanOutcomes.Count(
            outcome => outcome.SparseFallback
        );

        var completedGroups = 0;

        foreach (var group in groups)
        {
            var groupOutcomes = scanOutcomes
                .Where(outcome => ReferenceEquals(outcome.Group, group))
                .OrderBy(outcome => outcome.Order)
                .ToList();

            var aliases = groupOutcomes
                .SelectMany(outcome => outcome.Aliases)
                .ToList();
            var errors = groupOutcomes
                .Where(outcome => !string.IsNullOrWhiteSpace(outcome.Error))
                .Select(outcome => outcome.Error!)
                .ToList();
            var fingerprintLines = groupOutcomes
                .Where(outcome => !string.IsNullOrWhiteSpace(
                    outcome.FingerprintLine))
                .Select(outcome => outcome.FingerprintLine!)
                .ToList();

            // Source identity is derived from the ordered UTOC hashes, just like the
            // launch pipeline. Do not hash large PAK/UCAS payloads on every refresh.
            group.SourceFingerprint = fingerprintLines.Count == 0
                ? string.Empty
                : HashUtil.Sha256Text(
                    string.Join(
                        "\n",
                        fingerprintLines.OrderBy(
                            line => line,
                            StringComparer.OrdinalIgnoreCase
                        )
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

            var manifestPath = Path.Combine(
                _sourceRoot,
                group.ModId,
                "manifest.json"
            );
            group.NeedsExtraction = group.HasLocalization
                                    && !ManifestMatches(
                                        manifestPath,
                                        group.SourceFingerprint
                                    );
            group.TranslationFile = _buildLanguages
                .Select(language => TranslationScanner.FindTranslationFile(
                    _translationsRoot,
                    group,
                    language
                ))
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            group.UiStatus = ResolveStatus(
                group,
                _buildLanguages.Count
            );

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
        scanTimer.Stop();
        _log?.Invoke(
            $"MODS scan completed in {scanTimer.Elapsed.TotalSeconds:N1}s; "
            + $"containers={totalSources}, cache-reused={scanCacheHits}, "
            + $"retoc-scanned={retocScans}, "
            + $"sparse-accepted={sparseAccepted}, "
            + $"sparse-fallback={sparseFallbacks}."
        );

        return groups
            .OrderBy(x => x.ModName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private sealed class ContainerScanJob
    {
        public ModScanResult Group { get; init; } = new();
        public string Utoc { get; init; } = string.Empty;
        public int Order { get; init; }
    }

    private sealed class ContainerScanOutcome
    {
        public ModScanResult Group { get; init; } = new();
        public int Order { get; init; }
        public string? FingerprintLine { get; init; }
        public List<LocalizationAlias> Aliases { get; init; } = new();
        public string? Error { get; init; }
        public bool CacheHit { get; init; }
        public bool RetocScanned { get; init; }
        public bool SparseAccepted { get; init; }
        public bool SparseFallback { get; init; }
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
        if (string.IsNullOrWhiteSpace(mod.TranslationFile))
            return ModUiStatus.MissingTranslation;
        return ModUiStatus.Extracted;
    }
}

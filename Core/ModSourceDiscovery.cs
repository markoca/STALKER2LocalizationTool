using SharpCompress.Archives;

namespace LocalizationWorkbench.Core;

/// <summary>
/// Discovers physical mod sources the same way the release launch pipeline does:
/// complete adjacent .pak/.utoc/.ucas triplets can come from loose/extracted trees
/// or directly from ZIP/7z/RAR archives. Archive entries are materialized into a
/// deterministic cache outside the user's Mods folder before retoc sees them.
/// </summary>
public static class ModSourceDiscovery
{
    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar"];
    private static readonly HashSet<string> IgnoredSourceDirectories = new(
        new[] { ".archive_cache", ".source_cache", ".scan_cache" },
        StringComparer.OrdinalIgnoreCase
    );

    public sealed class SourceGroup
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public List<string> Containers { get; init; } = new();
        public Dictionary<string, string> ContainerLabelsByPath { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<string> OriginalSourceFiles { get; init; } = new();
    }

    private sealed class Triplet
    {
        public string Stem { get; init; } = string.Empty;
        public string Family { get; init; } = string.Empty;
        public string UtocPath { get; init; } = string.Empty;
        public string PakPath { get; init; } = string.Empty;
        public string UcasPath { get; init; } = string.Empty;
        public string SourceLabel { get; init; } = string.Empty;
    }

    private sealed class ArchiveTriplet
    {
        public string Stem { get; init; } = string.Empty;
        public string Family { get; init; } = string.Empty;
        public string Parent { get; init; } = string.Empty;
        public string PakMember { get; init; } = string.Empty;
        public string UtocMember { get; init; } = string.Empty;
        public string UcasMember { get; init; } = string.Empty;
        public long PakSize { get; init; }
        public long UtocSize { get; init; }
        public long UcasSize { get; init; }
    }

    private sealed class ArchiveDiscoveryCache
    {
        public int Version { get; set; } = 2;
        public long ArchiveLength { get; set; }
        public long ArchiveLastWriteUtcTicks { get; set; }
        public string ArchiveIdentity { get; set; } = string.Empty;
        public List<ArchiveTriplet> Triplets { get; set; } = new();
    }

    public static bool HasPotentialModSources(string modsRoot)
    {
        if (string.IsNullOrWhiteSpace(modsRoot) || !Directory.Exists(modsRoot))
            return false;

        try
        {
            foreach (var file in Directory.EnumerateFiles(
                         modsRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                if (IsInsideIgnoredSourceDirectory(modsRoot, file))
                    continue;

                var extension = Path.GetExtension(file);
                if (ArchiveExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    return true;

                if (!extension.Equals(".utoc", StringComparison.OrdinalIgnoreCase))
                    continue;

                var pak = Path.ChangeExtension(file, ".pak");
                var ucas = Path.ChangeExtension(file, ".ucas");
                if (File.Exists(pak) && File.Exists(ucas))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static Task<List<SourceGroup>> DiscoverAsync(
        string modsRoot,
        string materializationRoot,
        Action<string>? log = null,
        IProgress<(int Current, int Total, string Message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Discover(modsRoot, materializationRoot, log, progress, cancellationToken),
            cancellationToken
        );
    }

    private static List<SourceGroup> Discover(
        string modsRoot,
        string materializationRoot,
        Action<string>? log,
        IProgress<(int Current, int Total, string Message)>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(modsRoot))
            return new List<SourceGroup>();

        Directory.CreateDirectory(materializationRoot);

        var looseUtocs = new List<string>();
        var archivePaths = new List<string>();

        foreach (var path in Directory.EnumerateFiles(
                     modsRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsInsideIgnoredSourceDirectory(modsRoot, path))
                continue;

            var extension = Path.GetExtension(path);
            if (extension.Equals(".utoc", StringComparison.OrdinalIgnoreCase))
                looseUtocs.Add(path);
            else if (ArchiveExtensions.Contains(
                         extension,
                         StringComparer.OrdinalIgnoreCase))
                archivePaths.Add(path);
        }

        looseUtocs.Sort(StringComparer.OrdinalIgnoreCase);
        archivePaths.Sort(StringComparer.OrdinalIgnoreCase);

        var discoveryTotal = Math.Max(1, looseUtocs.Count + archivePaths.Count);
        var discoveryCurrent = 0;
        progress?.Report((0, discoveryTotal, "Discovering mod sources..."));

        var groups = new List<SourceGroup>();
        groups.AddRange(
            DiscoverLoose(
                modsRoot,
                looseUtocs,
                () =>
                {
                    discoveryCurrent++;
                    progress?.Report((
                        discoveryCurrent,
                        discoveryTotal,
                        "Discovering loose mod containers..."
                    ));
                },
                cancellationToken
            )
        );

        foreach (var archivePath in archivePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                groups.AddRange(
                    DiscoverArchive(
                        modsRoot,
                        archivePath,
                        materializationRoot,
                        log,
                        cancellationToken
                    )
                );
            }
            catch (Exception ex)
            {
                log?.Invoke(
                    $"Archive source ignored: {Path.GetRelativePath(modsRoot, archivePath)}: {ex.Message}"
                );
            }
            finally
            {
                discoveryCurrent++;
                progress?.Report((
                    discoveryCurrent,
                    discoveryTotal,
                    $"Discovering archive: {Path.GetFileName(archivePath)}"
                ));
            }
        }

        // First enforce unique physical source identity.
        var uniqueGroups = groups
            .GroupBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                if (group.Count() != 1)
                    throw new InvalidDataException(
                        $"Duplicate mod source identity detected: {group.Key}"
                    );
                return group.Single();
            })
            .ToList();

        // If the user has both an original Nexus archive and an extracted/loose
        // copy of the exact same IoStore containers, scan the loose copy only.
        // Keep the archive filename as the user-facing display name because it
        // usually carries the useful mod name + version information.
        var archiveBySignature = uniqueGroups
            .Where(group => string.Equals(group.Kind, "archive", StringComparison.OrdinalIgnoreCase))
            .GroupBy(ContainerSignature, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .First(),
                StringComparer.OrdinalIgnoreCase
            );

        var reconciled = new List<SourceGroup>();
        foreach (var group in uniqueGroups)
        {
            if (string.Equals(group.Kind, "archive", StringComparison.OrdinalIgnoreCase))
            {
                var signature = ContainerSignature(group);
                var matchingLoose = uniqueGroups.Any(candidate =>
                    string.Equals(candidate.Kind, "loose", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        ContainerSignature(candidate),
                        signature,
                        StringComparison.OrdinalIgnoreCase
                    ));

                if (matchingLoose)
                    continue;
            }

            if (string.Equals(group.Kind, "loose", StringComparison.OrdinalIgnoreCase)
                && archiveBySignature.TryGetValue(ContainerSignature(group), out var archiveMetadata))
            {
                reconciled.Add(new SourceGroup
                {
                    Key = group.Key,
                    Name = archiveMetadata.Name,
                    Kind = group.Kind,
                    Label = group.Label,
                    Containers = group.Containers,
                    ContainerLabelsByPath = group.ContainerLabelsByPath,
                    OriginalSourceFiles = group.OriginalSourceFiles,
                });
                continue;
            }

            reconciled.Add(group);
        }

        return reconciled
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<SourceGroup> DiscoverLoose(
        string modsRoot,
        IReadOnlyList<string> utocPaths,
        Action onProcessed,
        CancellationToken cancellationToken)
    {
        var triplets = new List<Triplet>();

        foreach (var utocPath in utocPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pakPath = Path.ChangeExtension(utocPath, ".pak");
            var ucasPath = Path.ChangeExtension(utocPath, ".ucas");

            // launch.py indexes only complete IoStore trios.
            if (!File.Exists(pakPath) || !File.Exists(ucasPath))
            {
                onProcessed();
                continue;
            }

            var relative = Path.GetRelativePath(modsRoot, utocPath);
            var parts = relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries
            );
            var physicalSource = parts.Length > 1 ? parts[0] : "<mods-root>";
            var stem = Path.GetFileNameWithoutExtension(utocPath);

            triplets.Add(new Triplet
            {
                Stem = stem,
                Family = PathUtil.InferContainerFamilyName(stem),
                UtocPath = utocPath,
                PakPath = pakPath,
                UcasPath = ucasPath,
                SourceLabel = physicalSource,
            });
            onProcessed();
        }

        foreach (var sourceGroup in triplets.GroupBy(
                     item => item.SourceLabel,
                     StringComparer.OrdinalIgnoreCase))
        {
            var retained = sourceGroup
                .Where(item => !PathUtil.IsNewContentContainer(item.Stem))
                .OrderBy(item => item.UtocPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (retained.Count == 0)
                continue;

            if (sourceGroup.Key == "<mods-root>")
            {
                foreach (var familyGroup in retained.GroupBy(
                             item => item.Family,
                             StringComparer.OrdinalIgnoreCase))
                {
                    var family = familyGroup.ToList();
                    yield return new SourceGroup
                    {
                        Key = $"loose|<mods-root>|{familyGroup.Key}",
                        Name = familyGroup.Key,
                        Kind = "loose",
                        Label = "<mods-root>",
                        Containers = family.Select(item => item.UtocPath).ToList(),
                        ContainerLabelsByPath = family.ToDictionary(
                            item => item.UtocPath,
                            item => item.Stem,
                            StringComparer.OrdinalIgnoreCase
                        ),
                        OriginalSourceFiles = family
                            .SelectMany(item => new[]
                            {
                                item.PakPath,
                                item.UtocPath,
                                item.UcasPath,
                            })
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                    };
                }

                continue;
            }

            yield return new SourceGroup
            {
                Key = $"loose|{sourceGroup.Key}",
                Name = PathUtil.ArchiveModDisplayName(sourceGroup.Key),
                Kind = "loose",
                Label = sourceGroup.Key,
                Containers = retained.Select(item => item.UtocPath).ToList(),
                ContainerLabelsByPath = retained.ToDictionary(
                    item => item.UtocPath,
                    item => item.Stem,
                    StringComparer.OrdinalIgnoreCase
                ),
                OriginalSourceFiles = retained
                    .SelectMany(item => new[]
                    {
                        item.PakPath,
                        item.UtocPath,
                        item.UcasPath,
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };
        }
    }

    private static IEnumerable<SourceGroup> DiscoverArchive(
        string modsRoot,
        string archivePath,
        string materializationRoot,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var archiveRelative = Path.GetRelativePath(modsRoot, archivePath);
        var archiveDisplay = Path.GetFileNameWithoutExtension(archivePath);
        var archiveInfo = new FileInfo(archivePath);
        var archiveIdentity = ArchiveIdentity(
            archivePath,
            archiveInfo
        );
        var discoveryCachePath = ArchiveDiscoveryCachePath(
            materializationRoot,
            archivePath
        );

        var cached = TryLoadArchiveDiscoveryCache(
            discoveryCachePath,
            archiveInfo
        );

        if (cached is not null)
        {
            if (cached.Triplets.Count == 0)
            {
                log?.Invoke(
                    $"Archive skipped (no complete IoStore triplets): {archiveRelative}"
                );
                yield break;
            }

            if (CanReuseMaterializedArchive(
                    materializationRoot,
                    archiveDisplay,
                    cached))
            {
                log?.Invoke($"Archive discovery cache reused: {archiveRelative}");

                foreach (var group in BuildCachedArchiveGroups(
                             archivePath,
                             archiveRelative,
                             archiveDisplay,
                             materializationRoot,
                             cached))
                {
                    yield return group;
                }

                yield break;
            }
        }

        using var archive = ArchiveFactory.OpenArchive(archivePath);

        var entries = archive.Entries
            .Where(entry => !entry.IsDirectory && !string.IsNullOrWhiteSpace(entry.Key))
            .ToList();

        var byNormalizedName = entries
            .GroupBy(
                entry => NormalizeArchivePath(entry.Key!),
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    if (group.Count() != 1)
                    {
                        throw new InvalidDataException(
                            $"Archive contains duplicate normalized member path: {group.Key}"
                        );
                    }
                    return group.Single();
                },
                StringComparer.OrdinalIgnoreCase
            );

        var triplets = new List<ArchiveTriplet>();

        foreach (var utoc in byNormalizedName.Keys
                     .Where(name => name.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parent = GetArchiveParent(utoc);
            var stem = Path.GetFileNameWithoutExtension(utoc);
            var pak = CombineArchivePath(parent, stem + ".pak");
            var ucas = CombineArchivePath(parent, stem + ".ucas");

            if (!byNormalizedName.ContainsKey(pak) || !byNormalizedName.ContainsKey(ucas))
                continue;

            triplets.Add(new ArchiveTriplet
            {
                Stem = stem,
                Family = PathUtil.InferContainerFamilyName(stem),
                Parent = parent,
                PakMember = pak,
                UtocMember = utoc,
                UcasMember = ucas,
                PakSize = byNormalizedName[pak].Size,
                UtocSize = byNormalizedName[utoc].Size,
                UcasSize = byNormalizedName[ucas].Size,
            });
        }

        if (triplets.Count == 0)
        {
            SaveArchiveDiscoveryCache(
                discoveryCachePath,
                new ArchiveDiscoveryCache
                {
                    Version = 2,
                    ArchiveLength = archiveInfo.Length,
                    ArchiveLastWriteUtcTicks = archiveInfo.LastWriteTimeUtc.Ticks,
                    ArchiveIdentity = archiveIdentity,
                    Triplets = new List<ArchiveTriplet>(),
                }
            );

            log?.Invoke(
                $"Archive skipped (no complete IoStore triplets): {archiveRelative}"
            );
            yield break;
        }

        SaveArchiveDiscoveryCache(
            discoveryCachePath,
            new ArchiveDiscoveryCache
            {
                Version = 2,
                ArchiveLength = archiveInfo.Length,
                ArchiveLastWriteUtcTicks = archiveInfo.LastWriteTimeUtc.Ticks,
                ArchiveIdentity = archiveIdentity,
                Triplets = triplets,
            }
        );

        var retained = triplets
            .Where(item => !PathUtil.IsNewContentContainer(item.Stem))
            .OrderBy(item => item.UtocMember, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (retained.Count == 0)
            yield break;

        var containers = new List<string>();
        var labels = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var triplet in retained)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetDir = MaterializedTripletDirectory(
                materializationRoot,
                archiveDisplay,
                archiveIdentity,
                triplet
            );
            Directory.CreateDirectory(targetDir);

            var utocTarget = Path.Combine(targetDir, triplet.Stem + ".utoc");

            // SCAN only needs the IoStore directory index. Large PAK/UCAS payloads
            // are materialized lazily by EXTRACT if localization is actually present.
            ExtractIfNeeded(byNormalizedName[triplet.UtocMember], utocTarget);

            containers.Add(utocTarget);
            labels[utocTarget] = triplet.Stem;
        }

        var displayName = PathUtil.ArchiveModDisplayName(archiveDisplay);

        log?.Invoke(
            $"Archive mod source: {archiveRelative} -> "
            + $"{retained.Count} IoStore container(s) [{displayName}]"
        );

        yield return new SourceGroup
        {
            Key = $"archive|{Path.GetFullPath(archivePath)}",
            Name = displayName,
            Kind = "archive",
            Label = archiveRelative,
            Containers = containers,
            ContainerLabelsByPath = labels,
            OriginalSourceFiles = new List<string> { archivePath },
        };
    }

    private static ArchiveDiscoveryCache? TryLoadArchiveDiscoveryCache(
        string cachePath,
        FileInfo archiveInfo)
    {
        if (!File.Exists(cachePath))
            return null;

        try
        {
            var cache = JsonUtil.Load<ArchiveDiscoveryCache>(cachePath);
            if (cache.Version != 2
                || cache.ArchiveLength != archiveInfo.Length
                || cache.ArchiveLastWriteUtcTicks != archiveInfo.LastWriteTimeUtc.Ticks)
            {
                return null;
            }

            if (cache.Triplets.Count == 0)
                return cache;

            return !string.IsNullOrWhiteSpace(cache.ArchiveIdentity)
                ? cache
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveArchiveDiscoveryCache(
        string cachePath,
        ArchiveDiscoveryCache cache)
    {
        try
        {
            JsonUtil.Save(cachePath, cache);
        }
        catch
        {
            // Discovery cache is an optimization only.
        }
    }

    private static string ArchiveDiscoveryCachePath(
        string materializationRoot,
        string archivePath)
    {
        var identity = Path.GetFullPath(archivePath);
        return Path.Combine(
            materializationRoot,
            ".archive_index",
            HashUtil.Sha256Text(identity) + ".json"
        );
    }

    private static bool CanReuseMaterializedArchive(
        string materializationRoot,
        string archiveDisplay,
        ArchiveDiscoveryCache cache)
    {
        foreach (var triplet in cache.Triplets)
        {
            var targetDir = MaterializedTripletDirectory(
                materializationRoot,
                archiveDisplay,
                cache.ArchiveIdentity,
                triplet
            );

            if (!FileMatchesSize(
                    Path.Combine(targetDir, triplet.Stem + ".utoc"),
                    triplet.UtocSize))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<SourceGroup> BuildCachedArchiveGroups(
        string archivePath,
        string archiveRelative,
        string archiveDisplay,
        string materializationRoot,
        ArchiveDiscoveryCache cache)
    {
        var retained = cache.Triplets
            .Where(item => !PathUtil.IsNewContentContainer(item.Stem))
            .OrderBy(item => item.UtocMember, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (retained.Count == 0)
            yield break;

        var containers = new List<string>();
        var labels = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var triplet in retained)
        {
            var targetDir = MaterializedTripletDirectory(
                materializationRoot,
                archiveDisplay,
                cache.ArchiveIdentity,
                triplet
            );
            var utocTarget = Path.Combine(
                targetDir,
                triplet.Stem + ".utoc"
            );

            containers.Add(utocTarget);
            labels[utocTarget] = triplet.Stem;
        }

        yield return new SourceGroup
        {
            Key = $"archive|{Path.GetFullPath(archivePath)}",
            Name = PathUtil.ArchiveModDisplayName(archiveDisplay),
            Kind = "archive",
            Label = archiveRelative,
            Containers = containers,
            ContainerLabelsByPath = labels,
            OriginalSourceFiles = new List<string> { archivePath },
        };
    }

    private static string MaterializedTripletDirectory(
        string materializationRoot,
        string archiveDisplay,
        string archiveIdentity,
        ArchiveTriplet triplet)
    {
        var memberIdentity = HashUtil.Sha256Text(triplet.UtocMember)[..12];
        return Path.Combine(
            materializationRoot,
            PathUtil.MakeSafeName(archiveDisplay) + "_" + archiveIdentity[..12],
            PathUtil.MakeSafeName(triplet.Stem) + "_" + memberIdentity
        );
    }

    private static bool FileMatchesSize(string path, long expectedSize)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length == expectedSize;
        }
        catch
        {
            return false;
        }
    }

    private static void ExtractIfNeeded(IArchiveEntry entry, string targetPath)
    {
        var target = new FileInfo(targetPath);
        if (target.Exists && target.Length == entry.Size)
            return;

        Directory.CreateDirectory(target.DirectoryName!);
        using var input = entry.OpenEntryStream();
        using var output = new FileStream(
            targetPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.SequentialScan
        );
        input.CopyTo(output);
    }

    private static string ContainerSignature(SourceGroup group)
    {
        return string.Join(
            "\n",
            group.ContainerLabelsByPath.Values
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        );
    }

    private static bool IsInsideIgnoredSourceDirectory(string modsRoot, string path)
    {
        var relative = Path.GetRelativePath(modsRoot, path);
        var parts = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        );

        return parts.Any(part => IgnoredSourceDirectories.Contains(part));
    }

    private static bool IsSupportedArchive(string path) =>
        ArchiveExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase
        );

    private static string NormalizeArchivePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static string GetArchiveParent(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }

    private static string CombineArchivePath(string parent, string name) =>
        string.IsNullOrEmpty(parent) ? name : parent + "/" + name;

    private static string ArchiveIdentity(
        string archivePath,
        FileInfo archiveInfo)
    {
        return HashUtil.Sha256Text(
            string.Join(
                "|",
                Path.GetFullPath(archivePath),
                archiveInfo.Length,
                archiveInfo.LastWriteTimeUtc.Ticks
            )
        );
    }
}

using SharpCompress.Archives;

namespace STALKER2LocalizationTool.Core;

/// <summary>
/// Discovers physical mod sources the same way the release launch pipeline does:
/// complete adjacent .pak/.utoc/.ucas triplets can come from loose/extracted trees
/// or directly from ZIP/7z/RAR archives. Archive entries are materialized into a
/// deterministic cache outside the user's Mods folder before retoc sees them.
/// </summary>
public static class ModSourceDiscovery
{
    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar"];

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
    }

    public static Task<List<SourceGroup>> DiscoverAsync(
        string modsRoot,
        string materializationRoot,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Discover(modsRoot, materializationRoot, log, cancellationToken),
            cancellationToken
        );
    }

    private static List<SourceGroup> Discover(
        string modsRoot,
        string materializationRoot,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(modsRoot))
            return new List<SourceGroup>();

        Directory.CreateDirectory(materializationRoot);

        var groups = new List<SourceGroup>();
        groups.AddRange(DiscoverLoose(modsRoot, cancellationToken));

        foreach (var archivePath in Directory
                     .EnumerateFiles(modsRoot, "*", SearchOption.AllDirectories)
                     .Where(IsSupportedArchive)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
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
        }

        // A normal source should be unique by physical source + inferred container
        // family. Keep grouping deterministic even when two archives use the same name.
        return groups
            .GroupBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                if (group.Count() != 1)
                    throw new InvalidDataException(
                        $"Duplicate mod source identity detected: {group.Key}"
                    );
                return group.Single();
            })
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<SourceGroup> DiscoverLoose(
        string modsRoot,
        CancellationToken cancellationToken)
    {
        var triplets = new List<Triplet>();

        foreach (var utocPath in Directory
                     .EnumerateFiles(modsRoot, "*.utoc", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pakPath = Path.ChangeExtension(utocPath, ".pak");
            var ucasPath = Path.ChangeExtension(utocPath, ".ucas");

            // launch.py indexes only complete IoStore trios.
            if (!File.Exists(pakPath) || !File.Exists(ucasPath))
                continue;

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
        }

        foreach (var sourceGroup in triplets.GroupBy(
                     item => item.SourceLabel,
                     StringComparer.OrdinalIgnoreCase))
        {
            foreach (var familyGroup in sourceGroup.GroupBy(
                         item => item.Family,
                         StringComparer.OrdinalIgnoreCase))
            {
                var containers = familyGroup
                    .OrderBy(item => item.UtocPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var displayName = sourceGroup.Key == "<mods-root>"
                    ? familyGroup.Key
                    : sourceGroup.Count() == containers.Count
                        ? sourceGroup.Key
                        : $"{sourceGroup.Key} — {familyGroup.Key}";

                yield return new SourceGroup
                {
                    Key = $"loose|{sourceGroup.Key}|{familyGroup.Key}",
                    Name = displayName,
                    Kind = "loose",
                    Label = sourceGroup.Key,
                    Containers = containers.Select(item => item.UtocPath).ToList(),
                    ContainerLabelsByPath = containers.ToDictionary(
                        item => item.UtocPath,
                        item => item.Stem,
                        StringComparer.OrdinalIgnoreCase
                    ),
                    OriginalSourceFiles = containers
                        .SelectMany(item => new[] { item.PakPath, item.UtocPath, item.UcasPath })
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                };
            }
        }
    }

    private static IEnumerable<SourceGroup> DiscoverArchive(
        string modsRoot,
        string archivePath,
        string materializationRoot,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);

        var entries = archive.Entries
            .Where(entry => !entry.IsDirectory && !string.IsNullOrWhiteSpace(entry.Key))
            .ToList();

        var byNormalizedName = entries.ToDictionary(
            entry => NormalizeArchivePath(entry.Key!),
            entry => entry,
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
            });
        }

        if (triplets.Count == 0)
            yield break;

        var archiveRelative = Path.GetRelativePath(modsRoot, archivePath);
        var archiveDisplay = Path.GetFileNameWithoutExtension(archivePath);
        var archiveHash = Sha256File(archivePath);

        foreach (var familyGroup in triplets.GroupBy(
                     item => item.Family,
                     StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var family = familyGroup
                .OrderBy(item => item.UtocMember, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var displayName = triplets.Count == family.Count
                ? archiveDisplay
                : $"{archiveDisplay} — {familyGroup.Key}";

            var containers = new List<string>();
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var triplet in family)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var memberIdentity = HashUtil.Sha256Text(triplet.UtocMember)[..12];
                var targetDir = Path.Combine(
                    materializationRoot,
                    PathUtil.MakeSafeName(archiveDisplay) + "_" + archiveHash[..12],
                    PathUtil.MakeSafeName(triplet.Stem) + "_" + memberIdentity
                );
                Directory.CreateDirectory(targetDir);

                var pakTarget = Path.Combine(targetDir, triplet.Stem + ".pak");
                var utocTarget = Path.Combine(targetDir, triplet.Stem + ".utoc");
                var ucasTarget = Path.Combine(targetDir, triplet.Stem + ".ucas");

                ExtractIfNeeded(byNormalizedName[triplet.PakMember], pakTarget);
                ExtractIfNeeded(byNormalizedName[triplet.UtocMember], utocTarget);
                ExtractIfNeeded(byNormalizedName[triplet.UcasMember], ucasTarget);

                containers.Add(utocTarget);
                labels[utocTarget] = triplet.Stem;
            }

            log?.Invoke(
                $"Archive mod source: {archiveRelative} -> {family.Count} complete IoStore container(s) [{familyGroup.Key}]"
            );

            yield return new SourceGroup
            {
                Key = $"archive|{Path.GetFullPath(archivePath)}|{familyGroup.Key}",
                Name = displayName,
                Kind = "archive",
                Label = archiveRelative,
                Containers = containers,
                ContainerLabelsByPath = labels,
                OriginalSourceFiles = new List<string> { archivePath },
            };
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

    private static string Sha256File(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            1024 * 1024,
            FileOptions.SequentialScan
        );
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

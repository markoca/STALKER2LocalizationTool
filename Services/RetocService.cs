using LocalizationWorkbench.Core;
using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Services;

public sealed class RetocService
{
    private static readonly Regex ChunkRegex = new(@"\b[0-9A-Fa-f]{24}\b", RegexOptions.Compiled);
    private readonly string _retocPath;
    private readonly Action<string>? _log;

    public RetocService(string retocPath, Action<string>? log = null)
    {
        _retocPath = retocPath;
        _log = log;
    }

    public async Task<List<LocalizationAlias>> ListLocalizationAssetsAsync(
        string utocPath,
        string modsRoot,
        CancellationToken cancellationToken = default)
    {
        var assets = new List<LocalizationAlias>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Exception? parseError = null;
        var gate = new object();

        void ConsumeLine(string rawLine)
        {
            if (parseError is not null)
                return;

            try
            {
                var line = rawLine.Replace('\\', '/');
                if (!line.Contains(
                        AppConstants.LocalizationDatabaseNeedle,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var chunkMatch = ChunkRegex.Match(line);
                if (!chunkMatch.Success)
                {
                    throw new InvalidDataException(
                        $"Could not read IoStore chunk ID from: {line}"
                    );
                }

                var parts = line.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries
                );
                string? virtualPath = null;

                var pathMatch = Regex.Match(
                    line,
                    @"(?<path>(?:\.\./){3}.*LocalizationDatabase\.uasset)\s*$",
                    RegexOptions.IgnoreCase
                );
                if (pathMatch.Success)
                    virtualPath = pathMatch.Groups["path"].Value;
                else if (parts.Length > 0
                         && parts[^1].EndsWith(
                             "LocalizationDatabase.uasset",
                             StringComparison.OrdinalIgnoreCase))
                    virtualPath = parts[^1];

                if (string.IsNullOrWhiteSpace(virtualPath))
                    return;

                var alias = new LocalizationAlias
                {
                    ZenChunkId = chunkMatch.Value.ToLowerInvariant(),
                    VirtualPath = virtualPath,
                    SourceUtoc = utocPath,
                    SourceUtocRelative = Path.GetRelativePath(
                        modsRoot,
                        utocPath
                    ),
                };

                var key = alias.ZenChunkId + "|" + alias.VirtualPath;

                lock (gate)
                {
                    if (seen.Add(key))
                        assets.Add(alias);
                }
            }
            catch (Exception ex)
            {
                parseError = ex;
            }
        }

        await ProcessRunner.RunAsync(
            _retocPath,
            new[] { "list", "--path", utocPath },
            log: null,
            cancellationToken: cancellationToken,
            outputLine: ConsumeLine,
            captureStandardOutput: false
        );

        if (parseError is not null)
            throw parseError;

        lock (gate)
        {
            return assets
                .OrderBy(x => x.VirtualPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ZenChunkId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public async Task ToLegacyAsync(
        string inputDirectory,
        string outputDirectory,
        string filter,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _log?.Invoke(
            $"retoc to-legacy: extracting localization assets only "
            + $"(--no-shaders, filter={filter})"
        );

        await ProcessRunner.RunAsync(
            _retocPath,
            new[]
            {
                "to-legacy",
                inputDirectory,
                outputDirectory,
                "--filter",
                filter,
                "--no-shaders",
            },
            log: null,
            cancellationToken: cancellationToken,
            captureStandardOutput: false,
            priorityClass: ProcessPriorityClass.BelowNormal
        );

        stopwatch.Stop();
        _log?.Invoke(
            $"retoc to-legacy completed in {stopwatch.Elapsed.TotalSeconds:N1}s"
        );
    }

    public Task ToZenAsync(
        string legacyDirectory,
        string outputUtoc,
        CancellationToken cancellationToken = default)
    {
        return ProcessRunner.RunAsync(
            _retocPath,
            new[]
            {
                "to-zen",
                legacyDirectory,
                outputUtoc,
                "--version",
                AppConstants.RetocEngineVersion,
            },
            _log,
            cancellationToken
        );
    }
}

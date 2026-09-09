using STALKER2LocalizationTool.Core;
using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.Services;

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
        var result = await ProcessRunner.RunAsync(
            _retocPath,
            new[] { "list", "--path", utocPath },
            null,
            cancellationToken
        );

        var assets = new List<LocalizationAlias>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in result.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Replace('\\', '/');
            if (!line.Contains(AppConstants.LocalizationDatabaseNeedle, StringComparison.OrdinalIgnoreCase))
                continue;

            var chunkMatch = ChunkRegex.Match(line);
            if (!chunkMatch.Success)
                throw new InvalidDataException($"Could not read IoStore chunk ID from: {line}");

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string? virtualPath = null;

            var pathMatch = Regex.Match(
                line,
                @"(?<path>(?:\.\./){3}.*LocalizationDatabase\.uasset)\s*$",
                RegexOptions.IgnoreCase
            );
            if (pathMatch.Success)
                virtualPath = pathMatch.Groups["path"].Value;
            else if (parts.Length > 0 && parts[^1].EndsWith("LocalizationDatabase.uasset", StringComparison.OrdinalIgnoreCase))
                virtualPath = parts[^1];

            if (string.IsNullOrWhiteSpace(virtualPath))
                continue;

            var alias = new LocalizationAlias
            {
                ZenChunkId = chunkMatch.Value.ToLowerInvariant(),
                VirtualPath = virtualPath,
                SourceUtoc = utocPath,
                SourceUtocRelative = Path.GetRelativePath(modsRoot, utocPath),
            };

            var key = alias.ZenChunkId + "|" + alias.VirtualPath;
            if (seen.Add(key))
                assets.Add(alias);
        }

        return assets
            .OrderBy(x => x.VirtualPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ZenChunkId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task ToLegacyAsync(
        string inputDirectory,
        string outputDirectory,
        string filter,
        CancellationToken cancellationToken = default)
    {
        return ProcessRunner.RunAsync(
            _retocPath,
            new[] { "to-legacy", inputDirectory, outputDirectory, "--filter", filter },
            _log,
            cancellationToken
        );
    }

    public Task ToZenAsync(
        string legacyDirectory,
        string outputUtoc,
        CancellationToken cancellationToken = default)
    {
        return ProcessRunner.RunAsync(
            _retocPath,
            new[] { "to-zen", legacyDirectory, outputUtoc, "--version", AppConstants.RetocEngineVersion },
            _log,
            cancellationToken
        );
    }
}

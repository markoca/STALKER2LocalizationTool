using STALKER2LocalizationTool.Core;
using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.Services;

public sealed class RepakService
{
    private static readonly Regex LocresRegex = new(
        @"(?<path>(?:Stalker2/)?Content/Localization/Game/(?<culture>[^/\\]+)/Game\.locres)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private readonly string _repakPath;
    private readonly Action<string>? _log;

    public RepakService(string repakPath, Action<string>? log = null)
    {
        _repakPath = repakPath;
        _log = log;
    }

    public async Task<List<LocresSource>> ListLocresAsync(
        string pakPath,
        string modsRoot,
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            _repakPath,
            new[] { "list", pakPath },
            null,
            cancellationToken
        );

        var output = new List<LocresSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in result.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Replace('\\', '/').Trim();
            var match = LocresRegex.Match(line);
            if (!match.Success)
                continue;

            var path = match.Groups["path"].Value.TrimStart('/');
            if (!path.StartsWith("Stalker2/", StringComparison.OrdinalIgnoreCase))
                path = "Stalker2/" + path;

            if (!seen.Add(path))
                continue;

            output.Add(new LocresSource
            {
                SourcePak = pakPath,
                SourcePakRelative = Path.GetRelativePath(modsRoot, pakPath),
                InternalPath = path,
                CultureCode = match.Groups["culture"].Value,
            });
        }

        return output;
    }

    public async Task UnpackAsync(string pakPath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        await ProcessRunner.RunAsync(
            _repakPath,
            new[] { "unpack", pakPath, "-o", outputDirectory },
            _log,
            cancellationToken
        );
    }

    public async Task UnpackEntriesAsync(
        string pakPath,
        string outputDirectory,
        IEnumerable<string> internalPaths,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var arguments = new List<string> { "unpack", pakPath, "-o", outputDirectory };
        foreach (var path in internalPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            arguments.Add("--include");
            arguments.Add(path.Replace('\\', '/'));
        }
        await ProcessRunner.RunAsync(_repakPath, arguments, _log, cancellationToken);
    }

    public async Task PackLocalizationAsync(string inputDirectory, string outputPak, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPak)!);
        if (File.Exists(outputPak)) File.Delete(outputPak);

        await ProcessRunner.RunAsync(
            _repakPath,
            new[]
            {
                "pack",
                "--mount-point", AppConstants.PakMountPoint,
                "--version", AppConstants.PakVersion,
                "--path-hash-seed", AppConstants.PathHashSeed,
                inputDirectory,
                outputPak,
            },
            _log,
            cancellationToken
        );

        // repak defaults to ../../../, which is the mount point used by the
        // known-good launch.py pipeline. Pass it explicitly above and verify the
        // finished PAK header so a platform/version-specific default can never
        // silently produce a PAK that FModel can read but the game mounts elsewhere.
        var info = await ProcessRunner.RunAsync(
            _repakPath,
            new[] { "info", outputPak },
            null,
            cancellationToken
        );

        var mountMatch = Regex.Match(
            info.StandardOutput,
            @"(?im)^\s*mount point:\s*(?<mount>.+?)\s*$"
        );
        if (!mountMatch.Success)
            throw new InvalidDataException("repak info did not report the PAK mount point.");

        var mountPoint = mountMatch.Groups["mount"].Value.Trim().Replace('\\', '/');
        if (!string.Equals(mountPoint, AppConstants.PakMountPoint, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Finished localization PAK has wrong mount point: '{mountPoint}'. " +
                $"Expected '{AppConstants.PakMountPoint}'."
            );
        }

        var versionMatch = Regex.Match(
            info.StandardOutput,
            @"(?im)^\s*version:\s*(?<version>\S+)\s*$"
        );
        if (!versionMatch.Success
            || !string.Equals(versionMatch.Groups["version"].Value.Trim(), AppConstants.PakVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Finished localization PAK is not {AppConstants.PakVersion}. repak info:\r\n{info.StandardOutput.Trim()}"
            );
        }

        _log?.Invoke(
            $"PAK header verified: mount={AppConstants.PakMountPoint}, version={AppConstants.PakVersion}, " +
            $"path-hash-seed={AppConstants.PathHashSeed}"
        );
    }

    public async Task<IReadOnlyList<string>> ListEntriesAsync(string pakPath, CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            _repakPath,
            new[] { "list", pakPath },
            null,
            cancellationToken
        );

        return result.StandardOutput
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Replace('\\', '/').Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}

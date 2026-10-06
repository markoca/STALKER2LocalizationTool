using LocalizationWorkbench.Core;

namespace LocalizationWorkbench.Services;

public sealed class UAssetGuiService
{
    private readonly string _uassetGuiPath;
    private readonly string _mappingsPath;
    private readonly Action<string>? _log;
    private readonly SemaphoreSlim _prepareLock = new(1, 1);
    private bool _prepared;

    public UAssetGuiService(string uassetGuiPath, string mappingsPath, Action<string>? log = null)
    {
        _uassetGuiPath = uassetGuiPath;
        _mappingsPath = mappingsPath;
        _log = log;
    }

    public async Task ToJsonAsync(string uassetPath, string jsonPath, CancellationToken cancellationToken = default)
    {
        _log?.Invoke("UAssetGUI: preparing...");
        await PrepareAsync(cancellationToken).ConfigureAwait(false);
        _log?.Invoke("UAssetGUI: preparation complete.");

        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);

        var uassetBytes = new FileInfo(uassetPath).Length;
        var uexpPath = Path.ChangeExtension(uassetPath, ".uexp");
        var uexpBytes = File.Exists(uexpPath) ? new FileInfo(uexpPath).Length : 0L;
        _log?.Invoke(
            $"UAssetGUI tojson starting: {Path.GetFileName(uassetPath)} "
            + $"(uasset={FormatBytes(uassetBytes)}, uexp={FormatBytes(uexpBytes)})"
        );

        try
        {
            await ProcessRunner.RunAsync(
                _uassetGuiPath,
                new[]
                {
                    "tojson",
                    uassetPath,
                    jsonPath,
                    AppConstants.EngineVersion,
                    AppConstants.UAssetGuiMappingsAlias,
                },
                log: null,
                cancellationToken: cancellationToken,
                captureStandardOutput: false,
                priorityClass: ProcessPriorityClass.BelowNormal
            ).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (IsBundleFailure(ex.Message))
        {
            throw new InvalidOperationException(
                "UAssetGUI failed before managed code could start. The configured UAssetGUI.exe cannot load its .NET application bundle; " +
                "the extracted .uasset has not been parsed yet and is not implicated by this error. " +
                $"Localization Workbench v{AppConstants.Version} is tested with UAssetGUI {AppConstants.UAssetGuiVersion}; " +
                "replace tools\\UAssetGUI.exe with a compatible known-good binary, then retry. " +
                "On Linux/Wine, UAssetGUI v1.1.0 also requires the .NET 8 Desktop Runtime in the same Wine prefix.",
                ex
            );
        }

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("UAssetGUI did not produce the expected JSON file.", jsonPath);

        _log?.Invoke(
            $"UAssetGUI tojson completed: {Path.GetFileName(uassetPath)} "
            + $"-> {FormatBytes(new FileInfo(jsonPath).Length)} JSON"
        );
    }

    private async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (_prepared)
            return;

        await _prepareLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_prepared)
                return;

            if (!File.Exists(_uassetGuiPath))
                throw new FileNotFoundException("UAssetGUI.exe was not found.", _uassetGuiPath);
            if (!File.Exists(_mappingsPath))
                throw new FileNotFoundException("Mappings.usmap was not found.", _mappingsPath);

            var hash = await HashUtil.Sha256FileAsync(
                _uassetGuiPath,
                cancellationToken
            ).ConfigureAwait(false);
            if (string.Equals(hash, AppConstants.UAssetGuiKnownBadSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "This UAssetGUI.exe matches a known-bad historical bundle. " +
                    $"SHA-256: {hash}. Replace tools\\UAssetGUI.exe with a compatible " +
                    $"UAssetGUI {AppConstants.UAssetGuiVersion} binary."
                );
            }

            if (!string.Equals(hash, AppConstants.UAssetGuiPinnedSha256, StringComparison.OrdinalIgnoreCase))
            {
                _log?.Invoke(
                    $"UAssetGUI: custom/non-pinned executable detected (sha256={hash}); continuing because custom tool paths are supported."
                );
            }

            await InstallMappingsForStableCliAsync(
                cancellationToken
            ).ConfigureAwait(false);
            _prepared = true;
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    private async Task InstallMappingsForStableCliAsync(
        CancellationToken cancellationToken)
    {
        // UAssetGUI v1.1.0 accepts a mappings NAME on the command line, not a full
        // mappings path. Keep this preparation fully asynchronous: large .usmap
        // files must never be read into memory or processed on the WinForms thread.
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException(
                "Could not resolve the Windows LocalApplicationData directory for UAssetGUI mappings."
            );
        }

        var mappingsDirectory = Path.Combine(
            localAppData,
            "UAssetGUI",
            "Mappings"
        );
        var installedPath = Path.Combine(
            mappingsDirectory,
            AppConstants.UAssetGuiMappingsAlias + ".usmap"
        );

        try
        {
            Directory.CreateDirectory(mappingsDirectory);

            var copyRequired = true;
            if (File.Exists(installedPath))
            {
                var sourceInfo = new FileInfo(_mappingsPath);
                var installedInfo = new FileInfo(installedPath);

                if (sourceInfo.Length == installedInfo.Length)
                {
                    var sourceHash = await HashUtil.Sha256FileAsync(
                        _mappingsPath,
                        cancellationToken
                    ).ConfigureAwait(false);
                    var installedHash = await HashUtil.Sha256FileAsync(
                        installedPath,
                        cancellationToken
                    ).ConfigureAwait(false);
                    copyRequired = !string.Equals(
                        sourceHash,
                        installedHash,
                        StringComparison.OrdinalIgnoreCase
                    );
                }
            }

            if (copyRequired)
            {
                await CopyFileAsync(
                    _mappingsPath,
                    installedPath,
                    cancellationToken
                ).ConfigureAwait(false);

                _log?.Invoke(
                    $"UAssetGUI mappings installed as "
                    + $"{AppConstants.UAssetGuiMappingsAlias} -> {installedPath}"
                );
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Could not install the selected mappings file into UAssetGUI's LocalAppData\\UAssetGUI\\Mappings directory. " +
                "UAssetGUI v1.1.0 requires mappings to be referenced by their installed name on the command line.",
                ex
            );
        }
    }

    private static async Task CopyFileAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        await using var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        await input.CopyToAsync(
            output,
            1024 * 1024,
            cancellationToken
        ).ConfigureAwait(false);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
            return $"{bytes / (1024d * 1024d * 1024d):N2} GiB";
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024d * 1024d):N1} MiB";
        if (bytes >= 1024L)
            return $"{bytes / 1024d:N1} KiB";
        return $"{bytes} B";
    }

    private static bool IsBundleFailure(string message) =>
        message.Contains("Failure processing application bundle", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Arithmetic overflow while reading bundle", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("-2147450721", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("0x8000809f", StringComparison.OrdinalIgnoreCase);
}

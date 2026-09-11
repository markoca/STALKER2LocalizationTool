using STALKER2LocalizationTool.Core;

namespace STALKER2LocalizationTool.Services;

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
        await PrepareAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);

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
                _log,
                cancellationToken
            );
        }
        catch (InvalidOperationException ex) when (IsBundleFailure(ex.Message))
        {
            throw new InvalidOperationException(
                "UAssetGUI failed before managed code could start. The configured UAssetGUI.exe cannot load its .NET application bundle; " +
                "the extracted .uasset has not been parsed yet and is not implicated by this error. " +
                $"The STALKER2 Localization Tool v{AppConstants.Version} pins upstream UAssetGUI {AppConstants.UAssetGuiVersion}; " +
                "replace tools\\UAssetGUI.exe in the source project with the known-good binary, republish, then retry. " +
                "On Linux/Wine, UAssetGUI v1.1.0 also requires the .NET 8 Desktop Runtime in the same Wine prefix.",
                ex
            );
        }

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("UAssetGUI did not produce the expected JSON file.", jsonPath);
    }

    private async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (_prepared)
            return;

        await _prepareLock.WaitAsync(cancellationToken);
        try
        {
            if (_prepared)
                return;

            if (!File.Exists(_uassetGuiPath))
                throw new FileNotFoundException("UAssetGUI.exe was not found.", _uassetGuiPath);
            if (!File.Exists(_mappingsPath))
                throw new FileNotFoundException("Mappings.usmap was not found.", _mappingsPath);

            var hash = await HashUtil.Sha256FileAsync(_uassetGuiPath, cancellationToken);
            if (string.Equals(hash, AppConstants.UAssetGuiKnownBadSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "This UAssetGUI.exe is the known-bad bundle that was accidentally shipped in STALKER2 Localization Tool v0.8.0-v0.8.2. " +
                    $"SHA-256: {hash}. Replace tools\\UAssetGUI.exe in the source project with the pinned upstream " +
                    $"UAssetGUI {AppConstants.UAssetGuiVersion} binary and republish."
                );
            }

            if (!string.Equals(hash, AppConstants.UAssetGuiPinnedSha256, StringComparison.OrdinalIgnoreCase))
            {
                _log?.Invoke(
                    $"UAssetGUI: custom/non-pinned executable detected (sha256={hash}); continuing because custom tool paths are supported."
                );
            }

            InstallMappingsForStableCli();
            _prepared = true;
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    private void InstallMappingsForStableCli()
    {
        // UAssetGUI v1.1.0 accepts a mappings NAME on the command line, not a full
        // mappings path. Put our selected mappings file in UAssetGUI's normal
        // LocalAppData mapping directory and invoke it by an isolated alias.
        // This form remains compatible with newer UAssetGUI versions as well.
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            throw new InvalidOperationException("Could not resolve the Windows LocalApplicationData directory for UAssetGUI mappings.");

        var mappingsDirectory = Path.Combine(localAppData, "UAssetGUI", "Mappings");
        var installedPath = Path.Combine(mappingsDirectory, AppConstants.UAssetGuiMappingsAlias + ".usmap");

        try
        {
            Directory.CreateDirectory(mappingsDirectory);

            var copyRequired = true;
            if (File.Exists(installedPath))
            {
                var sourceHash = SHA256.HashData(File.ReadAllBytes(_mappingsPath));
                var installedHash = SHA256.HashData(File.ReadAllBytes(installedPath));
                copyRequired = !sourceHash.AsSpan().SequenceEqual(installedHash);
            }

            if (copyRequired)
            {
                File.Copy(_mappingsPath, installedPath, overwrite: true);
                _log?.Invoke($"UAssetGUI mappings installed as {AppConstants.UAssetGuiMappingsAlias} -> {installedPath}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Could not install the selected mappings file into UAssetGUI's LocalAppData\\UAssetGUI\\Mappings directory. " +
                "UAssetGUI v1.1.0 requires mappings to be referenced by their installed name on the command line.",
                ex
            );
        }
    }

    private static bool IsBundleFailure(string message) =>
        message.Contains("Failure processing application bundle", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Arithmetic overflow while reading bundle", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("-2147450721", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("0x8000809f", StringComparison.OrdinalIgnoreCase);
}

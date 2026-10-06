using LocalizationWorkbench.Core;

namespace LocalizationWorkbench.Services;

public sealed class S2HocmmService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _s2HocmmPath;
    private readonly string _repakPath;
    private readonly Action<string>? _log;

    public S2HocmmService(string s2HocmmPath, string repakPath, Action<string>? log = null)
    {
        _s2HocmmPath = s2HocmmPath;
        _repakPath = repakPath;
        _log = log;
    }

    public async Task<string> BuildGameLocresAsync(
        IReadOnlyDictionary<string, string> flatTranslations,
        string culture,
        string workDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_s2HocmmPath))
            throw new FileNotFoundException($"Required S2HOCMM.exe not found: {_s2HocmmPath}", _s2HocmmPath);
        if (!File.Exists(_repakPath))
            throw new FileNotFoundException($"Required repak.exe not found: {_repakPath}", _repakPath);
        if (flatTranslations.Count == 0)
            throw new InvalidDataException("Cannot build an empty Game.locres with S2HOCMM.");
        if (string.IsNullOrWhiteSpace(culture))
            throw new ArgumentException("LOCRES culture cannot be empty.", nameof(culture));

        if (Directory.Exists(workDirectory))
            Directory.Delete(workDirectory, recursive: true);
        Directory.CreateDirectory(workDirectory);

        var inputDirectory = Path.Combine(workDirectory, "input");
        Directory.CreateDirectory(inputDirectory);

        // S2HOCMM -Pack expects a FLAT Dictionary<string,string>. The JSON filename
        // selects the culture folder: sr.json -> .../Localization/Game/sr/Game.locres.
        var inputJson = Path.Combine(inputDirectory, culture + ".json");
        File.WriteAllText(
            inputJson,
            JsonSerializer.Serialize(flatTranslations, JsonOptions) + Environment.NewLine,
            new UTF8Encoding(false)
        );

        // The user's proven launch pipeline lets S2HOCMM create ModOutput/Game.locres,
        // then ignores S2HOCMM's own final PAK and repacks ModOutput itself. Keep a
        // local repak.exe available so S2HOCMM can finish cleanly when possible.
        var localRepak = Path.Combine(workDirectory, "repak.exe");
        File.Copy(_repakPath, localRepak, overwrite: true);

        _log?.Invoke($"S2HOCMM LOCRES input: {inputJson} ({flatTranslations.Count} keys)");
        var result = await ProcessRunner.RunAsync(
            _s2HocmmPath,
            new[]
            {
                "-Pack",
                "--input", "input",
                "--repak", "repak.exe",
            },
            _log,
            cancellationToken,
            workingDirectory: workDirectory,
            throwOnNonZero: false,
            environment: new Dictionary<string, string?>
            {
                ["DOTNET_USENLS"] = "1",
            }
        );

        var generatedLocres = Path.Combine(
            workDirectory,
            "ModOutput",
            "Stalker2",
            "Content",
            "Localization",
            "Game",
            culture,
            "Game.locres"
        );

        if (!File.Exists(generatedLocres))
        {
            throw new InvalidOperationException(
                $"S2HOCMM did not create Game.locres (exit {result.ExitCode}).\r\n" +
                $"Expected: {generatedLocres}\r\n" +
                result.StandardError.Trim()
            );
        }

        if (result.ExitCode != 0)
        {
            _log?.Invoke(
                $"S2HOCMM returned {result.ExitCode} after creating Game.locres; " +
                "continuing with the tool's final V11 repak step."
            );
        }

        await VerifyWithS2HocmmDumpAsync(
            flatTranslations,
            culture,
            generatedLocres,
            workDirectory,
            cancellationToken
        );

        return generatedLocres;
    }

    private async Task VerifyWithS2HocmmDumpAsync(
        IReadOnlyDictionary<string, string> expected,
        string culture,
        string generatedLocres,
        string workDirectory,
        CancellationToken cancellationToken)
    {
        var verifyInput = Path.Combine(workDirectory, "verify_input");
        var toMerge = Path.Combine(workDirectory, "ToMerge");
        if (Directory.Exists(verifyInput)) Directory.Delete(verifyInput, recursive: true);
        if (Directory.Exists(toMerge)) Directory.Delete(toMerge, recursive: true);
        Directory.CreateDirectory(verifyInput);

        File.Copy(generatedLocres, Path.Combine(verifyInput, culture + ".locres"), overwrite: true);

        await ProcessRunner.RunAsync(
            _s2HocmmPath,
            new[] { "-Dump", "--locrespath", "verify_input" },
            _log,
            cancellationToken,
            workingDirectory: workDirectory,
            environment: new Dictionary<string, string?>
            {
                ["DOTNET_USENLS"] = "1",
            }
        );

        var verifyJson = Path.Combine(workDirectory, "ToMerge", "0_BaseGame", culture + ".json");
        if (!File.Exists(verifyJson))
            throw new FileNotFoundException("S2HOCMM verification JSON was not created.", verifyJson);

        var actual = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(verifyJson, Encoding.UTF8)
        ) ?? throw new InvalidDataException("S2HOCMM verification JSON is empty or invalid.");

        if (actual.Count != expected.Count)
        {
            throw new InvalidDataException(
                $"S2HOCMM verification entry count mismatch: expected {expected.Count}, got {actual.Count}."
            );
        }

        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value))
                throw new InvalidDataException($"S2HOCMM verification is missing key: {pair.Key}");
            if (!string.Equals(value, pair.Value, StringComparison.Ordinal))
                throw new InvalidDataException($"S2HOCMM verification value mismatch: {pair.Key}");
        }

        _log?.Invoke($"S2HOCMM -Dump verification passed: {actual.Count} keys.");
    }
}

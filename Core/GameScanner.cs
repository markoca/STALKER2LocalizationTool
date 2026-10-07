using LocalizationWorkbench.Models;
using LocalizationWorkbench.Services;

namespace LocalizationWorkbench.Core;

public sealed class GameScanner
{
    private readonly RetocService _retoc;
    private readonly RepakService _repak;
    private readonly string _gamePaksRoot;
    private readonly string _sourceRoot;
    private readonly string _translationsRoot;
    private readonly IReadOnlyList<BuildLanguage> _buildLanguages;
    private readonly Action<string>? _log;

    public GameScanner(
        RetocService retoc,
        RepakService repak,
        string gamePaksRoot,
        string sourceRoot,
        string translationsRoot,
        IEnumerable<int> buildLanguageIds,
        Action<string>? log = null)
    {
        _retoc = retoc;
        _repak = repak;
        _gamePaksRoot = gamePaksRoot;
        _sourceRoot = sourceRoot;
        _translationsRoot = translationsRoot;
        _buildLanguages = buildLanguageIds
            .Select(BuildLanguageCatalog.ById)
            .DistinctBy(language => language.Id)
            .ToList();
        _log = log;
    }

    public async Task<ModScanResult> ScanAsync(
        IProgress<(int Current, int Total, string Message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var game = new ModScanResult
        {
            ModId = "Game",
            ModName = "Game",
            ModSourceRoot = _gamePaksRoot,
        };

        if (!Directory.Exists(_gamePaksRoot))
        {
            game.ScanError = "Game Paks folder was not found.";
            game.UiStatus = ModUiStatus.Error;
            return game;
        }

        // Game localization lives in pakchunk0. Do not walk every game chunk: doing so
        // can discover unrelated or duplicate localization-like assets from other containers.
        game.Containers = Directory.EnumerateFiles(_gamePaksRoot, "*.utoc", SearchOption.TopDirectoryOnly)
            .Where(IsBaseGamePakChunk0)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        game.PakFiles = Directory.EnumerateFiles(_gamePaksRoot, "*.pak", SearchOption.TopDirectoryOnly)
            .Where(IsBaseGamePakChunk0)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _log?.Invoke("=== FOUND GAME LOCALIZATION ===");

        var aliases = new List<LocalizationAlias>();
        var locres = new List<LocresSource>();
        var errors = new List<string>();
        var total = game.Containers.Count + game.PakFiles.Count;
        var current = 0;

        foreach (var utoc in game.Containers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report((current, total, $"Scanning {Path.GetFileName(utoc)}"));
            try
            {
                aliases.AddRange(await _retoc.ListLocalizationAssetsAsync(utoc, _gamePaksRoot, cancellationToken));
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(utoc)}: {ex.Message}");
                _log?.Invoke($"Game IoStore scan failed for {utoc}: {ex.Message}");
            }
            finally
            {
                current++;
                progress?.Report((current, total, $"Scanned {Path.GetFileName(utoc)}"));
            }
        }

        foreach (var pak in game.PakFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report((current, total, $"Scanning {Path.GetFileName(pak)}"));
            try
            {
                locres.AddRange(await _repak.ListLocresAsync(pak, _gamePaksRoot, cancellationToken));
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(pak)}: {ex.Message}");
                _log?.Invoke($"Game PAK scan failed for {pak}: {ex.Message}");
            }
            finally
            {
                current++;
                progress?.Report((current, total, $"Scanned {Path.GetFileName(pak)}"));
            }
        }

        var aliasGrouping = LocalizationAliasGrouper.Group(aliases);
        game.Assets = aliasGrouping.Assets;

        if (aliasGrouping.ExactChunkAliasesCollapsed > 0)
        {
            _log?.Invoke(
                $"Game: ignored {aliasGrouping.ExactChunkAliasesCollapsed} duplicate localization alias(es) for identical Zen chunks."
            );
        }

        game.LocresAssets = locres
            .GroupBy(source => source.SourcePakRelative + "|" + source.InternalPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(source => source.SourcePakRelative, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.InternalPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        game.ScanError = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;

        if (game.HasLocalization)
        {
            var relevantFiles = game.Assets
                .SelectMany(group => group.Aliases)
                .Select(alias => alias.SourceUtoc)
                .Concat(game.LocresAssets.Select(source => source.SourcePak))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            var fingerprintLines = new List<string>();
            foreach (var path in relevantFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                var fingerprint = $"size:{info.Length};utc:{info.LastWriteTimeUtc.Ticks}";
                fingerprintLines.Add(Path.GetRelativePath(_gamePaksRoot, path).Replace('\\', '/') + "|" + fingerprint);
            }
            game.SourceFingerprint = HashUtil.Sha256Text(string.Join("\n", fingerprintLines));
        }

        var manifestPath = Path.Combine(_sourceRoot, game.ModId, "manifest.json");
        game.NeedsExtraction = game.HasLocalization && !ManifestMatches(manifestPath, game.SourceFingerprint);
        game.TranslationFile = _buildLanguages
            .Select(language => Path.Combine(_translationsRoot, game.ModId, language.Key + ".json"))
            .Where(File.Exists)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        game.UiStatus = ResolveStatus(game, _buildLanguages.Count);
        return game;
    }


    private static bool IsBaseGamePakChunk0(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Equals(AppConstants.BaseGamePakChunkPrefix, StringComparison.OrdinalIgnoreCase)
               || name.StartsWith(AppConstants.BaseGamePakChunkPrefix + "-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith(AppConstants.BaseGamePakChunkPrefix + "_", StringComparison.OrdinalIgnoreCase);
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

    private static ModUiStatus ResolveStatus(ModScanResult game, int selectedLanguageCount)
    {
        if (!string.IsNullOrWhiteSpace(game.ScanError)) return ModUiStatus.Error;
        if (!game.HasLocalization) return ModUiStatus.NoLocalization;
        if (game.NeedsExtraction) return ModUiStatus.NeedsExtraction;
        if (selectedLanguageCount == 0) return ModUiStatus.NoLanguageSelected;
        if (string.IsNullOrWhiteSpace(game.TranslationFile)) return ModUiStatus.MissingTranslation;
        return ModUiStatus.Available;
    }
}

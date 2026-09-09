using STALKER2LocalizationTool.Models;
using STALKER2LocalizationTool.Services;

namespace STALKER2LocalizationTool.Core;

public sealed class ExtractionService
{
    private readonly AppSettings _settings;
    private readonly RetocService _retoc;
    private readonly RepakService _repak;
    private readonly UAssetGuiService _uassetGui;
    private readonly string _sourceRoot;
    private readonly bool _hashSourceFiles;
    private readonly Action<string>? _log;

    public ExtractionService(
        AppSettings settings,
        RetocService retoc,
        RepakService repak,
        UAssetGuiService uassetGui,
        Action<string>? log = null,
        string? sourceRoot = null,
        bool hashSourceFiles = true)
    {
        _settings = settings;
        _retoc = retoc;
        _repak = repak;
        _uassetGui = uassetGui;
        _sourceRoot = string.IsNullOrWhiteSpace(sourceRoot) ? settings.ModsFolder : sourceRoot;
        _hashSourceFiles = hashSourceFiles;
        _log = log;
    }

    public async Task ExtractAsync(
        IEnumerable<ModScanResult> mods,
        IProgress<(int Current, int Total, string Message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var list = mods.Where(x => x.HasLocalization && x.NeedsExtraction).ToList();
        ValidatePrerequisites(list);
        Directory.CreateDirectory(_settings.CachedFolder);
        for (var index = 0; index < list.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = list[index];
            progress?.Report((index + 1, list.Count, mod.ModName));
            _log?.Invoke($"=== Extracting {mod.ModName} ===");
            await ExtractOneModAsync(mod, cancellationToken);
        }
    }

    private async Task ExtractOneModAsync(ModScanResult mod, CancellationToken cancellationToken)
    {
        var stagingRoot = Path.Combine(
            _settings.CachedFolder,
            $".{mod.ModId}.staging.{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var manifest = new ExtractedManifest
            {
                ModId = mod.ModId,
                ModName = mod.ModName,
                ExtractedAtUtc = DateTime.UtcNow,
                SourceFingerprint = mod.SourceFingerprint,
            };

            var isGame = string.Equals(mod.ModId, "Game", StringComparison.OrdinalIgnoreCase);
            var relevantFiles = mod.Assets
                .SelectMany(x => x.Aliases)
                .Select(x => x.SourceUtoc)
                .Concat(isGame ? mod.LocresAssets.Select(x => x.SourcePak) : Enumerable.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var source in relevantFiles)
            {
                manifest.SourceFiles.Add(new SourceFileFingerprint
                {
                    RelativePath = Path.GetRelativePath(_sourceRoot, source),
                    Sha256 = _hashSourceFiles
                        ? await HashUtil.Sha256FileAsync(source, cancellationToken)
                        : FileMetadataFingerprint(source),
                });
            }

            var languageDumps = BuildLanguageCatalog.All.ToDictionary(
                language => language.Id,
                _ => new SortedDictionary<string, string>(StringComparer.Ordinal)
            );

            await ExtractDatabaseAssetsAsync(mod, manifest, languageDumps, stagingRoot, cancellationToken);
            if (isGame)
                await ExtractLocresAssetsAsync(mod, manifest, languageDumps, stagingRoot, cancellationToken);

            JsonUtil.Save(Path.Combine(stagingRoot, "manifest.json"), manifest);
            foreach (var language in BuildLanguageCatalog.All)
            {
                JsonUtil.Save(
                    Path.Combine(stagingRoot, language.Key + ".json"),
                    languageDumps[language.Id]
                );
            }

            var finalRoot = Path.Combine(_settings.CachedFolder, mod.ModId);
            if (Directory.Exists(finalRoot))
                Directory.Delete(finalRoot, recursive: true);
            Directory.Move(stagingRoot, finalRoot);

            try
            {
                SeedEditableWorkspace(finalRoot, mod.ModId);
            }
            catch
            {
                // Do not leave a current manifest behind when the initial Editable
                // copy failed; the next scan must offer extraction again.
                TryDeleteDirectory(finalRoot);
                throw;
            }

            _log?.Invoke(
                isGame
                    ? $"Extracted {manifest.Assets.Count} database(s) and {manifest.LocresAssets.Count} LOCRES file(s) -> {finalRoot}"
                    : $"Extracted {manifest.Assets.Count} localization database(s) -> {finalRoot}"
            );
        }
        catch
        {
            TryDeleteDirectory(stagingRoot);
            throw;
        }
    }

    private void SeedEditableWorkspace(string cachedRoot, string modId)
    {
        var editableRoot = Path.Combine(_settings.EditableFolder, modId);
        if (Directory.Exists(editableRoot))
        {
            _log?.Invoke($"Editable workspace already exists; leaving it unchanged -> {editableRoot}");
            return;
        }

        Directory.CreateDirectory(_settings.EditableFolder);
        var stagingRoot = Path.Combine(
            _settings.EditableFolder,
            $".{modId}.staging.{Guid.NewGuid():N}"
        );

        try
        {
            CopyDirectory(cachedRoot, stagingRoot);
            Directory.Move(stagingRoot, editableRoot);
            _log?.Invoke($"Created editable 1:1 Editable copy -> {editableRoot}");
        }
        catch
        {
            TryDeleteDirectory(stagingRoot);
            throw;
        }
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, sourceFile);
            var destinationFile = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, overwrite: false);
        }
    }

    private async Task ExtractDatabaseAssetsAsync(
        ModScanResult mod,
        ExtractedManifest manifest,
        Dictionary<int, SortedDictionary<string, string>> languageDumps,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        foreach (var containerGroup in mod.Assets.GroupBy(x => x.Canonical.SourceUtoc, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceUtoc = containerGroup.Key;
            var workRoot = Path.Combine(stagingRoot, ".work", "db_" + Guid.NewGuid().ToString("N"));
            var input = Path.Combine(workRoot, "input");
            var legacy = Path.Combine(workRoot, "legacy");
            Directory.CreateDirectory(input);
            Directory.CreateDirectory(legacy);

            PrepareRetocInput(sourceUtoc, input);
            await _retoc.ToLegacyAsync(input, legacy, AppConstants.LocalizationDatabaseNeedle, cancellationToken);

            var scriptObjects = Path.Combine(legacy, "scriptobjects.bin");
            RequireFile(scriptObjects, "retoc scriptobjects.bin");

            foreach (var assetGroup in containerGroup)
            {
                var alias = assetGroup.Canonical;
                var legacyRelative = PathUtil.NormalizeVirtualPath(alias.VirtualPath);
                var extractedUasset = Path.Combine(legacy, legacyRelative);
                var extractedUexp = Path.ChangeExtension(extractedUasset, ".uexp");
                RequireFile(extractedUasset, alias.VirtualPath);
                RequireFile(extractedUexp, alias.VirtualPath + " .uexp");

                var assetFolderRelative = Path.Combine("assets", "database", assetGroup.ZenChunkId);
                var assetFolder = Path.Combine(stagingRoot, assetFolderRelative);
                Directory.CreateDirectory(assetFolder);

                var sourceUasset = Path.Combine(assetFolder, "source.uasset");
                var sourceUexp = Path.Combine(assetFolder, "source.uexp");
                var assetJson = Path.Combine(assetFolder, "asset.json");
                var storedScriptObjects = Path.Combine(assetFolder, "scriptobjects.bin");

                File.Copy(extractedUasset, sourceUasset, overwrite: true);
                File.Copy(extractedUexp, sourceUexp, overwrite: true);
                File.Copy(scriptObjects, storedScriptObjects, overwrite: true);

                await _uassetGui.ToJsonAsync(sourceUasset, assetJson, cancellationToken);
                var export = UAssetInspector.ReadLocalizationExport(assetJson, alias.VirtualPath);
                if (!export.ImportsLocalizationDatabaseClass)
                    throw new InvalidDataException($"{alias.VirtualPath}: asset does not import ModLocalizationDatabaseDataAsset");

                var parsed = LocalizationDatabaseCodec.Parse(export.Payload, alias.VirtualPath);
                var roundTrip = LocalizationDatabaseCodec.Serialize(parsed);
                if (!roundTrip.AsSpan().SequenceEqual(export.Payload))
                    throw new InvalidDataException($"{alias.VirtualPath}: untouched parser/serializer round-trip changed the payload");

                foreach (var record in parsed.Records)
                {
                    foreach (var language in BuildLanguageCatalog.All)
                    {
                        var value = record.Translations
                            .FirstOrDefault(x => x.LanguageId == language.Id)?
                            .Value ?? string.Empty;
                        MergeDumpValue(languageDumps[language.Id], record.Sid, value);
                    }
                }

                manifest.Assets.Add(new ExtractedAssetManifest
                {
                    ZenChunkId = assetGroup.ZenChunkId,
                    DatabaseName = Path.GetFileNameWithoutExtension(alias.VirtualPath),
                    VirtualPath = alias.VirtualPath,
                    LegacyRelativePath = legacyRelative,
                    SourceContainerRelativePath = alias.SourceUtocRelative,
                    Aliases = assetGroup.Aliases.Select(x => new AliasManifest
                    {
                        SourceContainerRelativePath = x.SourceUtocRelative,
                        VirtualPath = x.VirtualPath,
                    }).ToList(),
                    UassetFile = Path.Combine(assetFolderRelative, "source.uasset"),
                    UexpFile = Path.Combine(assetFolderRelative, "source.uexp"),
                    AssetJsonFile = Path.Combine(assetFolderRelative, "asset.json"),
                    ScriptObjectsFile = Path.Combine(assetFolderRelative, "scriptobjects.bin"),
                    SidCount = parsed.Records.Count,
                });
            }

            TryDeleteDirectory(workRoot);
        }
    }

    private async Task ExtractLocresAssetsAsync(
        ModScanResult mod,
        ExtractedManifest manifest,
        Dictionary<int, SortedDictionary<string, string>> languageDumps,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        var ordered = mod.LocresAssets
            .OrderBy(x => string.Equals(x.CultureCode, "en", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.CultureCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SourcePakRelative, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unpacked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var locresDocuments = new List<(BuildLanguage? Language, LocresDocument Document)>();
        var index = 0;

        foreach (var source in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!unpacked.TryGetValue(source.SourcePak, out var unpackRoot))
            {
                unpackRoot = Path.Combine(stagingRoot, ".work", "pak_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(unpackRoot);
                var includedPaths = ordered
                    .Where(item => string.Equals(item.SourcePak, source.SourcePak, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.InternalPath);
                await _repak.UnpackEntriesAsync(source.SourcePak, unpackRoot, includedPaths, cancellationToken);
                unpacked[source.SourcePak] = unpackRoot;
            }

            var unpackedLocres = Path.Combine(unpackRoot, PathUtil.NormalizePakPath(source.InternalPath));
            RequireFile(unpackedLocres, source.InternalPath);

            var assetFolderRelative = Path.Combine("assets", "locres", index.ToString("000"));
            var assetFolder = Path.Combine(stagingRoot, assetFolderRelative);
            Directory.CreateDirectory(assetFolder);
            var storedLocres = Path.Combine(assetFolder, "source.locres");
            File.Copy(unpackedLocres, storedLocres, overwrite: true);

            var document = LocresCodec.Load(storedLocres);
            var sourceLanguage = BuildLanguageCatalog.All.FirstOrDefault(language =>
                string.Equals(language.LocresCulture, source.CultureCode, StringComparison.OrdinalIgnoreCase));
            if (sourceLanguage is null)
            {
                _log?.Invoke($"{source.InternalPath}: no editable language JSON mapping for culture '{source.CultureCode}'");
            }
            locresDocuments.Add((sourceLanguage, document));

            manifest.LocresAssets.Add(new ExtractedLocresManifest
            {
                SourcePakRelativePath = source.SourcePakRelative,
                InternalPath = source.InternalPath,
                CultureCode = source.CultureCode,
                SourceLocresFile = Path.Combine(assetFolderRelative, "source.locres"),
                SidCount = document.EntryCount,
                LocresVersion = (int)document.Version,
            });

            _log?.Invoke($"{source.InternalPath}: native LOCRES v{(int)document.Version}, {document.EntryCount} entries");
            index++;
        }

        var namespaceCounts = locresDocuments
            .SelectMany(item => item.Document.Namespaces)
            .SelectMany(ns => ns.Entries.Select(entry => (Namespace: ns.Name, Key: entry.Key)))
            .Distinct()
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (var item in locresDocuments)
        {
            foreach (var ns in item.Document.Namespaces)
            {
                foreach (var entry in ns.Entries)
                {
                    var editableKey = namespaceCounts[entry.Key] > 1
                        ? $"{ns.Name}::{entry.Key}"
                        : entry.Key;

                    foreach (var languageDump in languageDumps.Values)
                        MergeDumpValue(languageDump, editableKey, string.Empty);

                    if (item.Language is not null)
                        MergeDumpValue(languageDumps[item.Language.Id], editableKey, entry.Value);
                }
            }
        }

        foreach (var root in unpacked.Values)
            TryDeleteDirectory(root);
    }

    private void PrepareRetocInput(string sourceUtoc, string input)
    {
        var sourceUcas = Path.ChangeExtension(sourceUtoc, ".ucas");
        var sourcePak = Path.ChangeExtension(sourceUtoc, ".pak");
        RequireFile(sourceUtoc, "source .utoc");
        RequireFile(sourceUcas, "source .ucas");

        FileLinker.LinkOrCopy(sourceUtoc, Path.Combine(input, Path.GetFileName(sourceUtoc)));
        FileLinker.LinkOrCopy(sourceUcas, Path.Combine(input, Path.GetFileName(sourceUcas)));
        if (File.Exists(sourcePak))
            FileLinker.LinkOrCopy(sourcePak, Path.Combine(input, Path.GetFileName(sourcePak)));

        var globalUtoc = Path.Combine(_settings.GamePaksFolder, "global.utoc");
        var globalUcas = Path.Combine(_settings.GamePaksFolder, "global.ucas");
        RequireFile(globalUtoc, "game global.utoc");
        RequireFile(globalUcas, "game global.ucas");
        FileLinker.LinkOrCopy(globalUtoc, Path.Combine(input, "global.utoc"));
        FileLinker.LinkOrCopy(globalUcas, Path.Combine(input, "global.ucas"));
    }

    private static void MergeDumpValue(
        SortedDictionary<string, string> dump,
        string key,
        string value)
    {
        if (!dump.TryGetValue(key, out var current) || string.IsNullOrEmpty(current))
            dump[key] = value;
    }

    private static string FileMetadataFingerprint(string path)
    {
        var info = new FileInfo(path);
        return $"size:{info.Length};utc:{info.LastWriteTimeUtc.Ticks}";
    }

    private void ValidatePrerequisites(IReadOnlyCollection<ModScanResult> mods)
    {
        if (mods.Any(x => x.Assets.Count > 0))
        {
            RequireFile(_settings.RetocPath, "retoc.exe");
            RequireFile(_settings.UAssetGuiPath, "UAssetGUI.exe");
            RequireFile(_settings.MappingsPath, "Mappings.usmap");
            RequireFile(Path.Combine(_settings.GamePaksFolder, "global.utoc"), "game global.utoc");
            RequireFile(Path.Combine(_settings.GamePaksFolder, "global.ucas"), "game global.ucas");
        }

        if (mods.Any(x => string.Equals(x.ModId, "Game", StringComparison.OrdinalIgnoreCase)
                          && x.LocresAssets.Count > 0))
            RequireFile(_settings.RepakPath, "repak.exe");
    }

    private static void RequireFile(string path, string label)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required {label} not found: {path}", path);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch { }
    }
}

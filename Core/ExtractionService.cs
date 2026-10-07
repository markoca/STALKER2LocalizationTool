using LocalizationWorkbench.Models;
using LocalizationWorkbench.Services;

namespace LocalizationWorkbench.Core;

public sealed class ExtractionService
{
    private readonly AppSettings _settings;
    private readonly RetocService _retoc;
    private readonly RepakService _repak;
    private readonly UAssetGuiService _uassetGui;
    private readonly string _sourceRoot;
    private readonly Action<string>? _log;

    public ExtractionService(
        AppSettings settings,
        RetocService retoc,
        RepakService repak,
        UAssetGuiService uassetGui,
        Action<string>? log = null,
        string? sourceRoot = null)
    {
        _settings = settings;
        _retoc = retoc;
        _repak = repak;
        _uassetGui = uassetGui;
        _sourceRoot = string.IsNullOrWhiteSpace(sourceRoot) ? settings.ModsFolder : sourceRoot;
        _log = log;
    }

    public async Task ExtractAsync(
        IEnumerable<ModScanResult> mods,
        IProgress<(int Current, int Total, string Message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var eligible = mods
            .Where(mod => mod.HasLocalization
                && string.IsNullOrWhiteSpace(mod.ScanError))
            .ToList();

        // Process only new or changed mods whenever source extraction is pending.
        // Translation-file recovery is a separate, fallback-only phase.
        var pending = eligible
            .Where(mod => mod.NeedsExtraction)
            .ToList();
        var list = pending.Count > 0
            ? pending
            : eligible.Where(mod =>
                mod.UiStatus == ModUiStatus.MissingTranslation
                || CanRestoreTranslationsFromSource(mod))
                .ToList();

        // Only a real source extraction needs retoc/UAssetGUI/repak.
        // MissingTranslation is a Translations-recovery case and can be restored
        // directly from the already-current Source workspace.
        ValidatePrerequisites(
            list.Where(x => x.NeedsExtraction).ToList()
        );

        Directory.CreateDirectory(_settings.SourceFolder);

        for (var index = 0; index < list.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = list[index];

            if (!mod.NeedsExtraction
                && (mod.UiStatus == ModUiStatus.MissingTranslation
                    || CanRestoreTranslationsFromSource(mod)))
            {
                progress?.Report((
                    index,
                    list.Count,
                    $"Restoring {mod.ModName}"
                ));
                _log?.Invoke(
                    $"=== Restoring Translations files for {mod.ModName} ==="
                );
                RestoreMissingTranslationsWorkspace(mod);
                progress?.Report((
                    index + 1,
                    list.Count,
                    $"Restored {mod.ModName}"
                ));
                continue;
            }

            progress?.Report((index, list.Count, $"Extracting {mod.ModName}"));
            _log?.Invoke($"=== Extracting {mod.ModName} ===");
            await ExtractOneModAsync(mod, cancellationToken);
            progress?.Report((index + 1, list.Count, $"Extracted {mod.ModName}"));
        }
    }

    private async Task ExtractOneModAsync(ModScanResult mod, CancellationToken cancellationToken)
    {
        var stagingRoot = Path.Combine(
            _settings.SourceFolder,
            $".{mod.ModId}.staging.{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var manifest = new ExtractedManifest
            {
                ModId = mod.ModId,
                ModName = mod.ModName,
                SourceFingerprint = mod.SourceFingerprint,
                SourceContainerLabels = mod.ContainerLabels
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };

            var languageDumps = BuildLanguageCatalog.All.ToDictionary(
                language => language.Id,
                _ => new SortedDictionary<string, string>(StringComparer.Ordinal)
            );

            var isGame = string.Equals(mod.ModId, "Game", StringComparison.OrdinalIgnoreCase);
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

            var finalRoot = Path.Combine(_settings.SourceFolder, mod.ModId);
            if (Directory.Exists(finalRoot))
                Directory.Delete(finalRoot, recursive: true);
            Directory.Move(stagingRoot, finalRoot);

            try
            {
                SeedTranslationsWorkspace(finalRoot, mod.ModId);
            }
            catch
            {
                // Do not leave a current manifest behind when the initial Translations
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

    private bool CanRestoreTranslationsFromSource(ModScanResult mod)
    {
        var sourceRoot = Path.Combine(
            _settings.SourceFolder,
            mod.ModId
        );
        if (!Directory.Exists(sourceRoot))
            return false;

        var translationsRoot = Path.Combine(
            _settings.TranslationsFolder,
            mod.ModId
        );

        return BuildLanguageCatalog.All.Any(language =>
        {
            var fileName = language.Key + ".json";
            return File.Exists(Path.Combine(sourceRoot, fileName))
                   && !File.Exists(Path.Combine(translationsRoot, fileName));
        });
    }

    private void RestoreMissingTranslationsWorkspace(ModScanResult mod)
    {
        var sourceRoot = Path.Combine(
            _settings.SourceFolder,
            mod.ModId
        );
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException(
                $"Source extraction was not found for {mod.ModName}: {sourceRoot}"
            );
        }

        var translationsRoot = Path.Combine(
            _settings.TranslationsFolder,
            mod.ModId
        );
        Directory.CreateDirectory(translationsRoot);

        var restored = 0;
        foreach (var language in BuildLanguageCatalog.All)
        {
            var fileName = language.Key + ".json";
            var sourceFile = Path.Combine(sourceRoot, fileName);
            var destinationFile = Path.Combine(translationsRoot, fileName);

            if (!File.Exists(sourceFile) || File.Exists(destinationFile))
                continue;

            File.Copy(sourceFile, destinationFile, overwrite: false);
            restored++;
        }

        _log?.Invoke(
            $"Restored {restored} missing translation JSON file(s) from Source -> "
            + translationsRoot
        );
    }

    private void SeedTranslationsWorkspace(string sourceRoot, string modId)
    {
        var translationsRoot = Path.Combine(_settings.TranslationsFolder, modId);
        if (Directory.Exists(translationsRoot))
        {
            _log?.Invoke($"Translations workspace already exists; leaving existing JSONs unchanged -> {translationsRoot}");
            return;
        }

        Directory.CreateDirectory(_settings.TranslationsFolder);
        var stagingRoot = Path.Combine(
            _settings.TranslationsFolder,
            $".{modId}.staging.{Guid.NewGuid():N}"
        );

        try
        {
            Directory.CreateDirectory(stagingRoot);

            foreach (var language in BuildLanguageCatalog.All)
            {
                var fileName = language.Key + ".json";
                var sourceFile = Path.Combine(sourceRoot, fileName);
                RequireFile(sourceFile, $"extracted {language.EnglishName} translation JSON");
                File.Copy(
                    sourceFile,
                    Path.Combine(stagingRoot, fileName),
                    overwrite: false
                );
            }

            Directory.Move(stagingRoot, translationsRoot);
            _log?.Invoke($"Created Translations JSON workspace -> {translationsRoot}");
        }
        catch
        {
            TryDeleteDirectory(stagingRoot);
            throw;
        }
    }

    private async Task ExtractDatabaseAssetsAsync(
        ModScanResult mod,
        ExtractedManifest manifest,
        Dictionary<int, SortedDictionary<string, string>> languageDumps,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        var sourceUtocs = mod.Assets
            .SelectMany(group => group.Aliases)
            .Select(alias => alias.SourceUtoc)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var extractedContainers = new Dictionary<string, RetocExtractedContainer>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (string.Equals(
                    mod.SourceKind,
                    "archive",
                    StringComparison.OrdinalIgnoreCase))
            {
                await ModSourceDiscovery
                    .EnsureArchiveContainersMaterializedAsync(
                        _sourceRoot,
                        Path.Combine(
                            _settings.SourceFolder,
                            ".source_cache"
                        ),
                        mod.SourceLabel,
                        sourceUtocs,
                        _log,
                        cancellationToken
                    );
            }
            // Extract every alias source first. The current launch.py baseline resolves
            // NewContent and OverrideContent only after inspecting the actual database
            // payloads, so we must not throw either side away before this point.
            foreach (var sourceUtoc in sourceUtocs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workRoot = Path.Combine(
                    stagingRoot,
                    ".work",
                    "db_" + Guid.NewGuid().ToString("N")
                );
                var fallbackInput = Path.Combine(workRoot, "input");
                var input = CreateRetocInputDirectory(
                    mod,
                    sourceUtoc,
                    fallbackInput
                );
                var legacy = Path.Combine(workRoot, "legacy");
                var json = Path.Combine(workRoot, "json");
                Directory.CreateDirectory(legacy);
                Directory.CreateDirectory(json);

                try
                {
                    var inputTimer = Stopwatch.StartNew();
                    await PrepareRetocInputAsync(
                        sourceUtoc,
                        input,
                        cancellationToken
                    );
                    inputTimer.Stop();
                    _log?.Invoke(
                        $"Prepared retoc input for {Path.GetFileName(sourceUtoc)} "
                        + $"in {inputTimer.Elapsed.TotalSeconds:N1}s"
                    );

                    var retocTimer = Stopwatch.StartNew();
                    await _retoc.ToLegacyAsync(
                        input,
                        legacy,
                        AppConstants.LocalizationDatabaseNeedle,
                        cancellationToken
                    );
                    retocTimer.Stop();
                    _log?.Invoke(
                        $"Converted localization assets from "
                        + $"{Path.GetFileName(sourceUtoc)} in "
                        + $"{retocTimer.Elapsed.TotalSeconds:N1}s"
                    );
                }
                finally
                {
                    TryDeleteDirectory(input);
                }

                var scriptObjects = Path.Combine(legacy, "scriptobjects.bin");
                RequireFile(scriptObjects, "retoc scriptobjects.bin");
                extractedContainers[sourceUtoc] = new RetocExtractedContainer
                {
                    WorkRoot = workRoot,
                    LegacyRoot = legacy,
                    JsonRoot = json,
                    ScriptObjects = scriptObjects,
                };
            }

            foreach (var assetGroup in mod.Assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parsedAliases = new List<ParsedAliasAsset>();

                foreach (var alias in assetGroup.Aliases)
                {
                    if (!extractedContainers.TryGetValue(alias.SourceUtoc, out var extracted))
                        throw new InvalidDataException($"No extracted container workspace for {alias.SourceUtoc}");

                    var legacyRelative = PathUtil.NormalizeVirtualPath(alias.VirtualPath);
                    var extractedUasset = Path.Combine(extracted.LegacyRoot, legacyRelative);
                    var extractedUexp = Path.ChangeExtension(extractedUasset, ".uexp");
                    RequireFile(extractedUasset, alias.VirtualPath);
                    RequireFile(extractedUexp, alias.VirtualPath + " .uexp");

                    var jsonName = $"{assetGroup.ZenChunkId}_{parsedAliases.Count:00}.json";
                    var assetJson = Path.Combine(extracted.JsonRoot, jsonName);

                    var uassetTimer = Stopwatch.StartNew();
                    await _uassetGui.ToJsonAsync(
                        extractedUasset,
                        assetJson,
                        cancellationToken
                    );
                    uassetTimer.Stop();
                    _log?.Invoke(
                        $"UAssetGUI tojson: {Path.GetFileName(extractedUasset)} "
                        + $"in {uassetTimer.Elapsed.TotalSeconds:N1}s"
                    );

                    var inspectTimer = Stopwatch.StartNew();
                    _log?.Invoke(
                        $"Inspecting UAssetGUI JSON: "
                        + $"{Path.GetFileName(assetJson)} "
                        + $"({new FileInfo(assetJson).Length / (1024d * 1024d):N1} MiB)"
                    );
                    var export = UAssetInspector.ReadLocalizationExport(
                        assetJson,
                        alias.VirtualPath
                    );
                    inspectTimer.Stop();
                    _log?.Invoke(
                        $"UAssetGUI JSON inspected in "
                        + $"{inspectTimer.Elapsed.TotalSeconds:N1}s; "
                        + $"payload={export.Payload.Length / (1024d * 1024d):N1} MiB"
                    );

                    if (!export.ImportsLocalizationDatabaseClass)
                    {
                        throw new InvalidDataException(
                            $"{alias.VirtualPath}: asset does not import "
                            + "ModLocalizationDatabaseDataAsset"
                        );
                    }

                    var parseTimer = Stopwatch.StartNew();
                    var parsed = LocalizationDatabaseCodec.Parse(
                        export.Payload,
                        alias.VirtualPath
                    );
                    parseTimer.Stop();
                    _log?.Invoke(
                        $"Localization database parsed in "
                        + $"{parseTimer.Elapsed.TotalSeconds:N1}s; "
                        + $"records={parsed.Records.Count:N0}"
                    );

                    var internalPackagePath = export.InternalPackagePath;

                    parsedAliases.Add(new ParsedAliasAsset
                    {
                        Alias = alias,
                        LegacyRelativePath = legacyRelative,
                        UassetPath = extractedUasset,
                        UexpPath = extractedUexp,
                        AssetJsonPath = assetJson,
                        ScriptObjectsPath = extracted.ScriptObjects,
                        Parsed = parsed,
                        InternalPackagePath = internalPackagePath,
                        DirectoryAliasPackagePath = PathUtil.DirectoryAliasPackagePathFromVirtualPath(alias.VirtualPath),
                    });
                }

                if (parsedAliases.Count == 0)
                    continue;

                var sidUnion = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in parsedAliases)
                    sidUnion.UnionWith(item.Parsed.Records.Select(record => record.Sid));

                var complete = parsedAliases
                    .Where(item => new HashSet<string>(
                        item.Parsed.Records.Select(record => record.Sid),
                        StringComparer.Ordinal
                    ).SetEquals(sidUnion))
                    .ToList();

                if (complete.Count == 0)
                {
                    var details = string.Join(
                        Environment.NewLine,
                        parsedAliases.Select(item =>
                            $"  {item.Alias.SourceUtocRelative}: SIDs={item.Parsed.Records.Count} :: {item.Alias.VirtualPath}")
                    );
                    throw new InvalidDataException(
                        $"Aliases of LocalizationDatabase chunk {assetGroup.ZenChunkId} expose incompatible SID sets:{Environment.NewLine}{details}"
                    );
                }

                var allOverrideAliases = parsedAliases
                    .Where(item => PathUtil.IsBaseContentAlias(item.Alias.VirtualPath))
                    .ToList();
                var completeOverrideAliases = complete
                    .Where(item => PathUtil.IsBaseContentAlias(item.Alias.VirtualPath))
                    .ToList();

                if (allOverrideAliases.Count > 0 && completeOverrideAliases.Count == 0)
                {
                    var details = string.Join(
                        Environment.NewLine,
                        parsedAliases.Select(item =>
                            $"  {item.Alias.SourceUtocRelative}: SIDs={item.Parsed.Records.Count} :: {item.Alias.VirtualPath}")
                    );
                    throw new InvalidDataException(
                        "OverrideContent alias exists but does not contain the complete LocalizationDatabase SID set; " +
                        $"refusing a plugin-path fallback for chunk {assetGroup.ZenChunkId}:{Environment.NewLine}{details}"
                    );
                }

                var canonical = complete
                    .OrderBy(item => PathUtil.IsBaseContentAlias(item.Alias.VirtualPath) ? 0 : 1)
                    .ThenBy(item => PathUtil.IsOverrideContentContainer(item.Alias.SourceUtocRelative) ? 0 : 1)
                    .ThenBy(item => item.Alias.SourceUtocRelative, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Alias.VirtualPath, StringComparer.OrdinalIgnoreCase)
                    .First();

                var pluginSourcePaths = parsedAliases
                    .Where(item => PathUtil.IsPluginContentAlias(item.Alias.VirtualPath))
                    .Select(item => item.DirectoryAliasPackagePath)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (pluginSourcePaths.Count > 1)
                {
                    throw new InvalidDataException(
                        $"LocalizationDatabase chunk {assetGroup.ZenChunkId} maps to multiple plugin package identities:" +
                        Environment.NewLine + string.Join(Environment.NewLine, pluginSourcePaths.Select(path => "  " + path))
                    );
                }

                string sourcePackageIdentityPath;
                if (pluginSourcePaths.Count == 1)
                {
                    sourcePackageIdentityPath = pluginSourcePaths[0];
                }
                else
                {
                    var internalCandidates = parsedAliases
                        .Select(item => item.InternalPackagePath)
                        .Where(path => !string.IsNullOrWhiteSpace(path) && path.StartsWith('/'))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (internalCandidates.Count != 1)
                    {
                        throw new InvalidDataException(
                            $"Could not determine one source Unreal package identity for chunk {assetGroup.ZenChunkId}:" +
                            Environment.NewLine + (internalCandidates.Count == 0
                                ? "  (none)"
                                : string.Join(Environment.NewLine, internalCandidates.Select(path => "  " + path)))
                        );
                    }
                    sourcePackageIdentityPath = internalCandidates[0];
                }

                if (!string.Equals(canonical.InternalPackagePath, sourcePackageIdentityPath, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"{canonical.Alias.VirtualPath}: source package identity does not match serializer-visible package name:" +
                        Environment.NewLine + $"  source identity : {sourcePackageIdentityPath}" +
                        Environment.NewLine + $"  serializer path : {canonical.InternalPackagePath}"
                    );
                }

                var assetFolderRelative = Path.Combine("assets", "database", assetGroup.ZenChunkId);
                var assetFolder = Path.Combine(stagingRoot, assetFolderRelative);
                Directory.CreateDirectory(assetFolder);

                var sourceUasset = Path.Combine(assetFolder, "source.uasset");
                var sourceUexp = Path.Combine(assetFolder, "source.uexp");
                var storedJson = Path.Combine(assetFolder, "asset.json");
                var storedScriptObjects = Path.Combine(assetFolder, "scriptobjects.bin");

                File.Copy(canonical.UassetPath, sourceUasset, overwrite: true);
                File.Copy(canonical.UexpPath, sourceUexp, overwrite: true);
                File.Copy(canonical.AssetJsonPath, storedJson, overwrite: true);
                File.Copy(canonical.ScriptObjectsPath, storedScriptObjects, overwrite: true);

                foreach (var record in canonical.Parsed.Records)
                {
                    foreach (var language in BuildLanguageCatalog.All)
                    {
                        var value = record.Translations
                            .FirstOrDefault(x => x.LanguageId == language.Id)?
                            .Value ?? string.Empty;
                        MergeDumpValue(languageDumps[language.Id], record.Sid, value);
                    }
                }

                var canonicalLegacyRelative = PathUtil.NormalizeVirtualPath(canonical.Alias.VirtualPath);
                manifest.Assets.Add(new ExtractedAssetManifest
                {
                    ZenChunkId = assetGroup.ZenChunkId,
                    DatabaseName = Path.GetFileNameWithoutExtension(canonical.Alias.VirtualPath),
                    VirtualPath = canonical.Alias.VirtualPath,
                    LegacyRelativePath = canonicalLegacyRelative,
                    SourceContainerRelativePath = canonical.Alias.SourceUtocRelative,
                    InternalPackagePath = canonical.InternalPackagePath,
                    SourcePackageIdentityPath = sourcePackageIdentityPath,
                    DirectoryAliasPackagePath = canonical.DirectoryAliasPackagePath,
                    Aliases = parsedAliases
                        .OrderBy(item => PathUtil.IsBaseContentAlias(item.Alias.VirtualPath) ? 0 : 1)
                        .ThenBy(item => item.Alias.SourceUtocRelative, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.Alias.VirtualPath, StringComparer.OrdinalIgnoreCase)
                        .Select(item => new AliasManifest
                        {
                            SourceContainerRelativePath = item.Alias.SourceUtocRelative,
                            VirtualPath = item.Alias.VirtualPath,
                            InternalPackagePath = item.InternalPackagePath,
                            DirectoryAliasPackagePath = item.DirectoryAliasPackagePath,
                            SidCount = item.Parsed.Records.Count,
                        })
                        .ToList(),
                    UassetFile = Path.Combine(assetFolderRelative, "source.uasset"),
                    UexpFile = Path.Combine(assetFolderRelative, "source.uexp"),
                    AssetJsonFile = Path.Combine(assetFolderRelative, "asset.json"),
                    ScriptObjectsFile = Path.Combine(assetFolderRelative, "scriptobjects.bin"),
                    SidCount = canonical.Parsed.Records.Count,
                });

                _log?.Invoke(
                    $"{mod.ModName}: {assetGroup.ZenChunkId} -> canonical {canonical.Alias.VirtualPath}; " +
                    $"aliases={parsedAliases.Count}, SIDs={canonical.Parsed.Records.Count}, " +
                    $"source identity={sourcePackageIdentityPath}, directory alias={canonical.DirectoryAliasPackagePath}"
                );
            }
        }
        finally
        {
            foreach (var extracted in extractedContainers.Values)
                TryDeleteDirectory(extracted.WorkRoot);
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

            var assetFolderRelative = Path.Combine("assets", "locres", index.ToString("000", CultureInfo.InvariantCulture));
            var assetFolder = Path.Combine(stagingRoot, assetFolderRelative);
            Directory.CreateDirectory(assetFolder);
            var storedLocres = Path.Combine(assetFolder, "source.locres");
            File.Copy(unpackedLocres, storedLocres, overwrite: true);

            var document = LocresCodec.Load(storedLocres);
            var sourceLanguage = BuildLanguageCatalog.All.FirstOrDefault(language =>
                string.Equals(language.LocresCulture, source.CultureCode, StringComparison.OrdinalIgnoreCase));
            if (sourceLanguage is null)
            {
                _log?.Invoke($"{source.InternalPath}: no translation language JSON mapping for culture '{source.CultureCode}'");
            }
            locresDocuments.Add((sourceLanguage, document));

            manifest.LocresAssets.Add(new ExtractedLocresManifest
            {
                InternalPath = source.InternalPath,
                CultureCode = source.CultureCode,
                SidCount = document.EntryCount,
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
                    var translationKey = namespaceCounts[entry.Key] > 1
                        ? $"{ns.Name}::{entry.Key}"
                        : entry.Key;

                    foreach (var languageDump in languageDumps.Values)
                        MergeDumpValue(languageDump, translationKey, string.Empty);

                    if (item.Language is not null)
                        MergeDumpValue(languageDumps[item.Language.Id], translationKey, entry.Value);
                }
            }
        }

        foreach (var root in unpacked.Values)
            TryDeleteDirectory(root);
    }

    private sealed class RetocExtractedContainer
    {
        public string WorkRoot { get; init; } = string.Empty;
        public string LegacyRoot { get; init; } = string.Empty;
        public string JsonRoot { get; init; } = string.Empty;
        public string ScriptObjects { get; init; } = string.Empty;
    }

    private sealed class ParsedAliasAsset
    {
        public LocalizationAlias Alias { get; init; } = new();
        public string LegacyRelativePath { get; init; } = string.Empty;
        public string UassetPath { get; init; } = string.Empty;
        public string UexpPath { get; init; } = string.Empty;
        public string AssetJsonPath { get; init; } = string.Empty;
        public string ScriptObjectsPath { get; init; } = string.Empty;
        public LocalizationPayload Parsed { get; init; } = new();
        public string InternalPackagePath { get; init; } = string.Empty;
        public string DirectoryAliasPackagePath { get; init; } = string.Empty;
    }

    private string CreateRetocInputDirectory(
        ModScanResult mod,
        string sourceUtoc,
        string fallbackInput)
    {
        if (string.Equals(
                mod.SourceKind,
                "loose",
                StringComparison.OrdinalIgnoreCase))
        {
            var sourceDirectory = Path.GetDirectoryName(sourceUtoc);
            if (!string.IsNullOrWhiteSpace(sourceDirectory))
            {
                var nearSource = Path.Combine(
                    sourceDirectory,
                    ".localization-workbench-input-"
                    + Guid.NewGuid().ToString("N")
                );

                try
                {
                    Directory.CreateDirectory(nearSource);
                    return nearSource;
                }
                catch (Exception ex)
                {
                    _log?.Invoke(
                        $"Could not create retoc input beside loose source; "
                        + $"using cache workspace instead: {ex.Message}"
                    );
                }
            }
        }

        Directory.CreateDirectory(fallbackInput);
        return fallbackInput;
    }

    private async Task PrepareRetocInputAsync(
        string sourceUtoc,
        string input,
        CancellationToken cancellationToken)
    {
        var sourceUcas = Path.ChangeExtension(sourceUtoc, ".ucas");
        var sourcePak = Path.ChangeExtension(sourceUtoc, ".pak");
        RequireFile(sourceUtoc, "source .utoc");
        RequireFile(sourceUcas, "source .ucas");

        await LinkRetocInputAsync(
            sourceUtoc,
            Path.Combine(input, Path.GetFileName(sourceUtoc)),
            cancellationToken
        );
        await LinkRetocInputAsync(
            sourceUcas,
            Path.Combine(input, Path.GetFileName(sourceUcas)),
            cancellationToken
        );

        if (File.Exists(sourcePak))
        {
            await LinkRetocInputAsync(
                sourcePak,
                Path.Combine(input, Path.GetFileName(sourcePak)),
                cancellationToken
            );
        }

        RequireValidGamePaksFolder();
        var globalUtoc = Path.Combine(
            _settings.GamePaksFolder,
            "global.utoc"
        );
        var globalUcas = Path.Combine(
            _settings.GamePaksFolder,
            "global.ucas"
        );

        await LinkRetocInputAsync(
            globalUtoc,
            Path.Combine(input, "global.utoc"),
            cancellationToken
        );
        await LinkRetocInputAsync(
            globalUcas,
            Path.Combine(input, "global.ucas"),
            cancellationToken
        );
    }

    private async Task LinkRetocInputAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        var linked = await FileLinker.LinkOrCopyAsync(
            source,
            destination,
            cancellationToken
        );

        if (!linked)
        {
            var sizeMiB = new FileInfo(source).Length / (1024d * 1024d);
            _log?.Invoke(
                $"Copied retoc input because linking was unavailable: "
                + $"{Path.GetFileName(source)} ({sizeMiB:N1} MiB)"
            );
        }
    }

    private static void MergeDumpValue(
        SortedDictionary<string, string> dump,
        string key,
        string value)
    {
        if (!dump.TryGetValue(key, out var current) || string.IsNullOrEmpty(current))
            dump[key] = value;
    }

    private void ValidatePrerequisites(IReadOnlyCollection<ModScanResult> mods)
    {
        if (mods.Any(x => x.Assets.Count > 0))
        {
            RequireFile(_settings.RetocPath, "retoc.exe");
            RequireFile(_settings.UAssetGuiPath, "UAssetGUI.exe");
            RequireFile(_settings.MappingsPath, "Mappings.usmap");
            RequireValidGamePaksFolder();
        }

        if (mods.Any(x => string.Equals(x.ModId, "Game", StringComparison.OrdinalIgnoreCase)
                          && x.LocresAssets.Count > 0))
            RequireFile(_settings.RepakPath, "repak.exe");
    }

    private void RequireValidGamePaksFolder()
    {
        if (string.IsNullOrWhiteSpace(_settings.GamePaksFolder)
            || !Directory.Exists(_settings.GamePaksFolder)
            || !File.Exists(Path.Combine(_settings.GamePaksFolder, "global.utoc"))
            || !File.Exists(Path.Combine(_settings.GamePaksFolder, "global.ucas")))
        {
            throw new InvalidOperationException(
                "Game Paks folder is not valid. Set the Game Paks folder in Settings."
            );
        }
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

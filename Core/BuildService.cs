using STALKER2LocalizationTool.Models;
using STALKER2LocalizationTool.Services;

namespace STALKER2LocalizationTool.Core;

public sealed class BuildService
{
    private readonly AppSettings _settings;
    private readonly RetocService _retoc;
    private readonly RepakService _repak;
    private readonly S2HocmmService _s2Hocmm;
    private readonly UAssetGuiService _uassetGui;
    private readonly Action<string>? _log;

    public BuildService(
        AppSettings settings,
        RetocService retoc,
        RepakService repak,
        UAssetGuiService uassetGui,
        Action<string>? log = null)
    {
        _settings = settings;
        _retoc = retoc;
        _repak = repak;
        _uassetGui = uassetGui;
        _s2Hocmm = new S2HocmmService(settings.S2HocmmPath, settings.RepakPath, log);
        _log = log;
    }

    public async Task<List<ModBuildResult>> BuildAllEditableAsync(
        IEnumerable<ModScanResult> mods,
        int targetLanguageId,
        BuildMode mode,
        IProgress<(int Current, int Total, string Message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var available = mods
            .Where(x => x.UiStatus is ModUiStatus.Available or ModUiStatus.Extracted
                        && !string.IsNullOrWhiteSpace(x.EditableTranslationFile))
            .ToList();
        ValidatePrerequisites(available, mode);

        if (mode == BuildMode.AllInOne)
            return await BuildAllInOneAsync(available, targetLanguageId, progress, cancellationToken);

        var results = new List<ModBuildResult>();
        for (var i = 0; i < available.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = available[i];
            progress?.Report((i, available.Count, $"Building {mod.ModName}"));
            _log?.Invoke($"=== Building {mod.ModName} ===");
            results.Add(await BuildOneAsync(mod, targetLanguageId, cancellationToken));
            progress?.Report((i + 1, available.Count, $"Built {mod.ModName}"));
        }
        return results;
    }

    private async Task<ModBuildResult> BuildOneAsync(ModScanResult mod, int targetLanguageId, CancellationToken cancellationToken)
    {
        var result = new ModBuildResult { ModId = mod.ModId, ModName = mod.ModName };
        var cachedRoot = Path.Combine(_settings.CachedFolder, mod.ModId);
        var manifest = LoadAndValidateManifest(mod, cachedRoot);
        var translations = LoadTranslations(mod);

        var language = BuildLanguageCatalog.ById(targetLanguageId);
        _log?.Invoke(
            $"{mod.ModName} / {language.EnglishName}: Editable JSON {mod.EditableTranslationFile} " +
            $"({translations.Count} entries)"
        );
        var outputModRoot = Path.Combine(
            _settings.OutputFolder,
            PathUtil.MakeSafeName(language.EnglishName),
            PathUtil.MakeSafeName(mod.ModName)
        );
        var workRoot = Path.Combine(outputModRoot, ".work");

        if (Directory.Exists(outputModRoot))
            Directory.Delete(outputModRoot, recursive: true);
        Directory.CreateDirectory(outputModRoot);
        Directory.CreateDirectory(workRoot);

        if (manifest.Assets.Count > 0)
            await BuildDatabaseOverlayAsync(manifest, translations, language, cachedRoot, outputModRoot, workRoot, result, cancellationToken);

        if (string.Equals(manifest.ModId, "Game", StringComparison.OrdinalIgnoreCase)
            && manifest.LocresAssets.Count > 0)
        {
            await BuildLocresOverlayAsync(
                manifest,
                translations,
                language,
                cachedRoot,
                outputModRoot,
                workRoot,
                result,
                cancellationToken
            );
        }

        TryDeleteDirectory(workRoot);

        result.Built = result.OutputFiles.Count > 0;
        result.Verified = true;
        if (!result.Built)
        {
            result.Message = "No translation keys matched buildable localization entries for the selected language.";
            TryDeleteDirectory(outputModRoot);
        }
        else
        {
            result.Message = $"Built and verified {result.OutputFiles.Count} output file(s).";
        }

        return result;
    }

    private static ExtractedManifest LoadAndValidateManifest(ModScanResult mod, string cachedRoot)
    {
        var manifestPath = Path.Combine(cachedRoot, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Cached manifest not found for {mod.ModName}", manifestPath);

        var manifest = JsonUtil.Load<ExtractedManifest>(manifestPath);
        if (manifest.SchemaVersion != AppConstants.ManifestSchemaVersion)
            throw new InvalidDataException($"{mod.ModName}: cached files use an older workspace format. Extract again first.");
        if (!string.Equals(manifest.SourceFingerprint, mod.SourceFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{mod.ModName}: cached files are from an older source version. Extract again first.");
        return manifest;
    }

    private static Dictionary<string, string> LoadTranslations(ModScanResult mod)
    {
        var translationPath = mod.EditableTranslationFile
                              ?? throw new FileNotFoundException($"Editable language JSON not found for {mod.ModName}");
        var translations = EditableScanner.LoadFlatTranslations(translationPath);
        if (translations.Count == 0)
            throw new InvalidDataException($"{mod.ModName}: language JSON is empty: {translationPath}");
        return translations;
    }

    private async Task<List<ModBuildResult>> BuildAllInOneAsync(
        IReadOnlyList<ModScanResult> available,
        int targetLanguageId,
        IProgress<(int Current, int Total, string Message)>? progress,
        CancellationToken cancellationToken)
    {
        var language = BuildLanguageCatalog.ById(targetLanguageId);
        var outputRoot = Path.Combine(
            _settings.OutputFolder,
            PathUtil.MakeSafeName(language.EnglishName),
            "All-in-One"
        );
        var workRoot = Path.Combine(outputRoot, ".work");
        var legacyRoot = Path.Combine(workRoot, "database", "legacy");

        if (Directory.Exists(outputRoot))
            Directory.Delete(outputRoot, recursive: true);
        Directory.CreateDirectory(legacyRoot);

        var results = new List<ModBuildResult>();
        var progressTotal = Math.Max(1, available.Count + 2);
        var expectedPackages = new Dictionary<string, ExpectedDatabasePackage>(StringComparer.OrdinalIgnoreCase);
        var pathOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var chunkOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sourceContainerLabels = new List<string>();
        var scriptObjectsCopied = false;

        for (var i = 0; i < available.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = available[i];
            progress?.Report((i, progressTotal, $"Adding {mod.ModName}"));
            _log?.Invoke($"=== Adding {mod.ModName} to All-in-One ===");

            var result = new ModBuildResult { ModId = mod.ModId, ModName = mod.ModName };
            results.Add(result);

            var cachedRoot = Path.Combine(_settings.CachedFolder, mod.ModId);
            var manifest = LoadAndValidateManifest(mod, cachedRoot);
            sourceContainerLabels.AddRange(manifest.SourceContainerLabels);
            var translations = LoadTranslations(mod);

            foreach (var asset in manifest.Assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateAssetIdentityMetadata(asset, mod.ModName);

                var sourceUasset = Path.Combine(cachedRoot, asset.UassetFile);
                var sourceUexp = Path.Combine(cachedRoot, asset.UexpFile);
                var sourceJson = Path.Combine(cachedRoot, asset.AssetJsonFile);
                var scriptObjects = Path.Combine(cachedRoot, asset.ScriptObjectsFile);

                RequireFile(sourceUasset, "pristine extracted .uasset");
                RequireFile(sourceUexp, "pristine extracted .uexp");
                RequireFile(sourceJson, "UAssetGUI JSON");
                RequireFile(scriptObjects, "scriptobjects.bin");

                var assetTimer = Stopwatch.StartNew();
                _log?.Invoke(
                    $"{mod.ModName} / {asset.DatabaseName}: "
                    + "reading cached LocalizationDatabase for All-in-One..."
                );

                var export = UAssetInspector.ReadLocalizationExport(
                    sourceJson,
                    asset.VirtualPath
                );

                _log?.Invoke(
                    $"{mod.ModName} / {asset.DatabaseName}: "
                    + $"cached database loaded in "
                    + $"{assetTimer.Elapsed.TotalSeconds:N1}s; "
                    + $"payload={export.Payload.Length / (1024d * 1024d):N1} MiB"
                );

                var patchTimer = Stopwatch.StartNew();
                var patch = LocalizationDatabaseCodec.Patch(
                    export.Payload,
                    translations,
                    language.Id,
                    asset.VirtualPath
                );
                patchTimer.Stop();

                _log?.Invoke(
                    $"{mod.ModName} / {asset.DatabaseName}: "
                    + $"localization patch prepared in "
                    + $"{patchTimer.Elapsed.TotalSeconds:N1}s; "
                    + $"matched={patch.MatchedSids.Count}, "
                    + $"changed={patch.ChangedSids.Count}"
                );

                result.MatchedSids += patch.MatchedSids.Count;
                result.ChangedSids += patch.ChangedSids.Count;

                // Match current launch.py: an already-correct source needs no physical
                // overlay asset. Only a real Serbian-slot change enters the output.
                if (patch.ChangedSids.Count == 0)
                    continue;

                var identityPath = PathUtil.NormalizeVirtualPathForComparison(asset.VirtualPath);
                if (pathOwners.TryGetValue(identityPath, out var pathOwner))
                {
                    throw new InvalidDataException(
                        $"All-in-One path collision: {asset.VirtualPath} is provided by both " +
                        $"'{pathOwner}' and '{mod.ModName}'."
                    );
                }

                if (chunkOwners.TryGetValue(asset.ZenChunkId, out var chunkOwner))
                {
                    throw new InvalidDataException(
                        $"All-in-One chunk collision: {asset.ZenChunkId} is provided by both " +
                        $"'{chunkOwner}' and '{mod.ModName}'."
                    );
                }

                pathOwners[identityPath] = mod.ModName;
                chunkOwners[asset.ZenChunkId] = mod.ModName;

                var outputUasset = Path.Combine(legacyRoot, asset.LegacyRelativePath);
                var packageTimer = Stopwatch.StartNew();
                var patchInfo = PackagePatcher.PatchLegacyPackage(
                    sourceUasset,
                    sourceUexp,
                    export,
                    patch.Payload,
                    outputUasset,
                    asset.VirtualPath
                );
                packageTimer.Stop();
                _log?.Invoke(
                    $"{mod.ModName} / {asset.DatabaseName}: "
                    + $"legacy package patched in "
                    + $"{packageTimer.Elapsed.TotalSeconds:N1}s"
                );

                _log?.Invoke(
                    $"{mod.ModName} / {asset.DatabaseName}: SerialSize {patchInfo.OldSerialSize} -> " +
                    $"{patchInfo.NewSerialSize}; matched {patch.MatchedSids.Count}, changed {patch.ChangedSids.Count}"
                );

                var outputUexp = Path.ChangeExtension(outputUasset, ".uexp");
                PackagePatcher.VerifyPayloadOccurrence(outputUexp, patch.Payload, asset.VirtualPath);
                LocalizationDatabaseCodec.VerifyExpectedValues(
                    patch.Payload,
                    language.Id,
                    patch.ExpectedValues,
                    asset.VirtualPath
                );

                if (!scriptObjectsCopied)
                {
                    File.Copy(scriptObjects, Path.Combine(legacyRoot, "scriptobjects.bin"), overwrite: true);
                    scriptObjectsCopied = true;
                }

                expectedPackages[identityPath] = ExpectedDatabasePackage.From(asset, patch.Payload, mod.ModName);
                result.AssetsPatched++;
            }

            progress?.Report((i + 1, progressTotal, $"Added {mod.ModName}"));
        }

        if (expectedPackages.Count == 0)
        {
            foreach (var result in results)
            {
                result.Verified = true;
                result.Message ??= "No Editable translation values required LocalizationDatabase changes.";
            }
            TryDeleteDirectory(outputRoot);
            return results;
        }

        var allInOnePatchSuffix = PathUtil.GetOverlayPatchSuffix(sourceContainerLabels);
        var outputUtoc = Path.Combine(
            outputRoot,
            $"{AppConstants.OverlayPrefix}_All_In_One{allInOnePatchSuffix}.utoc"
        );
        _log?.Invoke($"All-in-One overlay patch suffix: {allInOnePatchSuffix}");
        progress?.Report((available.Count, progressTotal, "Packaging All-in-One"));
        await _retoc.ToZenAsync(legacyRoot, outputUtoc, cancellationToken);

        RequireFile(outputUtoc, "built All-in-One database .utoc");
        RequireFile(Path.ChangeExtension(outputUtoc, ".ucas"), "built All-in-One database .ucas");
        RequireFile(Path.ChangeExtension(outputUtoc, ".pak"), "built All-in-One database .pak");

        progress?.Report((available.Count + 1, progressTotal, "Verifying All-in-One"));
        await VerifyFinishedPackagesAsync(
            outputUtoc,
            expectedPackages,
            Path.Combine(workRoot, "database"),
            cancellationToken
        );
        progress?.Report((progressTotal, progressTotal, "All-in-One verified"));

        var sharedFiles = new[]
        {
            outputUtoc,
            Path.ChangeExtension(outputUtoc, ".ucas"),
            Path.ChangeExtension(outputUtoc, ".pak"),
        };

        foreach (var result in results)
        {
            result.Verified = true;
            result.Built = result.AssetsPatched > 0;
            if (result.Built)
            {
                result.OutputUtoc = outputUtoc;
                result.OutputFiles.AddRange(sharedFiles);
                result.Message = $"Included {result.AssetsPatched} verified localization package(s) in the All-in-One overlay.";
            }
            else
            {
                result.Message ??= "No Editable translation values required LocalizationDatabase changes; not included.";
            }
        }

        TryDeleteDirectory(workRoot);
        _log?.Invoke($"All-in-One database package built & verified: {outputUtoc}");
        return results;
    }

    private async Task BuildDatabaseOverlayAsync(
        ExtractedManifest manifest,
        Dictionary<string, string> translations,
        BuildLanguage language,
        string cachedRoot,
        string outputModRoot,
        string workRoot,
        ModBuildResult result,
        CancellationToken cancellationToken)
    {
        var legacyRoot = Path.Combine(workRoot, "database", "legacy");
        Directory.CreateDirectory(legacyRoot);

        var expectedPackages = new Dictionary<string, ExpectedDatabasePackage>(StringComparer.OrdinalIgnoreCase);
        var scriptObjectsCopied = false;
        var matchedTotal = 0;
        var changedTotal = 0;
        var assetsPatched = 0;

        foreach (var asset in manifest.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAssetIdentityMetadata(asset, manifest.ModName);

            var sourceUasset = Path.Combine(cachedRoot, asset.UassetFile);
            var sourceUexp = Path.Combine(cachedRoot, asset.UexpFile);
            var sourceJson = Path.Combine(cachedRoot, asset.AssetJsonFile);
            var scriptObjects = Path.Combine(cachedRoot, asset.ScriptObjectsFile);

            RequireFile(sourceUasset, "pristine extracted .uasset");
            RequireFile(sourceUexp, "pristine extracted .uexp");
            RequireFile(sourceJson, "UAssetGUI JSON");
            RequireFile(scriptObjects, "scriptobjects.bin");

            var assetTimer = Stopwatch.StartNew();
            _log?.Invoke(
                $"{manifest.ModName} / {asset.DatabaseName}: "
                + "reading cached LocalizationDatabase..."
            );

            var export = UAssetInspector.ReadLocalizationExport(
                sourceJson,
                asset.VirtualPath
            );

            var inspectElapsed = assetTimer.Elapsed;
            _log?.Invoke(
                $"{manifest.ModName} / {asset.DatabaseName}: "
                + $"cached database loaded in "
                + $"{inspectElapsed.TotalSeconds:N1}s; "
                + $"payload={export.Payload.Length / (1024d * 1024d):N1} MiB"
            );

            var patchTimer = Stopwatch.StartNew();
            var patch = LocalizationDatabaseCodec.Patch(
                export.Payload,
                translations,
                language.Id,
                asset.VirtualPath
            );
            patchTimer.Stop();

            _log?.Invoke(
                $"{manifest.ModName} / {asset.DatabaseName}: "
                + $"localization patch prepared in "
                + $"{patchTimer.Elapsed.TotalSeconds:N1}s; "
                + $"matched={patch.MatchedSids.Count}, "
                + $"changed={patch.ChangedSids.Count}"
            );

            matchedTotal += patch.MatchedSids.Count;
            changedTotal += patch.ChangedSids.Count;

            // Match the current launch.py baseline: only databases whose target
            // Serbian slot actually changes need a physical override package.
            if (patch.ChangedSids.Count == 0)
                continue;

            assetsPatched++;
            var outputUasset = Path.Combine(legacyRoot, asset.LegacyRelativePath);
            var packageTimer = Stopwatch.StartNew();
            var patchInfo = PackagePatcher.PatchLegacyPackage(
                sourceUasset,
                sourceUexp,
                export,
                patch.Payload,
                outputUasset,
                asset.VirtualPath
            );
            packageTimer.Stop();
            _log?.Invoke(
                $"{manifest.ModName} / {asset.DatabaseName}: "
                + $"legacy package patched in "
                + $"{packageTimer.Elapsed.TotalSeconds:N1}s"
            );

            _log?.Invoke(
                $"{asset.DatabaseName}: SerialSize {patchInfo.OldSerialSize} -> {patchInfo.NewSerialSize}; " +
                $"matched {patch.MatchedSids.Count}, changed {patch.ChangedSids.Count}; " +
                $"source identity={asset.SourcePackageIdentityPath}; alias={asset.DirectoryAliasPackagePath}"
            );

            var outputUexp = Path.ChangeExtension(outputUasset, ".uexp");
            PackagePatcher.VerifyPayloadOccurrence(outputUexp, patch.Payload, asset.VirtualPath);
            LocalizationDatabaseCodec.VerifyExpectedValues(
                patch.Payload,
                language.Id,
                patch.ExpectedValues,
                asset.VirtualPath
            );

            if (!scriptObjectsCopied)
            {
                File.Copy(scriptObjects, Path.Combine(legacyRoot, "scriptobjects.bin"), overwrite: true);
                scriptObjectsCopied = true;
            }

            var normalizedVirtualPath = PathUtil.NormalizeVirtualPathForComparison(asset.VirtualPath);
            if (expectedPackages.ContainsKey(normalizedVirtualPath))
                throw new InvalidDataException($"{manifest.ModName}: duplicate canonical output path: {asset.VirtualPath}");
            expectedPackages[normalizedVirtualPath] = ExpectedDatabasePackage.From(asset, patch.Payload, manifest.ModName);
        }

        result.AssetsPatched = assetsPatched;
        result.MatchedSids = matchedTotal;
        result.ChangedSids = changedTotal;

        if (assetsPatched == 0)
        {
            _log?.Invoke($"{manifest.ModName}: no Editable values required LocalizationDatabase changes; database output skipped.");
            return;
        }

        var safe = Regex.Replace(PathUtil.MakeSafeName(manifest.ModName), @"[^A-Za-z0-9._-]+", "_");
        var patchSuffix = PathUtil.GetOverlayPatchSuffix(manifest.SourceContainerLabels);
        var outputUtoc = Path.Combine(
            outputModRoot,
            $"{AppConstants.OverlayPrefix}_{safe}{patchSuffix}.utoc"
        );
        _log?.Invoke($"{manifest.ModName}: overlay patch suffix {patchSuffix}");
        await _retoc.ToZenAsync(legacyRoot, outputUtoc, cancellationToken);

        RequireFile(outputUtoc, "built database .utoc");
        RequireFile(Path.ChangeExtension(outputUtoc, ".ucas"), "built database .ucas");
        RequireFile(Path.ChangeExtension(outputUtoc, ".pak"), "built database .pak");

        await VerifyFinishedPackagesAsync(
            outputUtoc,
            expectedPackages,
            Path.Combine(workRoot, "database"),
            cancellationToken
        );

        result.OutputUtoc = outputUtoc;
        result.OutputFiles.Add(outputUtoc);
        result.OutputFiles.Add(Path.ChangeExtension(outputUtoc, ".ucas"));
        result.OutputFiles.Add(Path.ChangeExtension(outputUtoc, ".pak"));
        _log?.Invoke($"Database overlay built & verified: {outputUtoc}");
    }

    private async Task BuildLocresOverlayAsync(
        ExtractedManifest manifest,
        Dictionary<string, string> translations,
        BuildLanguage language,
        string cachedRoot,
        string outputModRoot,
        string workRoot,
        ModBuildResult result,
        CancellationToken cancellationToken)
    {
        // LOCRES is exclusively a GAME workflow. MODS never scan, extract or build
        // Game.locres; they are LocalizationDatabase/IoStore-only.
        if (!string.Equals(manifest.ModId, "Game", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LOCRES build was requested for a non-GAME source.");

        await BuildBaseGameLocresFromEditableAsync(
            manifest,
            translations,
            language,
            outputModRoot,
            workRoot,
            result,
            cancellationToken
        );
    }

    private async Task BuildBaseGameLocresFromEditableAsync(
        ExtractedManifest manifest,
        IReadOnlyDictionary<string, string> translations,
        BuildLanguage language,
        string outputModRoot,
        string workRoot,
        ModBuildResult result,
        CancellationToken cancellationToken)
    {
        var flatLocres = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var emptyEntries = 0;

        foreach (var pair in translations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // launch.py's normalize_s2hocmm_input() intentionally omits empty/null
            // values. EditableScanner already normalizes null/non-string JSON values to
            // strings, so only the empty-string filter is needed here.
            if (string.IsNullOrEmpty(pair.Value))
            {
                emptyEntries++;
                continue;
            }

            flatLocres[pair.Key] = pair.Value;
        }

        if (flatLocres.Count == 0)
            throw new InvalidDataException(
                $"{manifest.ModName}: Editable/Game/{language.Key}.json has no non-empty localization values."
            );

        _log?.Invoke(
            $"{manifest.ModName}: authoritative Editable LOCRES -> {language.LocresCulture}.json; " +
            $"editable={translations.Count}, non-empty={flatLocres.Count}, empty-skipped={emptyEntries}"
        );

        var locresWork = Path.Combine(workRoot, "locres");
        var generatedLocres = await _s2Hocmm.BuildGameLocresAsync(
            flatLocres,
            language.LocresCulture,
            locresWork,
            cancellationToken
        );
        RequireFile(generatedLocres, "S2HOCMM-generated Game.locres");
        VerifyS2HocmmLocres(flatLocres, generatedLocres, manifest.ModName);

        // Keep GAME package naming/load order aligned with the known-good
        // launch.py release convention: six leading z characters and _P.pak.
        var safeLanguage = Regex.Replace(PathUtil.MakeSafeName(language.EnglishName), @"[^A-Za-z0-9._-]+", "_");
        var outputPak = Path.Combine(
            outputModRoot,
            $"{AppConstants.BaseGameLocresPakPrefix}_{safeLanguage}_Localization_P.pak"
        );
        var packRoot = Path.Combine(locresWork, "ModOutput");
        await _repak.PackLocalizationAsync(packRoot, outputPak, cancellationToken);
        RequireFile(outputPak, "built LOCRES .pak");

        var expectedEntry = $"{AppConstants.CanonicalLocresRoot}/{language.LocresCulture}/Game.locres";
        var entries = await _repak.ListEntriesAsync(outputPak, cancellationToken);
        var normalized = entries.Select(x => x.Replace('\\', '/').TrimStart('/')).ToList();
        if (!normalized.Contains(expectedEntry, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException($"LOCRES PAK is missing expected path: {expectedEntry}");

        var unexpectedEntries = normalized
            .Where(x => !string.Equals(x, expectedEntry, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (unexpectedEntries.Count > 0)
            throw new InvalidDataException("LOCRES PAK contains unexpected files: " + string.Join(", ", unexpectedEntries));

        // Do not trust only the PAK index. Unpack the finished installable PAK and
        // verify that the embedded Game.locres is byte-for-byte the exact binary
        // that passed S2HOCMM verification above.
        var finalVerifyRoot = Path.Combine(locresWork, "verify_final_pak");
        if (Directory.Exists(finalVerifyRoot))
            Directory.Delete(finalVerifyRoot, recursive: true);
        Directory.CreateDirectory(finalVerifyRoot);
        await _repak.UnpackEntriesAsync(
            outputPak,
            finalVerifyRoot,
            new[] { expectedEntry },
            cancellationToken
        );

        var packedLocres = Path.Combine(finalVerifyRoot, PathUtil.NormalizePakPath(expectedEntry));
        RequireFile(packedLocres, "Game.locres unpacked from finished PAK");
        var generatedBytes = await File.ReadAllBytesAsync(generatedLocres, cancellationToken);
        var packedBytes = await File.ReadAllBytesAsync(packedLocres, cancellationToken);
        if (!generatedBytes.AsSpan().SequenceEqual(packedBytes))
            throw new InvalidDataException("Final PAK Game.locres differs from the verified S2HOCMM output.");

        VerifyS2HocmmLocres(flatLocres, packedLocres, manifest.ModName + " final PAK");
        var locresSha256 = await HashUtil.Sha256FileAsync(generatedLocres, cancellationToken);
        var pakSha256 = await HashUtil.Sha256FileAsync(outputPak, cancellationToken);
        _log?.Invoke(
            $"Final PAK payload verified byte-for-byte: {expectedEntry} ({packedBytes.Length} bytes), " +
            $"Game.locres sha256={locresSha256}"
        );
        _log?.Invoke($"Final installable PAK sha256={pakSha256}");

        // Human-inspectable copy only. The file inside the PAK is also Game.locres;
        // keeping the standalone name identical avoids culture-suffix confusion.
        var outputLocres = Path.Combine(outputModRoot, "Game.locres");
        File.Copy(generatedLocres, outputLocres, overwrite: true);

        result.LocresBuilt = true;
        result.OutputLocresPak = outputPak;
        result.OutputFiles.Add(outputPak);
        result.OutputFiles.Add(outputLocres);
        _log?.Invoke(
            $"LOCRES built from authoritative Editable JSON via S2HOCMM: entries={flatLocres.Count}"
        );
        _log?.Invoke($"LOCRES built & verified: {outputPak}");
    }

    private static void VerifyS2HocmmLocres(
        IReadOnlyDictionary<string, string> expected,
        string generatedLocres,
        string label)
    {
        var document = LocresCodec.Load(generatedLocres);
        var ns = document.Namespaces.SingleOrDefault(x =>
            string.Equals(x.Name, AppConstants.S2LocresNamespace, StringComparison.Ordinal));

        if (ns is null)
        {
            var names = string.Join(", ", document.Namespaces.Select(x => x.Name));
            throw new InvalidDataException(
                $"{label}: S2HOCMM output is missing namespace '{AppConstants.S2LocresNamespace}'. " +
                $"Found: {names}"
            );
        }

        var actual = ns.Entries.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        if (actual.Count != expected.Count)
        {
            throw new InvalidDataException(
                $"{label}: S2HOCMM LOCRES entry count mismatch: expected {expected.Count}, got {actual.Count}."
            );
        }

        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value))
                throw new InvalidDataException($"{label}: S2HOCMM LOCRES is missing key: {pair.Key}");
            if (!string.Equals(value, pair.Value, StringComparison.Ordinal))
                throw new InvalidDataException($"{label}: S2HOCMM LOCRES value mismatch: {pair.Key}");
        }
    }

    private static void ValidateAssetIdentityMetadata(ExtractedAssetManifest asset, string label)
    {
        if (string.IsNullOrWhiteSpace(asset.ZenChunkId)
            || !Regex.IsMatch(asset.ZenChunkId, @"\A[0-9A-Fa-f]{24}\z"))
            throw new InvalidDataException($"{label}: invalid cached Zen chunk ID: {asset.ZenChunkId}");

        if (string.IsNullOrWhiteSpace(asset.InternalPackagePath)
            || string.IsNullOrWhiteSpace(asset.SourcePackageIdentityPath)
            || string.IsNullOrWhiteSpace(asset.DirectoryAliasPackagePath))
            throw new InvalidDataException($"{label}: cached package-identity metadata is incomplete. Extract again with workspace schema {AppConstants.ManifestSchemaVersion}.");

        if (!string.Equals(asset.InternalPackagePath, asset.SourcePackageIdentityPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{label}: source package identity does not match serializer-visible package path for {asset.VirtualPath}:" +
                Environment.NewLine + $"  source identity : {asset.SourcePackageIdentityPath}" +
                Environment.NewLine + $"  serializer path : {asset.InternalPackagePath}"
            );
        }

        var derivedAlias = PathUtil.DirectoryAliasPackagePathFromVirtualPath(asset.VirtualPath);
        if (!string.Equals(derivedAlias, asset.DirectoryAliasPackagePath, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{label}: cached directory alias does not match virtual path for {asset.VirtualPath}:" +
                Environment.NewLine + $"  cached : {asset.DirectoryAliasPackagePath}" +
                Environment.NewLine + $"  derived: {derivedAlias}"
            );
        }
    }

    private async Task VerifyFinishedPackagesAsync(
        string outputUtoc,
        IReadOnlyDictionary<string, ExpectedDatabasePackage> expectedPackages,
        string workRoot,
        CancellationToken cancellationToken)
    {
        if (expectedPackages.Count == 0)
            return;

        var verifyRoot = Path.Combine(workRoot, "verify_finished");
        var input = Path.Combine(verifyRoot, "input");
        var legacy = Path.Combine(verifyRoot, "legacy");
        TryDeleteDirectory(verifyRoot);
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(legacy);

        try
        {
            FileLinker.LinkOrCopy(outputUtoc, Path.Combine(input, Path.GetFileName(outputUtoc)));
            FileLinker.LinkOrCopy(
                Path.ChangeExtension(outputUtoc, ".ucas"),
                Path.Combine(input, Path.GetFileName(Path.ChangeExtension(outputUtoc, ".ucas")))
            );
            FileLinker.LinkOrCopy(
                Path.ChangeExtension(outputUtoc, ".pak"),
                Path.Combine(input, Path.GetFileName(Path.ChangeExtension(outputUtoc, ".pak")))
            );
            FileLinker.LinkOrCopy(Path.Combine(_settings.GamePaksFolder, "global.utoc"), Path.Combine(input, "global.utoc"));
            FileLinker.LinkOrCopy(Path.Combine(_settings.GamePaksFolder, "global.ucas"), Path.Combine(input, "global.ucas"));

            await _retoc.ToLegacyAsync(input, legacy, AppConstants.LocalizationDatabaseNeedle, cancellationToken);
            var outputAssets = await _retoc.ListLocalizationAssetsAsync(
                outputUtoc,
                Path.GetDirectoryName(outputUtoc)!,
                cancellationToken
            );

            var byPath = outputAssets
                .GroupBy(asset => PathUtil.NormalizeVirtualPathForComparison(asset.VirtualPath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            var expectedPathSet = expectedPackages.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var actualPathSet = byPath.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingPaths = expectedPathSet.Except(actualPathSet, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            var unexpectedPaths = actualPathSet.Except(expectedPathSet, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            var duplicatePaths = byPath.Where(pair => pair.Value.Count != 1).Select(pair => pair.Key).OrderBy(x => x).ToList();

            if (missingPaths.Count > 0 || unexpectedPaths.Count > 0 || duplicatePaths.Count > 0)
            {
                throw new InvalidDataException(
                    "IoStore canonical-path verification failed.\r\n" +
                    (missingPaths.Count > 0 ? "Missing: " + string.Join(", ", missingPaths) + "\r\n" : string.Empty) +
                    (unexpectedPaths.Count > 0 ? "Unexpected: " + string.Join(", ", unexpectedPaths) + "\r\n" : string.Empty) +
                    (duplicatePaths.Count > 0 ? "Duplicates: " + string.Join(", ", duplicatePaths) : string.Empty)
                );
            }

            var verified = 0;
            foreach (var pair in expectedPackages.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var expected = pair.Value;
                var candidates = byPath[pair.Key];
                if (candidates.Count != 1)
                    throw new InvalidDataException($"Expected one packaged LocalizationDatabase at {pair.Key}, found {candidates.Count}");

                var packaged = candidates[0];
                if (!string.Equals(packaged.ZenChunkId, expected.ZenChunkId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Original package identity was not preserved for {packaged.VirtualPath}:" +
                        Environment.NewLine + $"  expected chunk: {expected.ZenChunkId}" +
                        Environment.NewLine + $"  actual chunk  : {packaged.ZenChunkId}"
                    );
                }

                var packagedAliasPath = PathUtil.DirectoryAliasPackagePathFromVirtualPath(packaged.VirtualPath);
                if (!string.Equals(packagedAliasPath, expected.DirectoryAliasPackagePath, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Directory alias mismatch for {packaged.VirtualPath}: expected {expected.DirectoryAliasPackagePath}, got {packagedAliasPath}"
                    );
                }

                var legacyRelative = PathUtil.NormalizeVirtualPath(packaged.VirtualPath);
                var packagedUasset = Path.Combine(legacy, legacyRelative);
                var packagedUexp = Path.ChangeExtension(packagedUasset, ".uexp");
                RequireFile(packagedUexp, "round-tripped packaged LocalizationDatabase .uexp");
                PackagePatcher.VerifyPayloadOccurrence(packagedUexp, expected.Payload, $"chunk {expected.ZenChunkId}");

                _log?.Invoke(
                    $"Verified stock-retoc package: {packaged.VirtualPath}; " +
                    $"chunk={packaged.ZenChunkId} (original preserved); payload=exact"
                );
                verified++;
            }

            _log?.Invoke($"Verified {verified} stock-retoc canonical path/chunk/payload set(s).");
        }
        finally
        {
            TryDeleteDirectory(verifyRoot);
        }
    }

    private sealed class ExpectedDatabasePackage
    {
        public string Label { get; init; } = string.Empty;
        public string VirtualPath { get; init; } = string.Empty;
        public string ZenChunkId { get; init; } = string.Empty;
        public string DirectoryAliasPackagePath { get; init; } = string.Empty;
        public byte[] Payload { get; init; } = Array.Empty<byte>();

        public static ExpectedDatabasePackage From(ExtractedAssetManifest asset, byte[] payload, string label)
        {
            return new ExpectedDatabasePackage
            {
                Label = label + " :: " + asset.VirtualPath,
                VirtualPath = asset.VirtualPath,
                ZenChunkId = asset.ZenChunkId.ToLowerInvariant(),
                DirectoryAliasPackagePath = asset.DirectoryAliasPackagePath,
                Payload = payload,
            };
        }
    }

    private void ValidatePrerequisites(IReadOnlyCollection<ModScanResult> mods, BuildMode mode)
    {
        if (mods.Any(x => x.Assets.Count > 0))
        {
            RequireFile(_settings.RetocPath, "retoc.exe");
            RequireFile(_settings.UAssetGuiPath, "UAssetGUI.exe");
            RequireFile(_settings.MappingsPath, "Mappings.usmap");
            RequireValidGamePaksFolder();
        }

        if (mode == BuildMode.Modular
            && mods.Any(x => string.Equals(x.ModId, "Game", StringComparison.OrdinalIgnoreCase)
                             && x.LocresAssets.Count > 0))
        {
            RequireFile(_settings.RepakPath, "repak.exe");
            RequireFile(_settings.S2HocmmPath, "S2HOCMM.exe");
        }
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

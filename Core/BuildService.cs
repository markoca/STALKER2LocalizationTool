using STALKER2LocalizationTool.Models;
using STALKER2LocalizationTool.Services;

namespace STALKER2LocalizationTool.Core;

public sealed class BuildService
{
    private readonly AppSettings _settings;
    private readonly RetocService _retoc;
    private readonly RepakService _repak;
    private readonly S2HocmmService _s2Hocmm;
    private readonly Action<string>? _log;

    public BuildService(
        AppSettings settings,
        RetocService retoc,
        RepakService repak,
        Action<string>? log = null)
    {
        _settings = settings;
        _retoc = retoc;
        _repak = repak;
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
            .Where(x => x.UiStatus == ModUiStatus.Available && !string.IsNullOrWhiteSpace(x.EditableTranslationFile))
            .ToList();
        ValidatePrerequisites(available, mode);

        if (mode == BuildMode.AllInOne)
            return await BuildAllInOneAsync(available, targetLanguageId, progress, cancellationToken);

        var results = new List<ModBuildResult>();
        for (var i = 0; i < available.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = available[i];
            progress?.Report((i + 1, available.Count, mod.ModName));
            _log?.Invoke($"=== Building {mod.ModName} ===");
            results.Add(await BuildOneAsync(mod, targetLanguageId, cancellationToken));
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
        var expectedPayloads = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var expectedChunks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var chunkOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var scriptObjectsCopied = false;

        for (var i = 0; i < available.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = available[i];
            progress?.Report((i + 1, available.Count, mod.ModName));
            _log?.Invoke($"=== Adding {mod.ModName} to All-in-One ===");

            var result = new ModBuildResult { ModId = mod.ModId, ModName = mod.ModName };
            results.Add(result);

            var cachedRoot = Path.Combine(_settings.CachedFolder, mod.ModId);
            var manifest = LoadAndValidateManifest(mod, cachedRoot);
            var translations = LoadTranslations(mod);
            var overrideAssets = manifest.Assets
                .Where(asset => PathUtil.IsBaseContentAlias(asset.VirtualPath))
                .ToList();

            if (overrideAssets.Count == 0)
            {
                result.Verified = true;
                result.Message = "No OverrideContent localization database assets; skipped in All-in-One mode.";
                _log?.Invoke($"{mod.ModName}: no OverrideContent localization database assets; skipped.");
                continue;
            }

            foreach (var asset in overrideAssets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceUasset = Path.Combine(cachedRoot, asset.UassetFile);
                var sourceUexp = Path.Combine(cachedRoot, asset.UexpFile);
                var sourceJson = Path.Combine(cachedRoot, asset.AssetJsonFile);
                var scriptObjects = Path.Combine(cachedRoot, asset.ScriptObjectsFile);

                RequireFile(sourceUasset, "pristine extracted .uasset");
                RequireFile(sourceUexp, "pristine extracted .uexp");
                RequireFile(sourceJson, "UAssetGUI JSON");
                RequireFile(scriptObjects, "scriptobjects.bin");

                var export = UAssetInspector.ReadLocalizationExport(sourceJson, asset.VirtualPath);
                var patch = LocalizationDatabaseCodec.Patch(export.Payload, translations, language.Id, asset.VirtualPath);
                result.MatchedSids += patch.MatchedSids.Count;
                result.ChangedSids += patch.ChangedSids.Count;

                // A matched SID must be included even when its current source value already
                // equals the Editable translation. "changed" is diagnostic information only;
                // using it as the inclusion criterion produces empty builds for valid overlays.
                if (patch.MatchedSids.Count == 0)
                    continue;

                var legacyRelative = PathUtil.NormalizeVirtualPath(asset.VirtualPath);
                var identityPath = legacyRelative.Replace('\\', '/');

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

                var outputUasset = Path.Combine(legacyRoot, legacyRelative);
                var patchInfo = PackagePatcher.PatchLegacyPackage(
                    sourceUasset,
                    sourceUexp,
                    export,
                    patch.Payload,
                    outputUasset,
                    asset.VirtualPath
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

                expectedChunks.Add(asset.ZenChunkId);
                expectedPayloads[asset.ZenChunkId] = patch.Payload;
                result.AssetsPatched++;
            }
        }

        if (expectedPayloads.Count == 0)
        {
            foreach (var result in results)
            {
                result.Verified = true;
                result.Message ??= "No Editable translation keys matched OverrideContent localization database entries.";
            }
            TryDeleteDirectory(outputRoot);
            return results;
        }

        var outputUtoc = Path.Combine(outputRoot, $"{AppConstants.OverlayPrefix}_All_In_One_DB_P.utoc");
        await _retoc.ToZenAsync(legacyRoot, outputUtoc, cancellationToken);

        RequireFile(outputUtoc, "built All-in-One database .utoc");
        RequireFile(Path.ChangeExtension(outputUtoc, ".ucas"), "built All-in-One database .ucas");
        RequireFile(Path.ChangeExtension(outputUtoc, ".pak"), "built All-in-One database .pak");

        await VerifyPackageIdentityAsync(outputUtoc, expectedChunks, cancellationToken);
        await VerifyFinishedPayloadsAsync(
            outputUtoc,
            expectedPayloads,
            Path.Combine(workRoot, "database"),
            cancellationToken
        );

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
                result.Message = $"Included {result.AssetsPatched} OverrideContent asset(s) in the verified All-in-One package.";
            }
            else
            {
                result.Message ??= "No Editable translation keys matched OverrideContent localization database entries; not included.";
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

        var expectedPayloads = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var expectedChunks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scriptObjectsCopied = false;
        var matchedTotal = 0;
        var changedTotal = 0;
        var assetsPatched = 0;

        foreach (var asset in manifest.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceUasset = Path.Combine(cachedRoot, asset.UassetFile);
            var sourceUexp = Path.Combine(cachedRoot, asset.UexpFile);
            var sourceJson = Path.Combine(cachedRoot, asset.AssetJsonFile);
            var scriptObjects = Path.Combine(cachedRoot, asset.ScriptObjectsFile);

            RequireFile(sourceUasset, "pristine extracted .uasset");
            RequireFile(sourceUexp, "pristine extracted .uexp");
            RequireFile(sourceJson, "UAssetGUI JSON");
            RequireFile(scriptObjects, "scriptobjects.bin");

            var export = UAssetInspector.ReadLocalizationExport(sourceJson, asset.VirtualPath);
            var patch = LocalizationDatabaseCodec.Patch(export.Payload, translations, language.Id, asset.VirtualPath);
            matchedTotal += patch.MatchedSids.Count;
            changedTotal += patch.ChangedSids.Count;

            // Build every database asset with matched Editable translations, even when
            // the source already contains the requested value. The final overlay still
            // has to contain that asset so it can win localization precedence in-game.
            if (patch.MatchedSids.Count == 0)
                continue;

            assetsPatched++;
            var outputUasset = Path.Combine(legacyRoot, asset.LegacyRelativePath);
            var patchInfo = PackagePatcher.PatchLegacyPackage(
                sourceUasset,
                sourceUexp,
                export,
                patch.Payload,
                outputUasset,
                asset.VirtualPath
            );

            _log?.Invoke(
                $"{asset.DatabaseName}: SerialSize {patchInfo.OldSerialSize} -> {patchInfo.NewSerialSize}; " +
                $"matched {patch.MatchedSids.Count}, changed {patch.ChangedSids.Count}"
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

            expectedChunks.Add(asset.ZenChunkId);
            expectedPayloads[asset.ZenChunkId] = patch.Payload;
        }

        result.AssetsPatched = assetsPatched;
        result.MatchedSids = matchedTotal;
        result.ChangedSids = changedTotal;

        if (assetsPatched == 0)
        {
            _log?.Invoke($"{manifest.ModName}: no Editable translations matched database localization entries; database output skipped.");
            return;
        }

        var safe = Regex.Replace(PathUtil.MakeSafeName(manifest.ModName), @"[^A-Za-z0-9._-]+", "_");
        var outputUtoc = Path.Combine(outputModRoot, $"{AppConstants.OverlayPrefix}_{safe}_DB_P.utoc");
        await _retoc.ToZenAsync(legacyRoot, outputUtoc, cancellationToken);

        RequireFile(outputUtoc, "built database .utoc");
        RequireFile(Path.ChangeExtension(outputUtoc, ".ucas"), "built database .ucas");
        RequireFile(Path.ChangeExtension(outputUtoc, ".pak"), "built database .pak");

        await VerifyPackageIdentityAsync(outputUtoc, expectedChunks, cancellationToken);
        await VerifyFinishedPayloadsAsync(outputUtoc, expectedPayloads, Path.Combine(workRoot, "database"), cancellationToken);

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

    private async Task VerifyPackageIdentityAsync(string outputUtoc, HashSet<string> expectedChunks, CancellationToken cancellationToken)
    {
        var outputAssets = await _retoc.ListLocalizationAssetsAsync(
            outputUtoc,
            Path.GetDirectoryName(outputUtoc)!,
            cancellationToken
        );
        var outputChunkList = outputAssets.Select(x => x.ZenChunkId).ToList();
        var outputChunks = outputChunkList.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = expectedChunks.Except(outputChunks, StringComparer.OrdinalIgnoreCase).ToList();
        var unexpected = outputChunks.Except(expectedChunks, StringComparer.OrdinalIgnoreCase).ToList();
        var duplicates = outputChunkList
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (missing.Count > 0 || unexpected.Count > 0 || duplicates.Count > 0)
        {
            throw new InvalidDataException(
                "IoStore package identity verification failed.\r\n" +
                (missing.Count > 0 ? "Missing: " + string.Join(", ", missing) + "\r\n" : string.Empty) +
                (unexpected.Count > 0 ? "Unexpected: " + string.Join(", ", unexpected) + "\r\n" : string.Empty) +
                (duplicates.Count > 0 ? "Duplicates: " + string.Join(", ", duplicates) : string.Empty)
            );
        }
    }

    private async Task VerifyFinishedPayloadsAsync(
        string outputUtoc,
        Dictionary<string, byte[]> expectedPayloads,
        string workRoot,
        CancellationToken cancellationToken)
    {
        var verifyRoot = Path.Combine(workRoot, "verify_finished");
        var input = Path.Combine(verifyRoot, "input");
        var legacy = Path.Combine(verifyRoot, "legacy");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(legacy);

        FileLinker.LinkOrCopy(outputUtoc, Path.Combine(input, Path.GetFileName(outputUtoc)));
        FileLinker.LinkOrCopy(Path.ChangeExtension(outputUtoc, ".ucas"), Path.Combine(input, Path.GetFileName(Path.ChangeExtension(outputUtoc, ".ucas"))));
        FileLinker.LinkOrCopy(Path.ChangeExtension(outputUtoc, ".pak"), Path.Combine(input, Path.GetFileName(Path.ChangeExtension(outputUtoc, ".pak"))));
        FileLinker.LinkOrCopy(Path.Combine(_settings.GamePaksFolder, "global.utoc"), Path.Combine(input, "global.utoc"));
        FileLinker.LinkOrCopy(Path.Combine(_settings.GamePaksFolder, "global.ucas"), Path.Combine(input, "global.ucas"));

        await _retoc.ToLegacyAsync(input, legacy, AppConstants.LocalizationDatabaseNeedle, cancellationToken);
        var outputAssets = await _retoc.ListLocalizationAssetsAsync(
            outputUtoc,
            Path.GetDirectoryName(outputUtoc)!,
            cancellationToken
        );

        foreach (var pair in expectedPayloads)
        {
            var candidates = outputAssets
                .Where(x => string.Equals(x.ZenChunkId, pair.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count != 1)
                throw new InvalidDataException($"Packaged payload verification expected one database for chunk {pair.Key}, found {candidates.Count}");

            var legacyRelative = PathUtil.NormalizeVirtualPath(candidates[0].VirtualPath);
            var uexp = Path.ChangeExtension(Path.Combine(legacy, legacyRelative), ".uexp");
            RequireFile(uexp, "round-tripped packaged .uexp");
            PackagePatcher.VerifyPayloadOccurrence(uexp, pair.Value, $"chunk {pair.Key}");
        }

        _log?.Invoke($"Verified {expectedPayloads.Count} exact packaged RawExport payload(s).");
    }

    private void ValidatePrerequisites(IReadOnlyCollection<ModScanResult> mods, BuildMode mode)
    {
        if (mods.Any(x => x.Assets.Count > 0))
        {
            RequireFile(_settings.RetocPath, "retoc.exe");
            RequireFile(Path.Combine(_settings.GamePaksFolder, "global.utoc"), "game global.utoc");
            RequireFile(Path.Combine(_settings.GamePaksFolder, "global.ucas"), "game global.ucas");
        }

        if (mode == BuildMode.Modular
            && mods.Any(x => string.Equals(x.ModId, "Game", StringComparison.OrdinalIgnoreCase)
                             && x.LocresAssets.Count > 0))
        {
            RequireFile(_settings.RepakPath, "repak.exe");
            RequireFile(_settings.S2HocmmPath, "S2HOCMM.exe");
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

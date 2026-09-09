using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string SettingsDirectory { get; } = AppContext.BaseDirectory;

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        AppSettings settings;

        try
        {
            if (File.Exists(SettingsPath))
            {
                settings = JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(SettingsPath, Encoding.UTF8),
                    JsonOptions
                ) ?? new AppSettings();
            }
            else
            {
                settings = new AppSettings();
            }
        }
        catch
        {
            settings = new AppSettings();
        }

        ApplyDefaultsAndMigrate(settings);
        try { Save(settings); } catch { }
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(settings, JsonOptions),
            new UTF8Encoding(false)
        );
    }

    private static void ApplyDefaultsAndMigrate(AppSettings settings)
    {
        var baseDir = AppContext.BaseDirectory;
        if (settings.BuildLanguageIds is null)
        {
            settings.BuildLanguageIds = settings.BuildLanguageId is >= 0 and <= 17
                ? new List<int> { settings.BuildLanguageId.Value }
                : new List<int> { 4 };
        }
        settings.BuildLanguageIds = settings.BuildLanguageIds
            .Where(id => BuildLanguageCatalog.All.Any(language => language.Id == id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        settings.BuildLanguageId = null;

        settings.RetocPath = DefaultIfEmpty(settings.RetocPath, Path.Combine(baseDir, "tools", "retoc.exe"));
        settings.UAssetGuiPath = DefaultIfEmpty(settings.UAssetGuiPath, Path.Combine(baseDir, "tools", "UAssetGUI.exe"));
        settings.MappingsPath = DefaultIfEmpty(settings.MappingsPath, Path.Combine(baseDir, "tools", "Mappings.usmap"));
        settings.RepakPath = DefaultIfEmpty(settings.RepakPath, Path.Combine(baseDir, "tools", "repak.exe"));
        settings.S2HocmmPath = DefaultIfEmpty(settings.S2HocmmPath, FindS2Hocmm(baseDir));

        settings.ModsFolder = DefaultIfEmpty(settings.ModsFolder, Path.Combine(baseDir, "Mods"));
        settings.CachedFolder = ResolveWorkspaceFolder(
            settings.CachedFolder,
            settings.ExtractedFolder,
            Path.Combine(baseDir, "Extracted"),
            Path.Combine(baseDir, "Cached"),
            "Extracted",
            "Cached",
            settings.MigrationMessages
        );
        settings.EditableFolder = ResolveWorkspaceFolder(
            settings.EditableFolder,
            settings.ReadyFolder,
            Path.Combine(baseDir, "Ready"),
            Path.Combine(baseDir, "Editable"),
            "Ready",
            "Editable",
            settings.MigrationMessages
        );
        settings.OutputFolder = DefaultIfEmpty(settings.OutputFolder, Path.Combine(baseDir, "Output"));

        // Legacy JSON properties disappear on the next save.
        settings.ExtractedFolder = null;
        settings.ReadyFolder = null;

        foreach (var path in new[] { settings.ModsFolder, settings.CachedFolder, settings.EditableFolder, settings.OutputFolder })
        {
            try { Directory.CreateDirectory(path); } catch { }
        }

        if (string.IsNullOrWhiteSpace(settings.GamePaksFolder))
            settings.GamePaksFolder = SteamLocator.TryFindGamePaksFolder() ?? string.Empty;
    }

    private static string ResolveWorkspaceFolder(
        string currentValue,
        string? legacySettingValue,
        string oldDefault,
        string newDefault,
        string oldName,
        string newName,
        List<string> messages)
    {
        // Pre-RC settings can arrive in either form:
        //   ExtractedFolder/ReadyFolder legacy JSON properties, or
        //   CachedFolder/EditableFolder already populated with the old default path.
        // Treat only the application's old DEFAULT directories as legacy. A custom
        // path chosen by the user remains untouched even if its folder happens to
        // contain the old terminology.
        var currentPointsAtOldDefault = !string.IsNullOrWhiteSpace(currentValue)
                                       && PathsEqual(currentValue, oldDefault);
        var legacyPointsAtOldDefault = string.IsNullOrWhiteSpace(legacySettingValue)
                                       || PathsEqual(legacySettingValue, oldDefault);

        if (string.IsNullOrWhiteSpace(currentValue)
            && !string.IsNullOrWhiteSpace(legacySettingValue)
            && !PathsEqual(legacySettingValue, oldDefault))
        {
            messages.Add(
                $"Settings migrated: {oldName} folder is now called {newName}; " +
                $"custom path preserved: {legacySettingValue}"
            );
            return legacySettingValue;
        }

        var desired = currentPointsAtOldDefault || string.IsNullOrWhiteSpace(currentValue)
            ? newDefault
            : currentValue;

        // Only migrate the physical default workspace when this settings record is
        // actually using the default workspace family. Never rename a custom path.
        if ((currentPointsAtOldDefault || legacyPointsAtOldDefault)
            && PathsEqual(desired, newDefault))
        {
            MigrateDefaultWorkspace(oldDefault, newDefault, oldName, newName, messages);

            if (currentPointsAtOldDefault)
                messages.Add($"Settings path migrated: {oldName} -> {newName}.");
        }

        return desired;
    }

    private static void MigrateDefaultWorkspace(
        string oldDefault,
        string newDefault,
        string oldName,
        string newName,
        List<string> messages)
    {
        if (!Directory.Exists(oldDefault))
            return;

        if (!Directory.Exists(newDefault))
        {
            try
            {
                Directory.Move(oldDefault, newDefault);
                messages.Add($"Workspace migrated: {oldName} -> {newName}.");
            }
            catch (Exception ex)
            {
                messages.Add($"Could not rename {oldName} to {newName}: {ex.Message}");
            }
            return;
        }

        var oldHasEntries = HasEntries(oldDefault);
        var newHasEntries = HasEntries(newDefault);

        // Safe cleanup cases: no user data can be lost because one side is empty.
        if (!oldHasEntries)
        {
            try
            {
                Directory.Delete(oldDefault, recursive: false);
                messages.Add($"Removed empty legacy {oldName} workspace; {newName} is authoritative.");
            }
            catch (Exception ex)
            {
                messages.Add($"Could not remove empty legacy {oldName} folder: {ex.Message}");
            }
            return;
        }

        if (!newHasEntries)
        {
            try
            {
                Directory.Delete(newDefault, recursive: false);
                Directory.Move(oldDefault, newDefault);
                messages.Add($"Workspace migrated: {oldName} -> {newName} (empty destination replaced safely).");
            }
            catch (Exception ex)
            {
                messages.Add($"Could not migrate {oldName} to empty {newName}: {ex.Message}");
            }
            return;
        }

        // Both contain data. Do not merge, overwrite, delete or guess. The new
        // workspace remains authoritative so no new runtime writes go to the legacy
        // default folder, and the user gets a clear conflict warning.
        messages.Add(
            $"Both legacy {oldName} and current {newName} folders contain data. " +
            $"Nothing was merged or overwritten. The application will use only {newName}; " +
            $"review {oldName} manually before deleting it."
        );
    }


    private static bool HasEntries(string path)
    {
        try { return Directory.EnumerateFileSystemEntries(path).Any(); }
        catch { return true; }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase
            );
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindS2Hocmm(string baseDir)
    {
        var nestedLocal = Path.Combine(baseDir, "tools", "S2HOCMM", "S2HOCMM.exe");
        if (File.Exists(nestedLocal)) return nestedLocal;

        var local = Path.Combine(baseDir, "tools", "S2HOCMM.exe");
        if (File.Exists(local)) return local;

        // Developer-friendly discovery for the common sibling layout:
        //   <root>/STALKER2LocalizationTool/publish/win-x64/
        //   <root>/S2HOCMM/S2HOCMM.exe
        var current = new DirectoryInfo(baseDir);
        for (var i = 0; i < 5 && current is not null; i++, current = current.Parent)
        {
            var sibling = Path.Combine(current.FullName, "S2HOCMM", "S2HOCMM.exe");
            if (File.Exists(sibling)) return sibling;
        }

        return local;
    }

    private static string DefaultIfEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}

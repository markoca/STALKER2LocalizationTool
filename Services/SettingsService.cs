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

        // Persist only user overrides. Portable/default runtime paths are derived
        // from AppContext.BaseDirectory on every launch so moving or renaming the
        // application folder never leaves stale absolute paths in settings.json.
        var persisted = CreatePersistedSettings(settings);

        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(persisted, JsonOptions),
            new UTF8Encoding(false)
        );
    }

    private static AppSettings CreatePersistedSettings(AppSettings settings)
    {
        return new AppSettings
        {
            BuildLanguageIds = settings.BuildLanguageIds.ToList(),
            BuildLanguageId = null,

            // External/user-selected locations are persisted.
            GamePaksFolder = settings.GamePaksFolder,
            ModsFolder = settings.ModsFolder,

            // Internal portable paths are NEVER persisted. They are derived from
            // AppContext.BaseDirectory on every launch.
            CachedFolder = string.Empty,
            EditableFolder = string.Empty,
            OutputFolder = string.Empty,
            RetocPath = string.Empty,
            UAssetGuiPath = string.Empty,
            MappingsPath = string.Empty,
            RepakPath = string.Empty,
            S2HocmmPath = string.Empty,

            ExtractedFolder = null,
            ReadyFolder = null,
            AutoScan = settings.AutoScan,
        };
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

        // Internal runtime/workspace paths are ALWAYS derived from the directory
        // containing the currently running executable. They are intentionally not
        // restored from settings.json, so moving or renaming the portable app folder
        // can never leave stale absolute paths behind.
        settings.CachedFolder = Path.Combine(baseDir, "Cached");
        settings.EditableFolder = Path.Combine(baseDir, "Editable");
        settings.OutputFolder = Path.Combine(baseDir, "Output");

        settings.RetocPath = Path.Combine(baseDir, "tools", "retoc.exe");
        settings.UAssetGuiPath = Path.Combine(baseDir, "tools", "UAssetGUI.exe");
        settings.MappingsPath = Path.Combine(baseDir, "tools", "Mappings.usmap");
        settings.RepakPath = Path.Combine(baseDir, "tools", "repak.exe");
        settings.S2HocmmPath = Path.Combine(baseDir, "tools", "S2HOCMM.exe");

        // Mods is intentionally user-configurable. Its default follows the runtime,
        // but a custom external Mods folder is preserved.
        settings.ModsFolder = DefaultIfEmpty(settings.ModsFolder, Path.Combine(baseDir, "Mods"));

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

    private static string DefaultIfEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}

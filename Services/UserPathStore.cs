using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Services;

/// <summary>
/// Persists only user-selected external source locations.
/// Internal workspace/tool paths are never stored here; they always come from
/// AppContext.BaseDirectory through AppSettings.CreateRuntime().
/// </summary>
public static class UserPathStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string StorePath =>
        Path.Combine(AppContext.BaseDirectory, "user-paths.json");

    public static void Apply(AppSettings settings)
    {
        var saved = Load();
        settings.InterfaceLanguage = saved.InterfaceLanguage;

        if (IsValidGamePaksFolder(saved.GamePaksFolder))
            settings.GamePaksFolder = saved.GamePaksFolder;

        if (IsValidModsFolder(saved.ModsFolder))
            settings.ModsFolder = saved.ModsFolder;
    }

    public static void SaveValidated(AppSettings settings)
    {
        var saved = Load();
        saved.InterfaceLanguage = settings.InterfaceLanguage;

        if (IsValidGamePaksFolder(settings.GamePaksFolder))
            saved.GamePaksFolder = settings.GamePaksFolder.Trim();

        if (IsValidModsFolder(settings.ModsFolder))
            saved.ModsFolder = settings.ModsFolder.Trim();

        Save(saved);
    }

    private static PersistedPaths Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return new PersistedPaths();

            return JsonSerializer.Deserialize<PersistedPaths>(
                       File.ReadAllText(StorePath, Encoding.UTF8),
                       JsonOptions
                   ) ?? new PersistedPaths();
        }
        catch
        {
            return new PersistedPaths();
        }
    }

    private static void Save(PersistedPaths paths)
    {
        try
        {
            File.WriteAllText(
                StorePath,
                JsonSerializer.Serialize(paths, JsonOptions),
                new UTF8Encoding(false)
            );
        }
        catch
        {
            // Path persistence is a convenience only and must never break scanning.
        }
    }

    private static bool IsValidGamePaksFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        return File.Exists(Path.Combine(path, "global.utoc"))
               && File.Exists(Path.Combine(path, "global.ucas"));
    }

    private static bool IsValidModsFolder(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    private sealed class PersistedPaths
    {
        public string GamePaksFolder { get; set; } = string.Empty;
        public string ModsFolder { get; set; } = string.Empty;
        public string InterfaceLanguage { get; set; } = "en";
    }
}

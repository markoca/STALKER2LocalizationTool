using LocalizationWorkbench.Services;

namespace LocalizationWorkbench.Models;

public sealed class AppSettings
{
    public List<int> BuildLanguageIds { get; set; } = new() { 4 };

    public string GamePaksFolder { get; set; } = string.Empty;
    public string ModsFolder { get; set; } = string.Empty;
    public string SourceFolder { get; set; } = string.Empty;
    public string TranslationsFolder { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;

    public string RetocPath { get; set; } = string.Empty;
    public string UAssetGuiPath { get; set; } = string.Empty;
    public string MappingsPath { get; set; } = string.Empty;
    public string RepakPath { get; set; } = string.Empty;
    public string S2HocmmPath { get; set; } = string.Empty;

    public bool AutoScan { get; set; } = true;

    public static AppSettings CreateRuntime()
    {
        var baseDir = AppContext.BaseDirectory;

        var sourceFolder = Path.Combine(baseDir, "Source");
        var translationsFolder = Path.Combine(baseDir, "Translations");

        // One-time safe rename for pre-v2 workspace names. Never merge or overwrite
        // a destination that already exists.
        TryMigrateWorkspaceDirectory(
            Path.Combine(baseDir, "Cached"),
            sourceFolder
        );
        TryMigrateWorkspaceDirectory(
            Path.Combine(baseDir, "Editable"),
            translationsFolder
        );

        var settings = new AppSettings
        {
            GamePaksFolder = SteamLocator.TryFindGamePaksFolder() ?? string.Empty,
            ModsFolder = Path.Combine(baseDir, "Mods"),
            SourceFolder = sourceFolder,
            TranslationsFolder = translationsFolder,
            OutputFolder = Path.Combine(baseDir, "Output"),
            RetocPath = Path.Combine(baseDir, "tools", "retoc.exe"),
            UAssetGuiPath = Path.Combine(baseDir, "tools", "UAssetGUI.exe"),
            MappingsPath = Path.Combine(baseDir, "tools", "Mappings.usmap"),
            RepakPath = Path.Combine(baseDir, "tools", "repak.exe"),
            S2HocmmPath = Path.Combine(baseDir, "tools", "S2HOCMM.exe"),
        };

        foreach (var path in new[]
                 {
                     settings.ModsFolder,
                     settings.SourceFolder,
                     settings.TranslationsFolder,
                     settings.OutputFolder,
                 })
        {
            try { Directory.CreateDirectory(path); } catch { }
        }

        return settings;
    }

    private static void TryMigrateWorkspaceDirectory(
        string legacyPath,
        string destinationPath)
    {
        if (!Directory.Exists(legacyPath)
            || Directory.Exists(destinationPath))
        {
            return;
        }

        try
        {
            Directory.Move(legacyPath, destinationPath);
        }
        catch
        {
            // Upgrade convenience only. Runtime will safely use the new workspace path.
        }
    }
}

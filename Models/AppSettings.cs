using STALKER2LocalizationTool.Services;

namespace STALKER2LocalizationTool.Models;

public sealed class AppSettings
{
    public List<int> BuildLanguageIds { get; set; } = new() { 4 };

    public string GamePaksFolder { get; set; } = string.Empty;
    public string ModsFolder { get; set; } = string.Empty;
    public string CachedFolder { get; set; } = string.Empty;
    public string EditableFolder { get; set; } = string.Empty;
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

        var settings = new AppSettings
        {
            GamePaksFolder = SteamLocator.TryFindGamePaksFolder() ?? string.Empty,
            ModsFolder = Path.Combine(baseDir, "Mods"),
            CachedFolder = Path.Combine(baseDir, "Cached"),
            EditableFolder = Path.Combine(baseDir, "Editable"),
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
                     settings.CachedFolder,
                     settings.EditableFolder,
                     settings.OutputFolder,
                 })
        {
            try { Directory.CreateDirectory(path); } catch { }
        }

        return settings;
    }
}

namespace LocalizationWorkbench.Models;

public sealed class AppSettings
{
    public List<int> BuildLanguageIds { get; set; } = null!;

    // Read once when migrating a pre-0.6 settings file, then omitted on save.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BuildLanguageId { get; set; }

    public string GamePaksFolder { get; set; } = string.Empty;
    public string ModsFolder { get; set; } = string.Empty;
    public string CachedFolder { get; set; } = string.Empty;
    public string EditableFolder { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;

    // Legacy pre-RC1 names. They are consumed once during settings migration and
    // cleared before settings.json is saved again.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExtractedFolder { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReadyFolder { get; set; }

    public string RetocPath { get; set; } = string.Empty;
    public string UAssetGuiPath { get; set; } = string.Empty;
    public string MappingsPath { get; set; } = string.Empty;
    public string RepakPath { get; set; } = string.Empty;
    public string S2HocmmPath { get; set; } = string.Empty;

    public bool AutoScan { get; set; } = true;

    [JsonIgnore]
    public List<string> MigrationMessages { get; } = new();
}

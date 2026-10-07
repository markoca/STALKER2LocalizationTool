namespace LocalizationWorkbench.Models;

public enum ModUiStatus
{
    Unknown,
    NoLocalization,
    NeedsExtraction,
    MissingTranslation,
    NoLanguageSelected,
    Available,
    Extracted,
    BuiltVerified,
    Error,
}

public sealed class LocalizationAlias
{
    public string ZenChunkId { get; set; } = string.Empty;
    public string VirtualPath { get; set; } = string.Empty;
    public string SourceUtoc { get; set; } = string.Empty;
    public string SourceUtocRelative { get; set; } = string.Empty;
}

public sealed class LocalizationAssetGroup
{
    public string ZenChunkId { get; set; } = string.Empty;
    public LocalizationAlias Canonical { get; set; } = new();
    public List<LocalizationAlias> Aliases { get; set; } = new();
}

public sealed class LocresSource
{
    public string SourcePak { get; set; } = string.Empty;
    public string SourcePakRelative { get; set; } = string.Empty;
    public string InternalPath { get; set; } = string.Empty;
    public string CultureCode { get; set; } = string.Empty;
}

public sealed class ModScanResult
{
    public string ModId { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public string ModSourceRoot { get; set; } = string.Empty;
    public string SourceKind { get; set; } = "loose";
    public string SourceLabel { get; set; } = string.Empty;
    public List<string> Containers { get; set; } = new();
    public List<string> ContainerLabels { get; set; } = new();
    public List<string> OriginalSourceFiles { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, string> ContainerLabelsByPath { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<string> PakFiles { get; set; } = new();
    public List<LocalizationAssetGroup> Assets { get; set; } = new();
    public List<LocresSource> LocresAssets { get; set; } = new();
    public string SourceFingerprint { get; set; } = string.Empty;
    public bool NeedsExtraction { get; set; }
    public string? ScanError { get; set; }
    public string? TranslationFile { get; set; }
    public ModUiStatus UiStatus { get; set; } = ModUiStatus.Unknown;

    [JsonIgnore]
    public bool HasLocalization => Assets.Count > 0 || LocresAssets.Count > 0;

    [JsonIgnore]
    public string LocalizationKind => (Assets.Count > 0, LocresAssets.Count > 0) switch
    {
        (true, true) => $"DB {Assets.Count} + LOCRES {LocresAssets.Count}",
        (true, false) => $"DB {Assets.Count}",
        (false, true) => $"LOCRES {LocresAssets.Count}",
        _ => "—",
    };
}

namespace STALKER2LocalizationTool.Models;

public enum BuildMode
{
    Modular,
    AllInOne,
}

public sealed class ModBuildResult
{
    public string ModId { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public bool Built { get; set; }
    public bool Verified { get; set; }
    public int AssetsPatched { get; set; }
    public int MatchedSids { get; set; }
    public int ChangedSids { get; set; }
    public bool LocresBuilt { get; set; }
    public string? OutputUtoc { get; set; }
    public string? OutputLocresPak { get; set; }
    public List<string> OutputFiles { get; set; } = new();
    public string? Message { get; set; }
}

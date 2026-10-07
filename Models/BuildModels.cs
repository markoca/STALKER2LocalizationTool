namespace LocalizationWorkbench.Models;

public enum BuildMode
{
    Modular,
    AllInOne,
}

public sealed class ModBuildResult
{
    public string ModId { get; set; } = string.Empty;
    public bool Built { get; set; }
    public bool Verified { get; set; }
    public int AssetsPatched { get; set; }
    public List<string> OutputFiles { get; set; } = new();
}

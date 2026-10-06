namespace LocalizationWorkbench.Models;

public sealed class RawExportInfo
{
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public long SerialSize { get; set; }
    public long SerialOffset { get; set; }
    public int ExportCount { get; set; }
    public bool ImportsLocalizationDatabaseClass { get; set; }
}

namespace LocalizationWorkbench.Models;

public enum FStringEncoding
{
    Ansi,
    Wide,
}

public sealed class LocalizationTranslation
{
    public long LanguageId { get; set; }
    public string Value { get; set; } = string.Empty;
    public FStringEncoding Encoding { get; set; }
}

public sealed class LocalizationRecord
{
    public string Sid { get; set; } = string.Empty;
    public List<LocalizationTranslation> Translations { get; set; } = new();
}

public sealed class LocalizationPayload
{
    public List<LocalizationRecord> Records { get; set; } = new();
}

public sealed class PatchResult
{
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public List<string> MatchedSids { get; set; } = new();
    public List<string> ChangedSids { get; set; } = new();
    public Dictionary<string, string> ExpectedValues { get; set; } = new(StringComparer.Ordinal);
}

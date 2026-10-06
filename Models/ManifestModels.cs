namespace LocalizationWorkbench.Models;

public sealed class ExtractedManifest
{
    public int SchemaVersion { get; set; } = AppConstants.ManifestSchemaVersion;
    public string ModId { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public DateTime ExtractedAtUtc { get; set; }
    public string SourceFingerprint { get; set; } = string.Empty;
    public List<SourceFileFingerprint> SourceFiles { get; set; } = new();
    public List<ExtractedAssetManifest> Assets { get; set; } = new();
    public List<ExtractedLocresManifest> LocresAssets { get; set; } = new();
}

public sealed class SourceFileFingerprint
{
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class ExtractedAssetManifest
{
    public string ZenChunkId { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string VirtualPath { get; set; } = string.Empty;
    public string LegacyRelativePath { get; set; } = string.Empty;
    public string SourceContainerRelativePath { get; set; } = string.Empty;
    public string InternalPackagePath { get; set; } = string.Empty;
    public string SourcePackageIdentityPath { get; set; } = string.Empty;
    public string DirectoryAliasPackagePath { get; set; } = string.Empty;
    public List<AliasManifest> Aliases { get; set; } = new();
    public string UassetFile { get; set; } = string.Empty;
    public string UexpFile { get; set; } = string.Empty;
    public string AssetJsonFile { get; set; } = string.Empty;
    public string ScriptObjectsFile { get; set; } = string.Empty;
    public int SidCount { get; set; }
}

public sealed class ExtractedLocresManifest
{
    public string SourcePakRelativePath { get; set; } = string.Empty;
    public string InternalPath { get; set; } = string.Empty;
    public string CultureCode { get; set; } = string.Empty;
    public string SourceLocresFile { get; set; } = string.Empty;
    public int SidCount { get; set; }
    public int LocresVersion { get; set; }
}

public sealed class AliasManifest
{
    public string SourceContainerRelativePath { get; set; } = string.Empty;
    public string VirtualPath { get; set; } = string.Empty;
    public string InternalPackagePath { get; set; } = string.Empty;
    public string DirectoryAliasPackagePath { get; set; } = string.Empty;
    public int SidCount { get; set; }
}

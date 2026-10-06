using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

public static class EditableScanner
{
    public static string? FindTranslationFile(string editableRoot, ModScanResult mod, BuildLanguage language)
    {
        if (string.IsNullOrWhiteSpace(editableRoot) || !Directory.Exists(editableRoot))
            return null;

        var safeName = PathUtil.MakeSafeName(mod.ModName);
        var languageFile = language.Key + ".json";
        var candidates = new[]
        {
            Path.Combine(editableRoot, mod.ModId, languageFile),
            Path.Combine(editableRoot, safeName, languageFile),
            Path.Combine(editableRoot, mod.ModId + "." + languageFile),
            Path.Combine(editableRoot, safeName + "." + languageFile),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(editableRoot, languageFile, SearchOption.AllDirectories))
            {
                var dir = Path.GetFileName(Path.GetDirectoryName(path));
                if (string.Equals(dir, mod.ModId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dir, safeName, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
        }
        catch { }

        return null;
    }

    public static Dictionary<string, string> LoadFlatTranslations(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Translation JSON must be a top-level object: {path}");

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
                result[property.Name] = property.Value.GetString() ?? string.Empty;
            else if (property.Value.ValueKind == JsonValueKind.Null)
                result[property.Name] = string.Empty;
            else
                result[property.Name] = property.Value.ToString();
        }
        return result;
    }
}

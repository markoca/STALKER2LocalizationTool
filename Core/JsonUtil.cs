namespace LocalizationWorkbench.Core;

public static class JsonUtil
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static T Load<T>(string path) where T : class
    {
        var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), Pretty);
        return value ?? throw new InvalidDataException($"Could not read JSON: {path}");
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Pretty), new UTF8Encoding(false));
    }
}

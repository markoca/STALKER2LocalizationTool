using Microsoft.Win32;

namespace STALKER2LocalizationTool.Services;

public static class SteamLocator
{
    private const string GameRelativePath = @"steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl\Stalker2\Content\Paks";

    public static string? TryFindGamePaksFolder()
    {
        foreach (var root in CandidateSteamRoots())
        {
            var direct = Path.Combine(root, GameRelativePath);
            if (Directory.Exists(direct))
                return direct;

            var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
                continue;

            try
            {
                var text = File.ReadAllText(libraryFile, Encoding.UTF8);
                foreach (Match match in Regex.Matches(
                             text,
                             "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"",
                             RegexOptions.IgnoreCase))
                {
                    var library = match.Groups["path"].Value.Replace("\\\\", "\\");
                    var candidate = Path.Combine(library, GameRelativePath);
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }
            catch
            {
                // Detection is best-effort. Manual selection is always available.
            }
        }

        var commonCandidates = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
        };

        foreach (var root in commonCandidates)
        {
            var candidate = Path.Combine(root, GameRelativePath);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> CandidateSteamRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in new[]
                 {
                     ReadRegistry(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                     ReadRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                     ReadRegistry(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
                 })
        {
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value) && seen.Add(value))
                yield return value;
        }
    }

    private static string? ReadRegistry(RegistryKey hive, string subKey, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(subKey);
            return key?.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }
}

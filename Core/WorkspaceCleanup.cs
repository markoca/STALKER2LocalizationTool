using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

public static class WorkspaceCleanup
{
    private static readonly TimeSpan StaleAge = TimeSpan.FromHours(6);

    public static int RemoveStaleTransientDirectories(AppSettings settings, Action<string>? log = null)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        CollectTopLevelStaging(settings.SourceFolder, targets);
        CollectTopLevelStaging(settings.TranslationsFolder, targets);
        CollectWorkDirectories(settings.OutputFolder, targets);

        var removed = 0;
        foreach (var target in targets.OrderByDescending(path => path.Length))
        {
            try
            {
                if (!Directory.Exists(target) || !IsStale(target))
                    continue;
                Directory.Delete(target, recursive: true);
                removed++;
            }
            catch
            {
                // Cleanup is best-effort. Never block startup because a stale temp
                // directory is locked or owned by another process.
            }
        }

        if (removed > 0)
            log?.Invoke($"Startup cleanup: removed {removed} stale temporary workspace(s).");
        return removed;
    }

    private static bool IsStale(string path)
    {
        try
        {
            return DateTime.UtcNow - Directory.GetLastWriteTimeUtc(path) >= StaleAge;
        }
        catch
        {
            return false;
        }
    }

    private static void CollectTopLevelStaging(string root, HashSet<string> targets)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, ".*.staging.*", SearchOption.TopDirectoryOnly))
                targets.Add(directory);
        }
        catch { }
    }

    private static void CollectWorkDirectories(string root, HashSet<string> targets)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, ".work", SearchOption.AllDirectories).ToList())
                targets.Add(directory);
        }
        catch { }
    }
}

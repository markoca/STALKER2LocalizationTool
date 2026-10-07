using System.Runtime.InteropServices;

namespace LocalizationWorkbench.Core;

public static class FileLinker
{
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    public static void LinkOrCopy(string source, string destination)
    {
        if (TryLink(source, destination))
            return;

        File.Copy(source, destination, overwrite: true);
    }

    public static async Task<bool> LinkOrCopyAsync(
        string source,
        string destination,
        CancellationToken cancellationToken = default)
    {
        if (TryLink(source, destination))
            return true;

        await using var input = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        await using var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        await input.CopyToAsync(output, 1024 * 1024, cancellationToken);
        return false;
    }

    private static bool TryLink(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
            File.Delete(destination);

        try
        {
            if (OperatingSystem.IsWindows()
                && CreateHardLink(destination, source, IntPtr.Zero))
            {
                return true;
            }
        }
        catch
        {
        }

        try
        {
            File.CreateSymbolicLink(destination, source);
            return true;
        }
        catch
        {
        }

        return false;
    }
}

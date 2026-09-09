using System.Runtime.InteropServices;

namespace STALKER2LocalizationTool.Core;

public static class FileLinker
{
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    public static void LinkOrCopy(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination)) File.Delete(destination);

        try
        {
            if (OperatingSystem.IsWindows() && CreateHardLink(destination, source, IntPtr.Zero))
                return;
        }
        catch { }

        try
        {
            File.CreateSymbolicLink(destination, source);
            return;
        }
        catch { }

        File.Copy(source, destination, overwrite: true);
    }
}

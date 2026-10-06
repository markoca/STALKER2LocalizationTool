using STALKER2LocalizationTool.Localization;
using STALKER2LocalizationTool.Models;
using STALKER2LocalizationTool.UI;

namespace STALKER2LocalizationTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var settings = AppSettings.CreateRuntime();
        var localizer = new Localizer();

        Application.Run(new MainForm(settings, localizer));
    }
}

using STALKER2LocalizationTool.Localization;
using STALKER2LocalizationTool.Services;
using STALKER2LocalizationTool.UI;

namespace STALKER2LocalizationTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        var localizer = new Localizer();

        Application.Run(new MainForm(settingsService, settings, localizer));
    }
}

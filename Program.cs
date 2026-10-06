using LocalizationWorkbench.Localization;
using LocalizationWorkbench.Services;
using LocalizationWorkbench.UI;

namespace LocalizationWorkbench;

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

using LocalizationWorkbench.Localization;
using LocalizationWorkbench.Models;
using LocalizationWorkbench.Services;
using LocalizationWorkbench.UI;

namespace LocalizationWorkbench;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var settings = AppSettings.CreateRuntime();
        UserPathStore.Apply(settings);
        var localizer = new Localizer();
        localizer.SetLanguage(settings.InterfaceLanguage);

        Application.Run(new MainForm(settings, localizer));
    }
}

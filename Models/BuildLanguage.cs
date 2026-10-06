namespace LocalizationWorkbench.Models;

public sealed record BuildLanguage(int Id, string Key, string EnglishName, string LocresCulture);

public static class BuildLanguageCatalog
{
    public static readonly IReadOnlyList<BuildLanguage> All = new[]
    {
        new BuildLanguage(0,  "arabic", "Arabic", "ar"),
        new BuildLanguage(1,  "chinese_simplified", "Chinese (Simplified)", "zh-Hans"),
        new BuildLanguage(2,  "chinese_traditional", "Chinese (Traditional)", "zh-Hant"),
        new BuildLanguage(3,  "czech", "Czech", "cs"),
        new BuildLanguage(4,  "english", "English", "en"),
        new BuildLanguage(5,  "french", "French", "fr"),
        new BuildLanguage(6,  "german", "German", "de"),
        new BuildLanguage(7,  "italian", "Italian", "it"),
        new BuildLanguage(8,  "japanese", "Japanese", "ja"),
        new BuildLanguage(9,  "korean", "Korean", "ko"),
        new BuildLanguage(10, "polish", "Polish", "pl"),
        new BuildLanguage(11, "portuguese_brazil", "Portuguese (Brazil)", "pt-BR"),
        new BuildLanguage(12, "russian", "Russian", "ru"),
        new BuildLanguage(13, "serbian", "Serbian", "sr"),
        new BuildLanguage(14, "spanish_europe", "Spanish (Europe)", "es"),
        new BuildLanguage(15, "spanish_latin_america", "Spanish (Latin America)", "es-419"),
        new BuildLanguage(16, "turkish", "Turkish", "tr"),
        new BuildLanguage(17, "ukrainian", "Ukrainian", "uk"),
    };

    public static BuildLanguage ById(int id) =>
        All.FirstOrDefault(x => x.Id == id) ?? All.First(x => x.Id == 4);
}

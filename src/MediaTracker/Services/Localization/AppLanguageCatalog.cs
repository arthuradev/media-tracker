using System.Globalization;
using MediaTracker.Models;

namespace MediaTracker.Services;

public static class AppLanguageCatalog
{
    public static IReadOnlyList<AppLanguage> SupportedLanguages { get; } =
    [
        AppLanguage.English,
        AppLanguage.PortugueseBrazil,
        AppLanguage.Spanish
    ];

    public static CultureInfo GetCulture(AppLanguage language) => language switch
    {
        AppLanguage.PortugueseBrazil => CultureInfo.GetCultureInfo("pt-BR"),
        AppLanguage.Spanish => CultureInfo.GetCultureInfo("es-ES"),
        _ => CultureInfo.GetCultureInfo("en-US")
    };

    public static string GetCultureCode(AppLanguage language) => GetCulture(language).Name;

    public static AppLanguage InferFromCulture(CultureInfo? culture)
    {
        string name = culture?.Name ?? string.Empty;
        if (name.StartsWith("pt-BR", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.PortugueseBrazil;

        if (name.StartsWith("es-ES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("es-", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.Spanish;

        return AppLanguage.English;
    }

    public static bool TryParse(string? value, out AppLanguage language)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            language = AppLanguage.English;
            return false;
        }

        string normalized = value.Trim();
        foreach (var supportedLanguage in SupportedLanguages)
        {
            if (string.Equals(normalized, supportedLanguage.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, GetCultureCode(supportedLanguage), StringComparison.OrdinalIgnoreCase))
            {
                language = supportedLanguage;
                return true;
            }
        }

        language = AppLanguage.English;
        return false;
    }

    public static string GetNativeDisplayName(AppLanguage language) => language switch
    {
        AppLanguage.PortugueseBrazil => "Português (Brasil)",
        AppLanguage.Spanish => "Español",
        _ => "English"
    };
}

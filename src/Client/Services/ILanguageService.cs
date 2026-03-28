using Client.Models.Language;

namespace Client.Services;

public interface ILanguageService
{
    Task<List<LanguageItem>> GetAvailableLanguagesAsync();
    Task<List<LanguageModule>> GetModulesAsync();
    Task<Dictionary<string, string>> GetTranslationsAsync(string language, string moduleId, string? moduleName = null);
}

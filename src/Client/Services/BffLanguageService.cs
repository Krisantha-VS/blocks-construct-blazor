using System.Net.Http.Json;
using Client.Models.Language;
using Microsoft.AspNetCore.Components;

namespace Client.Services;

public class BffLanguageService(HttpClient http, NavigationManager nav) : ILanguageService
{
    private readonly HttpClient _http = ConfigureClient(http, nav);

    public async Task<List<LanguageItem>> GetAvailableLanguagesAsync()
    {
        return await _http.GetFromJsonAsync<List<LanguageItem>>("/api/localization/languages")
            ?? [];
    }

    public async Task<List<LanguageModule>> GetModulesAsync()
    {
        return await _http.GetFromJsonAsync<List<LanguageModule>>("/api/localization/modules")
            ?? [];
    }

    public async Task<Dictionary<string, string>> GetTranslationsAsync(string language, string moduleId, string? moduleName = null)
    {
        var query = $"language={Uri.EscapeDataString(language)}&moduleId={Uri.EscapeDataString(moduleId)}";
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            query += $"&moduleName={Uri.EscapeDataString(moduleName)}";
        }

        return await _http.GetFromJsonAsync<Dictionary<string, string>>($"/api/localization/translations?{query}")
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static HttpClient ConfigureClient(HttpClient client, NavigationManager nav)
    {
        client.BaseAddress ??= new Uri(nav.BaseUri);
        return client;
    }
}

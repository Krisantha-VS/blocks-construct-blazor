using System.Net.Http.Json;
using System.Text.Json;
using Client.Models.Language;

namespace Client.Services;

public interface ILanguageService
{
    Task<List<LanguageItem>> GetAvailableLanguagesAsync();
    Task<List<LanguageModule>> GetModulesAsync();
    Task<Dictionary<string, string>> GetTranslationsAsync(string language, string moduleId, string? moduleName = null);
}

public class LanguageService(HttpClient http, RuntimeClientConfig runtimeConfig) : ILanguageService
{
    private readonly HashSet<string> _generateAttempted = new(StringComparer.OrdinalIgnoreCase);

    private string ProjectKey => runtimeConfig.XBlocksKey;

    public async Task<List<LanguageItem>> GetAvailableLanguagesAsync()
    {
        try
        {
            var url = $"/uilm/v1/Language/Gets?projectKey={Uri.EscapeDataString(ProjectKey)}";
            using var response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var root = doc.RootElement;
            var list = TryGetPropertyIgnoreCase(root, "data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array
                ? dataElement
                : root;

            var result = new List<LanguageItem>();
            if (list.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in list.EnumerateArray())
            {
                result.Add(new LanguageItem
                {
                    ItemId = ReadString(item, "id", "itemId"),
                    LanguageName = ReadString(item, "name", "languageName"),
                    LanguageCode = ReadString(item, "code", "languageCode"),
                    IsDefault = ReadBool(item, "isDefault"),
                    ProjectKey = ReadString(item, "projectKey")
                });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<LanguageModule>> GetModulesAsync()
    {
        try
        {
            var url = $"/uilm/v1/Module/Gets?projectKey={Uri.EscapeDataString(ProjectKey)}";
            using var response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            var list = TryGetPropertyIgnoreCase(root, "data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array
                ? dataElement
                : root;

            var result = new List<LanguageModule>();
            if (list.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in list.EnumerateArray())
            {
                result.Add(new LanguageModule
                {
                    ItemId = ReadString(item, "id", "itemId"),
                    Name = ReadString(item, "name", "moduleName")
                });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    public async Task<Dictionary<string, string>> GetTranslationsAsync(string language, string moduleId, string? moduleName = null)
    {
        var candidates = BuildLanguageCandidates(language);
        foreach (var candidate in candidates)
        {
            var dict = await TryGetTranslationsAsync(candidate, moduleId, moduleName);
            if (dict is not null)
            {
                return dict;
            }
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, string>?> TryGetTranslationsAsync(string language, string moduleId, string? moduleName)
    {
        var urls = BuildUilmUrls(language, moduleId, moduleName).ToList();
        var dict = await TryFetchTranslationsAsync(urls);
        if (dict is not null)
        {
            return dict;
        }

        // Some tenants require generating module-language files before they can be fetched.
        if (await TryGenerateUilmFileAsync(language, moduleId))
        {
            dict = await TryFetchTranslationsAsync(urls);
            if (dict is not null)
            {
                return dict;
            }
        }

        return null;
    }

    private async Task<Dictionary<string, string>?> TryFetchTranslationsAsync(IReadOnlyList<string> urls)
    {
        foreach (var url in urls)
        {
            try
            {
                using var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();

                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return dict is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // Try the next endpoint variant.
            }
        }

        return null;
    }

    private async Task<bool> TryGenerateUilmFileAsync(string language, string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return false;
        }

        var shortLanguageCode = language.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? language;
        var key = $"{moduleId}|{shortLanguageCode}";
        if (!_generateAttempted.Add(key))
        {
            return false;
        }

        try
        {
            var payload = new
            {
                projectKey = ProjectKey,
                moduleId,
                languageCode = shortLanguageCode
            };

            using var response = await http.PostAsJsonAsync("/uilm/v1/Key/GenerateUilmFile", payload);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            if (TryGetPropertyIgnoreCase(doc.RootElement, "success", out var successElement)
                && successElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> BuildLanguageCandidates(string language)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(language) && seen.Add(language))
        {
            yield return language;
        }

        var normalized = language?.Replace('_', '-');
        if (!string.IsNullOrWhiteSpace(normalized) && seen.Add(normalized))
        {
            yield return normalized;
        }

        var shortCode = normalized?.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(shortCode) && seen.Add(shortCode))
        {
            yield return shortCode;
        }
    }

    private IEnumerable<string> BuildUilmUrls(string language, string moduleId, string? moduleName)
    {
        var encodedLanguage = Uri.EscapeDataString(language);
        var encodedModuleId = Uri.EscapeDataString(moduleId);
        var encodedProjectKey = Uri.EscapeDataString(ProjectKey);
        var encodedModuleName = Uri.EscapeDataString(moduleName ?? string.Empty);

        // Primary tenant-confirmed variant.
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            yield return $"/uilm/v1/Key/GetUilmFile?Language={encodedLanguage}&ModuleName={encodedModuleName}&ProjectKey={encodedProjectKey}";
            yield return $"/uilm/v1/Key/GetUilmFile?language={encodedLanguage}&moduleName={encodedModuleName}&projectKey={encodedProjectKey}";
        }

        // Contract variant from actions/get-uilm-file.md
        yield return $"/uilm/v1/Key/GetUilmFile?language={encodedLanguage}&moduleId={encodedModuleId}&projectKey={encodedProjectKey}";
        yield return $"/uilm/v1/Key/GetUilmFile?Language={encodedLanguage}&moduleId={encodedModuleId}&ProjectKey={encodedProjectKey}";

        // Some environments accept languageCode instead of language.
        yield return $"/uilm/v1/Key/GetUilmFile?languageCode={encodedLanguage}&moduleId={encodedModuleId}&projectKey={encodedProjectKey}";
        yield return $"/uilm/v1/Key/GetUilmFile?LanguageCode={encodedLanguage}&moduleId={encodedModuleId}&ProjectKey={encodedProjectKey}";

        // Legacy/blazor skill variant.
        yield return $"/uilm/v1/UilmFile/Get?projectKey={encodedProjectKey}&languageCode={encodedLanguage}&moduleId={encodedModuleId}";

        // Legacy module-name shape used in some deployments.
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            yield return $"/uilm/v1/UilmFile/Get?ProjectKey={encodedProjectKey}&LanguageCode={encodedLanguage}&ModuleName={encodedModuleName}";
        }
    }

    private static string ReadString(JsonElement source, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGetPropertyIgnoreCase(source, key, out var element) && element.ValueKind != JsonValueKind.Null)
            {
                return element.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool ReadBool(JsonElement source, string key)
    {
        if (TryGetPropertyIgnoreCase(source, key, out var element) &&
            (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
        {
            return element.GetBoolean();
        }

        return false;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement source, string propertyName, out JsonElement value)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (source.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in source.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

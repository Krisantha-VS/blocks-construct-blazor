using System.Net.Http.Json;
using System.Text.Json;
using Client.Models.Language;
using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/localization")]
public class LocalizationController(IBlocksApiGateway gateway, IConfiguration config) : ControllerBase
{
    private readonly HashSet<string> _generateAttempted = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _projectKey = config["ProjectKey"]
        ?? config["ApiSecurity:XBlocksKey"]
        ?? config["ApiClient:XBlocksKey"]
        ?? string.Empty;

    [HttpGet("languages")]
    public async Task<ActionResult<List<LanguageItem>>> GetAvailableLanguages(CancellationToken cancellationToken)
    {
        using var client = gateway.CreateClient();
        var url = $"/uilm/v1/Language/Gets?projectKey={Uri.EscapeDataString(_projectKey)}";
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

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

    [HttpGet("modules")]
    public async Task<ActionResult<List<LanguageModule>>> GetModules(CancellationToken cancellationToken)
    {
        using var client = gateway.CreateClient();
        var url = $"/uilm/v1/Module/Gets?projectKey={Uri.EscapeDataString(_projectKey)}";
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

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

    [HttpGet("translations")]
    public async Task<ActionResult<Dictionary<string, string>>> GetTranslations(
        [FromQuery] string language,
        [FromQuery] string moduleId,
        [FromQuery] string? moduleName,
        CancellationToken cancellationToken)
    {
        var candidates = BuildLanguageCandidates(language);
        foreach (var candidate in candidates)
        {
            var dict = await TryGetTranslationsAsync(candidate, moduleId, moduleName, cancellationToken);
            if (dict is not null)
            {
                return dict;
            }
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, string>?> TryGetTranslationsAsync(
        string language,
        string moduleId,
        string? moduleName,
        CancellationToken cancellationToken)
    {
        var urls = BuildUilmUrls(language, moduleId, moduleName).ToList();
        var dict = await TryFetchTranslationsAsync(urls, cancellationToken);
        if (dict is not null)
        {
            return dict;
        }

        if (await TryGenerateUilmFileAsync(language, moduleId, cancellationToken))
        {
            dict = await TryFetchTranslationsAsync(urls, cancellationToken);
            if (dict is not null)
            {
                return dict;
            }
        }

        return null;
    }

    private async Task<Dictionary<string, string>?> TryFetchTranslationsAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken)
    {
        using var client = gateway.CreateClient();

        foreach (var url in urls)
        {
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return dict is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // Try next endpoint variant.
            }
        }

        return null;
    }

    private async Task<bool> TryGenerateUilmFileAsync(string language, string moduleId, CancellationToken cancellationToken)
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
                projectKey = _projectKey,
                moduleId,
                languageCode = shortLanguageCode
            };

            using var client = gateway.CreateClient();
            using var response = await client.PostAsJsonAsync("/uilm/v1/Key/GenerateUilmFile", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
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
        var encodedProjectKey = Uri.EscapeDataString(_projectKey);
        var encodedModuleName = Uri.EscapeDataString(moduleName ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            yield return $"/uilm/v1/Key/GetUilmFile?Language={encodedLanguage}&ModuleName={encodedModuleName}&ProjectKey={encodedProjectKey}";
            yield return $"/uilm/v1/Key/GetUilmFile?language={encodedLanguage}&moduleName={encodedModuleName}&projectKey={encodedProjectKey}";
        }

        yield return $"/uilm/v1/Key/GetUilmFile?language={encodedLanguage}&moduleId={encodedModuleId}&projectKey={encodedProjectKey}";
        yield return $"/uilm/v1/Key/GetUilmFile?Language={encodedLanguage}&moduleId={encodedModuleId}&ProjectKey={encodedProjectKey}";
        yield return $"/uilm/v1/Key/GetUilmFile?languageCode={encodedLanguage}&moduleId={encodedModuleId}&projectKey={encodedProjectKey}";
        yield return $"/uilm/v1/Key/GetUilmFile?LanguageCode={encodedLanguage}&moduleId={encodedModuleId}&ProjectKey={encodedProjectKey}";
        yield return $"/uilm/v1/UilmFile/Get?projectKey={encodedProjectKey}&languageCode={encodedLanguage}&moduleId={encodedModuleId}";

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
        if (TryGetPropertyIgnoreCase(source, key, out var element)
            && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
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

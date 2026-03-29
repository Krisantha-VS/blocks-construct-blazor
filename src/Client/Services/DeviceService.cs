using System.Text.Json;
using System.Text.RegularExpressions;
using Client.Models.Profile;

namespace Client.Services;

public interface IDeviceService
{
    Task<DeviceSessionResponse> GetSessionsAsync(string userId, int page, int pageSize);
}

public class DeviceService(HttpClient http, RuntimeClientConfig runtimeConfig) : IDeviceService
{
    private string ProjectKey => runtimeConfig.XBlocksKey;

    public async Task<DeviceSessionResponse> GetSessionsAsync(string userId, int page, int pageSize)
    {
        var query = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["projectkey"] = ProjectKey,
            ["filter.userId"] = userId
        };

        var queryString = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var raw = await http.GetStringAsync($"/idp/v1/Iam/GetSessions?{queryString}");
        return ParseMongoResponse(raw);
    }

    private static DeviceSessionResponse ParseMongoResponse(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            var totalCount = TryGetPropertyIgnoreCase(root, "totalCount", out var totalCountElement)
                && totalCountElement.ValueKind == JsonValueKind.Number
                ? totalCountElement.GetInt32()
                : 0;

            var sessions = new List<DeviceSession>();
            if (TryGetPropertyIgnoreCase(root, "data", out var dataElement)
                && dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataElement.EnumerateArray())
                {
                    var session = ParseSession(item);
                    if (session is not null)
                        sessions.Add(session);
                }
            }

            var errors = TryGetPropertyIgnoreCase(root, "errors", out var errorsElement)
                ? errorsElement.ValueKind == JsonValueKind.Null
                    ? null
                    : errorsElement.GetRawText()
                : null;

            return new DeviceSessionResponse
            {
                TotalCount = totalCount,
                Data = sessions,
                Errors = errors
            };
        }
        catch
        {
            return new DeviceSessionResponse();
        }
    }

    private static DeviceSession? ParseSession(JsonElement item)
    {
        try
        {
            JsonElement source;
            JsonDocument? rowDocument = null;

            if (item.ValueKind == JsonValueKind.String)
            {
                var rawRow = item.GetString();
                if (string.IsNullOrWhiteSpace(rawRow))
                    return null;

                var cleaned = CleanMongoJson(rawRow);
                if (string.IsNullOrWhiteSpace(cleaned))
                    return null;

                rowDocument = JsonDocument.Parse(cleaned);
                source = rowDocument.RootElement;
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                source = item;
            }
            else
            {
                return null;
            }

            var session = new DeviceSession
            {
                ItemId = ReadObjectId(source, "_id"),
                RefreshToken = ReadString(source, "RefreshToken"),
                TenantId = ReadString(source, "TenantId"),
                IssuedUtc = ReadMongoDate(source, "IssuedUtc"),
                ExpiresUtc = ReadMongoDate(source, "ExpiresUtc"),
                UserId = ReadString(source, "UserId"),
                IpAddresses = ReadString(source, "IpAddresses"),
                IsActive = ReadBool(source, "IsActive"),
                CreateDate = ReadMongoDate(source, "CreateDate"),
                UpdateDate = ReadMongoDate(source, "UpdateDate"),
                DeviceInformation = ReadDeviceInformation(source)
            };

            rowDocument?.Dispose();
            return session;
        }
        catch
        {
            return null;
        }
    }

    private static string? CleanMongoJson(string input)
    {
        try
        {
            var cleaned = Regex.Replace(input, "ObjectId\\(\"([^\"]*)\"\\)", "\"$1\"");
            cleaned = Regex.Replace(cleaned, "ISODate\\(\"([^\"]*)\"\\)", "\"$1\"");
            cleaned = Regex.Replace(cleaned, @"([{,]\s*)(\w+)\s*:", "$1\"$2\":");

            return cleaned;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadObjectId(JsonElement source, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(source, propertyName, out var value))
            return string.Empty;

        if (value.ValueKind == JsonValueKind.Object
            && TryGetPropertyIgnoreCase(value, "$oid", out var oid)
            && oid.ValueKind == JsonValueKind.String)
        {
            return oid.GetString() ?? string.Empty;
        }

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;

        return string.Empty;
    }

    private static string ReadString(JsonElement source, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(source, propertyName, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static bool ReadBool(JsonElement source, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(source, propertyName, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static DateTime ReadMongoDate(JsonElement source, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(source, propertyName, out var value))
            return default;

        if (TryParseDateElement(value, out var date))
            return date;

        if (value.ValueKind == JsonValueKind.Object
            && TryGetPropertyIgnoreCase(value, "$date", out var dateValue)
            && TryParseDateElement(dateValue, out date))
        {
            return date;
        }

        return default;
    }

    private static bool TryParseDateElement(JsonElement value, out DateTime date)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (DateTime.TryParse(text, out date))
                return true;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixMs))
        {
            date = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
            return true;
        }

        date = default;
        return false;
    }

    private static DeviceInformation ReadDeviceInformation(JsonElement source)
    {
        if (!TryGetPropertyIgnoreCase(source, "DeviceInformation", out var value)
            || value.ValueKind != JsonValueKind.Object)
        {
            return new DeviceInformation();
        }

        return new DeviceInformation
        {
            Browser = ReadString(value, "Browser"),
            OS = ReadString(value, "OS"),
            Device = ReadString(value, "Device"),
            Brand = ReadString(value, "Brand"),
            Model = ReadString(value, "Model")
        };
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
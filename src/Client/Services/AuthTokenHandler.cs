using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace Client.Services;

/// <summary>
/// Handles auth for all API requests:
/// - attaches access token
/// - on 401, refreshes once and retries once
/// - on refresh failure, clears tokens and redirects to login
/// </summary>
public sealed class AuthTokenHandler(
    ILocalStorageService localStorage,
    NavigationManager nav) : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private static readonly HttpRequestOptionsKey<bool> IsRetryRequestOption = new("AuthTokenHandler.IsRetry");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachTokenAsync(request);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (IsTokenEndpoint(request.RequestUri) || IsRetryRequest(request))
        {
            return response;
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var refreshed = await TryRefreshTokenAsync(cancellationToken);
            if (!refreshed)
            {
                await LogoutAsync();
                return response;
            }

            response.Dispose();

            var retryRequest = await CloneRequestAsync(request);
            retryRequest.Options.Set(IsRetryRequestOption, true);
            await AttachTokenAsync(retryRequest);

            return await base.SendAsync(retryRequest, cancellationToken);
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token) && !IsTokenEndpoint(request.RequestUri))
        {
            token = await WaitForAccessTokenAsync();
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var refreshToken = await GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            using var refreshClient = new HttpClient
            {
                BaseAddress = new Uri(nav.BaseUri)
            };

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });

            var refreshResponse = await refreshClient.PostAsync("/api/auth/token", body, cancellationToken);

            if (refreshResponse.StatusCode == HttpStatusCode.BadRequest || !refreshResponse.IsSuccessStatusCode)
            {
                return false;
            }

            var payload = await refreshResponse.Content.ReadFromJsonAsync<RefreshTokenResponse>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(payload?.AccessToken))
            {
                return false;
            }

            await SetAccessTokenAsync(payload.AccessToken);
            if (!string.IsNullOrWhiteSpace(payload.RefreshToken))
            {
                await SetRefreshTokenAsync(payload.RefreshToken);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task LogoutAsync()
    {
        await localStorage.RemoveItemAsync("access_token");
        await localStorage.RemoveItemAsync("refresh_token");
        await localStorage.RemoveItemAsync("accessToken");
        await localStorage.RemoveItemAsync("refreshToken");
        nav.NavigateTo("/login", replace: true);
    }

    private static bool IsRetryRequest(HttpRequestMessage request) =>
        request.Options.TryGetValue(IsRetryRequestOption, out var isRetry) && isRetry;

    private static bool IsTokenEndpoint(Uri? uri) =>
        uri is not null
        && (uri.AbsolutePath.Contains("/idp/v1/Authentication/Token", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("/api/auth/token", StringComparison.OrdinalIgnoreCase));

    private async Task<string?> GetAccessTokenAsync()
    {
        var snake = NormalizeToken(await localStorage.GetItemAsStringAsync("access_token"));
        if (!string.IsNullOrWhiteSpace(snake))
        {
            return snake;
        }

        return NormalizeToken(await localStorage.GetItemAsStringAsync("accessToken"));
    }

    private async Task<string?> WaitForAccessTokenAsync()
    {
        // Initial hydration can race with the first protected request on app startup.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var token = await GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            await Task.Delay(120);
        }

        return null;
    }

    private async Task<string?> GetRefreshTokenAsync()
    {
        var snake = NormalizeToken(await localStorage.GetItemAsStringAsync("refresh_token"));
        if (!string.IsNullOrWhiteSpace(snake))
        {
            return snake;
        }

        return NormalizeToken(await localStorage.GetItemAsStringAsync("refreshToken"));
    }

    private async Task SetAccessTokenAsync(string token)
    {
        await localStorage.SetItemAsStringAsync("access_token", token);
        await localStorage.SetItemAsStringAsync("accessToken", token);
    }

    private async Task SetRefreshTokenAsync(string token)
    {
        await localStorage.SetItemAsStringAsync("refresh_token", token);
        await localStorage.SetItemAsStringAsync("refreshToken", token);
    }

    private static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return token.Trim().Trim('"');
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy
        };

        foreach (var header in original.Headers)
        {
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private sealed class RefreshTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }
}
using System.Net.Http.Json;
using Blazored.LocalStorage;
using Client.Models.Auth;
using Microsoft.AspNetCore.Components;

namespace Client.Services;

public class BffAuthService(
    HttpClient http,
    NavigationManager nav,
    ILocalStorageService localStorage,
    AppAuthStateProvider authState) : IAuthService
{
    private readonly HttpClient _http = ConfigureClient(http, nav);

    public async Task<SignInResponse> SignInAsync(string username, string password)
    {
        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password
        };

        var response = await _http.PostAsync("/api/auth/token", new FormUrlEncodedContent(payload));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SignInResponse>() ?? new SignInResponse();

        if (!result.EnableMfa && !string.IsNullOrWhiteSpace(result.AccessToken))
        {
            await SetTokensAsync(result.AccessToken, result.RefreshToken ?? string.Empty);
            authState.NotifyAuthStateChanged();
        }

        return result;
    }

    public async Task<SignInResponse> VerifyMfaAsync(MfaVerifyRequest request)
    {
        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "mfa_code",
            ["mfa_id"] = request.MfaId,
            ["mfa_type"] = request.MfaType,
            ["otp"] = request.Code
        };

        var response = await _http.PostAsync("/api/auth/token", new FormUrlEncodedContent(payload));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SignInResponse>() ?? new SignInResponse();
        if (!string.IsNullOrWhiteSpace(result.AccessToken))
        {
            await SetTokensAsync(result.AccessToken, result.RefreshToken ?? string.Empty);
            authState.NotifyAuthStateChanged();
        }

        return result;
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/reset-password", new
        {
            code = request.Code,
            newPassword = request.Password
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task SetPasswordAsync(SetPasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/activate", new
        {
            code = request.Code,
            password = request.Password
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task SignOutAsync()
    {
        try
        {
            var refreshToken = NormalizeToken(await localStorage.GetItemAsStringAsync("refresh_token"))
                ?? NormalizeToken(await localStorage.GetItemAsStringAsync("refreshToken"));

            await _http.PostAsJsonAsync("/api/auth/logout", new { refreshToken });
        }
        finally
        {
            await localStorage.RemoveItemAsync("access_token");
            await localStorage.RemoveItemAsync("refresh_token");
            await localStorage.RemoveItemAsync("accessToken");
            await localStorage.RemoveItemAsync("refreshToken");
            authState.NotifyAuthStateChanged();
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var token = NormalizeToken(await localStorage.GetItemAsStringAsync("access_token"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return NormalizeToken(await localStorage.GetItemAsStringAsync("accessToken"));
    }

    private async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await localStorage.SetItemAsStringAsync("access_token", accessToken);
        await localStorage.SetItemAsStringAsync("refresh_token", refreshToken);
        await localStorage.SetItemAsStringAsync("accessToken", accessToken);
        await localStorage.SetItemAsStringAsync("refreshToken", refreshToken);
    }

    private static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return token.Trim().Trim('"');
    }

    private static HttpClient ConfigureClient(HttpClient client, NavigationManager nav)
    {
        client.BaseAddress ??= new Uri(nav.BaseUri);
        return client;
    }
}

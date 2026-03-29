using System.Net.Http.Json;
using Blazored.LocalStorage;
using Client.Models.Auth;
using Microsoft.Extensions.Configuration;

namespace Client.Services;

public interface IAuthService
{
    Task<SignInResponse> SignInAsync(string username, string password);
    Task<SignInResponse> VerifyMfaAsync(MfaVerifyRequest request);
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task SetPasswordAsync(SetPasswordRequest request);
    Task SignOutAsync();
    Task<string?> GetAccessTokenAsync();
}

public class AuthService(
    HttpClient http,
    ILocalStorageService localStorage,
    RuntimeClientConfig runtimeConfig,
    AppAuthStateProvider authState) : IAuthService
{
    private string ProjectKey => runtimeConfig.XBlocksKey;

    public async Task<SignInResponse> SignInAsync(string username, string password)
    {
        if (!string.IsNullOrWhiteSpace(ProjectKey))
        {
            http.DefaultRequestHeaders.Remove("x-blocks-key");
            http.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", ProjectKey);
        }

        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password
        };

        var response = await http.PostAsync("/idp/v1/Authentication/Token", new FormUrlEncodedContent(payload));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SignInResponse>() ?? new SignInResponse();

        if (!result.EnableMfa && !string.IsNullOrWhiteSpace(result.AccessToken))
        {
            await SetTokensAsync(result.AccessToken, result.RefreshToken ?? "");
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

        var response = await http.PostAsync("/idp/v1/Authentication/Token", new FormUrlEncodedContent(payload));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SignInResponse>() ?? new SignInResponse();
        if (!string.IsNullOrWhiteSpace(result.AccessToken))
        {
            await SetTokensAsync(result.AccessToken, result.RefreshToken ?? "");
            authState.NotifyAuthStateChanged();
        }

        return result;
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var response = await http.PostAsJsonAsync("/idp/v1/Iam/Recover", new { email, projectKey = ProjectKey });
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var body = new { code = request.Code, newPassword = request.Password, projectKey = ProjectKey };
        var response = await http.PostAsJsonAsync("/idp/v1/Iam/ResetPassword", body);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetPasswordAsync(SetPasswordRequest request)
    {
        var body = new { code = request.Code, password = request.Password, projectKey = ProjectKey };
        var response = await http.PostAsJsonAsync("/idp/v1/Iam/Activate", body);
        response.EnsureSuccessStatusCode();
    }

    public async Task SignOutAsync()
    {
        try
        {
            var refreshToken = NormalizeToken(await localStorage.GetItemAsStringAsync("refresh_token"))
                ?? NormalizeToken(await localStorage.GetItemAsStringAsync("refreshToken"));
            await http.PostAsJsonAsync("/idp/v1/Authentication/Logout", new { refreshToken });
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
}

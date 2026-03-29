using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using Client.Models.Auth;

namespace Client.Services;

public interface ISsoService
{
    /// <summary>
    /// GET /idp/v1/Authentication/GetLoginOptions
    /// Returns which grant types + SSO providers are enabled for this project.
    /// Call on login page load to determine which UI elements to show.
    /// </summary>
    Task<LoginOptions?> GetLoginOptionsAsync();

    /// <summary>
    /// POST /idp/v1/Authentication/GetSocialLogInEndPoint
    /// Returns the external provider URL the browser must navigate to.
    /// </summary>
    Task<GetSocialLoginEndpointResponse?> GetSocialLoginEndpointAsync(
        string provider, string audience);

    /// <summary>
    /// POST /idp/v1/Authentication/Token (grant_type=social)
    /// Exchanges the ?code + ?state from the SSO callback URL for access/refresh tokens.
    /// </summary>
    Task<SignInResponse> ExchangeSocialCodeAsync(string code, string state);
}

public class SsoService(
    HttpClient http,
    RuntimeClientConfig runtimeConfig,
    ILocalStorageService localStorage,
    AppAuthStateProvider authState) : ISsoService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string ProjectKey => runtimeConfig.XBlocksKey;

    private void ApplyProjectKey()
    {
        http.DefaultRequestHeaders.Remove("x-blocks-key");
        if (!string.IsNullOrWhiteSpace(ProjectKey))
            http.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", ProjectKey);
    }

    // ── 1. GetLoginOptions ────────────────────────────────────────────────────

    public async Task<LoginOptions?> GetLoginOptionsAsync()
    {
        try
        {
            ApplyProjectKey();
            return await http.GetFromJsonAsync<LoginOptions>(
                "/idp/v1/Authentication/GetLoginOptions", JsonOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SSO] GetLoginOptions failed: {ex.Message}");
            return null;
        }
    }

    // ── 2. GetSocialLoginEndpoint ─────────────────────────────────────────────

    public async Task<GetSocialLoginEndpointResponse?> GetSocialLoginEndpointAsync(
        string provider, string audience)
    {
        try
        {
            ApplyProjectKey();

            var payload = new GetSocialLoginEndpointRequest
            {
                Provider       = provider,
                Audience       = audience,
                SendAsResponse = true,
            };

            var response = await http.PostAsJsonAsync(
                "/idp/v1/Authentication/GetSocialLogInEndPoint", payload, JsonOptions);

            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw))
                return new GetSocialLoginEndpointResponse { Error = "Empty response from server" };

            return JsonSerializer.Deserialize<GetSocialLoginEndpointResponse>(raw, JsonOptions)
                ?? new GetSocialLoginEndpointResponse { Error = "Invalid response" };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SSO] GetSocialLoginEndpoint failed: {ex.Message}");
            return new GetSocialLoginEndpointResponse { Error = ex.Message };
        }
    }

    // ── 3. Exchange code + state → tokens ────────────────────────────────────

    public async Task<SignInResponse> ExchangeSocialCodeAsync(string code, string state)
    {
        ApplyProjectKey();

        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "social",
            ["code"]       = code,
            ["state"]      = state,
        });

        var response = await http.PostAsync("/idp/v1/Authentication/Token", formData);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"SSO token exchange failed ({(int)response.StatusCode}): {err}");
        }

        var result = await response.Content.ReadFromJsonAsync<SignInResponse>(JsonOptions)
            ?? throw new Exception("Invalid token response from server");

        if (!result.EnableMfa && !string.IsNullOrWhiteSpace(result.AccessToken))
        {
            await localStorage.SetItemAsync("access_token",  result.AccessToken);
            await localStorage.SetItemAsync("refresh_token", result.RefreshToken ?? "");
            authState.NotifyAuthStateChanged();
        }

        return result;
    }
}

using System.Text.Json.Serialization;

namespace Client.Models.Auth;

/// <summary>
/// Response from GET /idp/v1/Authentication/GetLoginOptions.
/// Tells the UI which login methods are enabled and which SSO providers are configured.
/// </summary>
public class LoginOptions
{
    /// <summary>
    /// Which grant types are allowed: "password", "social", "authorization_code"
    /// </summary>
    public List<string> AllowedGrantTypes { get; set; } = new();

    /// <summary>
    /// List of SSO providers configured in the backend.
    /// The UI should ONLY show buttons for providers in this list.
    /// </summary>
    public List<SsoProviderInfo> SsoInfo { get; set; } = new();

    public bool PasswordGrantAllowed
        => AllowedGrantTypes.Contains("password", StringComparer.OrdinalIgnoreCase);

    public bool SocialGrantAllowed
        => AllowedGrantTypes.Contains("social", StringComparer.OrdinalIgnoreCase)
           && SsoInfo.Count > 0;
}

/// <summary>
/// One SSO provider configured in the backend.
/// provider = "microsoft" | "google" | "github" | "linkedin" | "x"
/// </summary>
public class SsoProviderInfo
{
    public string Provider { get; set; } = "";
    public string Audience { get; set; } = "";
}

/// <summary>
/// Request body for POST /idp/v1/Authentication/GetSocialLogInEndPoint
/// </summary>
public class GetSocialLoginEndpointRequest
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("audience")]
    public string Audience { get; set; } = "";

    [JsonPropertyName("sendAsResponse")]
    public bool SendAsResponse { get; set; } = true;
}

/// <summary>
/// Response from POST /idp/v1/Authentication/GetSocialLogInEndPoint
/// </summary>
public class GetSocialLoginEndpointResponse
{
    [JsonPropertyName("providerUrl")]
    public string? ProviderUrl { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("requiresMfa")]
    public bool RequiresMfa { get; set; }

    [JsonPropertyName("mfaToken")]
    public string? MfaToken { get; set; }

    [JsonPropertyName("mfaType")]
    public int? MfaType { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>
/// UI metadata for each known SSO provider — maps provider name to label + icon.
/// </summary>
public class SsoProviderMeta
{
    public string Provider { get; set; } = "";
    public string Label    { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string Audience { get; set; } = "";
}

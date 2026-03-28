using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace Client.Services;

public class AppAuthStateProvider(ILocalStorageService localStorage) : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = NormalizeToken(await localStorage.GetItemAsStringAsync("accessToken"));
            if (string.IsNullOrWhiteSpace(token))
            {
                return Anonymous();
            }

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                // Some IDP deployments can return opaque tokens. Treat token presence as authenticated.
                return AuthenticatedWithoutClaims();
            }

            var jwt = handler.ReadJwtToken(token);
            if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < DateTime.UtcNow)
            {
                return Anonymous();
            }

            var claims = jwt.Claims.Any()
                ? jwt.Claims
                : [new Claim(ClaimTypes.NameIdentifier, "authenticated-user")];

            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Anonymous();
        }
    }

    public void NotifyAuthStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return token.Trim().Trim('"');
    }

    private static AuthenticationState AuthenticatedWithoutClaims() =>
        new(new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "authenticated-user")
        ], "token")));

    private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));
}

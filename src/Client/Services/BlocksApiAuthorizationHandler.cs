using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace Client.Services;

public class BlocksApiAuthorizationHandler(ILocalStorageService localStorage) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            var accessToken = NormalizeToken(await localStorage.GetItemAsStringAsync("accessToken"));
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
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
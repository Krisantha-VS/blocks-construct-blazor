using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace Server.Services;

public class BlocksApiGateway(IHttpClientFactory httpClientFactory, IConfiguration config) : IBlocksApiGateway
{
    private readonly string _apiBaseUrl = config["ApiBaseUrl"]
        ?? config["ApiClient:BaseUrl"]
        ?? "https://api.seliseblocks.com";

    private readonly string _projectKey = config["ProjectKey"]
        ?? config["ApiSecurity:XBlocksKey"]
        ?? config["ApiClient:XBlocksKey"]
        ?? string.Empty;

    public HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_apiBaseUrl);

        if (!string.IsNullOrWhiteSpace(_projectKey))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", _projectKey);
        }

        return client;
    }

    public async Task<BlocksApiRelayResult> SendAsync(
        HttpRequest request,
        HttpRequestMessage outbound,
        CancellationToken cancellationToken,
        bool forwardAuthorization = false)
    {
        using var client = CreateClient();

        if (!string.IsNullOrWhiteSpace(_projectKey) && !outbound.Headers.Contains("x-blocks-key"))
        {
            outbound.Headers.TryAddWithoutValidation("x-blocks-key", _projectKey);
        }

        if (forwardAuthorization
            && request.Headers.TryGetValue("Authorization", out var authHeader)
            && !string.IsNullOrWhiteSpace(authHeader)
            && AuthenticationHeaderValue.TryParse(authHeader.ToString(), out var parsedAuth))
        {
            outbound.Headers.Authorization = parsedAuth;
        }

        using var response = await client.SendAsync(outbound, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return new BlocksApiRelayResult(
            (int)response.StatusCode,
            string.IsNullOrWhiteSpace(content) ? null : content,
            response.Content.Headers.ContentType?.ToString() ?? "application/json");
    }
}

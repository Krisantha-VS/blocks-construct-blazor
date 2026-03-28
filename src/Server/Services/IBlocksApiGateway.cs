using Microsoft.AspNetCore.Http;

namespace Server.Services;

public interface IBlocksApiGateway
{
    HttpClient CreateClient();
    Task<BlocksApiRelayResult> SendAsync(HttpRequest request, HttpRequestMessage outbound, CancellationToken cancellationToken, bool forwardAuthorization = false);
}

public sealed record BlocksApiRelayResult(int StatusCode, string? Content, string ContentType);

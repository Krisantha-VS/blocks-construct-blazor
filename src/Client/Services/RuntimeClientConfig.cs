namespace Client.Services;

public sealed class RuntimeClientConfig
{
    public string MicroserviceApiBaseUrl { get; init; } = string.Empty;
    public string XBlocksKey { get; init; } = string.Empty;
    public string ProjectSlug { get; init; } = string.Empty;
}
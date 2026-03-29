using Blazored.LocalStorage;
using Client.Services;
using Client.State;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Net.Http.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var bootstrapClient = new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
};

var runtimeConfig = await bootstrapClient.GetFromJsonAsync<RuntimeClientConfig>("client-config")
    ?? throw new InvalidOperationException("Runtime config endpoint returned no payload.");
var apiBaseUrl = runtimeConfig.MicroserviceApiBaseUrl
    ?? throw new InvalidOperationException("Runtime config is missing MicroserviceApiBaseUrl.");
var xBlocksKey = runtimeConfig.XBlocksKey;

if (string.IsNullOrWhiteSpace(xBlocksKey))
    throw new InvalidOperationException("Runtime config is missing XBlocksKey.");

builder.Services.AddSingleton(runtimeConfig);

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, AppAuthStateProvider>();
builder.Services.AddScoped<AppAuthStateProvider>();
builder.Services.AddTransient<AuthTokenHandler>();
builder.Services.AddSingleton<SidebarState>();
builder.Services.AddScoped<LanguageState>();

builder.Services.AddHttpClient<IAuthService, AuthService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<IUserService, UserService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<IDeviceService, DeviceService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<IInventoryService, InventoryService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<ILanguageService, LanguageService>(ConfigureBlocksApiClient);

// "LocalApi" — calls to this app's own Server controllers (e.g. /api/sales-orders).
// Always uses the same host the WASM was served from.
builder.Services.AddHttpClient("LocalApi", httpClient =>
{
    httpClient.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
});

// Default HttpClient also points to own server (for untyped injection via @inject HttpClient).
builder.Services.AddScoped(sp =>
{
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
    return httpClient;
});

// "BlocksExternalApi" — calls to the external SELISE Blocks microservice API.
builder.Services.AddHttpClient("BlocksExternalApi", httpClient =>
{
    httpClient.BaseAddress = new Uri(apiBaseUrl);
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
});

void ConfigureBlocksApiClient(HttpClient httpClient)
{
    httpClient.BaseAddress = new Uri(apiBaseUrl);
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
}

await builder.Build().RunAsync();

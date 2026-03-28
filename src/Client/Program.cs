using Blazored.LocalStorage;
using Client.Services;
using Client.State;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var apiBaseUrl = builder.Configuration["ApiClient:BaseUrl"] ?? builder.HostEnvironment.BaseAddress;
var xBlocksKey = builder.Configuration["ApiClient:XBlocksKey"];

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

builder.Services.AddHttpClient<ILanguageService, LanguageService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

// Default HttpClient for same-host app/API calls with x-blocks-key header.
builder.Services.AddScoped(sp =>
{
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };
    
    if (!string.IsNullOrWhiteSpace(xBlocksKey))
    {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
    }
    
    return httpClient;
});

// Named client for cross-service API calls that require x-blocks-key.
builder.Services.AddHttpClient("BlocksExternalApi", httpClient =>
{
    httpClient.BaseAddress = new Uri(apiBaseUrl);

    if (!string.IsNullOrWhiteSpace(xBlocksKey))
    {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
    }
});

void ConfigureBlocksApiClient(HttpClient httpClient)
{
    httpClient.BaseAddress = new Uri(apiBaseUrl);
    if (!string.IsNullOrWhiteSpace(xBlocksKey))
    {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
    }
}


await builder.Build().RunAsync();

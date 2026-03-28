using Blazored.LocalStorage;
using Client.Services;
using Client.State;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

void ConfigureLocalApiClient(HttpClient httpClient)
{
    httpClient.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
}

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, AppAuthStateProvider>();
builder.Services.AddScoped<AppAuthStateProvider>();
builder.Services.AddTransient<AuthTokenHandler>();
builder.Services.AddSingleton<SidebarState>();
builder.Services.AddScoped<LanguageState>();

builder.Services.AddHttpClient<IAuthService, BffAuthService>(ConfigureLocalApiClient);

builder.Services.AddHttpClient<IUserService, BffUserService>(ConfigureLocalApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<IDeviceService, BffDeviceService>(ConfigureLocalApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<IInventoryService, InventoryService>(ConfigureLocalApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<ILanguageService, BffLanguageService>(ConfigureLocalApiClient);

// Default HttpClient for same-host app/API calls.
builder.Services.AddScoped(sp =>
{
    return new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };
});

await builder.Build().RunAsync();

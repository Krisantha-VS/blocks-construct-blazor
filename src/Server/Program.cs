using Blocks.Genesis;
using Blazored.LocalStorage;
using Client.Services;
using Client.State;
using Microsoft.AspNetCore.Components.Authorization;
using Server.Components.Layout;
using Server.Extensions;

const string ServiceName = "blocks-construct-blazor-server";

await ApplicationConfigurations.ConfigureLogAndSecretsAsync(ServiceName, VaultType.Azure);

var builder = WebApplication.CreateBuilder(args);

ApplicationConfigurations.ConfigureApiEnv(builder, args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddBlazoredLocalStorage();
builder.Services
    .AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, AppAuthStateProvider>();
builder.Services.AddScoped<AppAuthStateProvider>();
builder.Services.AddSingleton<SidebarState>();
builder.Services.AddScoped<LanguageState>();
builder.Services.AddTransient<AuthTokenHandler>();

builder.Services.AddHttpClient();

var apiBase = GetRequiredConfigurationValue(
    builder.Configuration,
    "MICROSERVICE_API_BASE_URL",
    "MicroserviceApiBaseUrl");

if (!Uri.TryCreate(apiBase, UriKind.Absolute, out var apiBaseUri))
{
    throw new InvalidOperationException("Missing or invalid API base URL. Configure MICROSERVICE_API_BASE_URL or MicroserviceApiBaseUrl.");
}

var xBlocksKey = GetRequiredConfigurationValue(
    builder.Configuration,
    "X_BLOCKS_KEY",
    "XBlocksKey");

var projectSlug = GetRequiredConfigurationValue(
    builder.Configuration,
    "PROJECT_SLUG",
    "ProjectSlug");

builder.Services.AddSingleton(new RuntimeClientConfig
{
    MicroserviceApiBaseUrl = apiBaseUri.ToString(),
    XBlocksKey = xBlocksKey,
    ProjectSlug = projectSlug
});

builder.Services.AddHttpClient<IAuthService, AuthService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<IUserService, UserService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<IDeviceService, DeviceService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<IInventoryService, InventoryService>(ConfigureBlocksApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<ILanguageService, LanguageService>(ConfigureBlocksApiClient);

ApplicationConfigurations.ConfigureServices(builder.Services, new MessageConfiguration
{
    AzureServiceBusConfiguration = new()
    {
        Queues = new List<string>(),
        Topics = new List<string>(),
    },
});

ApplicationConfigurations.ConfigureApi(builder.Services);

builder.Services.AddApplicationServices(builder.Environment.WebRootPath);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseCors(policy => policy
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetIsOriginAllowed(_ => true)
    .AllowCredentials()
    .SetPreflightMaxAge(TimeSpan.FromDays(365)));

app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api"),
    apiPipeline =>
    {
        apiPipeline.UseMiddleware<TenantValidationMiddleware>();
        apiPipeline.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        apiPipeline.UseAuthentication();
        apiPipeline.UseAuthorization();
    });

app.MapControllers();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAntiforgery();

app.MapGet("/client-config", () => Results.Json(new
{
    MicroserviceApiBaseUrl = apiBase,
    XBlocksKey = xBlocksKey,
    ProjectSlug = projectSlug
}));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

await app.RunAsync();

void ConfigureBlocksApiClient(HttpClient httpClient)
{
    httpClient.BaseAddress = apiBaseUri;
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
}

static string GetRequiredConfigurationValue(IConfiguration configuration, params string[] keys)
{
    foreach (var key in keys)
    {
        var value = configuration[key];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    throw new InvalidOperationException($"Missing required configuration. Set one of: {string.Join(", ", keys)}.");
}

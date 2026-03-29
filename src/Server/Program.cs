using Blazored.LocalStorage;
using Client.Services;
using Client.State;
using Microsoft.AspNetCore.Components.Authorization;
using Server.Components.Layout;
using Server.Extensions;

// Load .env file for local development before builder reads configuration.
// Real environment variables (Docker / Kubernetes / CI) always take precedence.
// Walk up from CWD to find .env — handles both "dotnet run" from repo root
// and "dotnet watch --project src/Server" where CWD is src/Server.
var envFile = FindEnvFile(Directory.GetCurrentDirectory());
if (envFile is not null)
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            continue;

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
            continue;

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        // Only set if not already defined (real env vars win)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}

static string? FindEnvFile(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, ".env");
        if (File.Exists(candidate))
            return candidate;
        dir = dir.Parent;
    }
    return null;
}

var builder = WebApplication.CreateBuilder(args);

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

// Read from environment variables or configuration (no hardcoded URL/key values)
var apiBase = Environment.GetEnvironmentVariable("MICROSERVICE_API_BASE_URL")
    ?? builder.Configuration["MicroserviceApiBaseUrl"];

if (!Uri.TryCreate(apiBase, UriKind.Absolute, out var apiBaseUri))
{
    throw new InvalidOperationException("Missing or invalid microservice API base URL. Set MICROSERVICE_API_BASE_URL or MicroserviceApiBaseUrl in configuration.");
}

var xBlocksKey = Environment.GetEnvironmentVariable("X_BLOCKS_KEY")
    ?? builder.Configuration["XBlocksKey"];

if (string.IsNullOrWhiteSpace(xBlocksKey))
    throw new InvalidOperationException(
        "X_BLOCKS_KEY is required. Add it to your .env file or set the environment variable.");

builder.Services.AddSingleton(new RuntimeClientConfig
{
    MicroserviceApiBaseUrl = apiBaseUri.ToString(),
    XBlocksKey = xBlocksKey
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplicationServices(builder.Environment.WebRootPath);

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAntiforgery();

app.MapControllers();

app.MapGet("/client-config", () => Results.Json(new
{
    MicroserviceApiBaseUrl = apiBase,
    XBlocksKey = xBlocksKey
}));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

app.Run();

void ConfigureBlocksApiClient(HttpClient httpClient)
{
    httpClient.BaseAddress = apiBaseUri;
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-blocks-key", xBlocksKey);
}

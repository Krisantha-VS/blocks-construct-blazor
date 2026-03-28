using Blazored.LocalStorage;
using Client.Services;
using Client.State;
using Microsoft.AspNetCore.Components.Authorization;
using Server.Components.Layout;
using Server.Extensions;
using Server.Services;

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
builder.Services.AddSingleton<IBlocksApiGateway, BlocksApiGateway>();

builder.Services.AddHttpClient<IAuthService, BffAuthService>();
builder.Services.AddHttpClient<IUserService, BffUserService>()
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<IDeviceService, BffDeviceService>()
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<IInventoryService, InventoryService>()
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<ILanguageService, BffLanguageService>();

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
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

app.Run();

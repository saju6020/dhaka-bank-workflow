using System.Text.Json;
using Elsa.Studio.Branding;
using ElsaStudio.Approvals;
using ElsaStudio.Branding;
using Elsa.Studio.Contracts;
using Elsa.Studio.Core.BlazorWasm.Extensions;
using Elsa.Studio.Dashboard.Extensions;
using Elsa.Studio.Extensions;
using Elsa.Studio.Login.BlazorWasm.Extensions;
using Elsa.Studio.Login.Contracts;
using Elsa.Studio.Login.Extensions;
using Elsa.Studio.Login.HttpMessageHandlers;
using Elsa.Studio.Login.Models;
using Elsa.Studio.Models;
using Elsa.Studio.Options;
using Elsa.Studio.Security.Extensions;
using Elsa.Studio.Shell;
using Elsa.Studio.Shell.Extensions;
using Elsa.Studio.Workflows.Dashboard.Extensions;
using Elsa.Studio.Workflows.Designer.Extensions;
using Elsa.Studio.Workflows.Extensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

// Build the host.
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register root components.
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.RootComponents.RegisterCustomElsaStudioElements();

// Register shell services and modules.
builder.Services.AddCore();
builder.Services.AddShell();
var backendApiConfig = new BackendApiConfig
{
    ConfigureHttpClientBuilder = options => options.AuthenticationHandler = typeof(AuthenticatingApiHttpMessageHandler)
};
builder.Services.AddRemoteBackend(backendApiConfig);
builder.Services
    .AddLoginModule()
    .UseElsaIdentity();

// Work around claim parsing in Elsa.Studio.Login 3.8.x: parse JWT string claims without quotes and read the user name from the "name" claim.
builder.Services.AddSingleton<IJwtParser, ElsaStudio.JwtParser>();
builder.Services.Configure<IdentityTokenOptions>(options => options.NameClaimType = "name");

builder.Services.AddDashboardModule(backendApiConfig);
builder.Services.AddWorkflowsDashboardModule();
builder.Services.AddWorkflowsModule();
builder.Services.AddSecurityModule(backendApiConfig);
builder.Services.AddApprovalsModule(backendApiConfig);

// Client branding (name, logos, colors, links). The values are assigned from the client config after the host is built.
var clientBranding = new ClientBranding();
builder.Services.AddSingleton(clientBranding);
builder.Services.AddScoped<IBrandingProvider, ClientBrandingProvider>();
builder.Services.AddStudioThemeProvider<ClientThemeProvider>(ClientThemeProvider.Id);
builder.Services.Configure<StudioThemeOptions>(options => options.Theme = ClientThemeProvider.Id);

// Build the application.
var app = builder.Build();

// Apply client config.
var js = app.Services.GetRequiredService<IJSRuntime>();
var clientConfig = await js.InvokeAsync<JsonElement>("getClientConfig");
var apiUrl = clientConfig.GetProperty("apiUrl").GetString() ?? throw new InvalidOperationException("No API URL configured.");
app.Services.GetRequiredService<IOptions<BackendOptions>>().Value.Url = new(apiUrl);

if (clientConfig.TryGetProperty("branding", out var branding))
    clientBranding.Options = branding.Deserialize<ClientBrandingOptions>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();

// Run each startup task.
var startupTaskRunner = app.Services.GetRequiredService<IStartupTaskRunner>();
await startupTaskRunner.RunStartupTasksAsync();

// Run the application.
await app.RunAsync();
using Elsa.Extensions;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Services;
using ElsaServer.Approval;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Identity;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

var services = builder.Services;
var configuration = builder.Configuration;

services
    .AddElsa(elsa => elsa
        .UseIdentity(identity =>
        {
            identity.TokenOptions = options => options.SigningKey = "large-signing-key-for-signing-JWT-tokens";
            identity.UseEntityFrameworkCore(ef => ef.UsePostgreSql(builder.Configuration.GetConnectionString("PostgreSql")!));
            identity.UseDefaultAdmin("admin", "password", "admin", new List<string> { "*" });
        })
        .UseDefaultAuthentication()
        .UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UsePostgreSql(builder.Configuration.GetConnectionString("PostgreSql")!)))
        .UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UsePostgreSql(builder.Configuration.GetConnectionString("PostgreSql")!)))
        .UseScheduling()
        .UseJavaScript()
        .UseLiquid()
        .UseCSharp()
        .UseHttp(http => http.ConfigureHttpOptions = options => configuration.GetSection("Http").Bind(options))
        .UseWorkflowsApi()
        .UseDashboardApi()
        .UseWorkflowRuntimeDashboard()
        .AddActivitiesFrom<Program>()
        .AddWorkflowsFrom<Program>()
        .AddFastEndpointsAssembly<Program>()
    );

// Designer → approver → publisher governance: only approved drafts can be published.
services.AddHttpContextAccessor();
services.AddScoped<ApprovalService>();
services.AddScoped<WorkflowDefinitionPublisher>();
services.Replace(ServiceDescriptor.Scoped<IWorkflowDefinitionPublisher, ApprovalAwareWorkflowDefinitionPublisher>());

services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().WithExposedHeaders("*")));
services.AddRazorPages(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseRouting();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseWorkflowsApi();
app.UseWorkflows();
app.MapFallbackToPage("/_Host");
app.Run();
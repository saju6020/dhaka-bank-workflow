using Elsa.Studio;
using Elsa.Studio.Abstractions;
using Elsa.Studio.Contracts;
using Elsa.Studio.Extensions;
using Elsa.Studio.Models;
using Elsa.Studio.Security.Services;
using MudBlazor;

namespace ElsaStudio.Approvals;

/// <summary>
/// Registers this assembly with the Studio shell so the router picks up the Approvals page.
/// </summary>
public class ApprovalsFeature : FeatureBase;

/// <summary>
/// Shows the Approvals menu item to users who take part in the approval process.
/// </summary>
public class ApprovalsMenu(IIdentityPermissionService permissionService) : IMenuProvider
{
    public async ValueTask<IEnumerable<MenuItem>> GetMenuItemsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await permissionService.ListAsync(cancellationToken);
        string[] required = [ApprovalPermissions.All, ApprovalPermissions.Submit, ApprovalPermissions.Approve, ApprovalPermissions.Publish];

        if (!required.Any(permissions.Contains))
            return [];

        return
        [
            new MenuItem
            {
                Icon = Icons.Material.Outlined.FactCheck,
                Href = "approvals",
                Text = "Approvals",
                GroupName = MenuItemGroups.General.Name,
                Order = 20
            }
        ];
    }
}

public static class ApprovalsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the workflow approvals page, menu and API client. Requires the security module (for <see cref="IIdentityPermissionService"/>).
    /// </summary>
    public static IServiceCollection AddApprovalsModule(this IServiceCollection services, BackendApiConfig backendApiConfig)
    {
        services.AddScoped<IFeature, ApprovalsFeature>();
        services.AddScoped<IMenuProvider, ApprovalsMenu>();
        services.AddRemoteApi<IWorkflowApprovalsApi>(backendApiConfig);
        return services;
    }
}

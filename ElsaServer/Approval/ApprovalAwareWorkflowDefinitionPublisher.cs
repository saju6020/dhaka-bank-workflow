using Elsa.Common.Models;
using Elsa.Workflows;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Management.Models;
using Elsa.Workflows.Management.Services;
using FastEndpoints;

namespace ElsaServer.Approval;

/// <summary>
/// Decorates Elsa's publisher so that every publish path (save &amp; publish, publish, bulk publish, import) only publishes approved drafts,
/// and drafts can only be edited while they are Draft or Rejected (edits reset the approval to Draft).
/// Requests without an authenticated user (startup, background jobs) and users with the "*" permission are not restricted.
/// </summary>
public class ApprovalAwareWorkflowDefinitionPublisher(
    WorkflowDefinitionPublisher inner,
    IWorkflowDefinitionStore store,
    IHttpContextAccessor httpContextAccessor) : IWorkflowDefinitionPublisher
{
    private const string NotApprovedMessage = "Workflow must be approved before publishing.";

#pragma warning disable CS0618 // Required by the interface.
    public WorkflowDefinition New(IActivity? root = null) => inner.New(root);
#pragma warning restore CS0618

    public Task<WorkflowDefinition> NewAsync(IActivity? root = null, CancellationToken cancellationToken = default) => inner.NewAsync(root, cancellationToken);

    public async Task<PublishWorkflowDefinitionResult> PublishAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var filter = new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.Latest };
        var definition = await store.FindAsync(filter, cancellationToken);
        return definition == null ? await inner.PublishAsync(definitionId, cancellationToken) : await PublishAsync(definition, cancellationToken);
    }

    public async Task<PublishWorkflowDefinitionResult> PublishAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
            return await inner.PublishAsync(definition, cancellationToken);

        if (!ApprovalPermissions.Has(user, ApprovalPermissions.All))
        {
            var error = await GetPublishErrorAsync(definition, user, cancellationToken);

            if (error != null)
                return new PublishWorkflowDefinitionResult(false, [new WorkflowValidationError(error)], new AffectedWorkflows([]));
        }

        var current = ApprovalService.Read(definition) ?? new ApprovalRecord();
        ApprovalService.Write(definition, current with
        {
            Status = ApprovalStatus.Published,
            Version = definition.Version,
            DecidedBy = current.Status == ApprovalStatus.Approved ? current.DecidedBy : ApprovalPermissions.GetUserName(user)
        });

        return await inner.PublishAsync(definition, cancellationToken);
    }

    public Task<WorkflowDefinition?> RetractAsync(string definitionId, CancellationToken cancellationToken = default) => inner.RetractAsync(definitionId, cancellationToken);

    public Task<WorkflowDefinition> RetractAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default) => inner.RetractAsync(definition, cancellationToken);

    public Task<WorkflowDefinition> RevertVersionAsync(string definitionId, int version, CancellationToken cancellationToken = default) => inner.RevertVersionAsync(definitionId, version, cancellationToken);

    public Task<WorkflowDefinition?> GetDraftAsync(string definitionId, VersionOptions versionOptions, CancellationToken cancellationToken = default) => inner.GetDraftAsync(definitionId, versionOptions, cancellationToken);

    public async Task<WorkflowDefinition> SaveDraftAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            // Designers can edit until the workflow is submitted; it is locked while it awaits approval or publishing.
            if (!ApprovalPermissions.Has(user, ApprovalPermissions.All))
            {
                var filter = new WorkflowDefinitionFilter { DefinitionId = definition.DefinitionId, VersionOptions = VersionOptions.Latest };
                var latest = await store.FindAsync(filter, cancellationToken);
                var status = latest == null ? ApprovalStatus.Draft : ApprovalService.GetStatus(latest);

                // Adds the message to the current endpoint's errors and ends the request with 400.
                if (status is ApprovalStatus.Submitted or ApprovalStatus.Approved)
                    ValidationContext.Instance.ThrowError($"This workflow is '{status}' and cannot be edited. It can be edited again after it is rejected or published.");
            }

            // Overwrite any client-supplied approval data.
            ApprovalService.Write(definition, new ApprovalRecord { Status = ApprovalStatus.Draft });
        }

        return await inner.SaveDraftAsync(definition, cancellationToken);
    }

    private async Task<string?> GetPublishErrorAsync(WorkflowDefinition definition, System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!ApprovalPermissions.Has(user, ApprovalPermissions.Publish))
            return "You do not have permission to publish workflows.";

        // Never trust the passed instance's custom properties: the save endpoint copies them from the client.
        var persisted = await store.FindAsync(new WorkflowDefinitionFilter { Id = definition.Id }, cancellationToken);

        if (persisted == null || !ApprovalService.IsApproved(persisted))
            return NotApprovedMessage;

        // Block "change and publish" in a single request: the content must be exactly what was approved.
        if (persisted.StringData != definition.StringData)
            return "The workflow was changed after it was approved. Submit it for approval again.";

        return null;
    }
}

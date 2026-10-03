using System.Security.Claims;
using System.Text.Json;
using Elsa.Common.Models;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Filters;

namespace ElsaServer.Approval;

/// <summary>
/// Reads and changes the approval state of workflow definitions.
/// State changes are written to the store directly (not through the publisher) so that they don't reset the approval.
/// </summary>
public class ApprovalService(IWorkflowDefinitionStore store)
{
    private const string PropertyName = "Approval";

    public static ApprovalRecord? Read(WorkflowDefinition definition)
    {
        if (!definition.CustomProperties.TryGetValue(PropertyName, out var value) || value?.ToString() is not { Length: > 0 } json)
            return null;

        try
        {
            return JsonSerializer.Deserialize<ApprovalRecord>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Write(WorkflowDefinition definition, ApprovalRecord record)
    {
        // Copy the dictionary: drafts cloned from a published version share its custom properties instance.
        definition.CustomProperties = new Dictionary<string, object>(definition.CustomProperties)
        {
            [PropertyName] = JsonSerializer.Serialize(record)
        };
    }

    /// <summary>
    /// Returns the effective status of a version. A record that belongs to another version (e.g. copied by a revert) counts as Draft.
    /// </summary>
    public static string GetStatus(WorkflowDefinition definition)
    {
        if (definition.IsPublished)
            return ApprovalStatus.Published;

        var record = Read(definition);
        return record == null || record.Version != definition.Version ? ApprovalStatus.Draft : record.Status;
    }

    public static bool IsApproved(WorkflowDefinition definition) =>
        !definition.IsPublished && GetStatus(definition) == ApprovalStatus.Approved;

    public async Task<IEnumerable<ApprovalItem>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        var filter = new WorkflowDefinitionFilter { VersionOptions = VersionOptions.Latest, IsSystem = false };
        var definitions = await store.FindManyAsync(filter, cancellationToken);
        var items = definitions.Select(ToItem).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(status) ? items.ToList() : items.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public Task<ApprovalTransitionResult> SubmitAsync(string definitionId, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TransitionAsync(definitionId, [ApprovalStatus.Draft, ApprovalStatus.Rejected], (definition, current) => current with
        {
            Status = ApprovalStatus.Submitted,
            Version = definition.Version,
            SubmittedBy = ApprovalPermissions.GetUserName(user),
            SubmittedAt = DateTimeOffset.UtcNow,
            DecidedBy = null,
            DecidedAt = null,
            Comment = null
        }, cancellationToken);

    public Task<ApprovalTransitionResult> ApproveAsync(string definitionId, ClaimsPrincipal user, string? comment, CancellationToken cancellationToken) =>
        DecideAsync(definitionId, ApprovalStatus.Approved, user, comment, cancellationToken);

    public Task<ApprovalTransitionResult> RejectAsync(string definitionId, ClaimsPrincipal user, string? comment, CancellationToken cancellationToken) =>
        DecideAsync(definitionId, ApprovalStatus.Rejected, user, comment, cancellationToken);

    private Task<ApprovalTransitionResult> DecideAsync(string definitionId, string status, ClaimsPrincipal user, string? comment, CancellationToken cancellationToken) =>
        TransitionAsync(definitionId, [ApprovalStatus.Submitted], (definition, current) => current with
        {
            Status = status,
            Version = definition.Version,
            DecidedBy = ApprovalPermissions.GetUserName(user),
            DecidedAt = DateTimeOffset.UtcNow,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        }, cancellationToken);

    private async Task<ApprovalTransitionResult> TransitionAsync(string definitionId, string[] allowedFrom, Func<WorkflowDefinition, ApprovalRecord, ApprovalRecord> apply, CancellationToken cancellationToken)
    {
        var filter = new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.Latest };
        var definition = await store.FindAsync(filter, cancellationToken);

        if (definition == null)
            return ApprovalTransitionResult.NotFound();

        var status = GetStatus(definition);

        if (!allowedFrom.Contains(status))
            return ApprovalTransitionResult.Failed($"The workflow is '{status}'; this action requires it to be {string.Join(" or ", allowedFrom.Select(x => $"'{x}'"))}.");

        var current = Read(definition) is { } record && record.Version == definition.Version ? record : new ApprovalRecord();
        Write(definition, apply(definition, current));
        await store.SaveAsync(definition, cancellationToken);
        return ApprovalTransitionResult.Success(ToItem(definition));
    }

    private static ApprovalItem ToItem(WorkflowDefinition definition)
    {
        var status = GetStatus(definition);
        var record = Read(definition) is { } r && r.Version == definition.Version ? r : null;
        return new ApprovalItem(definition.DefinitionId, definition.Id, definition.Name, definition.Version, status,
            record?.SubmittedBy, record?.SubmittedAt, record?.DecidedBy, record?.DecidedAt, record?.Comment);
    }
}

public record ApprovalTransitionResult(ApprovalItem? Item, string? Error, bool IsNotFound)
{
    public static ApprovalTransitionResult Success(ApprovalItem item) => new(item, null, false);
    public static ApprovalTransitionResult Failed(string error) => new(null, error, false);
    public static ApprovalTransitionResult NotFound() => new(null, null, true);
}

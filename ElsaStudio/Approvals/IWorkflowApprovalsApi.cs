using Refit;

namespace ElsaStudio.Approvals;

/// <summary>
/// Client for the approval endpoints of the Elsa server (see ElsaServer/Approval).
/// </summary>
public interface IWorkflowApprovalsApi
{
    [Get("/workflow-approvals")]
    Task<ListApprovalsResponse> ListAsync(CancellationToken cancellationToken = default);

    [Post("/workflow-definitions/{definitionId}/approval/submit")]
    Task<ApprovalItem> SubmitAsync(string definitionId, [Body] ApprovalRequest request, CancellationToken cancellationToken = default);

    [Post("/workflow-definitions/{definitionId}/approval/approve")]
    Task<ApprovalItem> ApproveAsync(string definitionId, [Body] ApprovalRequest request, CancellationToken cancellationToken = default);

    [Post("/workflow-definitions/{definitionId}/approval/reject")]
    Task<ApprovalItem> RejectAsync(string definitionId, [Body] ApprovalRequest request, CancellationToken cancellationToken = default);

    [Post("/workflow-definitions/{definitionId}/publish")]
    Task PublishAsync(string definitionId, [Body] ApprovalRequest request, CancellationToken cancellationToken = default);
}

public record ApprovalRequest(string? Comment = null);

public record ListApprovalsResponse(ICollection<ApprovalItem> Items);

public record ApprovalItem(
    string DefinitionId,
    string Id,
    string? Name,
    int Version,
    string Status,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Comment);

public static class ApprovalStatus
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Published = "Published";

    public static readonly string[] All = [Draft, Submitted, Approved, Rejected, Published];
}

public static class ApprovalPermissions
{
    public const string All = "*";
    public const string Submit = "submit:workflow-definitions";
    public const string Approve = "approve:workflow-definitions";
    public const string Publish = "publish:workflow-definitions";
}

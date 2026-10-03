using System.Security.Claims;

namespace ElsaServer.Approval;

/// <summary>
/// The approval lifecycle of a workflow definition draft: Draft → Submitted → Approved/Rejected → Published.
/// </summary>
public static class ApprovalStatus
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Published = "Published";
}

/// <summary>
/// Permission names used to separate the designer, approver and publisher duties.
/// </summary>
public static class ApprovalPermissions
{
    public const string All = "*";
    public const string Write = "write:workflow-definitions";
    public const string Submit = "submit:workflow-definitions";
    public const string Approve = "approve:workflow-definitions";
    public const string Publish = "publish:workflow-definitions";
    public const string Read = "read:workflow-definitions";

    public static bool Has(ClaimsPrincipal user, string permission) =>
        user.FindAll("permissions").Any(x => x.Value == All || x.Value == permission);

    public static string GetUserName(ClaimsPrincipal user) =>
        user.FindFirst("name")?.Value ?? user.Identity?.Name ?? "unknown";
}

/// <summary>
/// The approval state stored on a workflow definition version (in its custom properties).
/// </summary>
public record ApprovalRecord
{
    public string Status { get; init; } = ApprovalStatus.Draft;
    public int Version { get; init; }
    public string? SubmittedBy { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public string? DecidedBy { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public string? Comment { get; init; }
}

/// <summary>
/// A workflow definition's latest version together with its approval state.
/// </summary>
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

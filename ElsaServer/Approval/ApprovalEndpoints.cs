using Elsa.Abstractions;
using FastEndpoints;

namespace ElsaServer.Approval;

public class ApprovalRequest
{
    public string DefinitionId { get; set; } = default!;
    public string? Comment { get; set; }
}

public class ListApprovalsRequest
{
    public string? Status { get; set; }
}

public record ListApprovalsResponse(ICollection<ApprovalItem> Items);

/// <summary>
/// Base class for the approval state transition endpoints.
/// </summary>
public abstract class ApprovalTransitionEndpoint : ElsaEndpoint<ApprovalRequest, ApprovalItem>
{
    protected async Task SendResultAsync(ApprovalTransitionResult result, CancellationToken cancellationToken)
    {
        if (result.IsNotFound)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        if (result.Error != null)
        {
            AddError(result.Error);
            await Send.ErrorsAsync(400, cancellationToken);
            return;
        }

        await Send.OkAsync(result.Item!, cancellationToken);
    }
}

public class SubmitForApproval(ApprovalService approvals) : ApprovalTransitionEndpoint
{
    public override void Configure()
    {
        Post("/workflow-definitions/{definitionId}/approval/submit");
        ConfigurePermissions(ApprovalPermissions.Submit);
    }

    public override async Task HandleAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        await SendResultAsync(await approvals.SubmitAsync(request.DefinitionId, User, cancellationToken), cancellationToken);
}

public class Approve(ApprovalService approvals) : ApprovalTransitionEndpoint
{
    public override void Configure()
    {
        Post("/workflow-definitions/{definitionId}/approval/approve");
        ConfigurePermissions(ApprovalPermissions.Approve);
    }

    public override async Task HandleAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        await SendResultAsync(await approvals.ApproveAsync(request.DefinitionId, User, request.Comment, cancellationToken), cancellationToken);
}

public class Reject(ApprovalService approvals) : ApprovalTransitionEndpoint
{
    public override void Configure()
    {
        Post("/workflow-definitions/{definitionId}/approval/reject");
        ConfigurePermissions(ApprovalPermissions.Approve);
    }

    public override async Task HandleAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        await SendResultAsync(await approvals.RejectAsync(request.DefinitionId, User, request.Comment, cancellationToken), cancellationToken);
}

public class ListApprovals(ApprovalService approvals) : ElsaEndpoint<ListApprovalsRequest, ListApprovalsResponse>
{
    public override void Configure()
    {
        Get("/workflow-approvals");
        ConfigurePermissions(ApprovalPermissions.Read);
    }

    public override async Task HandleAsync(ListApprovalsRequest request, CancellationToken cancellationToken)
    {
        var items = await approvals.ListAsync(request.Status, cancellationToken);
        await Send.OkAsync(new ListApprovalsResponse(items.ToList()), cancellationToken);
    }
}

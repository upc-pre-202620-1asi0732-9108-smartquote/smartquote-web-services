using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Application;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Application.Views;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;

namespace SmartQuote.API.SupplyRequests.Application;

public class PurchaseRequestQueryService(IPurchaseRequestRepository repository, ICurrentUser currentUser) : IPurchaseRequestSnapshotProvider
{
    public async Task<PurchaseRequestView> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await FindOrThrowAsync(requestId, cancellationToken);
        EnsureCanRead(request);
        return ToView(request);
    }

    public async Task<RequestHistoryView> GetHistoryAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await FindOrThrowAsync(requestId, cancellationToken);
        EnsureCanRead(request);

        var entries = request.StatusHistory
            .OrderBy(entry => entry.ChangedAt)
            .Select(entry => new RequestStatusEntryView(
                entry.FromStatus.ToString(),
                entry.ToStatus.ToString(),
                entry.ChangedBy,
                entry.ChangedAt,
                entry.Reason))
            .ToList();

        return new RequestHistoryView(requestId, entries);
    }

    public async Task<PagedResult<PurchaseRequestView>> ListAsync(
        RequestStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            throw new ArgumentException("Page must be greater than zero.");
        if (pageSize is < 1 or > 100)
            throw new ArgumentException("Page size must be between 1 and 100.");

        var requesterId = currentUser.IsInRole(SmartQuoteRoles.ProductionSpecialist)
            ? currentUser.UserId
            : (Guid?)null;

        var total = await repository.CountAsync(requesterId, status, cancellationToken);
        var requests = await repository.ListAsync(requesterId, status, (page - 1) * pageSize, pageSize, cancellationToken);
        return new PagedResult<PurchaseRequestView>(requests.Select(ToView).ToList(), page, pageSize, total);
    }

    public async Task<PurchaseRequestSnapshot?> GetSnapshotAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await repository.GetByIdAsync(new PurchaseRequestId(requestId), cancellationToken);
        return request is null ? null : ToSnapshot(request);
    }

    private async Task<PurchaseRequest> FindOrThrowAsync(Guid requestId, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(new PurchaseRequestId(requestId), cancellationToken)
            ?? throw new NotFoundException($"Purchase request '{requestId}' was not found.");
    }

    private void EnsureCanRead(PurchaseRequest request)
    {
        if (currentUser.IsInRole(SmartQuoteRoles.ProductionSpecialist) && request.RequesterId.Value != currentUser.UserId)
            throw new UnauthorizedAccessException("Production specialists may only access their own purchase requests.");
    }

    private static PurchaseRequestView ToView(PurchaseRequest request) => new(
        request.Id,
        request.RequesterId,
        request.RequiredDate,
        request.Priority.ToString(),
        request.Status.ToString(),
        ResolveNextResponsibleArea(request.Status),
        request.Version,
        request.CreatedAt,
        request.UpdatedAt,
        request.Items.Select(item => new RequestedItemView(
            item.Id,
            item.LineNumber,
            item.Description,
            item.Quantity,
            item.UnitOfMeasure,
            item.Requirements.Select(requirement => new TechnicalRequirementView(
                requirement.Id,
                requirement.Name,
                requirement.Operator.ToString(),
                requirement.ExpectedValue,
                requirement.UnitOfMeasure,
                requirement.IsMandatory)).ToList())).ToList(),
        request.Attachments.Select(attachment => new RequestAttachmentView(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.UploadedBy,
            attachment.UploadedAt)).ToList());

    private static PurchaseRequestSnapshot ToSnapshot(PurchaseRequest request) => new(
        request.Id.ToString(),
        request.Version,
        request.Status.ToString(),
        request.RequesterId.ToString(),
        request.RequiredDate,
        request.Priority.ToString(),
        request.Items.Select(item => new RequestedItemSnapshot(
            item.Id.ToString(),
            item.LineNumber,
            item.Description,
            item.Quantity,
            item.UnitOfMeasure,
            item.Requirements.Select(requirement => new TechnicalRequirementSnapshot(
                requirement.Id.ToString(),
                requirement.Name,
                requirement.Operator.ToString(),
                requirement.ExpectedValue,
                requirement.UnitOfMeasure,
                requirement.IsMandatory)).ToList())).ToList());

    private static string ResolveNextResponsibleArea(RequestStatus status) => status switch
    {
        RequestStatus.Submitted or RequestStatus.UnderReview => "Purchasing",
        RequestStatus.QuotationCollection => "Quotation Intake",
        RequestStatus.Evaluation => "Evaluation and Simulation",
        RequestStatus.Approved => "Purchase Ordering",
        RequestStatus.Ordered or RequestStatus.Rejected or RequestStatus.Cancelled => "Completed",
        _ => "Requester"
    };
}

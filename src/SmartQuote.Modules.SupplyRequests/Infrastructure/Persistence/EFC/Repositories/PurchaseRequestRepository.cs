using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Repositories;

public class PurchaseRequestRepository(SupplyRequestsDbContext context)
    : BaseRepository<PurchaseRequest, SupplyRequestsDbContext>(context), IPurchaseRequestRepository
{
    public async Task<PurchaseRequest?> GetByIdAsync(PurchaseRequestId requestId, CancellationToken cancellationToken = default) =>
        await Context.PurchaseRequests
            .AsSplitQuery()
            .Include(request => request.Items)
            .ThenInclude(item => item.Requirements)
            .Include(request => request.Attachments)
            .Include(request => request.StatusHistory)
            .Include(request => request.AssignmentHistory)
            .Include(request => request.RequirementRevisions)
            .FirstOrDefaultAsync(request => request.Id == requestId, cancellationToken);

    public async Task<IReadOnlyList<PurchaseRequest>> ListAsync(
        Guid? requesterId,
        RequestStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(Context.PurchaseRequests.AsNoTracking(), requesterId, status);
        return await query
            .AsSplitQuery()
            .OrderByDescending(request => request.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .Include(request => request.Items)
            .ThenInclude(item => item.Requirements)
            .Include(request => request.Attachments)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Guid? requesterId,
        RequestStatus? status,
        CancellationToken cancellationToken = default) =>
        ApplyFilters(Context.PurchaseRequests.AsNoTracking(), requesterId, status).CountAsync(cancellationToken);

    private static IQueryable<PurchaseRequest> ApplyFilters(
        IQueryable<PurchaseRequest> query,
        Guid? requesterId,
        RequestStatus? status)
    {
        if (requesterId.HasValue)
        {
            var userId = new SmartQuote.API.Shared.Domain.Model.ValueObjects.UserId(requesterId.Value);
            query = query.Where(request => request.RequesterId == userId);
        }

        if (status.HasValue)
            query = query.Where(request => request.Status == status.Value);

        // Drafts are private to their requesting specialist, including direct status filters.
        if (!requesterId.HasValue) query = query.Where(request => request.Status != RequestStatus.Draft);
        return query;
    }

    public async Task<SmartQuote.API.Shared.Application.PagedResult<PurchaseRequest>> ListAssignedAsync(Guid analystId,
        RequestStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(Context.PurchaseRequests.AsNoTracking(), null, status)
            .Where(r => r.AssignedAnalystId == analystId);
        var count = await query.CountAsync(ct);
        var items = await query.AsSplitQuery().Include(r => r.Items).ThenInclude(i => i.Requirements).Include(r => r.Attachments)
            .OrderBy(r => r.RequiredDate).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items, page, pageSize, count);
    }
}

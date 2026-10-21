using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;

public sealed class TechnicalClarification
{
    public Guid Id { get; private set; }
    public PurchaseRequestId RequestId { get; private set; } = null!;
    public Guid ItemId { get; private set; }
    public Guid RequirementId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid SpecialistId { get; private set; }
    public string Question { get; private set; } = "";
    public string Status { get; private set; } = "Pending";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? NoticeReadAt { get; private set; }
    public string? Answer { get; private set; }
    public Guid? AnsweredBy { get; private set; }
    public DateTimeOffset? AnsweredAt { get; private set; }
    public long SourceTechnicalRevision { get; private set; }
    public long Version { get; private set; }
    private TechnicalClarification() { }

    public static TechnicalClarification Create(Guid requestId, Guid itemId, Guid requirementId, Guid authorId,
        Guid specialistId, string question, long revision, DateTimeOffset now)
    {
        ValidateText(question);
        return new() { Id = Guid.NewGuid(), RequestId = new(requestId), ItemId = itemId, RequirementId = requirementId,
            AuthorId = authorId, SpecialistId = specialistId, Question = question.Trim(),
            SourceTechnicalRevision = revision, CreatedAt = now, Version = 1 };
    }

    public void Respond(string answer, Guid actorId, DateTimeOffset now)
    {
        if (actorId != SpecialistId) throw new UnauthorizedAccessException("Only the recipient can answer this clarification.");
        if (Status != "Pending") throw new DomainException("This clarification has already been answered.");
        ValidateText(answer);
        Answer = answer.Trim(); AnsweredBy = actorId; AnsweredAt = now; Status = "Answered"; Version++;
    }

    public void MarkRead(DateTimeOffset now) { if (NoticeReadAt is null) { NoticeReadAt = now; Version++; } }
    public void MarkRevisionApplied()
    {
        if (Status != "Answered") throw new DomainException("A requirement revision needs an answered clarification.");
        Status = "RevisionApplied"; Version++;
    }
    private static void ValidateText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 4000)
            throw new DomainException("Clarification text must contain between 1 and 4000 characters.");
    }
}

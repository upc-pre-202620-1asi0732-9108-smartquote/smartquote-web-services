using SmartQuote.API.Shared.Domain;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;
using Xunit;

namespace SmartQuote.Domain.Tests;

public sealed class AuditEventTests
{
    [Fact]
    public void RecordKeepsTheAffectedEntityActorAndTrimmedReason()
    {
        var entityId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var audit = AuditEvent.Record(AuditEvent.PurchaseOrder, entityId, "Issued", actorId, "  Approved.  ", DateTimeOffset.UtcNow);

        Assert.Equal(AuditEvent.PurchaseOrder, audit.EntityType);
        Assert.Equal(entityId, audit.EntityId);
        Assert.Equal("Issued", audit.Action);
        Assert.Equal(actorId, audit.ActorId);
        Assert.Equal("Approved.", audit.Reason);
    }

    [Fact]
    public void RecordRejectsEntityTypesThatAreNotAudited()
    {
        Assert.Throws<DomainException>(() =>
            AuditEvent.Record("Supplier", Guid.NewGuid(), "Created", Guid.NewGuid(), null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RecordRequiresAnAction()
    {
        Assert.Throws<DomainException>(() =>
            AuditEvent.Record(AuditEvent.PurchaseRequest, Guid.NewGuid(), " ", Guid.NewGuid(), null, DateTimeOffset.UtcNow));
    }
}

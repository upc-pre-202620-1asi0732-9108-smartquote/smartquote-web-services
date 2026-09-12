using SmartQuote.Modules.QuotationIntake.Application.Ports;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;

public sealed class QuotationIntakeUnitOfWork(QuotationIntakeDbContext context) : IQuotationIntakeUnitOfWork
{
    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

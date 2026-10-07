using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.IdentityAccess.Application.Ports;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;

public sealed class IdentityAccessUnitOfWork(IdentityAccessDbContext context) : IIdentityAccessUnitOfWork
{
    private const long RegistrationLockKey = 78401325791;
    private IDbContextTransaction? _registrationTransaction;

    public async Task BeginRegistrationAsync(CancellationToken cancellationToken = default)
    {
        if (_registrationTransaction is not null)
            throw new InvalidOperationException("A registration transaction is already active.");

        _registrationTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({RegistrationLockKey})", cancellationToken);
    }

    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (_registrationTransaction is not null)
                await _registrationTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres &&
                                                  postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                                                  postgres.ConstraintName == "ix_user_accounts_normalized_email")
        {
            if (_registrationTransaction is not null)
                await _registrationTransaction.RollbackAsync(cancellationToken);
            throw new ConflictException("Email is already registered.");
        }
        finally
        {
            if (_registrationTransaction is not null)
            {
                await _registrationTransaction.DisposeAsync();
                _registrationTransaction = null;
            }
        }
    }

    public async Task AbortRegistrationAsync(CancellationToken cancellationToken = default)
    {
        if (_registrationTransaction is null)
            return;

        try
        {
            await _registrationTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _registrationTransaction.DisposeAsync();
            _registrationTransaction = null;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.Modules.QuotationIntake.Application;
using SmartQuote.Modules.QuotationIntake.Application.OutboundServices;
using SmartQuote.Modules.QuotationIntake.Application.Ports;
using SmartQuote.Modules.QuotationIntake.Infrastructure.AI;
using SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Repositories;
using SmartQuote.Modules.QuotationIntake.Infrastructure.ReferenceReaders;
using SmartQuote.Modules.QuotationIntake.Infrastructure.Storage;

namespace SmartQuote.Modules.QuotationIntake;

public static class QuotationIntakeModule
{
    public static IServiceCollection AddQuotationIntakeModule(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddDbContext<QuotationIntakeDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "quotation_intake")));

        services.AddScoped<IQuotationIntakeUnitOfWork, QuotationIntakeUnitOfWork>();
        services.AddScoped<IPoultryQuoteRepository, PoultryQuoteRepository>();
        services.AddScoped<IPurchaseRequestReferenceReader, SupplyRequestReferenceAdapter>();
        services.AddScoped<IQuotationDocumentStorage, LocalQuotationDocumentStorage>();

        var provider = configuration["AI:Provider"] ?? "Stub";
        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IQuoteExtractionAgent, SemanticKernelAgentConnector>();
        else
            services.AddScoped<IQuoteExtractionAgent, StubQuoteExtractionAgent>();

        services.AddScoped<QuoteExtractionService>();
        services.AddOptions<QuotationExpiryOptions>().Bind(configuration.GetSection("QuotationExpiry"))
            .Validate(o => o.LeadDays is > 0 and <= 365 && o.PollSeconds >= 5 && o.BatchSize is > 0 and <= 1000, "Invalid quotation expiry options.")
            .Validate(o => { try { TimeZoneInfo.FindSystemTimeZoneById(o.TimeZone); return true; } catch { return false; } }, "Invalid quotation expiry timezone.")
            .ValidateOnStart();
        services.AddScoped<IQuotationExpiryStore, QuotationExpiryStore>();
        services.AddScoped<QuotationExpiryService>();
        services.AddHostedService<Infrastructure.Workers.QuotationExpiryWorker>();
        services.AddScoped<IVerifiedQuotationSnapshotProvider>(provider =>
            provider.GetRequiredService<QuoteExtractionService>());

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;
using Xunit;

namespace SmartQuote.Integration.Tests;

[Collection("API integration"), Trait("Category", "Integration"), Trait("Story", "US21"), Trait("Scenario", "E3")]
public sealed class PurchaseAwardMigrationTests
{
    // US21: execute the real upgrade over an old order, not just an empty-schema migration.
    [Fact]
    public async Task LegacyOrderKeepsIdentityNumberCurrencyAndSourcesAfterAwardBackfill()
    {
        var connection = Environment.GetEnvironmentVariable("SMARTQUOTE_TEST_CONNECTION") ?? throw new InvalidOperationException("An isolated test database is required.");
        var source = new NpgsqlConnectionStringBuilder(connection);
        if (source.Host is not ("localhost" or "127.0.0.1") || !source.Database!.Contains("test")) throw new InvalidOperationException("Test databases only.");
        var databaseName = "smartquote_test_award_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {databaseName}", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            source.Database = databaseName;
            await VerifyUpgrade(source.ConnectionString);
        }
        finally
        {
            // Only the GUID-named database created by this test is removed, on the temporary test server.
            await using var drop = new NpgsqlCommand($"DROP DATABASE {databaseName} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
    private static async Task VerifyUpgrade(string connection)
    {
        await using var evaluation = new EvaluationSimulationDbContext(new DbContextOptionsBuilder<EvaluationSimulationDbContext>()
            .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations_history", "evaluation_simulation")).Options);
        await evaluation.Database.MigrateAsync();
        await using var ordering = new PurchaseOrderingDbContext(new DbContextOptionsBuilder<PurchaseOrderingDbContext>()
            .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations_history", "purchase_ordering")).Options);
        var migrator = ordering.GetService<IMigrator>();
        await migrator.MigrateAsync("20261021024207_AddSupplierEligibilityAndPurchaseBudgets");
        var id = Guid.NewGuid(); var request = Guid.NewGuid(); var run = Guid.NewGuid(); var quote = Guid.NewGuid();
        var supplier = Guid.NewGuid(); var actor = Guid.NewGuid(); var line = Guid.NewGuid(); var item = Guid.NewGuid(); var fp = new string('a', 64);
        await ordering.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO purchase_ordering.supplier_accounts (id, tax_identifier, business_name, status, version, created_at)
            VALUES ({supplier}, '20123456789', 'Historical supplier', 'Pending', 1, CURRENT_TIMESTAMP);
            INSERT INTO purchase_ordering.purchase_orders
                (id, order_number, source_simulation_run_id, source_purchase_request_id, source_quotation_id, input_fingerprint,
                 supplier_reference_id, supplier_business_name, supplier_tax_identifier, approved_by, approved_at, idempotency_key,
                 status, currency, delivery_lead_time_days, delivery_conditions, delivery_destination, created_at)
            VALUES ({id}, 'PO-LEGACY-001', {run}, {request}, {quote}, {fp}, {supplier.ToString()}, 'Historical supplier',
                '20123456789', {actor}, CURRENT_TIMESTAMP, 'historical-key', 'Issued', 'PEN', 3, 'Original conditions', 'Original farm', CURRENT_TIMESTAMP);
            INSERT INTO purchase_ordering.purchase_order_lines
                (id, purchase_order_id, line_number, source_quotation_line_id, source_requested_item_id, description, quantity, unit_of_measure, unit_price)
            VALUES ({line}, {id}, 1, {Guid.NewGuid()}, {item}, 'Historical item', 10, 'kg', 4.5);
            """);
        await migrator.MigrateAsync();
        var order = await ordering.PurchaseOrders.AsNoTracking().Include(o => o.Lines).SingleAsync();
        var award = await ordering.PurchaseAwards.AsNoTracking().SingleAsync();
        Assert.Equal(id, order.Id.Value); Assert.Equal("PO-LEGACY-001", order.OrderNumber.Value); Assert.Equal("PEN", order.Currency);
        Assert.Equal(quote.ToString(), order.SourceDecision.QuotationId); Assert.Equal(run.ToString(), order.SourceDecision.SimulationRunId);
        Assert.Equal(id, award.Id); Assert.Equal(id, order.AwardId); Assert.Equal(supplier, order.AwardSupplierId);
        Assert.Equal(45, award.TotalPen); Assert.Equal("SingleSupplier", award.Mode); Assert.Equal("historical-key", award.IdempotencyKey);
        Assert.Equal(quote.ToString(), order.Lines[0].SourceQuotationId); Assert.Equal(4.5m, order.Lines[0].OriginalUnitPrice);
    }
}

using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Services;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;
using Xunit;

namespace SmartQuote.Domain.Tests;

public class FastFlowTests
{
    // US05/E2: Supplier Can Be Extracted And Corrected Before Verification; comprobación aislada.
    [Fact]
    [Trait("Story", "US05"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void SupplierCanBeExtractedAndCorrectedBeforeVerification()
    {
        var quote = PoultryQuote.Create(
            new PurchaseRequestReference(Guid.NewGuid().ToString()),
            new SupplierReference(Guid.NewGuid().ToString("N"), "", ""),
            new SourceDocument("offer.pdf", "application/pdf", "test-storage-key", new string('a', 64)));
        quote.BeginExtraction();
        quote.ApplyExtraction(new ExtractedQuotationData(
            new SupplierReference(quote.Supplier.SupplierId, "Acme Aves", ""),
            new DateOnly(2026, 12, 31), "PEN", 3,
            [new ExtractedLineData("Alimento balanceado", 1000, "kg", 4.5m, [])],
            [
                new ExtractedFieldData("supplier.businessName", "Acme Aves", 1m, 1, "Acme Aves", true),
                new ExtractedFieldData("validUntil", "2026-12-31", 1m, 1, "Válido", true),
                new ExtractedFieldData("currency", "PEN", 1m, 1, "PEN", true),
                new ExtractedFieldData("deliveryLeadTimeDays", "3", 1m, 1, "3 días", true),
                new ExtractedFieldData("lines[0].description", "Alimento balanceado", 1m, 1, "Alimento", true),
                new ExtractedFieldData("lines[0].quantity", "1000", 1m, 1, "1000 kg", true),
                new ExtractedFieldData("lines[0].unitOfMeasure", "kg", 1m, 1, "kg", true),
                new ExtractedFieldData("lines[0].unitPrice", "4.50", 1m, 1, "4.50", true)
            ]));

        Assert.True(quote.HasUnresolvedRequiredFields());
        var field = quote.Fields.Single(value => value.FieldPath == "supplier.taxIdentifier");
        quote.CorrectField(field.Id, "20123456789", new UserId(Guid.NewGuid()), "Verified against the quotation PDF.");
        Assert.False(quote.HasUnresolvedRequiredFields());
        Assert.Equal("20123456789", quote.Supplier.TaxIdentifier);
        quote.AddMissingSpecification(quote.Lines[0].Id, "Proteína", "20,5", "%", 1,
            "Proteína 20,5 %", new UserId(Guid.NewGuid()), "Omitted by the extraction agent.");
        Assert.Equal("20,5", quote.Lines[0].Specifications.Single().Value);
        Assert.Contains(quote.Fields, extracted => extracted.FieldPath == "lines[0].specifications[0].value" &&
            extracted.Source.TextReference == "Proteína 20,5 %");
    }

    // US07/E2: Technical Comparison Accepts Decimal Comma And Rejects Incompatible Units; comprobación aislada.
    [Fact]
    [Trait("Story", "US07"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void TechnicalComparisonAcceptsDecimalCommaAndRejectsIncompatibleUnits()
    {
        var criterion = EvaluationCriterion.Create("Proteína mínima", Guid.NewGuid().ToString(),
            CriterionCategory.TechnicalCompliance, CriterionMode.Mandatory,
            ComparisonOperator.GreaterThanOrEqual, "20", "%", 0, 1);
        var supported = new CriterionEvaluationInput(100, 3,
            new Dictionary<string, QuotationSpecificationSnapshotData>
            {
                [criterion.TargetField] = new("Proteína", "20,5", "%")
            });
        Assert.True(criterion.Evaluate(supported).Passed);
        Assert.Contains("Cumple", criterion.Evaluate(supported).Explanation);

        var mismatched = new CriterionEvaluationInput(100, 3,
            new Dictionary<string, QuotationSpecificationSnapshotData>
            {
                [criterion.TargetField] = new("Proteína", "20.5", "kg")
            });
        Assert.False(criterion.Evaluate(mismatched).Passed);
    }

    // US08/E2: Status Only Request Version Change Does Not Invalidate Decision Input; comprobación aislada.
    [Fact]
    [Trait("Story", "US08"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void StatusOnlyRequestVersionChangeDoesNotInvalidateDecisionInput()
    {
        var id = Guid.NewGuid().ToString();
        var initial = new RequestEvaluationSnapshot(id, 4, new DateOnly(2026, 12, 31), "Normal", [], DateTimeOffset.UtcNow);
        var afterStatusChange = new RequestEvaluationSnapshot(id, 5, initial.RequiredDate, initial.Priority, [], DateTimeOffset.UtcNow);
        Assert.Equal(new EvaluationDataset(initial, []).CalculateFingerprint(),
            new EvaluationDataset(afterStatusChange, []).CalculateFingerprint());
    }

    // US07/E1: Simulation Matches Documented Crude Protein To Minimum Protein Requirement; comprobación aislada.
    [Fact]
    [Trait("Story", "US07"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public void SimulationMatchesDocumentedCrudeProteinToMinimumProteinRequirement()
    {
        var requestId = Guid.NewGuid().ToString();
        var itemId = Guid.NewGuid().ToString();
        var requirementId = Guid.NewGuid().ToString();
        var scenario = EvaluationScenario.Create(requestId, new UserId(Guid.NewGuid()));
        scenario.AddCriterion(EvaluationCriterion.Create("Proteína mínima", requirementId,
            CriterionCategory.TechnicalCompliance, CriterionMode.Mandatory,
            ComparisonOperator.GreaterThanOrEqual, "20", "%", 0, 1));
        scenario.AddCriterion(EvaluationCriterion.Create("Precio total", "totalPrice",
            CriterionCategory.Price, CriterionMode.Weighted,
            ComparisonOperator.LessThanOrEqual, "999999", "", 100, 2));
        scenario.Activate();

        var request = new RequestEvaluationSnapshot(requestId, 3, new DateOnly(2026, 12, 31), "Normal",
            [new RequestItemSnapshotData(itemId, 1, "Alimento balanceado", 1000, "kg",
                [new RequestRequirementSnapshotData(requirementId, "Proteína mínima",
                    ComparisonOperator.GreaterThanOrEqual, "20", "%", true)])], DateTimeOffset.UtcNow);

        QuotationEvaluationSnapshot Quote(string name, string protein, decimal price) => new(
            Guid.NewGuid().ToString(), 3, Guid.NewGuid().ToString(), name, "20123456789", "PEN", 3,
            DateTimeOffset.UtcNow,
            [new QuotationLineSnapshotData(Guid.NewGuid().ToString(), itemId, 1, "Alimento", 1000, "kg", price,
                [new QuotationSpecificationSnapshotData("Crude protein", protein, "%")])], DateTimeOffset.UtcNow);

        var run = new SimulationEngine().Run(scenario,
            new EvaluationDataset(request, [Quote("A", "21", 4.5m), Quote("B", "20,5", 4.6m)]));
        Assert.All(run.Evaluations, evaluation => Assert.True(evaluation.IsEligible));
        Assert.NotNull(run.Recommendation);
    }

    // TS03/E1: Simulation Compares Pen And Usd Using Recorded Official Rate; comprobación aislada.
    [Fact]
    [Trait("Story", "TS03"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public void SimulationComparesPenAndUsdUsingRecordedOfficialRate()
    {
        var requestId = Guid.NewGuid().ToString();
        var scenario = EvaluationScenario.Create(requestId, new UserId(Guid.NewGuid()));
        scenario.AddCriterion(EvaluationCriterion.Create("Plazo máximo", "deliveryLeadTimeDays",
            CriterionCategory.DeliveryTime, CriterionMode.Mandatory,
            ComparisonOperator.LessThanOrEqual, "10", "days", 0, 1));
        scenario.AddCriterion(EvaluationCriterion.Create("Precio total", "totalPrice",
            CriterionCategory.Price, CriterionMode.Weighted,
            ComparisonOperator.LessThanOrEqual, "999999", "PEN", 100, 2));
        scenario.Activate();

        var request = new RequestEvaluationSnapshot(
            requestId, 1, new DateOnly(2026, 12, 31), "Normal", [], DateTimeOffset.UtcNow);

        QuotationEvaluationSnapshot Quote(string currency, decimal unitPrice) => new(
            Guid.NewGuid().ToString(), 1, Guid.NewGuid().ToString(), "Supplier", "20123456789",
            currency, 3, DateTimeOffset.UtcNow,
            [new QuotationLineSnapshotData(Guid.NewGuid().ToString(), null, 1, "Supply", 1000, "kg", unitPrice, [])],
            DateTimeOffset.UtcNow);

        var usd = Quote("USD", 1m);
        var pen = Quote("PEN", 3.6m);
        var rate = new ExchangeRateSnapshot(
            "USD", "PEN", 3.5m, "V", new DateOnly(2026, 9, 29),
            "SUNAT - Consulta de Tipo de Cambio", DateTimeOffset.UtcNow);

        var run = new SimulationEngine().Run(scenario, new EvaluationDataset(request, [usd, pen]), rate);

        Assert.Equal(usd.QuotationId, run.Recommendation!.QuotationId);
        Assert.Equal(1, run.GetEvaluation(usd.QuotationId).Rank);
        Assert.Equal(rate, run.ExchangeRate);
    }
}

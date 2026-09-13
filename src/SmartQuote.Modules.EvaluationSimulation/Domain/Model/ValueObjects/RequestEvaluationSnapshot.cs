using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public class RequestEvaluationSnapshot
{
    private readonly List<RequestItemSnapshotData> _items = [];

    public string RequestId { get; private set; } = string.Empty;
    public long Version { get; private set; }
    public DateOnly RequiredDate { get; private set; }
    public string Priority { get; private set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; private set; }

    public IReadOnlyList<RequestItemSnapshotData> Items => _items.AsReadOnly();

    private RequestEvaluationSnapshot() { }

    public RequestEvaluationSnapshot(
        string requestId,
        long version,
        DateOnly requiredDate,
        string priority,
        IEnumerable<RequestItemSnapshotData> items,
        DateTimeOffset capturedAt)
    {
        RequestId = requestId;
        Version = version;
        RequiredDate = requiredDate;
        Priority = priority;
        CapturedAt = capturedAt;
        _items.AddRange(items);
    }
}

public class RequestItemSnapshotData
{
    private readonly List<RequestRequirementSnapshotData> _requirements = [];

    public string SourceRequestedItemId { get; private set; } = string.Empty;
    public int LineNumber { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string UnitOfMeasure { get; private set; } = string.Empty;

    public IReadOnlyList<RequestRequirementSnapshotData> Requirements => _requirements.AsReadOnly();

    private RequestItemSnapshotData() { }

    public RequestItemSnapshotData(
        string sourceRequestedItemId,
        int lineNumber,
        string description,
        decimal quantity,
        string unitOfMeasure,
        IEnumerable<RequestRequirementSnapshotData> requirements)
    {
        SourceRequestedItemId = sourceRequestedItemId;
        LineNumber = lineNumber;
        Description = description;
        Quantity = quantity;
        UnitOfMeasure = unitOfMeasure;
        _requirements.AddRange(requirements);
    }
}

public record RequestRequirementSnapshotData(
    string? SourceRequirementId,
    string Name,
    ComparisonOperator Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    bool IsMandatory);

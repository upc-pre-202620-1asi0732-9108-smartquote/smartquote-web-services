using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.EvaluationSimulation.Application;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;

namespace SmartQuote.Modules.EvaluationSimulation.Interfaces.REST;

public record SensitivityConfigurationResource(IReadOnlyList<SensitivityWeight> Weights);
public record CreateSensitivityResource(IReadOnlyList<SensitivityConfigurationResource> Configurations);
public record PromoteSensitivityResource(int ExpectedCriteriaVersion);
[ApiController, Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
public sealed class SensitivityAnalysesController(SensitivityAnalysisService service) : ControllerBase
{
    [HttpPost("api/v1/evaluation-scenarios/{scenarioId:guid}/sensitivity-analyses")]
    public async Task<IActionResult> Create(Guid scenarioId, CreateSensitivityResource resource, CancellationToken ct)
    {
        var result = await service.CreateAsync(scenarioId, resource.Configurations.Select(c => c.Weights).ToList(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Analysis.Id }, result);
    }
    [HttpGet("api/v1/sensitivity-analyses/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    [HttpGet("api/v1/purchase-requests/{requestId:guid}/sensitivity-analyses")]
    public async Task<IActionResult> List(Guid requestId, CancellationToken ct) => Ok(await service.ListAsync(requestId, ct));
    [HttpPost("api/v1/sensitivity-analyses/{id:guid}/configurations/{configurationId:guid}/official-simulation")]
    public async Task<IActionResult> Promote(Guid id, Guid configurationId, PromoteSensitivityResource resource, CancellationToken ct) =>
        StatusCode(201, await service.PromoteAsync(id, configurationId, resource.ExpectedCriteriaVersion, ct));
}

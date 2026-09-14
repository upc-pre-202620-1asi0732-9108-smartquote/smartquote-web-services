using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.EvaluationSimulation.Application;
using SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Resources;
using SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Transform;

namespace SmartQuote.Modules.EvaluationSimulation.Interfaces.REST;

[ApiController]
[Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
public class SimulationsController(
    ScenarioApplicationService scenarioService,
    SimulationApplicationService simulationService) : ControllerBase
{
    [HttpPost("api/v1/evaluation-scenarios")]
    [ProducesResponseType<EvaluationScenarioResource>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EvaluationScenarioResource>> CreateScenario(
        [FromBody] CreateScenarioResource resource,
        CancellationToken cancellationToken)
    {
        var command = ScenarioResourceTransforms.ToCommand(resource);
        var scenarioId = await scenarioService.CreateAsync(command, cancellationToken);
        var view = await scenarioService.GetByIdAsync(scenarioId, cancellationToken);
        var created = ScenarioResourceTransforms.ToResource(view);

        return CreatedAtAction(nameof(GetScenarioById), new { scenarioId = created.ScenarioId }, created);
    }

    [HttpPost("api/v1/evaluation-scenarios/{scenarioId:guid}/versions")]
    [ProducesResponseType<EvaluationScenarioResource>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EvaluationScenarioResource>> CreateScenarioVersion(
        Guid scenarioId,
        [FromBody] CreateScenarioVersionResource resource,
        CancellationToken cancellationToken)
    {
        var command = ScenarioResourceTransforms.ToCommand(scenarioId, resource.Criteria);
        var nextId = await scenarioService.CreateNextVersionAsync(command, cancellationToken);
        var view = await scenarioService.GetByIdAsync(nextId, cancellationToken);
        var created = ScenarioResourceTransforms.ToResource(view);
        return CreatedAtAction(nameof(GetScenarioById), new { scenarioId = created.ScenarioId }, created);
    }

    [HttpGet("api/v1/evaluation-scenarios/{scenarioId:guid}")]
    [ProducesResponseType<EvaluationScenarioResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationScenarioResource>> GetScenarioById(
        Guid scenarioId,
        CancellationToken cancellationToken)
    {
        var view = await scenarioService.GetByIdAsync(scenarioId, cancellationToken);
        return Ok(ScenarioResourceTransforms.ToResource(view));
    }

    [HttpGet("api/v1/purchase-requests/{requestId:guid}/evaluation-scenario")]
    [ProducesResponseType<EvaluationScenarioResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationScenarioResource>> GetCurrentForRequest(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var view = await scenarioService.GetCurrentForRequestAsync(requestId.ToString(), cancellationToken);
        return Ok(ScenarioResourceTransforms.ToResource(view));
    }

    [HttpPost("api/v1/evaluation-scenarios/{scenarioId:guid}/simulations")]
    [ProducesResponseType<SimulationResultResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<SimulationResultResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SimulationResultResource>> RunSimulation(
        Guid scenarioId,
        CancellationToken cancellationToken)
    {
        var execution = await simulationService.RunAsync(scenarioId, cancellationToken);
        var view = await simulationService.GetResultAsync(execution.RunId, cancellationToken);
        var resource = SimulationResultResourceFromViewAssembler.ToResource(view);

        return execution.WasCreated
            ? CreatedAtAction(nameof(GetResult), new { simulationRunId = resource.SimulationRunId }, resource)
            : Ok(resource);
    }

    [HttpGet("api/v1/simulations/{simulationRunId:guid}")]
    [ProducesResponseType<SimulationResultResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SimulationResultResource>> GetResult(
        Guid simulationRunId,
        CancellationToken cancellationToken)
    {
        var view = await simulationService.GetResultAsync(simulationRunId, cancellationToken);
        return Ok(SimulationResultResourceFromViewAssembler.ToResource(view));
    }
}

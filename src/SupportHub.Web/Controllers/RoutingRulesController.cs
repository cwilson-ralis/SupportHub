namespace SupportHub.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;

[ApiController]
[Route("api/routing-rules")]
[Authorize(Policy = "Admin")]
public class RoutingRulesController(IRoutingRuleService _routingRuleService, IRoutingEngine _routingEngine) : ControllerBase
{
    // GET /api/routing-rules?companyId={id}
    [HttpGet]
    public async Task<IActionResult> GetRules([FromQuery] Guid companyId, CancellationToken ct = default)
    {
        var result = await _routingRuleService.GetRulesAsync(companyId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // GET /api/routing-rules/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRule(Guid id, CancellationToken ct = default)
    {
        var result = await _routingRuleService.GetRuleByIdAsync(id, ct);
        if (!result.IsSuccess) return NotFound(result.Error);
        return Ok(result.Value);
    }

    // POST /api/routing-rules
    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] CreateRoutingRuleRequest request, CancellationToken ct = default)
    {
        var result = await _routingRuleService.CreateRuleAsync(request, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return CreatedAtAction(nameof(GetRule), new { id = result.Value!.Id }, result.Value);
    }

    // PUT /api/routing-rules/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateRoutingRuleRequest request, CancellationToken ct = default)
    {
        var result = await _routingRuleService.UpdateRuleAsync(id, request, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return Ok(result.Value);
    }

    // DELETE /api/routing-rules/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct = default)
    {
        var result = await _routingRuleService.DeleteRuleAsync(id, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return NoContent();
    }

    // POST /api/routing-rules/reorder
    [HttpPost("reorder")]
    public async Task<IActionResult> ReorderRules([FromQuery] Guid companyId, [FromBody] ReorderRoutingRulesRequest request, CancellationToken ct = default)
    {
        var result = await _routingRuleService.ReorderRulesAsync(companyId, request, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return NoContent();
    }

    // POST /api/routing-rules/test
    [HttpPost("test")]
    public async Task<IActionResult> TestRouting([FromBody] RoutingContext context, CancellationToken ct = default)
    {
        var result = await _routingEngine.EvaluateAsync(context, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return Ok(result.Value);
    }
}

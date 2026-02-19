namespace SupportHub.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;

[ApiController]
[Route("api/email-configurations")]
[Authorize(Policy = "Admin")]
public class EmailConfigurationsController(IEmailConfigurationService _service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _service.GetAllAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmailConfigurationRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmailConfigurationRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        if (!result.IsSuccess)
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/test")]
    public IActionResult TestConnection(Guid id)
    {
        return Ok(new { connected = false, message = "Test connection not yet implemented." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return NoContent();
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetLogs(Guid id, [FromQuery] int count = 50, CancellationToken ct = default)
    {
        var result = await _service.GetLogsAsync(id, count, ct);
        if (!result.IsSuccess)
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return Ok(result.Value);
    }
}

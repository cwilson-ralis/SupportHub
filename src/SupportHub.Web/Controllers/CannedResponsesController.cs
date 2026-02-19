using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;

namespace SupportHub.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CannedResponsesController : ControllerBase
{
    private readonly ICannedResponseService _cannedResponseService;

    public CannedResponsesController(ICannedResponseService cannedResponseService)
    {
        _cannedResponseService = cannedResponseService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCannedResponsesAsync(
        [FromQuery] Guid? companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _cannedResponseService.GetCannedResponsesAsync(companyId, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCannedResponseAsync([FromBody] CreateCannedResponseRequest request, CancellationToken ct)
    {
        var result = await _cannedResponseService.CreateCannedResponseAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(null, new { id = result.Value!.Id }, result.Value)
            : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCannedResponseAsync(Guid id, [FromBody] UpdateCannedResponseRequest request, CancellationToken ct)
    {
        var result = await _cannedResponseService.UpdateCannedResponseAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCannedResponseAsync(Guid id, CancellationToken ct)
    {
        var result = await _cannedResponseService.DeleteCannedResponseAsync(id, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}

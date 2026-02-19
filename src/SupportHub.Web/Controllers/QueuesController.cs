namespace SupportHub.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;

[ApiController]
[Route("api/queues")]
[Authorize(Policy = "Admin")]
public class QueuesController(IQueueService _queueService) : ControllerBase
{
    // GET /api/queues?companyId={id}&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetQueues(
        [FromQuery] Guid companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _queueService.GetQueuesAsync(companyId, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // GET /api/queues/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQueue(Guid id, CancellationToken ct = default)
    {
        var result = await _queueService.GetQueueByIdAsync(id, ct);
        if (!result.IsSuccess) return NotFound(result.Error);
        return Ok(result.Value);
    }

    // POST /api/queues
    [HttpPost]
    public async Task<IActionResult> CreateQueue([FromBody] CreateQueueRequest request, CancellationToken ct = default)
    {
        var result = await _queueService.CreateQueueAsync(request, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return CreatedAtAction(nameof(GetQueue), new { id = result.Value!.Id }, result.Value);
    }

    // PUT /api/queues/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateQueue(Guid id, [FromBody] UpdateQueueRequest request, CancellationToken ct = default)
    {
        var result = await _queueService.UpdateQueueAsync(id, request, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return Ok(result.Value);
    }

    // DELETE /api/queues/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteQueue(Guid id, CancellationToken ct = default)
    {
        var result = await _queueService.DeleteQueueAsync(id, ct);
        if (!result.IsSuccess) return BadRequest(result.Error);
        return NoContent();
    }
}

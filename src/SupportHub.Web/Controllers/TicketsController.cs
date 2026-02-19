using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Enums;

namespace SupportHub.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ITicketMessageService _messageService;
    private readonly IInternalNoteService _noteService;
    private readonly IAttachmentService _attachmentService;
    private readonly ITagService _tagService;

    public TicketsController(
        ITicketService ticketService,
        ITicketMessageService messageService,
        IInternalNoteService noteService,
        IAttachmentService attachmentService,
        ITagService tagService)
    {
        _ticketService = ticketService;
        _messageService = messageService;
        _noteService = noteService;
        _attachmentService = attachmentService;
        _tagService = tagService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTicketsAsync([FromQuery] TicketFilterRequest filter, CancellationToken ct)
    {
        var result = await _ticketService.GetTicketsAsync(filter, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicketByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.GetTicketByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTicketAsync([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.CreateTicketAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTicketByIdAsync), new { id = result.Value!.Id }, result.Value)
            : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTicketAsync(Guid id, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.UpdateTicketAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> AssignTicketAsync(Guid id, [FromBody] Guid agentId, CancellationToken ct)
    {
        var result = await _ticketService.AssignTicketAsync(id, agentId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatusAsync(Guid id, [FromBody] TicketStatus newStatus, CancellationToken ct)
    {
        var result = await _ticketService.ChangeStatusAsync(id, newStatus, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/priority")]
    public async Task<IActionResult> ChangePriorityAsync(Guid id, [FromBody] TicketPriority newPriority, CancellationToken ct)
    {
        var result = await _ticketService.ChangePriorityAsync(id, newPriority, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTicketAsync(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.DeleteTicketAsync(id, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessagesAsync(Guid id, CancellationToken ct)
    {
        var result = await _messageService.GetMessagesAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> AddMessageAsync(Guid id, [FromBody] CreateTicketMessageRequest request, CancellationToken ct)
    {
        var result = await _messageService.AddMessageAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}/notes")]
    public async Task<IActionResult> GetNotesAsync(Guid id, CancellationToken ct)
    {
        var result = await _noteService.GetNotesAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNoteAsync(Guid id, [FromBody] CreateInternalNoteRequest request, CancellationToken ct)
    {
        var result = await _noteService.AddNoteAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> UploadAttachmentAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await _attachmentService.UploadAttachmentAsync(
            id, null, stream, file.FileName, file.ContentType, file.Length, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await _attachmentService.DownloadAttachmentAsync(attachmentId, ct);
        if (!result.IsSuccess) return NotFound(result.Error);
        var (fileStream, contentType, fileName) = result.Value!;
        return File(fileStream, contentType, fileName);
    }

    [HttpPost("{id:guid}/tags")]
    public async Task<IActionResult> AddTagAsync(Guid id, [FromBody] string tag, CancellationToken ct)
    {
        var result = await _tagService.AddTagAsync(id, tag, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}/tags/{tag}")]
    public async Task<IActionResult> RemoveTagAsync(Guid id, string tag, CancellationToken ct)
    {
        var result = await _tagService.RemoveTagAsync(id, tag, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}

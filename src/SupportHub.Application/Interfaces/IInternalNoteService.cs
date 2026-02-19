namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface IInternalNoteService
{
    Task<Result<InternalNoteDto>> AddNoteAsync(Guid ticketId, CreateInternalNoteRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<InternalNoteDto>>> GetNotesAsync(Guid ticketId, CancellationToken ct = default);
}

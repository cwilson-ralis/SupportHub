namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface ITicketMessageService
{
    Task<Result<TicketMessageDto>> AddMessageAsync(Guid ticketId, CreateTicketMessageRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TicketMessageDto>>> GetMessagesAsync(Guid ticketId, CancellationToken ct = default);
}

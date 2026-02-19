namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Domain.Enums;

public interface ITicketService
{
    Task<Result<TicketDto>> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<Result<TicketDto>> GetTicketByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<TicketSummaryDto>>> GetTicketsAsync(TicketFilterRequest filter, CancellationToken ct = default);
    Task<Result<TicketDto>> UpdateTicketAsync(Guid id, UpdateTicketRequest request, CancellationToken ct = default);
    Task<Result<bool>> AssignTicketAsync(Guid ticketId, Guid agentId, CancellationToken ct = default);
    Task<Result<bool>> ChangeStatusAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct = default);
    Task<Result<bool>> ChangePriorityAsync(Guid ticketId, TicketPriority newPriority, CancellationToken ct = default);
    Task<Result<bool>> DeleteTicketAsync(Guid id, CancellationToken ct = default);
}

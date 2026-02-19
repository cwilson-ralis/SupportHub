namespace SupportHub.Application.DTOs;

using SupportHub.Domain.Enums;

public record EmailConfigurationDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string SharedMailboxAddress,
    string DisplayName,
    bool IsActive,
    int PollingIntervalMinutes,
    DateTimeOffset? LastPolledAt,
    string? LastPolledMessageId,
    bool AutoCreateTickets,
    TicketPriority DefaultPriority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record CreateEmailConfigurationRequest(
    Guid CompanyId,
    string SharedMailboxAddress,
    string DisplayName,
    bool IsActive = true,
    int PollingIntervalMinutes = 2,
    bool AutoCreateTickets = true,
    TicketPriority DefaultPriority = TicketPriority.Medium);

public record UpdateEmailConfigurationRequest(
    string? DisplayName,
    bool? IsActive,
    int? PollingIntervalMinutes,
    bool? AutoCreateTickets,
    TicketPriority? DefaultPriority);

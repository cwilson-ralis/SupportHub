namespace SupportHub.Application.DTOs;

using SupportHub.Domain.Enums;

public record TicketDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string TicketNumber,
    string Subject,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    TicketSource Source,
    string RequesterEmail,
    string RequesterName,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    string? System,
    string? IssueType,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    string? AiClassification,
    IReadOnlyList<TicketTagDto> Tags,
    IReadOnlyList<TicketAttachmentDto> Attachments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public record TicketSummaryDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    TicketStatus Status,
    TicketPriority Priority,
    TicketSource Source,
    string RequesterName,
    string RequesterEmail,
    string CompanyName,
    string? AssignedAgentName,
    string? System,
    string? IssueType,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public record CreateTicketRequest(
    Guid CompanyId,
    string Subject,
    string Description,
    TicketPriority Priority,
    TicketSource Source,
    string RequesterEmail,
    string RequesterName,
    string? System,
    string? IssueType,
    IReadOnlyList<string>? Tags);

public record UpdateTicketRequest(
    string? Subject,
    string? Description,
    TicketPriority? Priority,
    string? System,
    string? IssueType);

public record TicketFilterRequest(
    Guid? CompanyId,
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? AssignedAgentId,
    string? SearchTerm,
    IReadOnlyList<string>? Tags,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int Page = 1,
    int PageSize = 25);

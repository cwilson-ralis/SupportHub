namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface IEmailProcessingService
{
    Task<Result<Guid?>> ProcessInboundEmailAsync(InboundEmailMessage message, Guid emailConfigurationId, CancellationToken ct = default);
}

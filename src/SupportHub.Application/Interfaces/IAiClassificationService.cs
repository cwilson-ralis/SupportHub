namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface IAiClassificationService
{
    Task<Result<AiClassificationResult>> ClassifyAsync(string subject, string body, Guid companyId, CancellationToken ct = default);
}

namespace SupportHub.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;

public class NoOpAiClassificationService(ILogger<NoOpAiClassificationService> _logger) : IAiClassificationService
{
    public Task<Result<AiClassificationResult>> ClassifyAsync(string subject, string body, Guid companyId, CancellationToken ct = default)
    {
        _logger.LogInformation("AI classification requested for CompanyId {CompanyId} but no provider is configured", companyId);

        var result = new AiClassificationResult(
            SuggestedQueueName: null,
            SuggestedTags: [],
            SuggestedIssueType: null,
            Confidence: 0,
            ModelUsed: "none",
            RawResponse: string.Empty);

        return Task.FromResult(Result<AiClassificationResult>.Success(result));
    }
}

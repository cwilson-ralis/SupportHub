namespace SupportHub.Application.DTOs;

public record AiClassificationResult(
    string? SuggestedQueueName,
    IReadOnlyList<string> SuggestedTags,
    string? SuggestedIssueType,
    double Confidence,
    string ModelUsed,
    string RawResponse);

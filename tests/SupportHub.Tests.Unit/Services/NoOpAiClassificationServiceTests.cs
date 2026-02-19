namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SupportHub.Infrastructure.Services;

public class NoOpAiClassificationServiceTests
{
    private readonly NoOpAiClassificationService _sut;

    public NoOpAiClassificationServiceTests()
    {
        var logger = Substitute.For<ILogger<NoOpAiClassificationService>>();
        _sut = new NoOpAiClassificationService(logger);
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsSuccess()
    {
        var result = await _sut.ClassifyAsync("subject", "body", Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsZeroConfidence()
    {
        var result = await _sut.ClassifyAsync("subject", "body", Guid.NewGuid());

        result.Value!.Confidence.Should().Be(0);
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsEmptySuggestedTags()
    {
        var result = await _sut.ClassifyAsync("subject", "body", Guid.NewGuid());

        result.Value!.SuggestedTags.Should().BeEmpty();
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsNullQueueAndIssueType()
    {
        var result = await _sut.ClassifyAsync("subject", "body", Guid.NewGuid());

        result.Value!.SuggestedQueueName.Should().BeNull();
        result.Value!.SuggestedIssueType.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsModelUsedAsNone()
    {
        var result = await _sut.ClassifyAsync("subject", "body", Guid.NewGuid());

        result.Value!.ModelUsed.Should().Be("none");
    }
}

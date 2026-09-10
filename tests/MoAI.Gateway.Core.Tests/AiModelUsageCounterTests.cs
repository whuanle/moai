using MediatR;
using MoAI.AIChannel.Services;
using MoAI.Hangfire.Services;
using Moq;
using Xunit;

namespace MoAI.Gateway.Core.Tests;

public class AiModelUsageCounterTests
{
    private static readonly Guid ModelId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid ResourceId = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");

    [Fact]
    public void CounterKey_RoundTripsAllDimensions()
    {
        var expected = new AiModelUsageCounterDimension(ModelId, 12, 34L, 4, ResourceId);

        var value = AiModelUsageCounterKey.Create(expected, AiModelUsageMetric.Completion);
        var result = AiModelUsageCounterKey.TryParse(value, out var actual, out var metric);

        Assert.True(result);
        Assert.Equal("v1:0123456789abcdef0123456789abcdef:12:34:4:fedcba9876543210fedcba9876543210:completion", value);
        Assert.Equal(expected, actual);
        Assert.Equal(AiModelUsageMetric.Completion, metric);
    }

    [Theory]
    [InlineData("")]
    [InlineData("v2:0123456789abcdef0123456789abcdef:12:34:4:fedcba9876543210fedcba9876543210:count")]
    [InlineData("v1:01234567-89ab-cdef-0123-456789abcdef:12:34:4:fedcba9876543210fedcba9876543210:count")]
    [InlineData("v1:0123456789abcdef0123456789abcdef:-1:34:4:fedcba9876543210fedcba9876543210:count")]
    [InlineData("v1:0123456789abcdef0123456789abcdef:12:34:4:fedcba9876543210fedcba9876543210:unknown")]
    public void CounterKey_RejectsInvalidValues(string value)
    {
        Assert.False(AiModelUsageCounterKey.TryParse(value, out _, out _));
    }

    [Fact]
    public async Task IncrementAsync_SendsAllPositiveMetrics()
    {
        IncrementCounterActivatorCommand? command = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<IncrementCounterActivatorCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IncrementCounterActivatorCommand, CancellationToken>((value, _) => command = value)
            .Returns(Task.CompletedTask);
        var counter = new AiModelUsageCounter(mediator.Object);

        await counter.IncrementAsync(ModelId, 12, 34L, 4, ResourceId, 100, 25);

        Assert.NotNull(command);
        Assert.Equal("ai-model-usage", command.Name);
        Assert.Equal(4, command.Counters.Count);
        Assert.Equal(1, command.Counters[CreateKey(AiModelUsageMetric.Count)]);
        Assert.Equal(100, command.Counters[CreateKey(AiModelUsageMetric.Prompt)]);
        Assert.Equal(25, command.Counters[CreateKey(AiModelUsageMetric.Completion)]);
        Assert.Equal(125, command.Counters[CreateKey(AiModelUsageMetric.Total)]);
    }

    [Fact]
    public async Task IncrementAsync_WithZeroTokens_StillCountsCall()
    {
        IncrementCounterActivatorCommand? command = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<IncrementCounterActivatorCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IncrementCounterActivatorCommand, CancellationToken>((value, _) => command = value)
            .Returns(Task.CompletedTask);
        var counter = new AiModelUsageCounter(mediator.Object);

        await counter.IncrementAsync(ModelId, 12, 34L, 4, ResourceId, 0, 0);

        Assert.NotNull(command);
        Assert.Single(command.Counters);
        Assert.Equal(1, command.Counters[CreateKey(AiModelUsageMetric.Count)]);
    }

    [Fact]
    public async Task IncrementAsync_WithNegativeTokens_Throws()
    {
        var counter = new AiModelUsageCounter(Mock.Of<IMediator>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            counter.IncrementAsync(ModelId, 12, 34L, 4, ResourceId, -1, 0));
    }

    [Fact]
    public void Aggregate_GroupsMetricsAndIgnoresInvalidEntries()
    {
        var values = new Dictionary<string, long>
        {
            [CreateKey(AiModelUsageMetric.Count)] = 3,
            [CreateKey(AiModelUsageMetric.Prompt)] = 90,
            [CreateKey(AiModelUsageMetric.Completion)] = 30,
            [CreateKey(AiModelUsageMetric.Total)] = 120,
            ["invalid"] = 10,
        };

        var result = AiModelUsageCounterActivatorJob.Aggregate(values);

        var increment = Assert.Single(result).Value;
        Assert.Equal(3, increment.Count);
        Assert.Equal(90, increment.PromptTokens);
        Assert.Equal(30, increment.CompletionTokens);
        Assert.Equal(120, increment.TotalTokens);
    }

    private static string CreateKey(AiModelUsageMetric metric)
    {
        return AiModelUsageCounterKey.Create(
            new AiModelUsageCounterDimension(ModelId, 12, 34L, 4, ResourceId),
            metric);
    }
}
using MediatR;
using MoAI.Hangfire.Services;
using MoAI.Wiki.Services;
using Moq;
using Xunit;

namespace MoAI.UsageCounters.Tests;

public class WikiUsageCounterTests
{
    [Fact]
    public async Task IncrementAsync_SendsWikiUsageCommand()
    {
        IncrementCounterActivatorCommand? command = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<IncrementCounterActivatorCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IncrementCounterActivatorCommand, CancellationToken>((value, _) => command = value)
            .Returns(Task.CompletedTask);
        var counter = new WikiUsageCounter(mediator.Object);

        await counter.IncrementAsync(42);

        Assert.NotNull(command);
        Assert.Equal("wiki-usage", command.Name);
        Assert.Equal(1, command.Counters["42"]);
    }

    [Fact]
    public async Task IncrementAsync_WithInvalidId_Throws()
    {
        var counter = new WikiUsageCounter(Mock.Of<IMediator>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => counter.IncrementAsync(0));
    }

    [Fact]
    public void Parse_AcceptsCanonicalPositiveIntegerAndRejectsInvalidValues()
    {
        var values = new Dictionary<string, long>
        {
            ["42"] = 5,
            ["042"] = 4,
            ["-1"] = 3,
            ["invalid"] = 2,
            ["7"] = 0,
        };

        var result = WikiUsageCounterActivatorJob.Parse(values);

        var item = Assert.Single(result);
        Assert.Equal(42, item.Key);
        Assert.Equal(5, item.Value);
    }
}
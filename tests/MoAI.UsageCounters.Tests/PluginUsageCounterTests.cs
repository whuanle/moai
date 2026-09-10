using MediatR;
using MoAI.AIPlugin.Services;
using MoAI.Hangfire.Services;
using Moq;
using Xunit;

namespace MoAI.UsageCounters.Tests;

public class PluginUsageCounterTests
{
    [Fact]
    public async Task IncrementAsync_SendsPluginUsageCommand()
    {
        var pluginId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        IncrementCounterActivatorCommand? command = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<IncrementCounterActivatorCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IncrementCounterActivatorCommand, CancellationToken>((value, _) => command = value)
            .Returns(Task.CompletedTask);
        var counter = new PluginUsageCounter(mediator.Object);

        await counter.IncrementAsync(pluginId);

        Assert.NotNull(command);
        Assert.Equal("plugin-usage", command.Name);
        Assert.Equal(1, command.Counters[pluginId.ToString("N")]);
    }

    [Fact]
    public async Task IncrementAsync_WithEmptyId_Throws()
    {
        var counter = new PluginUsageCounter(Mock.Of<IMediator>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => counter.IncrementAsync(Guid.Empty));
    }

    [Fact]
    public void Parse_AcceptsCanonicalUuidAndRejectsInvalidValues()
    {
        var pluginId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        var values = new Dictionary<string, long>
        {
            [pluginId.ToString("N")] = 5,
            [pluginId.ToString("D")] = 4,
            ["invalid"] = 3,
            [Guid.NewGuid().ToString("N")] = 0,
        };

        var result = PluginUsageCounterActivatorJob.Parse(values);

        var item = Assert.Single(result);
        Assert.Equal(pluginId, item.Key);
        Assert.Equal(5, item.Value);
    }
}
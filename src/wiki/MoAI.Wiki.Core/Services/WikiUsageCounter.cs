using System.Globalization;
using MediatR;
using MoAI.Hangfire.Services;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库使用次数计数器默认实现.
/// </summary>
public class WikiUsageCounter : IWikiUsageCounter
{
    private const string CounterName = "wiki-usage";
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiUsageCounter"/> class.
    /// </summary>
    /// <param name="mediator">消息中介器.</param>
    public WikiUsageCounter(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(int wikiId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(wikiId);

        return _mediator.Send(
            new IncrementCounterActivatorCommand
            {
                Name = CounterName,
                Counters = new Dictionary<string, long>
                {
                    [wikiId.ToString(CultureInfo.InvariantCulture)] = 1,
                },
            },
            cancellationToken);
    }
}
using Maomi.MQ;
using MediatR;
using MoAI.AiModel.Events;
using MoAI.Database;
using MoAI.Hangfire.Services;

namespace MoAI.AiModel.Consumer;

/// <summary>
/// 记录模型使用量.
/// </summary>
[Consumer("aimodel_useage", Qos = 100)]
public class AiModelUseageMessageConsumer : IConsumer<AiModelUseageMessage>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelUseageMessageConsumer"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public AiModelUseageMessageConsumer(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, AiModelUseageMessage message)
    {
        // token 使用量
        await _databaseContext.AiModelUseageLogs.AddAsync(new Database.Entities.AiModelUseageLogEntity
        {
            Channel = message.Channel,
            CompletionTokens = message.TokenUsage.CompletionTokens,
            PromptTokens = message.TokenUsage.PromptTokens,
            TotalTokens = message.TokenUsage.TotalTokens,
            ModelId = message.AiModelId,
            UseriId = message.ContextUserId,
            CreateUserId = message.ContextUserId,
            UpdateUserId = message.ContextUserId,
        });

        await _databaseContext.SaveChangesAsync();

        await _mediator.Send(new IncrementCounterActivatorCommand
        {
            Name = "aimodel",
            Counters = new Dictionary<string, int>
            {
                { message.AiModelId.ToString(), 1 }
            },
        });

        // 插件用量统计
        var plugins = message.PluginUsage.Where(x => !x.Key.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase)).ToDictionary();
        var wikis = message.PluginUsage.Where(x => x.Key.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase))
            .ToDictionary(x => x.Key.Remove(0, "wiki_".Length), x => x.Value);

        await _mediator.Send(new IncrementCounterActivatorCommand
        {
            Name = "plugin",
            Counters = plugins
        });

        await _mediator.Send(new IncrementCounterActivatorCommand
        {
            Name = "wiki",
            Counters = wikis
        });
    }

    /// <inheritdoc/>
    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, AiModelUseageMessage message)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, AiModelUseageMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }
}

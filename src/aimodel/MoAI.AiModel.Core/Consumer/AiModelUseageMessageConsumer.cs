using Maomi.MQ;
using MoAI.AiModel.Events;
using MoAI.Database;

namespace MoAI.AiModel.Consumer;

/// <summary>
/// 记录模型使用量.
/// </summary>
[Consumer("aimodel_useage", Qos = 100)]
public class AiModelUseageMessageConsumer : IConsumer<AiModelUseageMessage>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelUseageMessageConsumer"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public AiModelUseageMessageConsumer(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, AiModelUseageMessage message)
    {
        await _databaseContext.AiModelUseageLogs.AddAsync(new Database.Entities.AiModelUseageLogEntity
        {
            Channel = message.Channel,
            CompletionTokens = message.Usage.CompletionTokens,
            PromptTokens = message.Usage.PromptTokens,
            TotalTokens = message.Usage.TotalTokens,
            ModelId = message.AiModelId,
            UseriId = message.ContextUserId,
            CreateUserId = message.ContextUserId,
            UpdateUserId = message.ContextUserId,
        });
        await _databaseContext.SaveChangesAsync();
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

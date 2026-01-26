#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Chat;
using MoAI.AI.Chat.Models;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Models;
using MoAI.App.Chat.Works.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Runtime.CompilerServices;

namespace MoAI.App.Chat.Works.Commands;

/// <summary>
/// <inheritdoc cref="DebugChatAppInstanceCommand"/>
/// </summary>
public partial class DebugChatAppInstanceCommandHandler : ProcessingChatStreamBase, IStreamRequestHandler<DebugChatAppInstanceCommand, AiProcessingChatItem>, IAsyncDisposable
{
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebugChatAppInstanceCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="nativePluginFactory"></param>
    /// <param name="aiClientBuilder"></param>
    /// <param name="messagePublisher"></param>
    public DebugChatAppInstanceCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, IMediator mediator, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, INativePluginFactory nativePluginFactory, IAiClientBuilder aiClientBuilder, IMessagePublisher messagePublisher, IRedisDatabase redisDatabase)
        : base(serviceProvider, databaseContext, mediator, loggerFactory, httpClientFactory, nativePluginFactory, aiClientBuilder, messagePublisher)
    {
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<AiProcessingChatItem> Handle(DebugChatAppInstanceCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 获取应用信息
        var appEntity = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appEntity == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var debugKey = $"{appEntity.Id}:{request.ContextUserId}";

        // 补全对话上下文，无论是否第一次新建，后续按流程走就行
        var history = await _redisDatabase.GetAsync<List<AppChatappChatHistoryEntity>>(debugKey) ?? new List<AppChatappChatHistoryEntity>();

        ProcessingChatStreamParameters processingChatStreamParameters = new()
        {
            ChatId = Guid.CreateVersion7(),
            CompletionId = $"chatcmpl-" + Guid.CreateVersion7().ToString("N"),
            ContextUserId = request.ContextUserId,
            ContextUserType = request.ContextUserType,
            FileKey = request.FileKey,
            ModelId = request.ModelId,
            Prompt = request.Prompt,
            ExecutionSettings = request.ExecutionSettings ?? Array.Empty<KeyValueString>(),
            ChatMessages = history.Select(x => new RoleProcessingChoice { Role = x.Role, Choices = x.Content.JsonToObject<IReadOnlyCollection<DefaultAiProcessingChoice>>()! }).ToArray(),
            Plugins = request.Plugins ?? Array.Empty<string>(),
            Question = request.Question,
            WikiIds = request.WikiIds ?? Array.Empty<int>(),
        };

        var userChatRecord = new AppChatappChatHistoryEntity
        {
            ChatId = processingChatStreamParameters.ChatId,
            CompletionsId = processingChatStreamParameters.CompletionId,
            Content = GenerateChatProcessingChoice(processingChatStreamParameters.ChatId, request.Question, request.FileKey).ToJsonString(),
            Role = AuthorRole.User.Label,
        };

        await _databaseContext.AppChatappChatHistories.AddAsync(userChatRecord);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        AiProcessingChatItem lastChunk = default!;
        await foreach (var item in HandlerAsync(processingChatStreamParameters, cancellationToken))
        {
            lastChunk = item;
            if (appEntity.IsForeign)
            {
                yield return new AiProcessingChatItem
                {
                    Choices = item.Choices.Where(choice => choice.StreamType != AiProcessingChatStreamType.Plugin).ToArray(),
                    FinishReason = item.FinishReason,
                    Usage = item.Usage
                };
            }
            else
            {
                yield return item;
            }
        }

        var aiChatRecord = new AppChatappChatHistoryEntity
        {
            ChatId = processingChatStreamParameters.ChatId,
            CompletionsId = processingChatStreamParameters.CompletionId,
            Content = lastChunk.Choices.ToJsonString(),
            Role = AuthorRole.Assistant.Label
        };

        history.Add(aiChatRecord);
    }
}

#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MoAI.AI.Chat;
using MoAI.AI.Chat.Models;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Events;
using MoAI.App.AIAssistant.Handlers;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin;
using ModelContextProtocol.Protocol;
using System.Runtime.CompilerServices;

namespace MoAI.App.Apps.CommonApp.Handlers;

/// <summary>
/// <inheritdoc cref="ProcessingAppChatCommand"/>
/// </summary>
public partial class ProcessingAppChatCommandHandler : ProcessingChatStreamBase, IStreamRequestHandler<ProcessingAppChatCommand, AiProcessingChatItem>, IAsyncDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingAppChatCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="nativePluginFactory"></param>
    /// <param name="aiClientBuilder"></param>
    /// <param name="messagePublisher"></param>
    public ProcessingAppChatCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, IMediator mediator, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, INativePluginFactory nativePluginFactory, IAiClientBuilder aiClientBuilder, IMessagePublisher messagePublisher)
        : base(serviceProvider, databaseContext, mediator, loggerFactory, httpClientFactory, nativePluginFactory, aiClientBuilder, messagePublisher)
    {
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<AiProcessingChatItem> Handle(ProcessingAppChatCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 获取应用信息
        var appEntity = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appEntity == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var appCommonEntity = await _databaseContext.AppCommons
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appCommonEntity == null)
        {
            throw new BusinessException("应用配置不存在") { StatusCode = 404 };
        }

        // 检查对话
        var chatObjectEntity = await _databaseContext.AppCommonChats
            .Where(x => x.Id == request.ChatId && x.AppId == request.AppId && x.TeamId == request.TeamId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatObjectEntity == null)
        {
            throw new BusinessException("对话不存在") { StatusCode = 404 };
        }

        // 补全对话上下文，无论是否第一次新建，后续按流程走就行
        var history = await _databaseContext.AppCommonChatHistories
            .Where(x => x.ChatId == request.ChatId)
            .OrderBy(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        ProcessingChatStreamParameters processingChatStreamParameters = new()
        {
            ChatId = request.ChatId,
            CompletionId = $"chatcmpl-" + Guid.CreateVersion7().ToString("N"),
            ContextUserId = request.ContextUserId,
            ContextUserType = request.ContextUserType,
            FileKey = request.FileKey,
            ModelId = appCommonEntity.ModelId,
            Prompt = appCommonEntity.Prompt,
            ExecutionSettings = appCommonEntity.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>() ?? Array.Empty<KeyValueString>(),
            ChatMessages = history.Select(x => new RoleProcessingChoice { Role = x.Role, Choices = x.Content.JsonToObject<IReadOnlyCollection<DefaultAiProcessingChoice>>()! }).ToArray(),
            Plugins = appCommonEntity.Plugins.JsonToObject<IReadOnlyCollection<string>>()!,
            Question = request.Question,
            WikiIds = appCommonEntity.Plugins.JsonToObject<IReadOnlyCollection<int>>()!,
        };

        var userChatRecord = new AppAssistantChatHistoryEntity
        {
            ChatId = request.ChatId,
            CompletionsId = processingChatStreamParameters.CompletionId,
            Content = GenerateChatProcessingChoice(request.ChatId, request.Question, request.FileKey).ToJsonString(),
            Role = AuthorRole.User.Label,
        };

        await _databaseContext.AppAssistantChatHistories.AddAsync(userChatRecord);
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

        chatObjectEntity.OutTokens += lastChunk.Usage?.CompletionTokens ?? 0;
        chatObjectEntity.InputTokens += lastChunk.Usage?.PromptTokens ?? 0;
        chatObjectEntity.TotalTokens += lastChunk.Usage?.TotalTokens ?? 0;
        _databaseContext.AppCommonChats.Update(chatObjectEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var aiChatRecord = new AppCommonChatHistoryEntity
        {
            ChatId = chatObjectEntity.Id,
            CompletionsId = processingChatStreamParameters.CompletionId,
            Content = lastChunk.Choices.ToJsonString(),
            Role = AuthorRole.Assistant.Label
        };

        await _databaseContext.AppCommonChatHistories.AddAsync(aiChatRecord);
        await _databaseContext.SaveChangesAsync();
    }
}

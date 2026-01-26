#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable CA1031 // 不捕获常规异常类型

using MediatR;
using MoAI.AI.Models;
using MoAI.App.Chat.Works.Commands;
using MoAI.App.Chat.Works.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Extensions;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.App.Chat.Works.Queries;

public class QueryDebugChatAppInstanceCommandHandler : IRequestHandler<QueryDebugChatAppInstanceCommand, QueryDebugChatAppInstanceCommandResponse>
{
    private readonly IRedisDatabase _redisDatabase;

    public QueryDebugChatAppInstanceCommandHandler(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    public async Task<QueryDebugChatAppInstanceCommandResponse> Handle(QueryDebugChatAppInstanceCommand request, CancellationToken cancellationToken)
    {
        var debugKey = $"{request.AppId}:{request.ContextUserId}";
        var historyEntities = await _redisDatabase.GetAsync<IReadOnlyCollection<AppChatappChatHistoryEntity>>(debugKey);

        historyEntities ??= Array.Empty<AppChatappChatHistoryEntity>();
        List<AppChatHistoryItem> chatHistoryItems = new();

        foreach (var item in historyEntities)
        {
            var choices = item.Content.JsonToObject<IReadOnlyCollection<DefaultAiProcessingChoice>>();

            chatHistoryItems.Add(new AppChatHistoryItem
            {
                RecordId = item.Id,
                AuthorName = item.Role,
                Choices = choices?.Select(x => x.ToAiProcessingChoice()).ToArray() ?? Array.Empty<AiProcessingChoice>()
            });
        }

        return new QueryDebugChatAppInstanceCommandResponse
        {
            ChatHistory = chatHistoryItems
        };
    }
}

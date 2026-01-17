using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.App.Apps.CommonApp.Queries;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Store.Queries;

namespace MoAI.App.AppCommon.Queries;

/// <summary>
/// <inheritdoc cref="QueryAppChatHistoryCommand"/>
/// </summary>
public class QueryAppChatHistoryCommandHandler : IRequestHandler<QueryAppChatHistoryCommand, QueryAppChatHistoryCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppChatHistoryCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryAppChatHistoryCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryAppChatHistoryCommandResponse> Handle(QueryAppChatHistoryCommand request, CancellationToken cancellationToken)
    {
        // 检查应用是否存在且用户有权访问
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var chatCommontEntity = await _databaseContext.AppCommonChats
            .Where(x => x.Id == request.ChatId && x.CreateUserId == request.ContextUserId && x.AppId == request.AppId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatCommontEntity == null)
        {
            throw new BusinessException("未找到对话记录") { StatusCode = 404 };
        }

        var historyEntities = await _databaseContext.AppCommonChatHistories
            .Where(x => x.ChatId == request.ChatId)
            .OrderBy(x => x.CreateTime)
            .ToArrayAsync(cancellationToken);

        List<AppChatHistoryItem> chatHistoryItems = new();

        foreach (var item in historyEntities)
        {
            var choices = item.Content.JsonToObject<IReadOnlyCollection<DefaultAiProcessingChoice>>();
            if (app.IsForeign)
            {
                choices = choices!.Where(x => x.StreamType != AiProcessingChatStreamType.Plugin).ToArray();
            }

            Dictionary<string, DefaultAiProcessingChoice> fileKvs = new();

            foreach (var choice in choices ?? Array.Empty<DefaultAiProcessingChoice>())
            {
                if (choice.FileCall?.FileKey != null)
                {
                    var ossObjectKey = $"chat/{request.ChatId}/{choice.FileCall.FileKey}";
                    fileKvs.Add(ossObjectKey, choice);
                }
            }

            if (fileKvs.Count > 0)
            {
                var fileUrls = await _mediator.Send(
                    new QueryFileDownloadUrlCommand
                    {
                        ExpiryDuration = TimeSpan.FromHours(3),
                        ObjectKeys = fileKvs.Select(x => new KeyValueString
                        {
                            Key = x.Key,
                            Value = x.Key
                        }).ToArray()
                    },
                    cancellationToken);

                foreach (var fileUrl in fileUrls.Urls)
                {
                    if (fileKvs.TryGetValue(fileUrl.Key, out var choice))
                    {
                        choice.FileCall!.FileUrl = fileUrl.Value.ToString();
                    }
                }
            }

            chatHistoryItems.Add(new AppChatHistoryItem
            {
                RecordId = item.Id,
                AuthorName = item.Role,
                Choices = choices?.Select(x => x.ToAiProcessingChoice()).ToArray() ?? Array.Empty<AiProcessingChoice>()
            });
        }

        return new QueryAppChatHistoryCommandResponse
        {
            ChatId = chatCommontEntity.Id,
            Title = chatCommontEntity.Title,
            Avatar = app.Avatar,
            CreateTime = chatCommontEntity.CreateTime,
            UpdateTime = chatCommontEntity.UpdateTime,
            ChatHistory = chatHistoryItems,
        };
    }
}

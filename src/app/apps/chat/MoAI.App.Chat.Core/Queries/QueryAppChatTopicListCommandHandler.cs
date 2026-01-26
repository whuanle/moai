using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Chat.Works.Models;
using MoAI.App.Chat.Works.Queries;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.App.AppCommon.Queries;

/// <summary>
/// <inheritdoc cref="QueryChatApptInstanceTopicListCommand"/>
/// </summary>
public class QueryAppChatTopicListCommandHandler : IRequestHandler<QueryChatApptInstanceTopicListCommand, QueryChatAppInstanceTopicListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppChatTopicListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAppChatTopicListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryChatAppInstanceTopicListCommandResponse> Handle(QueryChatApptInstanceTopicListCommand request, CancellationToken cancellationToken)
    {
        // 检查应用是否存在且用户有权访问
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        // 检查用户是否有权访问应用（公开应用或用户在团队内）
        if (!app.IsPublic)
        {
            var isTeamMember = await _databaseContext.TeamUsers
                .AnyAsync(x => x.UserId == request.ContextUserId, cancellationToken);

            if (!isTeamMember)
            {
                throw new BusinessException("无权访问此应用") { StatusCode = 403 };
            }
        }

        var chatTopics = await _databaseContext.AppChatappChats
            .Where(x => x.AppId == request.AppId && x.CreateUserId == request.ContextUserId)
            .OrderByDescending(x => x.UpdateTime)
            .Select(x => new AppChatTopicItem
            {
                ChatId = x.Id,
                Title = x.Title,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryChatAppInstanceTopicListCommandResponse { Items = chatTopics };
    }
}

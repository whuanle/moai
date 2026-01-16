using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Database;

namespace MoAI.App.Apps.CommonApp.Queries;

/// <summary>
/// <inheritdoc cref="QueryAppChatTopicListCommand"/>
/// </summary>
public class QueryAppChatTopicListCommandHandler : IRequestHandler<QueryAppChatTopicListCommand, QueryAppChatTopicListCommandResponse>
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
    public async Task<QueryAppChatTopicListCommandResponse> Handle(QueryAppChatTopicListCommand request, CancellationToken cancellationToken)
    {
        var chatTopics = await _databaseContext.AppCommonChats
            .Where(x => x.AppId == request.AppId && x.CreateUserId == request.ContextUserId)
            .OrderByDescending(x => x.UpdateTime)
            .Select(x => new AppChatTopicItem
            {
                ChatId = x.Id,
                Title = x.Title,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryAppChatTopicListCommandResponse { Items = chatTopics };
    }
}

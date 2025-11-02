using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserViewAiAssistantChatTopicListCommand"/>
/// </summary>
public class QueryAiAssistantChatTopicListCommandHandler : IRequestHandler<QueryUserViewAiAssistantChatTopicListCommand, QueryAiAssistantChatTopicListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantChatTopicListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userContext"></param>
    public QueryAiAssistantChatTopicListCommandHandler(DatabaseContext databaseContext, UserContext userContext)
    {
        _databaseContext = databaseContext;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAiAssistantChatTopicListCommandResponse> Handle(QueryUserViewAiAssistantChatTopicListCommand request, CancellationToken cancellationToken)
    {
        var chatTopics = await _databaseContext.AppAssistantChats
            .Where(x => x.CreateUserId == _userContext.UserId)
            .OrderByDescending(x => x.UpdateTime)
            .Select(x => new AiAssistantChatTopic
            {
                ChatId = x.Id,
                Title = x.Title,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryAiAssistantChatTopicListCommandResponse { Items = chatTopics };
    }
}

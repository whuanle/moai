using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// <inheritdoc cref="QueryAiAssistantCreatorCommand"/>
/// </summary>
public class QueryAiAssistantCreatorCommandHandler : IRequestHandler<QueryAiAssistantCreatorCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantCreatorCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAiAssistantCreatorCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(QueryAiAssistantCreatorCommand request, CancellationToken cancellationToken)
    {
        var creatorId = await _databaseContext.AppAssistantChats
            .Where(x => x.Id == request.ChatId)
            .Select(x => x.CreateUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return new SimpleInt { Value = creatorId };
    }
}

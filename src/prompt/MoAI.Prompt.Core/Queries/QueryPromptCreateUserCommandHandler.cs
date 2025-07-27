using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;

namespace MoAI.Prompt.Queries;

public class QueryPromptCreateUserCommandHandler : IRequestHandler<QueryPromptCreateUserCommand, QueryPromptCreateUserCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    public QueryPromptCreateUserCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<QueryPromptCreateUserCommandResponse> Handle(QueryPromptCreateUserCommand request, CancellationToken cancellationToken)
    {
        return await _databaseContext.Prompts
            .Where(x => x.Id == request.PromptId)
            .Select(x => new QueryPromptCreateUserCommandResponse
            {
                UserId = x.CreateUserId
            })
            .FirstOrDefaultAsync(cancellationToken) ?? new QueryPromptCreateUserCommandResponse
            {
                UserId = 0
            };
    }
}

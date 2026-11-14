using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Queries;
using MoAI.AIChannel.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// <inheritdoc cref="QueryAIModelAuthorizationCommand"/>
/// </summary>
public class QueryAIModelAuthorizationCommandHandler : IRequestHandler<QueryAIModelAuthorizationCommand, QueryAIModelAuthorizationCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAIModelAuthorizationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAIModelAuthorizationCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAIModelAuthorizationCommandResponse> Handle(QueryAIModelAuthorizationCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("未找到模型，请检查 id 是否正确.") { StatusCode = 404 };
        }

        var isTeamChannel = await _databaseContext.AiChannels
            .AnyAsync(x => x.Id == model.ChannelId && x.TeamId > 0, cancellationToken);
        if (isTeamChannel)
        {
            throw new BusinessException("团队自有渠道模型仅本团队可用，无团队授权概念.") { StatusCode = 400 };
        }

        var response = new QueryAIModelAuthorizationCommandResponse
        {
            ModelId = request.ModelId,
            IsPublic = model.IsPublic,
            Items = new List<QueryAIModelAuthorizationCommandResponseItem>(),
        };

        if (model.IsPublic)
        {
            return response;
        }

        var authorizations = await _databaseContext.AiModelAuthorizations
            .Where(x => x.AiModelId == request.ModelId)
            .Join(_databaseContext.Teams, a => a.TeamId, t => t.Id, (a, t) => new { a.TeamId, t.Name })
            .OrderBy(x => x.TeamId)
            .ToListAsync(cancellationToken);

        response.Items = authorizations
            .Select(x => new QueryAIModelAuthorizationCommandResponseItem
            {
                TeamId = x.TeamId,
                TeamName = x.Name,
            })
            .ToList();

        return response;
    }
}

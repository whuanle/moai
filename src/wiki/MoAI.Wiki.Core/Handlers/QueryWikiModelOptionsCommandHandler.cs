using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiModelOptionsCommand"/>
/// </summary>
public class QueryWikiModelOptionsCommandHandler : IRequestHandler<QueryWikiModelOptionsCommand, QueryWikiModelOptionsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiModelOptionsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryWikiModelOptionsCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiModelOptionsCommandResponse> Handle(QueryWikiModelOptionsCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var authorizedModelIds = await _databaseContext.AiModelAuthorizations
            .Where(x => x.TeamId == request.TeamId)
            .Select(x => x.AiModelId)
            .ToListAsync(cancellationToken);

        var models = await (from m in _databaseContext.AiModels
                            join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                            where m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                                && (m.IsPublic || authorizedModelIds.Contains(m.Id))
                            select new { m.Id, m.Name, m.ModelKind }).ToListAsync(cancellationToken);

        return new QueryWikiModelOptionsCommandResponse
        {
            EmbeddingModels = models
                .Where(x => string.Equals(x.ModelKind, "embedding", StringComparison.OrdinalIgnoreCase))
                .Select(x => new WikiModelOptionItem { Id = x.Id, Name = x.Name, ModelKind = x.ModelKind })
                .ToList(),
            ConversationModels = models
                .Where(x => string.Equals(x.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
                .Select(x => new WikiModelOptionItem { Id = x.Id, Name = x.Name, ModelKind = x.ModelKind })
                .ToList(),
            RerankModels = models
                .Where(x => string.Equals(x.ModelKind, "rerank", StringComparison.OrdinalIgnoreCase))
                .Select(x => new WikiModelOptionItem { Id = x.Id, Name = x.Name, ModelKind = x.ModelKind })
                .ToList(),
        };
    }
}

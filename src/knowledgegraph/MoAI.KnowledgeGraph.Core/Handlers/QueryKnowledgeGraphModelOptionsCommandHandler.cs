using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphModelOptionsCommand"/>
/// </summary>
public class QueryKnowledgeGraphModelOptionsCommandHandler : IRequestHandler<QueryKnowledgeGraphModelOptionsCommand, QueryKnowledgeGraphModelOptionsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphModelOptionsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryKnowledgeGraphModelOptionsCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphModelOptionsCommandResponse> Handle(QueryKnowledgeGraphModelOptionsCommand request, CancellationToken cancellationToken)
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
                            select new { m.Id, m.Name, m.ModelKind })
            .ToListAsync(cancellationToken);

        // ModelKind 大小写归一在内存判断（StringComparison 不可翻译为 SQL）
        return new QueryKnowledgeGraphModelOptionsCommandResponse
        {
            ConversationModels = models
                .Where(x => string.Equals(x.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .Select(x => new KnowledgeGraphModelOptionItem { Id = x.Id, Name = x.Name })
                .ToList(),
        };
    }
}

using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.KnowledgeGraph.Models;
using MoAI.Team.Services;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱权限判定实现.
/// </summary>
[InjectOnScoped]
public class KnowledgeGraphAuthorizer : IKnowledgeGraphAuthorizer
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphAuthorizer"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public KnowledgeGraphAuthorizer(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<TeamRole> RequireTeamRoleAsync(long teamId, bool adminOnly, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var role = await _teamService.GetMyRoleAsync(teamId, userId, cancellationToken);
        if (role == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (adminOnly && role == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以执行该操作.") { StatusCode = 403 };
        }

        return role.Value;
    }

    /// <inheritdoc/>
    public async Task<(KnowledgeGraphEntity Graph, TeamRole Role)> AuthorizeAsync(long kgId, bool adminOnly, CancellationToken cancellationToken)
    {
        var graph = await _databaseContext.KnowledgeGraphs.FirstOrDefaultAsync(x => x.Id == kgId, cancellationToken);
        if (graph == null)
        {
            throw new BusinessException("知识图谱不存在.") { StatusCode = 404 };
        }

        var role = await RequireTeamRoleAsync(graph.TeamId, adminOnly, cancellationToken);
        return (graph, role);
    }

    /// <inheritdoc/>
    public async Task<(KnowledgeGraphEntity Graph, TeamRole Role)> AuthorizeManagedAsync(long kgId, bool adminOnly, CancellationToken cancellationToken)
    {
        var result = await AuthorizeAsync(kgId, adminOnly, cancellationToken);
        if (string.Equals(result.Graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱为只读.") { StatusCode = 409 };
        }

        return result;
    }
}

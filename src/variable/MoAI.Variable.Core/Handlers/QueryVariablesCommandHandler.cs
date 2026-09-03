using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Variable.Queries;
using MoAI.Variable.Queries.Responses;

namespace MoAI.Variable.Handlers;

/// <summary>
/// <inheritdoc cref="QueryVariablesCommand"/>
/// </summary>
public class QueryVariablesCommandHandler : IRequestHandler<QueryVariablesCommand, QueryVariablesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryVariablesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryVariablesCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryVariablesCommandResponse> Handle(QueryVariablesCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var query = _databaseContext.TeamVariables
            .Where(x => x.TeamId == request.TeamId);

        if (!string.IsNullOrWhiteSpace(request.GroupName))
        {
            query = query.Where(x => x.GroupName == request.GroupName);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Key.Contains(keyword) || x.Description.Contains(keyword));
        }

        var items = await query
            .OrderBy(x => x.GroupName)
            .ThenBy(x => x.Key)
            .Select(x => new TeamVariableItem
            {
                VariableId = x.Id,
                TeamId = x.TeamId,
                Key = x.Key,
                GroupName = x.GroupName,
                IsSecret = x.IsSecret,
                // 私密变量值对成员恒为 null，仅管理员在详情接口可见
                Value = x.IsSecret ? null : x.Value,
                Description = x.Description,
                UpdateTime = x.UpdateTime
            })
            .ToListAsync(cancellationToken);

        return new QueryVariablesCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)myRole.Value,
            Items = items
        };
    }
}

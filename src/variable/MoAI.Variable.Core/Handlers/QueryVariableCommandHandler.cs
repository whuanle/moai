using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Variable.Queries;
using MoAI.Variable.Queries.Responses;

namespace MoAI.Variable.Handlers;

/// <summary>
/// <inheritdoc cref="QueryVariableCommand"/>
/// </summary>
public class QueryVariableCommandHandler : IRequestHandler<QueryVariableCommand, QueryVariableCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryVariableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryVariableCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryVariableCommandResponse> Handle(QueryVariableCommand request, CancellationToken cancellationToken)
    {
        var variable = await _databaseContext.TeamVariables
            .FirstOrDefaultAsync(x => x.Id == request.VariableId, cancellationToken);

        if (variable == null)
        {
            throw new BusinessException("变量不存在.") { StatusCode = 404 };
        }

        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(variable.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        // 私密变量的值永不回传前端（Owner/Admin 编辑时也只能在 value 不填时保持、填写时覆盖）
        // 普通变量则返回明文值，便于编辑时回填
        return new QueryVariableCommandResponse
        {
            VariableId = variable.Id,
            TeamId = variable.TeamId,
            Key = variable.Key,
            Name = variable.Name,
            IsSecret = variable.IsSecret,
            Value = variable.IsSecret ? null! : variable.Value,
            Description = variable.Description,
            UpdateTime = variable.UpdateTime
        };
    }
}

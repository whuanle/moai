using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Variable.Commands;

namespace MoAI.Variable.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteVariableCommand"/>
/// </summary>
public class DeleteVariableCommandHandler : IRequestHandler<DeleteVariableCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteVariableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public DeleteVariableCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteVariableCommand request, CancellationToken cancellationToken)
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

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理变量.") { StatusCode = 403 };
        }

        // 实体实现 IFullAudited，框架将删除转换为软删除并记录操作人
        _databaseContext.TeamVariables.Remove(variable);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Service;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Variable.Commands;

namespace MoAI.Variable.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateVariableCommand"/>
/// </summary>
public class UpdateVariableCommandHandler : IRequestHandler<UpdateVariableCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IAESProvider _aesProvider;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateVariableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="aesProvider">AES 加密服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public UpdateVariableCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IAESProvider aesProvider, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _aesProvider = aesProvider;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateVariableCommand request, CancellationToken cancellationToken)
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

        if (request.GroupName != null)
        {
            variable.GroupName = request.GroupName;
        }

        if (request.Description != null)
        {
            variable.Description = request.Description;
        }

        // 值为 null 表示保持不变（私密变量编辑时前端留空以避免回传明文）
        if (request.Value != null)
        {
            variable.Value = variable.IsSecret ? _aesProvider.Encrypt(request.Value) : request.Value;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}

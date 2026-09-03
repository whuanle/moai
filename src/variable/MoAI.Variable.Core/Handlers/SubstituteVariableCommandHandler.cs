using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Variable.Commands;
using MoAI.Variable.Commands.Responses;
using MoAI.Variable.Services;

namespace MoAI.Variable.Handlers;

/// <summary>
/// <inheritdoc cref="SubstituteVariableCommand"/>
/// </summary>
public class SubstituteVariableCommandHandler : IRequestHandler<SubstituteVariableCommand, SubstituteVariableCommandResponse>
{
    private readonly IVariableService _variableService;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubstituteVariableCommandHandler"/> class.
    /// </summary>
    /// <param name="variableService">变量服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public SubstituteVariableCommandHandler(IVariableService variableService, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _variableService = variableService;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<SubstituteVariableCommandResponse> Handle(SubstituteVariableCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        // 替换结果可能包含私密值，仅允许管理员直接调用（成员侧由插件运行时在服务端内部替换）
        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以执行变量替换.") { StatusCode = 403 };
        }

        var content = await _variableService.SubstituteAsync(request.TeamId, request.Content, cancellationToken);

        return new SubstituteVariableCommandResponse
        {
            Content = content
        };
    }
}

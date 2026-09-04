using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Service;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Variable.Commands;

namespace MoAI.Variable.Handlers;

/// <summary>
/// <inheritdoc cref="CreateVariableCommand"/>
/// </summary>
public class CreateVariableCommandHandler : IRequestHandler<CreateVariableCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IAESProvider _aesProvider;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVariableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="aesProvider">AES 加密服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public CreateVariableCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IAESProvider aesProvider, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _aesProvider = aesProvider;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateVariableCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理变量.") { StatusCode = 403 };
        }

        var keyExist = await _databaseContext.TeamVariables
            .AnyAsync(x => x.TeamId == request.TeamId && x.Key == request.Key, cancellationToken);

        if (keyExist)
        {
            throw new BusinessException("变量名已存在，请更换后重试.") { StatusCode = 409 };
        }

        var variable = new TeamVariableEntity
        {
            TeamId = request.TeamId,
            Key = request.Key,
            Name = request.Name ?? string.Empty,
            IsSecret = request.IsSecret,
            // 私密变量 AES 加密落库，普通变量明文
            Value = request.IsSecret ? _aesProvider.Encrypt(request.Value) : request.Value,
            Description = request.Description ?? string.Empty,
        };

        _databaseContext.TeamVariables.Add(variable);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleLong
        {
            Value = variable.Id
        };
    }
}

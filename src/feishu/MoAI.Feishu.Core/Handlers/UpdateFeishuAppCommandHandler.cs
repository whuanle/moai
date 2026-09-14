using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Feishu.Commands;
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.Feishu.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateFeishuAppCommand"/>
/// </summary>
public class UpdateFeishuAppCommandHandler : IRequestHandler<UpdateFeishuAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly FeishuConnectionManager _connectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateFeishuAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="connectionManager">飞书长连接管理器.</param>
    public UpdateFeishuAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, FeishuConnectionManager connectionManager)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _connectionManager = connectionManager;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateFeishuAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.FeishuApps
            .FirstOrDefaultAsync(x => x.Id == request.FeishuAppId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("飞书应用连接不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(entity.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理飞书应用连接.") { StatusCode = 403 };
        }

        var nameExist = await _databaseContext.FeishuApps
            .AnyAsync(x => x.TeamId == entity.TeamId && x.Id != entity.Id && x.Name == request.Name, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("连接名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        entity.Name = request.Name;
        entity.Description = request.Description ?? string.Empty;
        entity.IsDisable = request.IsDisable;
        if (!string.IsNullOrWhiteSpace(request.AppSecret))
        {
            entity.AppSecret = request.AppSecret;
        }

        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            entity.Domain = request.Domain;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        _connectionManager.ApplyUpdate(entity.Id, entity.AppId, entity.AppSecret, entity.Domain, entity.IsDisable);

        return EmptyCommandResponse.Default;
    }
}

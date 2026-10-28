using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.DingTalk.Commands;
using MoAI.DingTalk.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.DingTalk.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateDingTalkAppCommand"/>
/// </summary>
public class UpdateDingTalkAppCommandHandler : IRequestHandler<UpdateDingTalkAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly DingTalkConnectionManager _connectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDingTalkAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="connectionManager">钉钉长连接管理器.</param>
    public UpdateDingTalkAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, DingTalkConnectionManager connectionManager)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _connectionManager = connectionManager;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateDingTalkAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.DingTalkApps
            .FirstOrDefaultAsync(x => x.Id == request.DingTalkAppId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("钉钉应用连接不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(entity.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理钉钉应用连接.") { StatusCode = 403 };
        }

        var nameExist = await _databaseContext.DingTalkApps
            .AnyAsync(x => x.TeamId == entity.TeamId && x.Id != entity.Id && x.Name == request.Name, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("连接名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        // AppKey 可编辑（填错凭证无需删除重建），保持全局唯一
        if (!string.IsNullOrWhiteSpace(request.AppKey) && request.AppKey != entity.AppKey)
        {
            var appKeyExist = await _databaseContext.DingTalkApps
                .AnyAsync(x => x.AppKey == request.AppKey && x.Id != entity.Id, cancellationToken);

            if (appKeyExist)
            {
                throw new BusinessException("该钉钉应用已创建过连接.") { StatusCode = 409 };
            }
        }

        entity.Name = request.Name;
        entity.Description = request.Description ?? string.Empty;
        entity.IsDisable = request.IsDisable;
        if (!string.IsNullOrWhiteSpace(request.AppSecret))
        {
            entity.AppSecret = request.AppSecret;
        }

        if (!string.IsNullOrWhiteSpace(request.AppKey))
        {
            entity.AppKey = request.AppKey;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        _connectionManager.ApplyUpdate(entity.Id, entity.AppKey, entity.AppSecret, entity.IsDisable);

        return EmptyCommandResponse.Default;
    }
}

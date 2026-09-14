using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Feishu.Commands;
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.Feishu.Handlers;

/// <summary>
/// <inheritdoc cref="CreateFeishuAppCommand"/>
/// </summary>
public class CreateFeishuAppCommandHandler : IRequestHandler<CreateFeishuAppCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly FeishuConnectionManager _connectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFeishuAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="connectionManager">飞书长连接管理器.</param>
    public CreateFeishuAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, FeishuConnectionManager connectionManager)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _connectionManager = connectionManager;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateFeishuAppCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理飞书应用连接.") { StatusCode = 403 };
        }

        var nameExist = await _databaseContext.FeishuApps
            .AnyAsync(x => x.TeamId == request.TeamId && x.Name == request.Name, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("连接名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        // 一个飞书开放平台应用全局只允许创建一条连接，避免同一应用多处建连互相挤掉线
        var appIdExist = await _databaseContext.FeishuApps
            .AnyAsync(x => x.AppId == request.AppId, cancellationToken);

        if (appIdExist)
        {
            throw new BusinessException("该飞书应用已创建过连接.") { StatusCode = 409 };
        }

        var entity = new FeishuAppEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = (int)request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            AppId = request.AppId,
            AppSecret = request.AppSecret,
            Domain = string.IsNullOrWhiteSpace(request.Domain) ? FeishuConnectionManager.DefaultDomain : request.Domain,
            IsDisable = false,
        };

        _databaseContext.FeishuApps.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        _connectionManager.ApplyCreate(entity.Id, entity.AppId, entity.AppSecret, entity.Domain);

        return new SimpleGuid
        {
            Value = entity.Id
        };
    }
}

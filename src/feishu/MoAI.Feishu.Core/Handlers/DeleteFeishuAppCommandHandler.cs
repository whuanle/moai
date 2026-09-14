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
/// <inheritdoc cref="DeleteFeishuAppCommand"/>
/// </summary>
public class DeleteFeishuAppCommandHandler : IRequestHandler<DeleteFeishuAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly FeishuConnectionManager _connectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteFeishuAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="connectionManager">飞书长连接管理器.</param>
    public DeleteFeishuAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, FeishuConnectionManager connectionManager)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _connectionManager = connectionManager;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteFeishuAppCommand request, CancellationToken cancellationToken)
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

        // 解除全部绑定后再删除连接
        await _databaseContext.SoftDeleteAsync(_databaseContext.FeishuAppBindings.Where(x => x.FeishuAppId == entity.Id));
        await _databaseContext.SoftDeleteAsync(_databaseContext.FeishuApps.Where(x => x.Id == entity.Id));

        _connectionManager.ApplyDelete(entity.Id);

        return EmptyCommandResponse.Default;
    }
}

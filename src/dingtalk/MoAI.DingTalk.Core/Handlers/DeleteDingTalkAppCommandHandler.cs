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
/// <inheritdoc cref="DeleteDingTalkAppCommand"/>
/// </summary>
public class DeleteDingTalkAppCommandHandler : IRequestHandler<DeleteDingTalkAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly DingTalkConnectionManager _connectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteDingTalkAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="connectionManager">钉钉长连接管理器.</param>
    public DeleteDingTalkAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, DingTalkConnectionManager connectionManager)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _connectionManager = connectionManager;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteDingTalkAppCommand request, CancellationToken cancellationToken)
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

        // 解除全部绑定后再删除连接
        await _databaseContext.SoftDeleteAsync(_databaseContext.DingTalkAppBindings.Where(x => x.DingTalkAppId == entity.Id));
        await _databaseContext.SoftDeleteAsync(_databaseContext.DingTalkApps.Where(x => x.Id == entity.Id));

        await _connectionManager.ApplyDeleteAsync(entity.Id);

        return EmptyCommandResponse.Default;
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.WeixinWork.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.WeixinWork.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteWeixinWorkAppCommand"/>
/// </summary>
public class DeleteWeixinWorkAppCommandHandler : IRequestHandler<DeleteWeixinWorkAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWeixinWorkAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public DeleteWeixinWorkAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWeixinWorkAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.WeixinWorkApps
            .FirstOrDefaultAsync(x => x.Id == request.WeixinWorkAppId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("企业微信应用连接不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(entity.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理企业微信应用连接.") { StatusCode = 403 };
        }

        // 解除全部绑定后再删除连接
        await _databaseContext.SoftDeleteAsync(_databaseContext.WeixinWorkAppBindings.Where(x => x.WeixinWorkAppId == entity.Id));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WeixinWorkApps.Where(x => x.Id == entity.Id));

        return EmptyCommandResponse.Default;
    }
}

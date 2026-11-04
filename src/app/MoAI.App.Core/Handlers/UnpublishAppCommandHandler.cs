using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UnpublishAppCommand"/>
/// </summary>
public class UnpublishAppCommandHandler : IRequestHandler<UnpublishAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnpublishAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    public UnpublishAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IOrgGrantService orgGrantService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UnpublishAppCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以撤回发布应用.") { StatusCode = 403 };
        }

        // 撤回发布同时自动取消上架：清 is_public，并撤下仍在审核中的上架申请（历史申请记录保留）
        var pendingReviews = await _databaseContext.PublicationReviews
            .Where(x => x.ResourceType == (int)PublicationResourceType.App
                && x.ResourceId == app.Id.ToString()
                && x.State == (int)PublicationState.Pending)
            .ToListAsync(cancellationToken);

        if (pendingReviews.Count > 0)
        {
            _databaseContext.PublicationReviews.RemoveRange(pendingReviews);
        }

        if (app.PublishStatus != 0 || app.IsPublic)
        {
            app.PublishStatus = 0;
            app.PublishTime = null;
            app.PublishVersion = null;
            app.IsPublic = false;
            await _databaseContext.SaveChangesAsync(cancellationToken);

            // 联动下架同步清空应用的组织可见授权（重新上架会按新申请重新物化）
            await _orgGrantService.ReplaceGrantsAsync(OrgGrantResourceType.PublicApp, app.Id, [], cancellationToken);
        }
        else if (pendingReviews.Count > 0)
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}

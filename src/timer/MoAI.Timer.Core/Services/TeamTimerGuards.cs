using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.Timer.Services;

/// <summary>
/// 定时器命令的公共校验：团队角色门禁与绑定应用合法性.
/// </summary>
internal static class TeamTimerGuards
{
    /// <summary>
    /// 校验团队成员角色，写操作需要 Admin 及以上.
    /// </summary>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">当前用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task EnsureTeamAdminAsync(ITeamService teamService, long teamId, long userId, CancellationToken cancellationToken)
    {
        var myRole = await teamService.GetMyRoleAsync(teamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理定时器.") { StatusCode = 403 };
        }
    }

    /// <summary>
    /// 校验团队成员身份（读操作）.
    /// </summary>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">当前用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task EnsureTeamMemberAsync(ITeamService teamService, long teamId, long userId, CancellationToken cancellationToken)
    {
        var myRole = await teamService.GetMyRoleAsync(teamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }
    }

    /// <summary>
    /// 校验绑定的应用：存在、本团队、未禁用、Agent/流程类型且已发布.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>应用实体.</returns>
    public static async Task<AppEntity> EnsureValidAppAsync(DatabaseContext databaseContext, long teamId, Guid appId, CancellationToken cancellationToken)
    {
        var app = await databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == appId, cancellationToken)
            ?? throw new BusinessException("绑定的应用不存在.") { StatusCode = 404 };

        if (app.TeamId != teamId)
        {
            throw new BusinessException("只能绑定本团队的应用.") { StatusCode = 403 };
        }

        if (app.IsDisable)
        {
            throw new BusinessException("绑定的应用已被禁用.") { StatusCode = 400 };
        }

        if (app.AppType != (int)AppType.Agent && app.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("应用类型不支持绑定定时器.") { StatusCode = 400 };
        }

        if (app.PublishStatus != 1)
        {
            throw new BusinessException("绑定的应用尚未发布，发布后才可被定时执行.") { StatusCode = 400 };
        }

        return app;
    }
}

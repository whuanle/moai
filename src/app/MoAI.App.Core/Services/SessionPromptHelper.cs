using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Services;

/// <summary>
/// 会话专家提示词校验：会话仅可绑定本人个人提示词或会话所属团队的提示词.
/// </summary>
internal static class SessionPromptHelper
{
    /// <summary>
    /// 校验提示词对会话可用，不可用时抛 404.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="promptId">提示词 id.</param>
    /// <param name="sessionTeamId">会话所属团队 id.</param>
    /// <param name="userId">会话归属用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回校验任务.</returns>
    public static async Task EnsureUsableAsync(DatabaseContext databaseContext, ITeamService teamService, int promptId, int sessionTeamId, long userId, CancellationToken cancellationToken)
    {
        var prompt = await databaseContext.Prompts
            .Where(x => x.Id == promptId)
            .Select(x => new { x.TeamId, x.CreateUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (prompt == null)
        {
            throw new BusinessException("提示词不存在.") { StatusCode = 404 };
        }

        var usable = prompt.TeamId == 0
            ? prompt.CreateUserId == userId
            : prompt.TeamId == sessionTeamId
                && await teamService.GetMyRoleAsync(sessionTeamId, userId, cancellationToken) != null;

        if (!usable)
        {
            throw new BusinessException("提示词不存在或不可用.") { StatusCode = 404 };
        }
    }
}

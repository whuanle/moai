using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Services;

/// <summary>
/// 会话专家提示词校验：可用范围 = 应用绑定 ∪ 市场公开 ∪ 本人个人提示词；
/// 不含团队提示词（团队资源经管理员绑定应用后才会进入候选）.
/// </summary>
internal static class SessionPromptHelper
{
    /// <summary>
    /// 校验提示词对指定应用可用，不可用时抛 404.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="promptId">提示词 id.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="userId">会话归属用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回校验任务.</returns>
    public static async Task EnsureUsableAsync(DatabaseContext databaseContext, int promptId, Guid appId, long userId, CancellationToken cancellationToken)
    {
        var prompt = await databaseContext.Prompts
            .Where(x => x.Id == promptId)
            .Select(x => new { x.TeamId, x.CreateUserId, x.IsPublic })
            .FirstOrDefaultAsync(cancellationToken);

        if (prompt == null)
        {
            throw new BusinessException("提示词不存在.") { StatusCode = 404 };
        }

        var boundIds = await databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == appId)
            .Select(x => x.Prompts)
            .FirstOrDefaultAsync(cancellationToken);

        var usable = prompt.IsPublic
            || (prompt.TeamId == 0 && prompt.CreateUserId == userId)
            || AppAgentConfigJson.ParseIntList(boundIds).Contains(promptId);

        if (!usable)
        {
            throw new BusinessException("提示词不存在或不可用.") { StatusCode = 404 };
        }
    }
}

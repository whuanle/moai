using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;

namespace MoAI.App.Handlers;

/// <summary>
/// 消息读取侧脱敏兜底：按应用安全策略对会话/日志消息项脱敏后返回.
/// <para>写侧（函数调用中间件与会话历史落库）已对新数据脱敏，此处兜底覆盖启用脱敏前的存量消息.</para>
/// </summary>
internal static class AppMessageSecurityMasker
{
    /// <summary>
    /// 按应用加载脱敏策略；未配置/未启用返回 <see cref="AppSecurityPolicy.Disabled"/>.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>脱敏策略.</returns>
    public static async Task<AppSecurityPolicy> LoadPolicyAsync(DatabaseContext databaseContext, Guid appId, CancellationToken cancellationToken)
    {
        var entity = await databaseContext.AppSecurityConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken);
        return AppSecurityPolicy.Parse(entity);
    }

    /// <summary>
    /// 就地脱敏消息项：工具调用参数按「工具参数」范围、模型正文与思维链按「模型回复」范围、
    /// 工具消息正文按「工具结果」范围（存量数据的兜底口径，与新数据写侧一致）.
    /// </summary>
    /// <param name="policy">脱敏策略.</param>
    /// <param name="items">消息项.</param>
    public static void Mask(AppSecurityPolicy policy, IEnumerable<AppMessageItem> items)
    {
        if (!policy.IsActive)
        {
            return;
        }

        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.ToolCalls) && item.ToolCalls != "[]")
            {
                item.ToolCalls = policy.MaskToolArgsText(item.ToolCalls);
            }

            if (string.Equals(item.Role, "tool", StringComparison.OrdinalIgnoreCase))
            {
                item.Content = policy.MaskToolResultText(item.Content);
            }
            else if (string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                item.Content = policy.MaskModelText(item.Content);
                item.Reasoning = policy.MaskModelText(item.Reasoning);
            }
        }
    }
}

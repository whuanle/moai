using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// MCP 工具注解落库：把 readOnlyHint/destructiveHint 同步到 plugin_tool_meta（工具元数据表）.
/// 只更新注解位，不改动管理员维护的补充说明与调用策略；无注解的工具不建行.
/// </summary>
public static class McpToolHintsSyncHelper
{
    /// <summary>
    /// 按插件 id 同步工具注解（upsert）.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginId">插件记录 id（plugin.id）.</param>
    /// <param name="hints">工具注解（key = 工具名）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>任务.</returns>
    public static async Task UpsertAsync(
        DatabaseContext databaseContext,
        Guid pluginId,
        IReadOnlyDictionary<string, McpToolHints> hints,
        CancellationToken cancellationToken)
    {
        var hinted = hints.Where(x => x.Value.HasValue).ToList();
        if (hinted.Count == 0)
        {
            return;
        }

        var existing = await databaseContext.PluginToolMetas
            .Where(x => x.PluginId == pluginId)
            .ToListAsync(cancellationToken);

        foreach (var (toolKey, hint) in hinted)
        {
            var row = existing.FirstOrDefault(x => x.ToolKey == toolKey);
            if (row != null)
            {
                // 只回写注解位，保留管理员配置
                if (hint.ReadOnlyHint.HasValue)
                {
                    row.ReadOnlyHint = hint.ReadOnlyHint.Value;
                }

                if (hint.DestructiveHint.HasValue)
                {
                    row.DestructiveHint = hint.DestructiveHint.Value;
                }

                databaseContext.PluginToolMetas.Update(row);
            }
            else
            {
                databaseContext.PluginToolMetas.Add(new Database.Entities.PluginToolMetaEntity
                {
                    PluginId = pluginId,
                    ToolKey = toolKey,
                    ExtraNotes = string.Empty,
                    CallPolicy = string.Empty,
                    ReadOnlyHint = hint.ReadOnlyHint ?? false,
                    DestructiveHint = hint.DestructiveHint ?? false,
                });
            }
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}

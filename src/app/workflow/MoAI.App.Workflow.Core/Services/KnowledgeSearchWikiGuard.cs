using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Definition;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// knowledgeSearch 节点配置守卫：节点 config.wikiIds 引用的知识库必须属于当前团队，
/// 在保存草稿、发布、调试执行时校验，避免配置残留越权知识库 id.
/// </summary>
public static class KnowledgeSearchWikiGuard
{
    /// <summary>
    /// 校验定义中全部知识库检索节点引用的知识库均属于指定团队，否则抛出 400.
    /// </summary>
    public static async Task EnsureWikisBelongToTeamAsync(DatabaseContext databaseContext, long teamId, WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        var wikiIds = new HashSet<long>();
        foreach (var node in definition.Nodes)
        {
            if (!string.Equals(node.Type, NodeTypes.KnowledgeSearch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var config = node.GetConfig();
            if (config.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // 单数 wikiId（现行为）
            if (config.TryGetProperty("wikiId", out var wikiIdEl) && wikiIdEl.ValueKind == JsonValueKind.Number && wikiIdEl.TryGetInt64(out var singleId) && singleId > 0)
            {
                wikiIds.Add(singleId);
            }

            // 兼容旧版复数 wikiIds 数组
            if (config.TryGetProperty("wikiIds", out var wikiIdsEl) && wikiIdsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in wikiIdsEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var id) && id > 0)
                    {
                        wikiIds.Add(id);
                    }
                }
            }
        }

        if (wikiIds.Count == 0)
        {
            return;
        }

        var ownedIds = await databaseContext.Wikis
            .Where(x => x.TeamId == (int)teamId)
            .Select(x => (long)x.Id)
            .ToListAsync(cancellationToken);

        var invalidIds = wikiIds.Except(ownedIds).ToList();
        if (invalidIds.Count > 0)
        {
            throw new BusinessException($"知识库检索节点引用了不存在或不属于本团队的知识库：{string.Join(", ", invalidIds)}.") { StatusCode = 400 };
        }
    }
}

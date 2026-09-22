using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Definition;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// kgSearch 节点配置守卫：节点 config.graphId 引用的知识图谱必须属于当前团队且为平台托管（managed），
/// 在保存草稿、发布、调试执行时校验，避免配置残留越权或外部接入（不支持向量检索）的知识图谱 id.
/// </summary>
public static class KnowledgeGraphSearchGuard
{
    /// <summary>
    /// 校验定义中全部知识图谱检索节点引用的知识图谱均属于指定团队且为托管图谱，否则抛出 400.
    /// </summary>
    public static async Task EnsureGraphsBelongToTeamAsync(DatabaseContext databaseContext, long teamId, WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        var graphIds = new HashSet<long>();
        foreach (var node in definition.Nodes)
        {
            if (!string.Equals(node.Type, NodeTypes.KnowledgeGraphSearch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var config = node.GetConfig();
            if (config.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // 缺失或非法的 graphId 由节点执行期报错，这里只收集合法值
            if (config.TryGetProperty("graphId", out var graphIdEl) && graphIdEl.ValueKind == JsonValueKind.Number && graphIdEl.TryGetInt64(out var graphId) && graphId > 0)
            {
                graphIds.Add(graphId);
            }
        }

        if (graphIds.Count == 0)
        {
            return;
        }

        var ownedIds = await databaseContext.KnowledgeGraphs
            .Where(x => x.TeamId == (int)teamId && x.Mode == KnowledgeGraphModes.Managed)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var invalidIds = graphIds.Except(ownedIds).ToList();
        if (invalidIds.Count > 0)
        {
            throw new BusinessException($"知识图谱检索节点引用了不存在或不属于本团队的知识图谱：{string.Join(", ", invalidIds)}.") { StatusCode = 400 };
        }
    }
}

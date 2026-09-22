using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Nodes;
using MoAI.Database;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流知识图谱检索端口实现：kgSearch 节点通过此服务检索知识图谱.
/// 只允许检索本团队托管（managed）知识图谱（配置中出现其他团队或外部接入图谱的 id 时忽略），检索转调 <see cref="IGraphSearchService"/>（向量召回 + 一跳邻居的子图文本化）.
/// </summary>
[InjectOnScoped]
public class WorkflowGraphSearchClient : IWorkflowGraphSearchClient
{
    private readonly DatabaseContext _databaseContext;
    private readonly IGraphSearchService _graphSearchService;
    private readonly WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowGraphSearchClient"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="graphSearchService">知识图谱检索服务.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域）.</param>
    public WorkflowGraphSearchClient(
        DatabaseContext databaseContext,
        IGraphSearchService graphSearchService,
        WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _graphSearchService = graphSearchService;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowGraphSearchHit>> SearchAsync(IReadOnlyCollection<long> kgIds, string query, int top, CancellationToken cancellationToken)
    {
        var teamId = (int)_executionContext.TeamId;
        var ownedIds = await _databaseContext.KnowledgeGraphs
            .Where(x => x.TeamId == teamId && x.Mode == KnowledgeGraphModes.Managed)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // 只检索本团队托管图谱，配置中的其他 id 忽略
        var validIds = ownedIds.Where(kgIds.Contains).ToList();
        if (validIds.Count == 0)
        {
            return [];
        }

        var result = await _graphSearchService.SearchAsync(validIds, query, top, cancellationToken: cancellationToken);

        // Text 用共享片段构造器按 hit 自身字段（含 hit.Neighbors）生成，与检索 API 的 Text 段同源同形；
        // 不读 result.Contents（与 Hits 的位置耦合）也不拆 result.Text（API 侧整体拼接且可能已截断，无法安全按命中切段）
        return result.Hits.Select(hit => new WorkflowGraphSearchHit
        {
            KgId = hit.KgId,
            NodeId = hit.NodeId,
            Name = hit.Name,
            EntityTypeName = hit.EntityTypeName,
            Description = hit.Description,
            Score = hit.Score,
            Text = GraphSearchTextHelper.BuildHitFragment(hit, hit.Neighbors),
        }).ToList();
    }
}

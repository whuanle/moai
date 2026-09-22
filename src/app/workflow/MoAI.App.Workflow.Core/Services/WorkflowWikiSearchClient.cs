using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Nodes;
using MoAI.Database;
using MoAI.Wiki.Services;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流知识库检索端口实现：knowledgeSearch 节点通过此服务检索知识库.
/// 只允许检索本团队知识库（配置中出现其他团队的知识库 id 时忽略），检索转调 <see cref="IWikiSearchService"/>（按各库配置的 embedding 模型做向量召回）.
/// </summary>
[InjectOnScoped]
public class WorkflowWikiSearchClient : IWorkflowWikiSearchClient
{
    private readonly DatabaseContext _databaseContext;
    private readonly IWikiSearchService _wikiSearchService;
    private readonly WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowWikiSearchClient"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="wikiSearchService">知识库检索服务.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域）.</param>
    public WorkflowWikiSearchClient(
        DatabaseContext databaseContext,
        IWikiSearchService wikiSearchService,
        WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _wikiSearchService = wikiSearchService;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowWikiSearchHit>> SearchAsync(IReadOnlyCollection<long> wikiIds, string query, int top, CancellationToken cancellationToken)
    {
        var teamId = (int)_executionContext.TeamId;
        var ownedIds = await _databaseContext.Wikis
            .Where(x => x.TeamId == teamId)
            .Select(x => (long)x.Id)
            .ToListAsync(cancellationToken);

        // 只检索本团队知识库，配置中的其他 id 忽略
        var validIds = ownedIds.Where(wikiIds.Contains).ToList();
        if (validIds.Count == 0)
        {
            return [];
        }

        var hits = await _wikiSearchService.SearchAsync(validIds, query, top, cancellationToken);
        return hits.Select(x => new WorkflowWikiSearchHit
        {
            WikiId = x.WikiId,
            DocumentId = x.DocumentId,
            DocumentName = x.DocumentName,
            ChunkId = x.ChunkId,
            Content = x.Content,
            Score = x.Score,
        }).ToList();
    }
}

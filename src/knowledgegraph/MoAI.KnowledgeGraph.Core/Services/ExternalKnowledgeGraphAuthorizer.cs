using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 外部应用 token 的知识图谱授权器实现.
/// </summary>
[InjectOnScoped]
public class ExternalKnowledgeGraphAuthorizer : IExternalKnowledgeGraphAuthorizer
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalKnowledgeGraphAuthorizer"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public ExternalKnowledgeGraphAuthorizer(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphEntity> AuthorizeAsync(long knowledgeGraphId, long teamId, bool write, CancellationToken cancellationToken)
    {
        var graph = await _databaseContext.KnowledgeGraphs.FirstOrDefaultAsync(x => x.Id == knowledgeGraphId, cancellationToken);
        if (graph == null || graph.TeamId != teamId)
        {
            throw new BusinessException("知识图谱不存在.") { StatusCode = 404 };
        }

        if (write && string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
        {
            throw new BusinessException("接入模式图谱为只读.") { StatusCode = 409 };
        }

        return graph;
    }
}

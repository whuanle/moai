using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphEmbeddingConfigCommand"/>
/// </summary>
public class UpdateKnowledgeGraphEmbeddingConfigCommandHandler : IRequestHandler<UpdateKnowledgeGraphEmbeddingConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphEmbeddingConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    public UpdateKnowledgeGraphEmbeddingConfigCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphEmbeddingConfigCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱不支持向量检索配置.") { StatusCode = 409 };
        }

        var authorizedModelIds = await _databaseContext.AiModelAuthorizations
            .Where(x => x.TeamId == graph.TeamId)
            .Select(x => x.AiModelId)
            .ToListAsync(cancellationToken);

        var model = await (from m in _databaseContext.AiModels
                           join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                           where m.Id == request.EmbeddingModelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                               && (m.IsPublic || authorizedModelIds.Contains(m.Id))
                           select new { m.Id, m.ModelKind })
            .FirstOrDefaultAsync(cancellationToken);
        if (model == null)
        {
            throw new BusinessException("向量化模型不存在、未启用或未授权给该团队.") { StatusCode = 400 };
        }

        // ModelKind 大小写归一在内存判断（镜像 UpdateWikiEmbeddingCommandHandler 的写法）
        if (!string.Equals(model.ModelKind, "embedding", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是向量化模型.") { StatusCode = 400 };
        }

        graph.EmbeddingModelId = request.EmbeddingModelId;
        graph.EmbeddingDimensions = request.EmbeddingDimensions;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}

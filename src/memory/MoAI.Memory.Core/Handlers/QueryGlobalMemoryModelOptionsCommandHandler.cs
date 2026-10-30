using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Memory.Queries;
using MoAI.Memory.Queries.Responses;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="QueryGlobalMemoryModelOptionsCommand"/>
/// </summary>
public class QueryGlobalMemoryModelOptionsCommandHandler : IRequestHandler<QueryGlobalMemoryModelOptionsCommand, QueryGlobalMemoryModelOptionsResponse>
{
    private const string EmbeddingKind = "embedding";

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryGlobalMemoryModelOptionsCommandHandler"/> class.
    /// </summary>
    public QueryGlobalMemoryModelOptionsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryGlobalMemoryModelOptionsResponse> Handle(QueryGlobalMemoryModelOptionsCommand request, CancellationToken cancellationToken)
    {
        // 全局记忆按 teamId=0 解析模型，仅公开且启用中的 embedding 模型（渠道须启用）可选
        var items = await _databaseContext.AiModels.AsNoTracking()
            .Where(m => m.Enabled && m.IsPublic && m.ModelKind == EmbeddingKind)
            .Join(
                _databaseContext.AiChannels.Where(c => c.Enabled),
                m => m.ChannelId,
                c => c.Id,
                (m, c) => new { m.Id, m.Name })
            .Select(x => new GlobalMemoryModelOption { Id = x.Id, Name = x.Name })
            .ToListAsync(cancellationToken);

        return new QueryGlobalMemoryModelOptionsResponse { Items = items };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries.Respones;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询 ai 供应商列表和模型数量.
/// </summary>
public class QueryAiModelProviderListCommandHandler : IRequestHandler<QueryAiModelProviderListCommand, QueryAiModelProviderListResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiModelProviderListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryAiModelProviderListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAiModelProviderListResponse> Handle(QueryAiModelProviderListCommand request, CancellationToken cancellationToken)
    {
        var providerCounts = await _dbContext.AiModels
            .GroupBy(x => x.AiProvider)
            .Select(x => new QueryAiModelProviderCount
            {
                Provider = x.Key.JsonToObject<AiProvider>(),
                Count = x.Count()
            })
            .ToListAsync(cancellationToken);

        var typeCounts = await _dbContext.AiModels
            .GroupBy(x => x.AiModelType)
            .Select(x => new QueryAiModelTypeCount
            {
                Type = x.Key.JsonToObject<AiModelType>(),
                Count = x.Count()
            })
            .ToListAsync(cancellationToken);

        return new QueryAiModelProviderListResponse
        {
            Providers = providerCounts,
            Types = typeCounts
        };
    }
}
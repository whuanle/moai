using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Models;
using MoAI.AIChannel.Queries;
using MoAI.AIChannel.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// <inheritdoc cref="QueryAIModelAuthorizationCommand"/>
/// </summary>
public class QueryAIModelAuthorizationCommandHandler : IRequestHandler<QueryAIModelAuthorizationCommand, QueryAIModelAuthorizationCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAIModelAuthorizationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAIModelAuthorizationCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAIModelAuthorizationCommandResponse> Handle(QueryAIModelAuthorizationCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("未找到模型，请检查 id 是否正确.") { StatusCode = 404 };
        }

        var limits = await _databaseContext.AiModelLimits
            .Where(x => x.ModelId == request.ModelId)
            .Select(x => new { x.Id, x.TeamId, x.PeriodUnit, x.PeriodValue, x.LimitValue, x.ExpirationTime })
            .ToListAsync(cancellationToken);

        var quotas = await _databaseContext.AiModelQuota
            .Where(x => x.ModelId == request.ModelId)
            .Select(x => new { x.LimitId, x.UsedTokens })
            .ToListAsync(cancellationToken);
        var usedByLimitId = quotas.ToDictionary(x => x.LimitId, x => x.UsedTokens);

        AIModelQuotaInfo? MapLimit(int teamId)
        {
            var limit = limits.FirstOrDefault(x => x.TeamId == teamId);
            if (limit == null)
            {
                return null;
            }

            return new AIModelQuotaInfo
            {
                LimitId = limit.Id,
                PeriodUnit = limit.PeriodUnit,
                PeriodValue = limit.PeriodValue,
                LimitValue = limit.LimitValue,
                UsedTokens = usedByLimitId.GetValueOrDefault(limit.Id),
                ExpirationTime = limit.ExpirationTime,
            };
        }

        var response = new QueryAIModelAuthorizationCommandResponse
        {
            ModelId = request.ModelId,
            IsPublic = model.IsPublic,
            GlobalQuota = MapLimit(0),
            Items = new List<QueryAIModelAuthorizationCommandResponseItem>(),
        };

        if (model.IsPublic)
        {
            return response;
        }

        var authorizations = await _databaseContext.AiModelAuthorizations
            .Where(x => x.AiModelId == request.ModelId)
            .Join(_databaseContext.Teams, a => a.TeamId, t => t.Id, (a, t) => new { a.TeamId, t.Name })
            .OrderBy(x => x.TeamId)
            .ToListAsync(cancellationToken);

        response.Items = authorizations
            .Select(x => new QueryAIModelAuthorizationCommandResponseItem
            {
                TeamId = x.TeamId,
                TeamName = x.Name,
                Quota = MapLimit(x.TeamId),
            })
            .ToList();

        return response;
    }
}

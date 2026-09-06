using MediatR;
using MoAI.Gateway.Queries;
using MoAI.Gateway.Queries.Responses;
using MoAI.Gateway.Services;

namespace MoAI.Gateway.Handlers;

/// <summary>
/// 查询团队可用的网关模型.
/// </summary>
public class QueryTeamGatewayModelsCommandHandler : IRequestHandler<QueryTeamGatewayModelsCommand, QueryTeamGatewayModelsCommandResponse>
{
    private readonly GatewayModelResolver _gatewayModelResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamGatewayModelsCommandHandler"/> class.
    /// </summary>
    /// <param name="gatewayModelResolver">网关模型解析服务.</param>
    public QueryTeamGatewayModelsCommandHandler(GatewayModelResolver gatewayModelResolver)
    {
        _gatewayModelResolver = gatewayModelResolver;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamGatewayModelsCommandResponse> Handle(QueryTeamGatewayModelsCommand request, CancellationToken cancellationToken)
    {
        var views = await _gatewayModelResolver.ListAsync(request.TeamId, cancellationToken);

        // 同名 model_id 可能授权自多个渠道，列表按模型名去重保留第一条.
        var items = views
            .GroupBy(x => x.Model.ModelId, StringComparer.Ordinal)
            .Select(g => g.First())
            .Select(x => new TeamGatewayModelItem
            {
                AiModelId = x.Model.Id,
                Name = x.Model.Name,
                ModelId = x.Model.ModelId,
                ChannelName = x.Channel.Name,
                ProviderKey = x.Channel.ProviderKey,
                Quota = x.Quota == null ? null : new TeamGatewayModelQuota
                {
                    PeriodValue = x.Quota.PeriodValue,
                    PeriodUnit = x.Quota.PeriodUnit,
                    LimitValue = x.Quota.LimitValue,
                    UsedTokens = x.Quota.UsedTokens,
                    PeriodEnd = x.Quota.PeriodEnd,
                    ExpirationTime = x.Quota.ExpirationTime,
                },
                TotalUsedTokens = x.TotalUsedTokens,
            })
            .ToList();

        return new QueryTeamGatewayModelsCommandResponse { Items = items };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Gateway.Queries;
using MoAI.Gateway.Queries.Responses;
using MoAI.Gateway.Services;

namespace MoAI.Gateway.Handlers;

/// <summary>
/// 查询团队网关 API Key 列表.
/// </summary>
public class QueryTeamApiKeysCommandHandler : IRequestHandler<QueryTeamApiKeysCommand, QueryTeamApiKeysCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamApiKeysCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryTeamApiKeysCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamApiKeysCommandResponse> Handle(QueryTeamApiKeysCommand request, CancellationToken cancellationToken)
    {
        // 用 DateTimeOffset 比较 DateTimeOffset 列，避免 timestamptz 与 timestamp 的操作符不匹配.
        var now = DateTimeOffset.Now;
        var items = await _databaseContext.TeamApiKeys
            .Where(x => x.TeamId == request.TeamId)
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new TeamApiKeyItem
            {
                Id = x.Id,
                Name = x.Name,
                KeyPrefix = x.KeyPrefix,
                IsDisable = x.IsDisable,
                IsExpired = x.ExpireTime < now,
                ExpireTime = x.ExpireTime >= ApiKeySentinels.NeverExpireBoundary ? null : x.ExpireTime,
                LastUsedTime = x.LastUsedTime < ApiKeySentinels.NeverUsedBoundary ? null : x.LastUsedTime,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryTeamApiKeysCommandResponse { Items = items };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySystemAccessAppsCommand"/>
/// </summary>
public class QuerySystemAccessAppsCommandHandler : IRequestHandler<QuerySystemAccessAppsCommand, QuerySystemAccessAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemAccessAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QuerySystemAccessAppsCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QuerySystemAccessAppsCommandResponse> Handle(QuerySystemAccessAppsCommand request, CancellationToken cancellationToken)
    {
        var pageNo = Math.Max(1, request.PageNo);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = request.Search?.Trim();

        // 接入数量有限，过滤/排序/分页在内存侧进行，保持与 sqlite 单测兼容
        var query = _databaseContext.AccessApps.Where(x => x.Level == AccessAppLevel.System).AsQueryable();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(x => x.Name.Contains(search) || x.KeyPrefix.StartsWith(search));
        }

        var apps = await query.ToListAsync(cancellationToken);
        var totalCount = apps.Count;
        var paged = apps
            .OrderByDescending(x => x.CreateTime)
            .Skip((pageNo - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new QuerySystemAccessAppsCommandResponseItem
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                KeyPrefix = x.KeyPrefix,
                Scopes = SystemApiKeyScopeCodes.ToCodes((SystemApiKeyScopes)x.Scopes),
                Status = x.Status,
                CreateUserId = (int)x.CreateUserId,
                CreateTime = x.CreateTime,
                UpdateUserId = (int)x.UpdateUserId,
                UpdateTime = x.UpdateTime,
            })
            .ToList();
        await _userInfoFillService.FillAsync(paged, cancellationToken);

        return new QuerySystemAccessAppsCommandResponse { TotalCount = totalCount, Items = paged };
    }
}

/// <summary>
/// <inheritdoc cref="QuerySystemAccessAppInfoCommand"/>
/// </summary>
public class QuerySystemAccessAppInfoCommandHandler : IRequestHandler<QuerySystemAccessAppInfoCommand, QuerySystemAccessAppInfoCommandResponse>
{
    /// <inheritdoc/>
    public Task<QuerySystemAccessAppInfoCommandResponse> Handle(QuerySystemAccessAppInfoCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new QuerySystemAccessAppInfoCommandResponse
        {
            Id = request.AppId,
            Name = request.Name,
            Scopes = SystemApiKeyScopeCodes.ToCodes(request.Scopes),
            Status = request.Status,
        });
    }
}

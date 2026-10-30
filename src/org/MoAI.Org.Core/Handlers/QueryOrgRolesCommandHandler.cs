using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="QueryOrgRolesCommand"/>
/// </summary>
public class QueryOrgRolesCommandHandler : IRequestHandler<QueryOrgRolesCommand, QueryOrgRolesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOrgRolesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryOrgRolesCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryOrgRolesCommandResponse> Handle(QueryOrgRolesCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.OrgRoles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Name.Contains(search));
        }

        // 排序在内存侧进行，保持与 sqlite 单测兼容；path 数值层级序（父先于子、同级按 id 数值序）
        var roles = await query.ToListAsync(cancellationToken);
        roles.Sort(static (a, b) => OrgRoleTreeGuard.ComparePath(a.Path, b.Path));

        var userCounts = await _databaseContext.OrgUserRoles
            .GroupBy(x => x.RoleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var parentIds = roles.Where(x => x.ParentId != null).Select(x => x.ParentId!.Value).ToHashSet();

        var defaultRoleValue = await _databaseContext.Settings
            .Where(x => x.Key == MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);
        _ = int.TryParse(defaultRoleValue, out var defaultRoleId);

        var items = roles.Select(x => new QueryOrgRolesCommandResponseItem
        {
            OrgRoleId = x.Id,
            Name = x.Name,
            ParentId = x.ParentId,
            Path = x.Path,
            UserCount = userCounts.GetValueOrDefault(x.Id),
            HasChildren = parentIds.Contains(x.Id),
            IsDefault = x.Id == defaultRoleId,
            CreateTime = x.CreateTime,
            CreateUserId = (int)x.CreateUserId,
            UpdateTime = x.UpdateTime,
            UpdateUserId = (int)x.UpdateUserId,
        }).ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);
        return new QueryOrgRolesCommandResponse { Items = items };
    }
}

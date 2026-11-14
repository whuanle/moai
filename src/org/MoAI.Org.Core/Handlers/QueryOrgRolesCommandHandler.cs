using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
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

        // 排序在内存侧进行，保持与 sqlite 单测兼容：同级按 sort_order,id 升序（拖拽排序结果），DFS 输出保证父先于子（前端组树依赖此序）
        var roles = await query.ToListAsync(cancellationToken);
        var orderedRoles = OrderByTree(roles);

        var userCounts = await _databaseContext.OrgUserRoles
            .GroupBy(x => x.RoleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var parentIds = roles.Where(x => x.ParentId != null).Select(x => x.ParentId!.Value).ToHashSet();

        var items = orderedRoles.Select(x => new QueryOrgRolesCommandResponseItem
        {
            OrgRoleId = x.Id,
            Name = x.Name,
            ParentId = x.ParentId,
            Path = x.Path,
            UserCount = userCounts.GetValueOrDefault(x.Id),
            HasChildren = parentIds.Contains(x.Id),
            SortOrder = x.SortOrder,
            CreateTime = x.CreateTime,
            CreateUserId = (int)x.CreateUserId,
            UpdateTime = x.UpdateTime,
            UpdateUserId = (int)x.UpdateUserId,
        }).ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);
        return new QueryOrgRolesCommandResponse { Items = items };
    }

    /// <summary>
    /// 树序输出：同级按 (SortOrder, Id) 升序分桶后 DFS（父先于子，前端按列表序组树）.
    /// 孤儿数据（父被软删等异常）按 id 序兜底追加，避免丢行.
    /// </summary>
    /// <param name="roles">全量角色平铺列表.</param>
    /// <returns>父先于子的有序列表.</returns>
    private static List<OrgRoleEntity> OrderByTree(List<OrgRoleEntity> roles)
    {
        var byParent = roles
            .GroupBy(x => x.ParentId ?? 0)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var bucket = g.ToList();
                    bucket.Sort(static (a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : a.Id.CompareTo(b.Id));
                    return bucket;
                });

        var ordered = new List<OrgRoleEntity>(roles.Count);

        // 先序 DFS：桶内同级正序输出；树深受 path 255 列长上限约束（≤20 层），递归安全
        void Walk(int parentKey)
        {
            if (!byParent.TryGetValue(parentKey, out var bucket))
            {
                return;
            }

            foreach (var node in bucket)
            {
                ordered.Add(node);
                Walk(node.Id);
            }
        }

        Walk(0);

        if (ordered.Count < roles.Count)
        {
            var seen = ordered.Select(x => x.Id).ToHashSet();
            ordered.AddRange(roles.Where(x => !seen.Contains(x.Id)).OrderBy(x => x.Id));
        }

        return ordered;
    }
}

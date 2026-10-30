using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="QueryOrgRoleUsersCommand"/>
/// </summary>
public class QueryOrgRoleUsersCommandHandler : IRequestHandler<QueryOrgRoleUsersCommand, QueryOrgRoleUsersCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOrgRoleUsersCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryOrgRoleUsersCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryOrgRoleUsersCommandResponse> Handle(QueryOrgRoleUsersCommand request, CancellationToken cancellationToken)
    {
        var roleExists = await _databaseContext.OrgRoles
            .AnyAsync(x => x.Id == request.RoleId, cancellationToken);
        if (!roleExists)
        {
            throw new BusinessException("角色不存在.") { StatusCode = 404 };
        }

        var bindings = await _databaseContext.OrgUserRoles
            .Where(x => x.RoleId == request.RoleId)
            .ToListAsync(cancellationToken);

        // 排序在内存侧进行，保持与 sqlite 单测兼容
        bindings.Sort(static (a, b) => a.CreateTime != b.CreateTime ? a.CreateTime.CompareTo(b.CreateTime) : a.Id.CompareTo(b.Id));

        var userIds = bindings.Select(x => x.UserId).Distinct().ToList();
        var users = userIds.Count == 0
            ? []
            : await _databaseContext.Users
                .Where(x => userIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);
        var rootValue = await _databaseContext.Settings
            .Where(s => s.Key == "root")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        IEnumerable<QueryOrgRoleUsersCommandResponseItem> result = bindings.Select(x =>
        {
            var user = users.GetValueOrDefault(x.UserId);
            return new QueryOrgRoleUsersCommandResponseItem
            {
                UserId = x.UserId,
                UserName = user?.UserName ?? $"#{x.UserId}",
                NickName = user?.NickName ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                Phone = user?.Phone ?? string.Empty,
                Avatar = user?.AvatarPath ?? string.Empty,
                IsAdmin = user?.IsAdmin == true,
                IsRoot = user != null && user.Id.ToString() == rootValue,
                IsDisable = user?.IsDisable == true,
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
            };
        });

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            result = result.Where(x => x.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) || x.NickName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var items = result.ToList();
        await _userInfoFillService.FillAsync(items, cancellationToken);
        return new QueryOrgRoleUsersCommandResponse { Items = items };
    }
}

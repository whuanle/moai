using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="QueryUserOrgRolesCommand"/>
/// </summary>
public class QueryUserOrgRolesCommandHandler : IRequestHandler<QueryUserOrgRolesCommand, QueryUserOrgRolesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserOrgRolesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryUserOrgRolesCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryUserOrgRolesCommandResponse> Handle(QueryUserOrgRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users
            .Where(x => x.Id == request.UserId)
            .Select(x => new { x.UserName, x.NickName })
            .FirstOrDefaultAsync(cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        var roleIds = await _databaseContext.OrgUserRoles
            .Where(x => x.UserId == request.UserId)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);

        // 排序在内存侧进行，保持与 sqlite 单测兼容；path 数值层级序
        var roles = roleIds.Count == 0
            ? []
            : await _databaseContext.OrgRoles
                .Where(x => roleIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        roles.Sort(static (a, b) => OrgRoleTreeGuard.ComparePath(a.Path, b.Path));

        return new QueryUserOrgRolesCommandResponse
        {
            UserId = request.UserId,
            UserName = user.UserName,
            NickName = user.NickName,
            Items = roles.Select(x => new QueryUserOrgRolesCommandResponseItem
            {
                OrgRoleId = x.Id,
                Name = x.Name,
                Path = x.Path,
            }).ToList(),
        };
    }
}

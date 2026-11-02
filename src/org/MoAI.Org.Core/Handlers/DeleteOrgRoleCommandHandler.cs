using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteOrgRoleCommand"/>
/// </summary>
public class DeleteOrgRoleCommandHandler : IRequestHandler<DeleteOrgRoleCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantCache _orgGrantCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteOrgRoleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantCache">组织授权缓存（角色树与授权记录变更需失效）.</param>
    public DeleteOrgRoleCommandHandler(DatabaseContext databaseContext, IOrgGrantCache orgGrantCache)
    {
        _databaseContext = databaseContext;
        _orgGrantCache = orgGrantCache;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteOrgRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _databaseContext.OrgRoles
            .FirstOrDefaultAsync(x => x.Id == request.OrgRoleId, cancellationToken);
        if (role == null)
        {
            throw new BusinessException("组织不存在.") { StatusCode = 404 };
        }

        var hasChildren = await _databaseContext.OrgRoles
            .AnyAsync(x => x.ParentId == role.Id, cancellationToken);
        if (hasChildren)
        {
            throw new BusinessException("存在子组织，无法删除，请先调整或删除子组织.") { StatusCode = 400 };
        }

        _databaseContext.OrgRoles.Remove(role);

        // 级联解绑该角色下的全部用户
        var bindings = await _databaseContext.OrgUserRoles
            .Where(x => x.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        _databaseContext.OrgUserRoles.RemoveRange(bindings);

        // 级联软删引用该角色的组织授权记录，并记录受影响资源用于删缓存
        var affectedGrants = await _databaseContext.OrgGrants
            .Where(x => x.OrgRoleId == role.Id)
            .ToListAsync(cancellationToken);
        _databaseContext.OrgGrants.RemoveRange(affectedGrants);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 失效角色树缓存与受影响资源/用户的授权缓存（TTL 兜底，显式删除保证判定即时生效）
        await _orgGrantCache.RemoveRolePathsAsync(cancellationToken);
        foreach (var resourceKey in affectedGrants
            .Select(x => OrgGrantCacheKeys.ResourceKey((OrgGrantResourceType)x.ResourceType, x.ResourceId))
            .Distinct())
        {
            await _orgGrantCache.RemoveResourceGrantsAsync(resourceKey, cancellationToken);
        }

        foreach (var userId in bindings.Select(x => x.UserId).Distinct())
        {
            await _orgGrantCache.RemoveUserRoleIdsAsync(userId, cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}

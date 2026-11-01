using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateOrgRoleCommand"/>
/// </summary>
public class UpdateOrgRoleCommandHandler : IRequestHandler<UpdateOrgRoleCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantCache _orgGrantCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrgRoleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantCache">组织授权缓存（子树迁移重写 path 需失效）.</param>
    public UpdateOrgRoleCommandHandler(DatabaseContext databaseContext, IOrgGrantCache orgGrantCache)
    {
        _databaseContext = databaseContext;
        _orgGrantCache = orgGrantCache;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOrgRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _databaseContext.OrgRoles
            .FirstOrDefaultAsync(x => x.Id == request.OrgRoleId, cancellationToken);
        if (role == null)
        {
            throw new BusinessException("组织不存在.") { StatusCode = 404 };
        }

        if (request.ParentId == role.Id)
        {
            throw new BusinessException("不能将组织设为自己的上级.") { StatusCode = 400 };
        }

        var parentChanged = request.ParentId != role.ParentId;
        var parent = await OrgRoleTreeGuard.FindParentAsync(_databaseContext, request.ParentId, cancellationToken);
        await OrgRoleTreeGuard.EnsureNoDuplicateNameAsync(_databaseContext, request.ParentId, request.Name, role.Id, cancellationToken);

        role.Name = request.Name;

        if (parentChanged)
        {
            // 防环：新父 path 不得以本角色 path 为前缀（自身或任意后代作父都会命中）
            if (parent != null && parent.Path.StartsWith(role.Path, StringComparison.Ordinal))
            {
                throw new BusinessException("不能将组织移动到自身或其下级组织之下.") { StatusCode = 400 };
            }

            var oldPath = role.Path;
            var descendants = await _databaseContext.OrgRoles
                .Where(x => x.Id != role.Id && EF.Functions.Like(x.Path, oldPath + "%"))
                .ToListAsync(cancellationToken);
            var newParentPath = parent?.Path ?? "/";
            var maxDescendantSuffixLength = descendants.Count == 0 ? 0 : descendants.Max(x => x.Path.Length - oldPath.Length);
            OrgRoleTreeGuard.EnsurePathLength(newParentPath, role.Id, maxDescendantSuffixLength);

            // 子树整体迁移：本角色新 path = 新父 path + 本角色段；后代 = 新 path + 原相对后缀
            var newPath = OrgRoleTreeGuard.BuildPath(newParentPath, role.Id);
            foreach (var descendant in descendants)
            {
                descendant.Path = newPath + descendant.Path.Substring(oldPath.Length);
            }

            role.Path = newPath;
            role.ParentId = request.ParentId;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 子树迁移重写 path 后失效全量 path 缓存（改名不动 path 也一并失效，操作低频）
        await _orgGrantCache.RemoveRolePathsAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}

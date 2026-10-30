using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateOrgRoleCommand"/>
/// </summary>
public class UpdateOrgRoleCommandHandler : IRequestHandler<UpdateOrgRoleCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrgRoleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateOrgRoleCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOrgRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _databaseContext.OrgRoles
            .FirstOrDefaultAsync(x => x.Id == request.OrgRoleId, cancellationToken);
        if (role == null)
        {
            throw new BusinessException("角色不存在.") { StatusCode = 404 };
        }

        if (request.ParentId == role.Id)
        {
            throw new BusinessException("不能将角色设为自己的上级.") { StatusCode = 400 };
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
                throw new BusinessException("不能将角色移动到自身或其下级角色之下.") { StatusCode = 400 };
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
        return EmptyCommandResponse.Default;
    }
}

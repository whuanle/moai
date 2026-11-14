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
            // 防环 + 子树整体迁移重写 path
            await OrgRoleTreeGuard.RewriteSubtreePathsAsync(_databaseContext, role, parent, cancellationToken);
            role.ParentId = request.ParentId;

            // 落位：新同级末尾追加（拖拽排序走独立的 order 命令）
            var maxSort = await _databaseContext.OrgRoles
                .Where(x => x.ParentId == request.ParentId && x.Id != role.Id)
                .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0;
            role.SortOrder = maxSort + 1;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 子树迁移重写 path 后失效全量 path 缓存（改名不动 path 也一并失效，操作低频）
        await _orgGrantCache.RemoveRolePathsAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}

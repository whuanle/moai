using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateOrgRoleOrderCommand"/>
/// </summary>
public class UpdateOrgRoleOrderCommandHandler : IRequestHandler<UpdateOrgRoleOrderCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantCache _orgGrantCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrgRoleOrderCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantCache">组织授权缓存（跨级移动重写 path 需失效）.</param>
    public UpdateOrgRoleOrderCommandHandler(DatabaseContext databaseContext, IOrgGrantCache orgGrantCache)
    {
        _databaseContext = databaseContext;
        _orgGrantCache = orgGrantCache;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOrgRoleOrderCommand request, CancellationToken cancellationToken)
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

        if (request.PrevOrgRoleId == role.Id)
        {
            throw new BusinessException("不能以自身作为插入参照.") { StatusCode = 400 };
        }

        var parentChanged = request.ParentId != role.ParentId;
        var parent = await OrgRoleTreeGuard.FindParentAsync(_databaseContext, request.ParentId, cancellationToken);

        // 目标同级（不含自身），按当前展示序（sort_order,id）
        var siblings = await _databaseContext.OrgRoles
            .Where(x => x.ParentId == request.ParentId && x.Id != role.Id)
            .ToListAsync(cancellationToken);
        siblings.Sort(static (a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : a.Id.CompareTo(b.Id));

        if (request.PrevOrgRoleId.HasValue && siblings.All(x => x.Id != request.PrevOrgRoleId.Value))
        {
            throw new BusinessException("插入参照组织不在目标同级中.") { StatusCode = 400 };
        }

        if (parentChanged)
        {
            // 防环 + 子树整体迁移重写 path
            await OrgRoleTreeGuard.RewriteSubtreePathsAsync(_databaseContext, role, parent, cancellationToken);
            role.ParentId = request.ParentId;
        }

        // 目标同级重排：插到 prev 之后（prev null=置于首位），同级整体重编号 1..n 消除空档
        var orderedIds = new List<int>(siblings.Count + 1);
        var inserted = request.PrevOrgRoleId == null;
        if (inserted)
        {
            orderedIds.Add(role.Id);
        }

        foreach (var sibling in siblings)
        {
            orderedIds.Add(sibling.Id);
            if (!inserted && sibling.Id == request.PrevOrgRoleId)
            {
                orderedIds.Add(role.Id);
                inserted = true;
            }
        }

        if (!inserted)
        {
            orderedIds.Add(role.Id);
        }

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var sibling = siblings.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (sibling != null)
            {
                sibling.SortOrder = i + 1;
            }
        }

        role.SortOrder = orderedIds.IndexOf(role.Id) + 1;

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 跨级移动重写 path 后失效全量 path 缓存（纯同层排序不动 path，操作低频一并失效）
        await _orgGrantCache.RemoveRolePathsAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}

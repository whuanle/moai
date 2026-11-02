using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Org.Commands;
using MoAI.Org.Queries.Responses;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="CreateOrgRoleCommand"/>
/// </summary>
public class CreateOrgRoleCommandHandler : IRequestHandler<CreateOrgRoleCommand, CreateOrgRoleCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantCache _orgGrantCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrgRoleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantCache">组织授权缓存（角色树变更需失效）.</param>
    public CreateOrgRoleCommandHandler(DatabaseContext databaseContext, IOrgGrantCache orgGrantCache)
    {
        _databaseContext = databaseContext;
        _orgGrantCache = orgGrantCache;
    }

    /// <inheritdoc/>
    public async Task<CreateOrgRoleCommandResponse> Handle(CreateOrgRoleCommand request, CancellationToken cancellationToken)
    {
        var parent = await OrgRoleTreeGuard.FindParentAsync(_databaseContext, request.ParentId, cancellationToken);
        await OrgRoleTreeGuard.EnsureNoDuplicateNameAsync(_databaseContext, request.ParentId, request.Name, null, cancellationToken);

        // 同级排序：新组织追加在同级末尾（拖拽排序走独立的 order 命令）
        var maxSort = await _databaseContext.OrgRoles
            .Where(x => x.ParentId == request.ParentId)
            .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0;

        var parentPath = parent?.Path ?? "/";
        var entity = new OrgRoleEntity
        {
            Name = request.Name,
            ParentId = request.ParentId,
            Path = string.Empty,
            SortOrder = maxSort + 1,
        };
        _databaseContext.OrgRoles.Add(entity);

        // path 由自增 id 拼出，事务内两段保存保证 id 与 path 一致落库
        await using var transaction = await _databaseContext.Database.BeginTransactionAsync(cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        OrgRoleTreeGuard.EnsurePathLength(parentPath, entity.Id, 0);
        entity.Path = OrgRoleTreeGuard.BuildPath(parentPath, entity.Id);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // 角色树新增节点，失效全量 path 缓存
        await _orgGrantCache.RemoveRolePathsAsync(cancellationToken);

        return new CreateOrgRoleCommandResponse { OrgRoleId = entity.Id, Path = entity.Path };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Org.Commands;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="CreateOrgRoleCommand"/>
/// </summary>
public class CreateOrgRoleCommandHandler : IRequestHandler<CreateOrgRoleCommand, CreateOrgRoleCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrgRoleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateOrgRoleCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateOrgRoleCommandResponse> Handle(CreateOrgRoleCommand request, CancellationToken cancellationToken)
    {
        var parent = await OrgRoleTreeGuard.FindParentAsync(_databaseContext, request.ParentId, cancellationToken);
        await OrgRoleTreeGuard.EnsureNoDuplicateNameAsync(_databaseContext, request.ParentId, request.Name, null, cancellationToken);

        var parentPath = parent?.Path ?? "/";
        var entity = new OrgRoleEntity
        {
            Name = request.Name,
            ParentId = request.ParentId,
            Path = string.Empty,
        };
        _databaseContext.OrgRoles.Add(entity);

        // path 由自增 id 拼出，事务内两段保存保证 id 与 path 一致落库
        await using var transaction = await _databaseContext.Database.BeginTransactionAsync(cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        OrgRoleTreeGuard.EnsurePathLength(parentPath, entity.Id, 0);
        entity.Path = OrgRoleTreeGuard.BuildPath(parentPath, entity.Id);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateOrgRoleCommandResponse { OrgRoleId = entity.Id, Path = entity.Path };
    }
}

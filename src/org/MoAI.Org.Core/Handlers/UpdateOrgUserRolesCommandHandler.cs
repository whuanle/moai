using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateOrgUserRolesCommand"/>
/// </summary>
public class UpdateOrgUserRolesCommandHandler : IRequestHandler<UpdateOrgUserRolesCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrgUserRolesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateOrgUserRolesCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOrgUserRolesCommand request, CancellationToken cancellationToken)
    {
        var userExists = await _databaseContext.Users
            .AnyAsync(x => x.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        var roleIds = request.RoleIds.Distinct().ToList();
        if (roleIds.Count > 0)
        {
            var existingRoleIds = await _databaseContext.OrgRoles
                .Where(x => roleIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            if (existingRoleIds.Count != roleIds.Count)
            {
                throw new BusinessException("部分角色不存在.") { StatusCode = 400 };
            }
        }

        // 角色集合整体覆盖：按差集增删
        var existingBindings = await _databaseContext.OrgUserRoles
            .Where(x => x.UserId == request.UserId)
            .ToListAsync(cancellationToken);
        _databaseContext.OrgUserRoles.RemoveRange(existingBindings.Where(x => !roleIds.Contains(x.RoleId)));

        var addRoleIds = roleIds.Where(id => existingBindings.All(x => x.RoleId != id));
        foreach (var roleId in addRoleIds)
        {
            _databaseContext.OrgUserRoles.Add(new OrgUserRoleEntity
            {
                UserId = request.UserId,
                RoleId = roleId,
            });
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}

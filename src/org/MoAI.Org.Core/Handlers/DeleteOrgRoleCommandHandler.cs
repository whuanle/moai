using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteOrgRoleCommand"/>
/// </summary>
public class DeleteOrgRoleCommandHandler : IRequestHandler<DeleteOrgRoleCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteOrgRoleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteOrgRoleCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteOrgRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _databaseContext.OrgRoles
            .FirstOrDefaultAsync(x => x.Id == request.OrgRoleId, cancellationToken);
        if (role == null)
        {
            throw new BusinessException("角色不存在.") { StatusCode = 404 };
        }

        var hasChildren = await _databaseContext.OrgRoles
            .AnyAsync(x => x.ParentId == role.Id, cancellationToken);
        if (hasChildren)
        {
            throw new BusinessException("存在子角色，无法删除，请先调整或删除子角色.") { StatusCode = 400 };
        }

        _databaseContext.OrgRoles.Remove(role);

        // 级联解绑该角色下的全部用户
        var bindings = await _databaseContext.OrgUserRoles
            .Where(x => x.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        _databaseContext.OrgUserRoles.RemoveRange(bindings);

        // 被删角色是默认角色时同步清空设置，避免悬挂 id（新注册用户视为无默认角色）
        var defaultSetting = await _databaseContext.Settings
            .FirstOrDefaultAsync(x => x.Key == MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey, cancellationToken);
        if (defaultSetting != null && defaultSetting.Value == role.Id.ToString())
        {
            defaultSetting.Value = string.Empty;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}

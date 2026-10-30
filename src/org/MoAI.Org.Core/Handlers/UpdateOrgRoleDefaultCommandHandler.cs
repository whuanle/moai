using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;
using MoAI.Settings.Commands;
using MoAI.Settings.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateOrgRoleDefaultCommand"/>
/// </summary>
public class UpdateOrgRoleDefaultCommandHandler : IRequestHandler<UpdateOrgRoleDefaultCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrgRoleDefaultCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="settingsService">设置领域服务.</param>
    public UpdateOrgRoleDefaultCommandHandler(DatabaseContext databaseContext, ISettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOrgRoleDefaultCommand request, CancellationToken cancellationToken)
    {
        if (request.OrgRoleId != null)
        {
            var roleExists = await _databaseContext.OrgRoles
                .AnyAsync(x => x.Id == request.OrgRoleId.Value, cancellationToken);
            if (!roleExists)
            {
                throw new BusinessException("角色不存在.") { StatusCode = 404 };
            }
        }

        // 后设覆盖旧值；只影响新注册用户，不追溯存量绑定
        await _settingsService.SaveSettingAsync(new SaveSettingCommand
        {
            Key = MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey,
            Value = request.OrgRoleId?.ToString() ?? string.Empty,
        }, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}

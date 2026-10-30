using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Seed;

namespace MoAI.Org.Core.Services;

/// <summary>
/// 注册增强钩子：新注册用户自动获得组织默认角色（ORG_DEFAULT_ROLE 设置项，空=无默认）.
/// 只入列不保存，由注册主流程统一提交；角色缺失（被删/软删）静默跳过；异常吞掉保证注册不受影响.
/// </summary>
public class UserRegistrationOrgRoleHook : IUserRegistrationHook
{
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<UserRegistrationOrgRoleHook> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegistrationOrgRoleHook"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="logger">日志.</param>
    public UserRegistrationOrgRoleHook(DatabaseContext databaseContext, ILogger<UserRegistrationOrgRoleHook> logger)
    {
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task OnUserRegisteredAsync(long userId, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _databaseContext.Settings
                .Where(x => x.Key == SettingDefinitions.OrgDefaultRoleKey)
                .Select(x => x.Value)
                .FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out var roleId))
            {
                return;
            }

            // 角色缺失（默认角色被删后设置未及时清理等场景）静默跳过
            var roleExists = await _databaseContext.OrgRoles
                .AnyAsync(x => x.Id == roleId, cancellationToken);
            if (!roleExists)
            {
                return;
            }

            var hasBinding = await _databaseContext.OrgUserRoles
                .AnyAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);
            if (hasBinding)
            {
                return;
            }

            _databaseContext.OrgUserRoles.Add(new OrgUserRoleEntity
            {
                UserId = userId,
                RoleId = roleId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "注册默认角色赋予失败，跳过.{UserId}", userId);
        }
    }
}

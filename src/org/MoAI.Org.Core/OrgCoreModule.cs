using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Account.Services;

namespace MoAI.Org;

/// <summary>
/// OrgCoreModule.
/// </summary>
[InjectModule<OrgSharedModule>]
[InjectModule<OrgApiModule>]
public class OrgCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 注册增强钩子：新用户自动获得组织默认角色（Auth 注册流程经接口消费，宿主组合）
        context.Services.AddScoped<IUserRegistrationHook, Core.Services.UserRegistrationOrgRoleHook>();
    }
}

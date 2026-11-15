using Maomi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Subsystem.Hubs;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem;

/// <summary>
/// SubsystemCoreModule.
/// </summary>
[InjectModule<SubsystemSharedModule>]
[InjectModule<SubsystemApiModule>]
public class SubsystemCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 接入事件推送：机器端 Hub 认证方案
        context.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, SubsysHubAuthenticationHandler>(SubsystemAuthDefaults.HubScheme, _ => { });
    }
}

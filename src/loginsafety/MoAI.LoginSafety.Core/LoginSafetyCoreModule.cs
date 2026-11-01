using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.LoginSafety.Services;

namespace MoAI.LoginSafety;

/// <summary>
/// LoginSafetyCoreModule.
/// </summary>
[InjectModule<LoginSafetySharedModule>]
[InjectModule<LoginSafetyApiModule>]
public class LoginSafetyCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<ILoginRiskService, LoginRiskService>();
        context.Services.AddScoped<ILoginSafetySettingsService, LoginSafetySettingsService>();

        // IP 归属地解析（xdb 全量载入内存，进程内共享）
        context.Services.AddSingleton<IIpLocationService, Ip2RegionService>();
    }
}

using Maomi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MoAI.App.Services;

namespace MoAI.App;

/// <summary>
/// AppApiModule.
/// </summary>
public class AppApiModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 外部接入认证方案：/api/external 专用，校验 audience 为 Server + |external 的外部 token
        context.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ExternalJwtBearerAuthenticationHandler>(ExternalAuthDefaults.AuthenticationScheme, _ => { });

        // 外部认证中间件（须注册于 CustomAuthorizaMiddleware 之前）
        context.Services.AddScoped<ExternalAuthenticationMiddleware>();
    }
}

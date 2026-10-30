using Maomi;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.Subsystem;

/// <summary>
/// SubsystemApiModule.
/// </summary>
public class SubsystemApiModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 工厂激活中间件（IMiddleware），供 Program.cs 在外部接入中间件之后挂载
        context.Services.AddScoped<SubsystemAuthenticationMiddleware>();
        context.Services.AddScoped<SystemAccessKeyMiddleware>();
    }
}

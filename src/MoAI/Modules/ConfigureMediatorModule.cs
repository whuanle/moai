using Maomi;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Filters;

namespace MoAI.Modules;

/// <summary>
/// 配置 Mediator .
/// </summary>
public class ConfigureMediatorModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MediatorPipelineBehavior<,>));
        context.Services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });
    }
}

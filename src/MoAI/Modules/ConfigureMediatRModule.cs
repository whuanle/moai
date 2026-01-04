using Maomi;
using MediatR;
using MoAI.Filters;
using MoAI.Infra;

namespace MoAI.Modules;

/// <summary>
/// 配置 MediatR .
/// </summary>
public class ConfigureMediatRModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MediatRPipelineBehavior<,>));
        context.Services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblies(context.Modules.Select(x => x.Assembly).Distinct().ToArray());
            options.RegisterGenericHandlers = true;
            options.AddOpenBehavior(typeof(MediatRPipelineBehavior<,>));
        });
    }
}